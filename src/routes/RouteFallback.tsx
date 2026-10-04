import { LogoPulse } from '@/components/brand/Logo'

/** Suspense fallback shown while a route chunk loads. */
export function RouteFallback() {
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex min-h-[60dvh] flex-col items-center justify-center gap-6 px-6"
    >
      <LogoPulse />
      <p className="text-body-sm text-muted-foreground">لحظة واحدة…</p>
    </div>
  )
}
