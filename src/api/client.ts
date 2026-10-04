import { API_BASE_URL, REQUEST_TIMEOUT_MS, STORAGE, apiUrlBase } from '@/api/config'
import { ApiError, kindForStatus } from '@/api/errors'
import {
  clearSession,
  getAccessToken,
  getRefreshToken,
  isAccessTokenExpiring,
  setSessionFromAuth,
} from '@/api/session'
import type { ApiEnvelope, AuthenticationResponse, ProblemDocument } from '@/api/schema'

export type QueryValue = string | number | boolean | null | undefined | (string | number)[]
export type QueryParams = Record<string, QueryValue>

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  query?: QueryParams
  body?: unknown
  /** Sent as multipart/form-data instead of JSON. */
  form?: FormData
  signal?: AbortSignal
  /** Skip the Authorization header even when signed in (public endpoints). */
  anonymous?: boolean
  /** Retries for transient failures. GETs default to 2, mutations to 0. */
  retries?: number
}

/** Broadcast when refreshing fails, so the app can drop to the login screen once. */
export const AUTH_EXPIRED_EVENT = 'ustaz:auth-expired'

function emitAuthExpired(): void {
  if (typeof window === 'undefined') return
  window.dispatchEvent(new CustomEvent(AUTH_EXPIRED_EVENT))
}

/* -------------------------------------------------------------------------- */
/*                                  URL + body                                 */
/* -------------------------------------------------------------------------- */

export function buildUrl(path: string, query?: QueryParams): string {
  const suffix = `${API_BASE_URL}${path.startsWith('/') ? path : `/${path}`}`
  // API_BASE_URL is origin-relative by default, so resolve it against the page.
  const url = new URL(suffix, apiUrlBase())
  if (query) {
    for (const [key, value] of Object.entries(query)) {
      if (value === undefined || value === null || value === '') continue
      if (Array.isArray(value)) {
        value.forEach((v) => url.searchParams.append(key, String(v)))
      } else {
        url.searchParams.set(key, String(value))
      }
    }
  }
  return url.toString()
}

/** The active locale, read from storage because this module is outside React. */
function currentLocale(): 'ar' | 'en' {
  try {
    return localStorage.getItem(STORAGE.locale) === 'en' ? 'en' : 'ar'
  } catch {
    return 'ar'
  }
}

function currentLocaleHeader(): string {
  return currentLocale() === 'en' ? 'en' : 'ar-EG'
}

/* -------------------------------------------------------------------------- */
/*                              Error translation                              */
/* -------------------------------------------------------------------------- */

/**
 * Wording for the cases where the server gives us nothing safe to show. These
 * live here rather than in the locale files because the fetch client runs
 * outside React and cannot reach the `useI18n` hook.
 */
const FALLBACK = {
  unauthorized: {
    ar: 'محتاج تسجّل دخولك تاني عشان تكمّل.',
    en: 'Please sign in again to continue.',
  },
  forbidden: {
    ar: 'الصفحة دي مش ضمن صلاحياتك.',
    en: "This isn't part of what your account can reach.",
  },
  notFound: {
    ar: 'مش لاقيين اللي بتدوّر عليه.',
    en: "We couldn't find what you're looking for.",
  },
  conflict: {
    ar: 'العملية دي اتعملت قبل كده أو فيه تعارض في البيانات.',
    en: 'That was already done, or the data has changed since you loaded it.',
  },
  tooMany: {
    ar: 'محاولات كتير في وقت قصير — استنى شوية وجرّب تاني.',
    en: 'Too many attempts in a short time — wait a moment and try again.',
  },
  server: {
    ar: 'السيرفر مش مستجيب دلوقتي. جرّب تاني بعد لحظات.',
    en: "The server isn't responding right now. Try again in a moment.",
  },
  unknown: { ar: 'حصل خطأ غير متوقع.', en: 'Something went wrong.' },
  offline: {
    ar: 'مفيش اتصال بالإنترنت دلوقتي. اتأكد من الشبكة وجرّب تاني.',
    en: "You're offline. Check your connection and try again.",
  },
  timeout: {
    ar: 'الطلب أخد وقت أطول من اللازم. جرّب تاني.',
    en: 'That took longer than expected. Try again.',
  },
} as const

const fallback = (key: keyof typeof FALLBACK) => FALLBACK[key][currentLocale()]

/** ProblemDetails in, a sentence a student can read out. */
function friendlyMessage(status: number, problem: ProblemDocument | null): string {
  const fieldMessages = problem?.errors ? Object.values(problem.errors).flat() : []
  if (fieldMessages.length === 1) return fieldMessages[0]

  const fromServer = problem?.detail || problem?.title
  const generic =
    status === 401
      ? fallback('unauthorized')
      : status === 403
        ? fallback('forbidden')
        : status === 404
          ? fallback('notFound')
          : status === 409
            ? fallback('conflict')
            : status === 429
              ? fallback('tooMany')
              : status >= 500
                ? fallback('server')
                : fallback('unknown')

  // 500s frequently leak stack traces or type names — never show those.
  if (status >= 500) return generic
  if (fieldMessages.length > 1) return fieldMessages[0]
  return fromServer && fromServer.length < 180 ? fromServer : generic
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: ProblemDocument | null = null
  try {
    const text = await response.text()
    if (text) {
      const parsed = JSON.parse(text) as ProblemDocument
      if (parsed && typeof parsed === 'object') problem = parsed
    }
  } catch {
    /* Non-JSON error body (HTML error page, empty 403) — the status carries the meaning. */
  }

  return new ApiError({
    kind: kindForStatus(response.status),
    status: response.status,
    message: friendlyMessage(response.status, problem),
    detail: problem?.detail ?? undefined,
    fieldErrors: problem?.errors ?? {},
    traceId: problem?.traceId,
    code: problem?.code,
    problem: problem ?? undefined,
  })
}

