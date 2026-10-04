/**
 * Single place that knows where the backend lives and what it is called.
 *
 * By default the app talks to the API on its own origin: in development Vite
 * proxies `/api` to the real host, and in production the app is served behind
 * the same host. That keeps every request same-origin, so the API's CORS
 * allow-list never has to know about the client.
 *
 * Set `VITE_API_BASE_URL` to call a different host directly — that origin must
 * then appear in the API's `Cors__AllowedOrigins`, or the browser will block it.
 */

const RAW_BASE = (import.meta.env.VITE_API_BASE_URL as string | undefined)?.trim() ?? ''

/** Base origin with any trailing slash removed — paths are always joined with one. */
export const API_ORIGIN = RAW_BASE.replace(/\/+$/, '')

/** Every USTAZ endpoint lives under this prefix. */
export const API_PREFIX = '/api/v1'

export const API_BASE_URL = `${API_ORIGIN}${API_PREFIX}`

/** Resolves a possibly-relative API URL against the right origin. */
export function apiUrlBase(): string {
  if (API_ORIGIN) return API_ORIGIN
  return typeof window === 'undefined' ? 'http://localhost' : window.location.origin
}

/** Where uploaded media lives when the API hands back a relative path. */
export function resolveMediaUrl(url: string | null | undefined): string | undefined {
  if (!url) return undefined
  if (/^(https?:)?\/\//i.test(url) || url.startsWith('data:') || url.startsWith('blob:')) return url
  return `${apiUrlBase()}/${url.replace(/^\/+/, '')}`
}

export const STORAGE = {
  accessToken: 'ustaz.auth.access',
  refreshToken: 'ustaz.auth.refresh',
  expiresAt: 'ustaz.auth.expires',
  user: 'ustaz.auth.user',
  locale: 'ustaz.locale',
} as const

/** Requests that take longer than this are treated as a network failure. */
export const REQUEST_TIMEOUT_MS = 25_000
