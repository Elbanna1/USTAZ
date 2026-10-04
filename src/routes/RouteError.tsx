import { Link, isRouteErrorResponse, useRouteError } from 'react-router-dom'
import { Home, RotateCcw } from 'lucide-react'
import { Logo } from '@/components/brand/Logo'
import { Button } from '@/components/ui/button'
import { Spot } from '@/components/illustrations/Spot'
import { AmbientGlow, GoldRule } from '@/components/illustrations/CoverArt'

/** Router-level error boundary — catches render/loader failures per route tree. */
export function RouteError() {
  const error = useRouteError()

  const status = isRouteErrorResponse(error) ? error.status : undefined
  const message =
    isRouteErrorResponse(error)
      ? error.statusText || 'الصفحة مش متاحة'
      : error instanceof Error
        ? error.message
        : 'حصل خطأ غير متوقع'

  return (
    <div className="relative flex min-h-dvh items-center justify-center overflow-hidden bg-background px-6 py-16">
      <AmbientGlow />

      <div className="relative w-full max-w-lg text-center">
        <Link to="/" className="inline-block" aria-label="أستاذ">
          <Logo size="lg" />
        </Link>

        <Spot name="error" className="mx-auto mt-10 h-32" />

        <h1 className="mt-8 text-display-lg text-foreground">
          {status === 404 ? 'الصفحة مش موجودة' : 'حصل خطأ'}
        </h1>

        <p className="mt-4 text-body text-muted-foreground">
          {status === 404
            ? 'اللينك ده مش شغّال. جرّب ترجع للرئيسية.'
            : 'حاول تحدّث الصفحة. لو المشكلة فضلت، كلّمنا وهنساعدك.'}
        </p>

        {status !== 404 && (
          <p
            dir="ltr"
            className="mx-auto mt-5 max-w-md overflow-x-auto rounded-lg border border-border bg-muted/50 p-3 text-start font-latin text-caption text-muted-foreground"
          >
            {message}
          </p>
        )}

        <GoldRule className="mx-auto my-8 max-w-xs" />

        <div className="flex flex-wrap items-center justify-center gap-3">
          <Button onClick={() => window.location.reload()} size="lg">
            <RotateCcw className="size-4" />
            حدّث الصفحة
          </Button>
          <Button asChild variant="outline" size="lg">
            <Link to="/">
              <Home className="size-4" />
              الرجوع للرئيسية
            </Link>
          </Button>
        </div>
      </div>
    </div>
  )
}
