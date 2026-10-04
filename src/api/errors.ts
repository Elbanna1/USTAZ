import type { ProblemDocument } from '@/api/schema'

export type ApiErrorKind =
  | 'network'
  | 'timeout'
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'rateLimited'
  | 'server'
  | 'unknown'

/**
 * The one error type the UI ever sees. Raw ProblemDetails never reaches a screen —
 * components read `kind`, `fieldErrors`, and the already-friendly `message`.
 */
export class ApiError extends Error {
  readonly kind: ApiErrorKind
  readonly status: number
  readonly detail?: string
  readonly fieldErrors: Record<string, string[]>
  readonly traceId?: string
  /** The API's business-rule code, when it sent one. */
  readonly code?: string
  readonly problem?: ProblemDocument

  constructor(init: {
    kind: ApiErrorKind
    status: number
    message: string
    detail?: string
    fieldErrors?: Record<string, string[]>
    traceId?: string
    code?: string
    problem?: ProblemDocument
  }) {
    super(init.message)
    this.name = 'ApiError'
    this.kind = init.kind
    this.status = init.status
    this.detail = init.detail
    this.fieldErrors = init.fieldErrors ?? {}
    this.traceId = init.traceId
    this.code = init.code
    this.problem = init.problem
  }

  get isValidation(): boolean {
    return this.kind === 'validation' || Object.keys(this.fieldErrors).length > 0
  }

  /** A plan limit or verification gate rather than a mistake the user made. */
  get isBusinessRule(): boolean {
    return this.status === 422 || Boolean(this.code)
  }

  /** Transient failures worth an automatic retry. */
  get isRetryable(): boolean {
    return this.kind === 'network' || this.kind === 'timeout' || this.kind === 'server'
  }

  /** First message recorded against a form field, if any. */
  fieldError(field: string): string | undefined {
    const direct = this.fieldErrors[field] ?? this.fieldErrors[capitalize(field)]
    if (direct?.length) return direct[0]
    const key = Object.keys(this.fieldErrors).find(
      (k) => k.toLowerCase().replace(/^\$\./, '') === field.toLowerCase(),
    )
    return key ? this.fieldErrors[key][0] : undefined
  }
}

function capitalize(value: string): string {
  return value.charAt(0).toUpperCase() + value.slice(1)
}

export function kindForStatus(status: number): ApiErrorKind {
  if (status === 400 || status === 422) return 'validation'
  // 422 keeps the `validation` kind so forms still read field errors from it;
  // `isBusinessRule` is what separates a plan gate from a bad input.
  if (status === 401) return 'unauthorized'
  if (status === 403) return 'forbidden'
  if (status === 404) return 'notFound'
  if (status === 409) return 'conflict'
  if (status === 429) return 'rateLimited'
  if (status >= 500) return 'server'
  return 'unknown'
}

export function isApiError(error: unknown): error is ApiError {
  return error instanceof ApiError
}
