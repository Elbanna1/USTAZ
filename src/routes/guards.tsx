import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { Lock } from 'lucide-react'
import { useAuth, type AppRole } from '@/hooks/use-auth'
import { Button } from '@/components/ui/button'
import { Link } from 'react-router-dom'
import { useI18n } from '@/i18n'
import { BrandedSplash } from '@/components/layout/BrandedSplash'
import { usePageTitle } from '@/hooks/use-page-title'

/** Blocks a route until we know who is signed in; sends guests to login. */
export function RequireAuth({ children }: { children?: React.ReactNode }) {
  const { isAuthenticated, isBootstrapping } = useAuth()
  const location = useLocation()

  if (isBootstrapping) return <BrandedSplash />
  if (!isAuthenticated) {
    // Remember where they were headed so login can return them there.
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  return <>{children ?? <Outlet />}</>
}

/** Requires one of the given personas. Renders a friendly wall, never a redirect loop. */
export function RequireRole({
  roles,
  children,
}: {
  roles: AppRole[]
  children?: React.ReactNode
}) {
  const { isAuthenticated, isBootstrapping, hasRole, home } = useAuth()
  const location = useLocation()

  if (isBootstrapping) return <BrandedSplash />
  if (!isAuthenticated) {
    return <Navigate to="/login" replace state={{ from: location.pathname + location.search }} />
  }
  if (!hasRole(...roles)) return <ForbiddenWall home={home} />
  return <>{children ?? <Outlet />}</>
}

/** Keeps a signed-in user out of the auth screens. */
export function RedirectIfAuthenticated({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isBootstrapping, home } = useAuth()
  if (isBootstrapping) return <BrandedSplash />
  if (isAuthenticated) return <Navigate to={home} replace />
  return <>{children}</>
}

function ForbiddenWall({ home }: { home: string }) {
  const { t } = useI18n()
  usePageTitle(t('states.forbiddenTitle'))
  return (
    <div className="container-page flex min-h-[60vh] flex-col items-center justify-center gap-5 py-16 text-center">
      <span className="grid size-14 place-items-center rounded-2xl bg-warning/12 text-warning-strong">
        <Lock className="size-7" />
      </span>
      <div className="max-w-md space-y-2">
        <h1 className="font-display text-heading-lg text-foreground">
          {t('states.forbiddenTitle')}
        </h1>
        <p className="text-body-sm text-muted-foreground">{t('states.forbiddenDescription')}</p>
      </div>
      <Button asChild>
        <Link to={home}>{t('nav.dashboard')}</Link>
      </Button>
    </div>
  )
}
