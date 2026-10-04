import { Star } from 'lucide-react'
import { cn, formatNumber } from '@/lib/utils'

interface RatingProps {
  value: number
  count?: number
  size?: 'sm' | 'md' | 'lg'
  /** `stars` draws five glyphs; `compact` shows one star + the number. */
  variant?: 'stars' | 'compact'
  className?: string
}

const SIZES = {
  sm: { star: 'size-3.5', text: 'text-caption' },
  md: { star: 'size-4', text: 'text-body-sm' },
  lg: { star: 'size-5', text: 'text-body' },
} as const

export function Rating({ value, count, size = 'md', variant = 'compact', className }: RatingProps) {
  const s = SIZES[size]
  const rounded = value.toFixed(1)

  if (variant === 'compact') {
    return (
      <span className={cn('inline-flex items-center gap-1.5', className)}>
        <Star className={cn(s.star, 'fill-accent text-accent')} aria-hidden />
        <span className={cn('tnum font-display font-bold text-foreground', s.text)}>{rounded}</span>
        {count !== undefined && (
          <span className={cn('tnum text-muted-foreground', s.text)}>({formatNumber(count)})</span>
        )}
        <span className="sr-only">تقييم {rounded} من ٥</span>
      </span>
    )
  }

  return (
    <span className={cn('inline-flex items-center gap-2', className)}>
      <span className="flex items-center gap-0.5" role="img" aria-label={`تقييم ${rounded} من ٥`}>
        {[0, 1, 2, 3, 4].map((i) => {
          const fill = Math.max(0, Math.min(1, value - i))
          return (
            <span key={i} className="relative inline-block">
              <Star className={cn(s.star, 'text-muted-foreground/30')} aria-hidden />
              <span
                className="absolute inset-0 overflow-hidden"
                style={{ width: `${fill * 100}%`, insetInlineStart: 0 }}
                aria-hidden
              >
                <Star className={cn(s.star, 'fill-accent text-accent')} />
              </span>
            </span>
          )
        })}
      </span>
      <span className={cn('tnum font-display font-bold text-foreground', s.text)}>{rounded}</span>
      {count !== undefined && (
        <span className={cn('tnum text-muted-foreground', s.text)}>
          · {formatNumber(count)} تقييم
        </span>
      )}
    </span>
  )
}

/** Horizontal bar breakdown of 5→1 star counts. */
export function RatingBreakdown({
  distribution,
  total,
  className,
}: {
  distribution: Record<1 | 2 | 3 | 4 | 5, number>
  total: number
  className?: string
}) {
  return (
    <div className={cn('space-y-2', className)}>
      {([5, 4, 3, 2, 1] as const).map((star) => {
        const n = distribution[star] ?? 0
        const pct = total ? Math.round((n / total) * 100) : 0
        return (
          <div key={star} className="flex items-center gap-3">
            <span className="tnum flex w-8 items-center gap-1 text-caption text-muted-foreground">
              {star}
              <Star className="size-3 fill-accent text-accent" aria-hidden />
            </span>
            <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-muted">
              <div
                className="h-full rounded-full bg-accent transition-[width] duration-700 ease-premium"
                style={{ width: `${pct}%` }}
              />
            </div>
            <span className="tnum w-10 text-end text-caption text-muted-foreground">{pct}%</span>
          </div>
        )
      })}
    </div>
  )
}