const networkError = () =>
  new ApiError({
    kind: 'network',
    status: 0,
    message: fallback('offline'),
  })

const timeoutError = () =>
  new ApiError({
    kind: 'timeout',
    status: 0,
    message: fallback('timeout'),
  })

/* -------------------------------------------------------------------------- */
/*                         Refresh token — single flight                       */
/* -------------------------------------------------------------------------- */

let refreshInFlight: Promise<string | null> | null = null

async function performRefresh(): Promise<string | null> {
  const refreshToken = getRefreshToken()
  if (!refreshToken) return null

  try {
    const response = await fetch(buildUrl('/auth/refresh-token'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      body: JSON.stringify({ refreshToken }),
    })
    if (!response.ok) return null
    const envelope = (await response.json()) as ApiEnvelope<AuthenticationResponse>
    if (!envelope?.data?.accessToken) return null
    setSessionFromAuth(envelope.data)
    return envelope.data.accessToken
  } catch {
    return null
  }
}

/**
 * Concurrent 401s share one refresh call; everyone waits on the same promise so
 * the backend never sees a stampede of refresh requests.
 */
export function refreshAccessToken(): Promise<string | null> {
  if (!refreshInFlight) {
    refreshInFlight = performRefresh().finally(() => {
      refreshInFlight = null
    })
  }
  return refreshInFlight
}

function endSession(): void {
  clearSession()
  emitAuthExpired()
}

/* -------------------------------------------------------------------------- */
/*                                   Request                                   */
/* -------------------------------------------------------------------------- */

const sleep = (ms: number) => new Promise((resolve) => setTimeout(resolve, ms))

async function rawFetch(
  path: string,
  options: RequestOptions,
  token: string | null,
): Promise<Response> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort('timeout'), REQUEST_TIMEOUT_MS)

  const onAbort = () => controller.abort(options.signal?.reason)
  options.signal?.addEventListener('abort', onAbort, { once: true })

  const headers: Record<string, string> = {
    Accept: 'application/json',
    'Accept-Language': currentLocaleHeader(),
  }
  if (token) headers.Authorization = `Bearer ${token}`

  let body: BodyInit | undefined
  if (options.form) {
    body = options.form // Let the browser set the multipart boundary.
  } else if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(options.body)
  }

  try {
    return await fetch(buildUrl(path, options.query), {
      method: options.method ?? 'GET',
      headers,
      body,
      signal: controller.signal,
    })
  } finally {
    clearTimeout(timer)
    options.signal?.removeEventListener('abort', onAbort)
  }
}

/**
 * Performs one API call and returns the unwrapped `data` payload.
 * Handles auth refresh, retries, and turns every failure into an `ApiError`.
 */
export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const isRead = (options.method ?? 'GET') === 'GET'
  const maxRetries = options.retries ?? (isRead ? 2 : 0)

  let token = options.anonymous ? null : getAccessToken()

  // Refresh proactively rather than paying for a guaranteed 401 round-trip.
  if (!options.anonymous && token && isAccessTokenExpiring()) {
    token = (await refreshAccessToken()) ?? token
  }

  for (let attempt = 0; ; attempt++) {
    let response: Response
    try {
      response = await rawFetch(path, options, token)
    } catch (error) {
      if (options.signal?.aborted) throw error
      const isTimeout =
        (error as Error)?.name === 'AbortError' || (error as Error)?.name === 'TimeoutError'
      const apiError = isTimeout ? timeoutError() : networkError()
      if (attempt < maxRetries) {
        await sleep(400 * 2 ** attempt)
        continue
      }
      throw apiError
    }

    if (response.status === 401 && !options.anonymous) {
      const fresh = await refreshAccessToken()
      if (fresh) {
        token = fresh
        // One retry with the new token; a second 401 means the session is truly gone.
        const retried = await rawFetch(path, options, token)
        if (retried.ok) return unwrap<T>(retried)
        if (retried.status === 401) {
          endSession()
          throw await toApiError(retried)
        }
        throw await toApiError(retried)
      }
      endSession()
      throw await toApiError(response)
    }

    if (response.ok) return unwrap<T>(response)

    const apiError = await toApiError(response)
    if (apiError.isRetryable && attempt < maxRetries && isRead) {
      await sleep(400 * 2 ** attempt)
      continue
    }
    throw apiError
  }
}

async function unwrap<T>(response: Response): Promise<T> {
  if (response.status === 204) return undefined as T
  const text = await response.text()
  if (!text) return undefined as T

  let parsed: unknown
  try {
    parsed = JSON.parse(text)
  } catch {
    return undefined as T
  }

  // Successful responses are always `{ success, message, data }`.
  if (parsed && typeof parsed === 'object' && 'data' in (parsed as object)) {
    return (parsed as ApiEnvelope<T>).data
  }
  return parsed as T
}

/* Convenience verbs — every endpoint module builds on these. */
export const api = {
  get: <T>(path: string, query?: QueryParams, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'GET', query }),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'POST', body }),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'PUT', body }),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'PATCH', body }),
  delete: <T>(path: string, options?: RequestOptions) =>
    request<T>(path, { ...options, method: 'DELETE' }),
  upload: <T>(path: string, form: FormData, options?: RequestOptions) =>
    request<T>(path, { ...options, method: options?.method ?? 'POST', form }),
}
