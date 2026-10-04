import * as React from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { AUTH_EXPIRED_EVENT } from '@/api/client'
import { authApi } from '@/api/endpoints/auth'
import {
  clearSession,
  getSession,
  setSessionFromAuth,
  subscribeToSession,
  updateSessionUser,
  type Session,
} from '@/api/session'
import { isApiError } from '@/api/errors'
import { teamApi } from '@/api/endpoints/tenant'
import type {
  AuthenticationResponse,
  CurrentUserDto,
  LoginCommand,
  RegisterParentCommand,
  RegisterStudentCommand,
  RegisterTeacherCommand,
} from '@/api/schema'

/** The five personas the product is built around. */
export type AppRole = 'admin' | 'teacher' | 'assistant' | 'parent' | 'student'

/** Highest-privilege role first — this is the order a multi-role user lands in. */
export const ROLE_PRIORITY: AppRole[] = ['admin', 'teacher', 'assistant', 'parent', 'student']

export const ROLE_HOME: Record<AppRole, string> = {
  admin: '/admin/dashboard',
  teacher: '/teacher/dashboard',
  assistant: '/teacher/dashboard',
  parent: '/parent/dashboard',
  student: '/student/dashboard',
}

const TENANT_ASSISTANT_ROLES = ['Assistant', 'Moderator', 'ContentManager']

/** Turns the backend's identity roles + workspace role into our persona list. */
export function rolesForUser(user: CurrentUserDto | null | undefined): AppRole[] {
  if (!user) return []
  const identity = user.roles ?? []
  const roles = new Set<AppRole>()

  if (identity.includes('Admin')) roles.add('admin')
  if (identity.includes('Teacher')) roles.add('teacher')
  if (identity.includes('Parent')) roles.add('parent')
  if (identity.includes('Student')) roles.add('student')
  if (user.tenantRole && TENANT_ASSISTANT_ROLES.includes(user.tenantRole)) roles.add('assistant')

  // A workspace owner without the Teacher identity role still runs a teacher console.
  if (!roles.size && user.tenantId) roles.add('teacher')

  return ROLE_PRIORITY.filter((role) => roles.has(role))
}

interface AuthValue {
  user: CurrentUserDto | null
  session: Session | null
  isAuthenticated: boolean
  /** True only while the very first session restore is running. */
  isBootstrapping: boolean
  roles: AppRole[]
  role: AppRole | null
  hasRole: (...roles: AppRole[]) => boolean
  /** Permission strings granted to an assistant inside their workspace. */
  permissions: string[]
  can: (permission: string) => boolean
  home: string

  login: (input: LoginCommand) => Promise<CurrentUserDto>
  registerStudent: (input: RegisterStudentCommand) => Promise<CurrentUserDto>
  registerTeacher: (input: RegisterTeacherCommand) => Promise<CurrentUserDto>
  registerParent: (input: RegisterParentCommand) => Promise<CurrentUserDto>
  logout: () => Promise<void>
  refreshUser: () => Promise<CurrentUserDto | null>
  setUser: (user: CurrentUserDto) => void
}

const AuthContext = React.createContext<AuthValue | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const queryClient = useQueryClient()
  const [session, setSession] = React.useState<Session | null>(() => getSession())
  const [isBootstrapping, setIsBootstrapping] = React.useState(() => Boolean(getSession()))

  /* Keep React in step with the token store (other tabs, refresh, logout). */
  React.useEffect(() => subscribeToSession(setSession), [])

  /* A failed refresh ends the session exactly once, wherever it happened. */
  React.useEffect(() => {
    const onExpired = () => {
      setSession(null)
      queryClient.clear()
    }
    window.addEventListener(AUTH_EXPIRED_EVENT, onExpired)
    return () => window.removeEventListener(AUTH_EXPIRED_EVENT, onExpired)
  }, [queryClient])

  /* Revalidate the stored user once on boot — roles may have changed server-side. */
  React.useEffect(() => {
    let cancelled = false
    if (!getSession()) {
      setIsBootstrapping(false)
      return
    }
    authApi
      .me()
      .then((user) => {
        if (cancelled) return
        updateSessionUser(user)
      })
      .catch((error) => {
        // 401 already cleared the session inside the client; anything else
        // (offline, 5xx) keeps the cached user so the app stays usable.
        if (isApiError(error) && error.kind === 'unauthorized') clearSession()
      })
      .finally(() => {
        if (!cancelled) setIsBootstrapping(false)
      })
    return () => {
      cancelled = true
    }
  }, [])

  const adopt = React.useCallback(
    (auth: AuthenticationResponse) => {
      const next = setSessionFromAuth(auth)
      setSession(next)
      queryClient.clear()
      return next.user
    },
    [queryClient],
  )

  /**
   * Assistants carry granular workspace permissions. We read them from the member
   * record once; a failure just means the UI shows what the backend allows anyway.
   */
  const [permissions, setPermissions] = React.useState<string[]>([])
  const userId = session?.user?.id
  const tenantRole = session?.user?.tenantRole

  React.useEffect(() => {
    if (!userId || !tenantRole || !TENANT_ASSISTANT_ROLES.includes(tenantRole)) {
      setPermissions([])
      return
    }
    let cancelled = false
    teamApi
      .list({ PageSize: 100 })
      .then((page) => {
        if (cancelled) return
        const me = page.items.find((member) => member.userId === userId)
        setPermissions(me?.permissions ?? [])
      })
      .catch(() => undefined)
    return () => {
      cancelled = true
    }
  }, [userId, tenantRole])

  const value = React.useMemo<AuthValue>(() => {
    const user = session?.user ?? null
    const roles = rolesForUser(user)
    const role = roles[0] ?? null

    return {
      user,
      session,
      isAuthenticated: Boolean(session),
      isBootstrapping,
      roles,
      role,
      hasRole: (...wanted) => wanted.some((r) => roles.includes(r)),
      permissions,
      can: (permission) => roles.includes('admin') || permissions.includes(permission),
      home: role ? ROLE_HOME[role] : '/',

      login: async (input) => adopt(await authApi.login(input)),
      registerStudent: async (input) => adopt(await authApi.registerStudent(input)),
      registerTeacher: async (input) => adopt(await authApi.registerTeacher(input)),
      registerParent: async (input) => adopt(await authApi.registerParent(input)),

      logout: async () => {
        const refreshToken = session?.refreshToken
        if (refreshToken) {
          // Best effort — a failed revoke must never trap the user in the app.
          await authApi.logout(refreshToken).catch(() => undefined)
        }
        clearSession()
        setSession(null)
        queryClient.clear()
      },

      refreshUser: async () => {
        try {
          const user = await authApi.me()
          updateSessionUser(user)
          return user
        } catch {
          return null
        }
      },

      setUser: (next) => updateSessionUser(next),
    }
  }, [session, isBootstrapping, permissions, adopt, queryClient])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthValue {
  const ctx = React.useContext(AuthContext)
  if (!ctx) throw new Error('useAuth must be used inside <AuthProvider>')
  return ctx
}

/** Convenience for components that only branch on the active persona. */
export function useRole(): AppRole | null {
  return useAuth().role
}
