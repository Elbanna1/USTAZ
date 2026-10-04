import { ChevronLeft, ChevronRight } from 'lucide-react'
import { motion } from 'framer-motion'
import type { Paginated } from '@/api/schema'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useI18n } from '@/i18n'

interface PaginationProps {
  page: Pick<Paginated<unknown>, 'pageNumber' | 'pageSize' | 'totalCount' | 'totalPages' | 'hasNext' | 'hasPrevious'>
  onChange: (pageNumber: number) => void
  className?: string
  /** Hides the "showing X of Y" line in tight layouts. */
  compact?: boolean
}

/** Builds `1 … 4 5 6 … 20` so the control stays one row on a phone. */
function pageWindow(current: number, total: number): (number | 'gap')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1)
  const pages = new Set<number>([1, total, current, current - 1, current + 1])
  if (current <= 3) [2, 3, 4].forEach((p) => pages.add(p))
  if (current >= total - 2) [total - 3, total - 2, total - 1].forEach((p) => pages.add(p))

  const sorted = [...pages].filter((p) => p >= 1 && p <= total).sort((a, b) => a - b)
  const out: (number | 'gap')[] = []
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) out.push('gap')
    out.push(p)
  })
  return out
}

/** Server-side pagination control. Mirrors direction automatically in RTL. */
export function Pagination({ page, onChange, className, compact = false }: PaginationProps) {
  const { t, formatNumber, isRtl } = useI18n()
  const total = page.totalPages ?? 0
  if (total <= 1) return null

  const current = page.pageNumber ?? 1
  const Previous = isRtl ? ChevronRight : ChevronLeft
  const Next = isRtl ? ChevronLeft : ChevronRight

  const firstItem = (current - 1) * (page.pageSize ?? 0) + 1
  const lastItem = Math.min(current * (page.pageSize ?? 0), page.totalCount ?? 0)

  return (
    <nav
      className={cn('flex flex-wrap items-center justify-between gap-4 pt-2', className)}
      aria-label={t('common.page')}
    >
      {!compact && (
        <p className="text-caption text-muted-foreground">
          <span className="tnum">
            {formatNumber(firstItem)}–{formatNumber(lastItem)}
          </span>{' '}
          {t('common.of')}{' '}
          <span className="tnum font-semibold text-foreground">
            {formatNumber(page.totalCount ?? 0)}
          </span>
        </p>
      )}

      <div className="flex items-center gap-1">
        <Button
          variant="outline"
          size="icon"
          disabled={!page.hasPrevious}
          onClick={() => onChange(current - 1)}
          aria-label={t('common.previous')}
        >
          <Previous className="size-4" />
        </Button>

        {pageWindow(current, total).map((entry, i) =>
          entry === 'gap' ? (
            <span key={`gap-${i}`} className="px-1 text-muted-foreground" aria-hidden>
              …
            </span>
          ) : (
            <button
              key={entry}
              type="button"
              onClick={() => onChange(entry)}
              aria-current={entry === current ? 'page' : undefined}
              className={cn(
                'tnum relative grid size-9 place-items-center rounded-lg text-body-sm font-semibold transition-colors',
                entry === current
                  ? 'text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted hover:text-foreground',
              )}
            >
              {entry === current && (
                <motion.span
                  layoutId="pagination-active"
                  className="absolute inset-0 rounded-lg bg-primary"
                  transition={{ type: 'spring', stiffness: 420, damping: 34 }}
                />
              )}
              <span className="relative">{formatNumber(entry)}</span>
            </button>
          ),
        )}

        <Button
          variant="outline"
          size="icon"
          disabled={!page.hasNext}
          onClick={() => onChange(current + 1)}
          aria-label={t('common.next')}
        >
          <Next className="size-4" />
        </Button>
      </div>
    </nav>
  )
}
