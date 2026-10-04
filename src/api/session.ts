import { STORAGE } from '@/api/config'
import type { AuthenticationResponse, CurrentUserDto } from '@/api/schema'

/**
 * Token + identity storage, kept outside React so the fetch client can read it
 * synchronously during a retry. React subscribes via `subscribeToSession`.
 */

export interface Session {
  accessToken: string
  refreshToken: string
  /** Epoch milliseconds. */
  expiresAt: number
  user: CurrentUserDto
}

type Listener = (session: Session | null) => void

const listeners = new Set<Listener>()
let cached: Session | null | undefined

function readStorage(): Session | null {
  try {
    const accessToken = localStorage.getItem(STORAGE.accessToken)
    const refreshToken = localStorage.getItem(STORAGE.refreshToken)
    const expiresAt = Number(localStorage.getItem(STORAGE.expiresAt) ?? 0)
    const rawUser = localStorage.getItem(STORAGE.user)
    if (!accessToken || !refreshToken || !rawUser) return null
    return { accessToken, refreshToken, expiresAt, user: JSON.parse(rawUser) as CurrentUserDto }
  } catch {
    return null
  }
}

export function getSession(): Session | null {
  if (cached === undefined) cached = readStorage()
  return cached
}

export function getAccessToken(): string | null {
  return getSession()?.accessToken ?? null
}

export function getRefreshToken(): string | null {
  return getSession()?.refreshToken ?? null
}

/** True once the access token is inside its last minute of life. */
export function isAccessTokenExpiring(skewMs = 60_000): boolean {
  const session = getSession()
  if (!session?.expiresAt) return false
  return Date.now() >= session.expiresAt - skewMs
}

function persist(session: Session | null): void {
  cached = session
  try {
    if (!session) {
      localStorage.removeItem(STORAGE.accessToken)
      localStorage.removeItem(STORAGE.refreshToken)
      localStorage.removeItem(STORAGE.expiresAt)
      localStorage.removeItem(STORAGE.user)
    } else {
      localStorage.setItem(STORAGE.accessToken, session.accessToken)
      localStorage.setItem(STORAGE.refreshToken, session.refreshToken)
      localStorage.setItem(STORAGE.expiresAt, String(session.expiresAt))
      localStorage.setItem(STORAGE.user, JSON.stringify(session.user))
    }
  } catch {
    /* Private mode / storage blocked — the session still lives for this tab. */
  }
  listeners.forEach((listener) => listener(session))
}

export function setSessionFromAuth(auth: AuthenticationResponse): Session {
  const expiresAt = auth.accessTokenExpiresAt
    ? new Date(auth.accessTokenExpiresAt).getTime()
    : Date.now() + (auth.expiresInSeconds ?? 3600) * 1000
  const session: Session = {
    accessToken: auth.accessToken,
    refreshToken: auth.refreshToken,
    expiresAt,
    user: auth.user,
  }
  persist(session)
  return session
}

export function updateSessionUser(user: CurrentUserDto): void {
  const current = getSession()
  if (!current) return
  persist({ ...current, user })
}

export function clearSession(): void {
  persist(null)
}

export function subscribeToSession(listener: Listener): () => void {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

/* Keep multiple tabs in agreement about who is signed in. */
if (typeof window !== 'undefined') {
  window.addEventListener('storage', (event) => {
    if (event.key && !Object.values(STORAGE).includes(event.key as never)) return
    cached = readStorage()
    listeners.forEach((listener) => listener(cached ?? null))
  })
}
