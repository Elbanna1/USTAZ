import * as React from 'react'
import { ArrowUpDown } from 'lucide-react'
import { motion } from 'framer-motion'
import type { Paginated } from '@/api/schema'
import { cn } from '@/lib/utils'
import { Card } from '@/components/ui/card'
import { EmptyState } from '@/components/common/states'
import { Pagination } from '@/components/common/Pagination'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import type { SpotName } from '@/components/illustrations/Spot'
import { useI18n } from '@/i18n'
import { transitions } from '@/lib/motion'

export interface Column<T> {
  id: string
  header: string
  /** Renders the cell. Receives the whole row. */
  cell: (row: T) => React.ReactNode
  /** Sort accessor — omit to make the column unsortable. */
  sortValue?: (row: T) => string | number
  className?: string
  headerClassName?: string
  /** Hidden below `lg` so narrow screens show only the essentials. */
  hideOnMobile?: boolean
}

interface DataTableProps<T> {
  rows: T[]
  columns: Column<T>[]
  rowKey: (row: T) => string
  /** Rendered on phones instead of the table. */
  mobileCard?: (row: T) => React.ReactNode
  pageSize?: number
  loading?: boolean
  /** Rendered instead of the table when the query failed. */
  error?: unknown
  onRetry?: () => void
  empty?: {
    title: string
    description?: string
    spot?: SpotName
    action?: { label: string; onClick?: () => void; href?: string }
  }
  className?: string
  caption?: string
  /**
   * Server-paginated mode: pass the API page envelope and a page setter, and the
   * table stops slicing rows itself.
   */
  serverPage?: Paginated<unknown>
  onPageChange?: (page: number) => void
  /** Dims the table while a background refetch is in flight. */
  isFetching?: boolean
}

/**
 * Sortable, paginated table with a card fallback on small screens.
 * Sorting and paging are client-side because the data layer is local.
 */
export function DataTable<T>({
  rows,
  columns,
  rowKey,
  mobileCard,
  pageSize = 8,
  loading = false,
  error,
  onRetry,
  empty,
  className,
  caption,
  serverPage,
  onPageChange,
  isFetching = false,
}: DataTableProps<T>) {
  const { t, locale, formatNumber } = useI18n()
  const [sort, setSort] = React.useState<{ id: string; dir: 'asc' | 'desc' } | null>(null)
  const [page, setPage] = React.useState(1)
  const serverMode = Boolean(serverPage && onPageChange)

  // Any change to the underlying rows (filters, search) resets pagination.
  React.useEffect(() => setPage(1), [rows.length])

  const sorted = React.useMemo(() => {
    if (!sort) return rows
    const col = columns.find((c) => c.id === sort.id)
    if (!col?.sortValue) return rows
    const accessor = col.sortValue
    return [...rows].sort((a, b) => {
      const av = accessor(a)
      const bv = accessor(b)
      const cmp =
        typeof av === 'number' && typeof bv === 'number'
          ? av - bv
          : String(av).localeCompare(String(bv), locale === 'ar-EG' ? 'ar' : 'en')
      return sort.dir === 'asc' ? cmp : -cmp
    })
  }, [rows, sort, columns, locale])

  const totalPages = Math.max(1, Math.ceil(sorted.length / pageSize))
  const current = Math.min(page, totalPages)
  const pageRows = serverMode ? sorted : sorted.slice((current - 1) * pageSize, current * pageSize)

  function toggleSort(id: string) {
    setSort((s) =>
      s?.id === id ? (s.dir === 'asc' ? { id, dir: 'desc' } : null) : { id, dir: 'asc' },
    )
  }

  if (loading) {
    return (
      <Card className={cn('p-4', className)}>
        <RowSkeleton count={pageSize > 6 ? 6 : pageSize} />
      </Card>
    )
  }

  if (error) {
    return <QueryError error={error} onRetry={onRetry} className={className} compact />
  }

  if (rows.length === 0) {
    return (
      <EmptyState
        spot={empty?.spot ?? 'search'}
        title={empty?.title ?? t('states.noResultsTitle')}
        description={empty?.description ?? t('states.noResultsDescription')}
        action={empty?.action}
        className={className}
        compact
      />
    )
  }

  return (
    <div className={cn('space-y-4 transition-opacity', isFetching && 'opacity-60', className)}>
      {/* Mobile cards */}
      {mobileCard && (
        <div className="space-y-3 lg:hidden">
          {pageRows.map((row, i) => (
            <motion.div
              key={rowKey(row)}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ ...transitions.fast, delay: Math.min(i * 0.03, 0.2) }}
            >
              {mobileCard(row)}
            </motion.div>
          ))}
        </div>
      )}

      {/* Table */}
      <Card className={cn('overflow-hidden', mobileCard && 'hidden lg:block')}>
        <div className="overflow-x-auto">
          <table className="w-full text-body-sm">
            {caption && <caption className="sr-only">{caption}</caption>}
            <thead>
              <tr className="border-b border-border bg-muted/40">
                {columns.map((col) => (
                  <th
                    key={col.id}
                    scope="col"
                    className={cn(
                      'whitespace-nowrap p-4 text-start font-display text-caption font-bold text-muted-foreground',
                      col.hideOnMobile && 'hidden xl:table-cell',
                      col.headerClassName,
                    )}
                  >
                    {col.sortValue ? (
                      <button
                        type="button"
                        onClick={() => toggleSort(col.id)}
                        aria-label={`${t('common.filter')} — ${col.header}`}
                        className={cn(
                          'inline-flex items-center gap-1.5 rounded transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                          sort?.id === col.id && 'text-foreground',
                        )}
                      >
                        {col.header}
                        <ArrowUpDown
                          className={cn('size-3.5 opacity-50', sort?.id === col.id && 'opacity-100')}
                        />
                      </button>
                    ) : (
                      col.header
                    )}
                  </th>
                ))}
              </tr>
            </thead>

            <tbody>
              {pageRows.map((row) => (
                <tr
                  key={rowKey(row)}
                  className="border-b border-border transition-colors last:border-b-0 hover:bg-muted/35"
                >
                  {columns.map((col) => (
                    <td
                      key={col.id}
                      className={cn(
                        'p-4 align-middle',
                        col.hideOnMobile && 'hidden xl:table-cell',
                        col.className,
                      )}
                    >
                      {col.cell(row)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {/* Pagination */}
      {serverMode && serverPage ? (
        <Pagination page={serverPage} onChange={onPageChange!} />
      ) : totalPages > 1 ? (
        <nav className="flex flex-wrap items-center justify-between gap-3" aria-label={t('common.page')}>
          <p className="tnum text-caption text-muted-foreground">
            {formatNumber((current - 1) * pageSize + 1)}–
            {formatNumber(Math.min(current * pageSize, sorted.length))} {t('common.of')}{' '}
            {formatNumber(sorted.length)}
          </p>
          <Pagination
            page={{
              pageNumber: current,
              pageSize,
              totalCount: sorted.length,
              totalPages,
              hasNext: current < totalPages,
              hasPrevious: current > 1,
            }}
            onChange={setPage}
            compact
          />
        </nav>
      ) : null}
    </div>
  )
}
