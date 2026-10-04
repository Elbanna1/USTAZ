import { LogoPulse } from '@/components/brand/Logo'
import { useI18n } from '@/i18n'

/**
 * Shown while the stored session is being restored. Deliberately identical in
 * feel to the route fallback so a cold start reads as one continuous load.
 */
export function BrandedSplash({ label }: { label?: string }) {
  const { t } = useI18n()
  return (
    <div
      role="status"
      aria-live="polite"
      className="flex min-h-[60dvh] flex-col items-center justify-center gap-6 px-6"
    >
      <LogoPulse />
      <p className="text-body-sm text-muted-foreground">{label ?? t('common.loading')}</p>
    </div>
  )
}
