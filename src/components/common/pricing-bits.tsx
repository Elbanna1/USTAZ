import { Tag, Users } from 'lucide-react'
import type { PriceBreakdown } from '@/types'
import { cn, percent } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { useI18n } from '@/i18n'

/* -------------------------------- Price tag ------------------------------- */

interface PriceTagProps {
  breakdown: PriceBreakdown
  /** Suffix shown after the amount, e.g. "/ شهريًا". */
  period?: string
  size?: 'sm' | 'md' | 'lg'
  align?: 'start' | 'end'
  className?: string
  showBadge?: boolean
  /** ISO code from the API; defaults to Egyptian pounds. */
  currency?: string
}

const PRICE_SIZES = {
  sm: { main: 'text-[1.125rem]', old: 'text-caption' },
  md: { main: 'text-price', old: 'text-body-sm' },
  lg: { main: 'text-[2rem]', old: 'text-body' },
} as const

export function PriceTag({
  breakdown,
  period,
  size = 'md',
  align = 'start',
  className,
  showBadge = true,
  currency = 'EGP',
}: PriceTagProps) {
  const s = PRICE_SIZES[size]
  const { t, formatCurrency, locale } = useI18n()
  const discounted = breakdown.discountAmount > 0
  const isFree = breakdown.final <= 0

  return (
    <div className={cn('flex flex-col gap-1', align === 'end' && 'items-end', className)}>
      <div className="flex flex-wrap items-baseline gap-2">
        <span
          className={cn(
            'tnum font-display font-extrabold leading-none tracking-tight text-foreground',
            s.main,
          )}
        >
          {isFree ? t('common.free') : formatCurrency(breakdown.final, currency)}
        </span>
        {period && <span className="text-caption text-muted-foreground">{period}</span>}
      </div>

      {discounted && (
        <div className="flex flex-wrap items-center gap-2">
          <span className={cn('tnum text-muted-foreground line-through', s.old)}>
            {formatCurrency(breakdown.original, currency)}
          </span>
          {showBadge && (
            <Badge variant="destructive" size="sm" className="tnum">
              {locale === 'ar-EG' ? `وفّر ${breakdown.percentOff}%` : `Save ${breakdown.percentOff}%`}
            </Badge>
          )}
        </div>
      )}
    </div>
  )
}

/* ------------------------------ Discount badge ---------------------------- */

export function DiscountRibbon({
  percentOff,
  label,
  className,
}: {
  percentOff: number
  label?: string
  className?: string
}) {
  const { locale } = useI18n()
  if (percentOff <= 0) return null
  const discountLabel = locale === 'ar-EG' ? `خصم ${percentOff}%` : `${percentOff}% off`
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full bg-gradient-to-l from-accent to-accent-soft px-3 py-1 font-display text-caption font-bold text-accent-foreground shadow-soft',
        className,
      )}
    >
      <Tag className="size-3.5" aria-hidden />
      <span className="tnum">{discountLabel}</span>
      {label && <span className="opacity-80">· {label}</span>}
    </span>
  )
}

/* ------------------------------- Seats meter ------------------------------ */

export function SeatsMeter({
  taken,
  total,
  className,
  showLabel = true,
}: {
  taken: number
  total: number
  className?: string
  showLabel?: boolean
}) {
  const { locale, formatNumber } = useI18n()
  const filled = percent(taken, total)
  const left = Math.max(0, total - taken)
  const critical = left === 0
  const low = !critical && left <= Math.max(3, Math.round(total * 0.15))

  return (
    <div className={cn('space-y-1.5', className)}>
      {showLabel && (
        <div className="flex items-center justify-between gap-2 text-caption">
          <span className="inline-flex items-center gap-1.5 text-muted-foreground">
            <Users className="size-3.5" aria-hidden />
            <span className="tnum">
              {locale === 'ar-EG'
                ? `${formatNumber(taken)} من ${formatNumber(total)} مكان`
                : `${formatNumber(taken)} of ${formatNumber(total)} seats`}
            </span>
          </span>
          <span
            className={cn(
              'tnum font-semibold',
              critical ? 'text-destructive-strong' : low ? 'text-warning-strong' : 'text-success-strong',
            )}
          >
            {critical
              ? locale === 'ar-EG'
                ? 'مكتملة'
                : 'Full'
              : locale === 'ar-EG'
                ? `فاضل ${formatNumber(left)}`
                : `${formatNumber(left)} left`}
          </span>
        </div>
      )}
      <div
        className="h-1.5 w-full overflow-hidden rounded-full bg-muted"
        role="progressbar"
        aria-valuenow={filled}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-label={locale === 'ar-EG' ? 'الأماكن المحجوزة' : 'Seats taken'}
      >
        <div
          className={cn(
            'h-full rounded-full transition-[width] duration-700 ease-premium',
            critical ? 'bg-destructive' : low ? 'bg-warning' : 'bg-primary',
          )}
          style={{ width: `${filled}%` }}
        />
      </div>
    </div>
  )
}
