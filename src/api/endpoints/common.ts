import type { QueryParams } from '@/api/client'
import type { Paginated } from '@/api/schema'

/** Paging + search shape shared by every list endpoint in the API. */
export interface PageQuery {
  Page?: number
  PageSize?: number
  Search?: string
  Sort?: string
}

/**
 * The window every analytics endpoint accepts. `From`/`To` are only read when
 * `Period` is `Custom`; sending them with any other period is ignored, not an
 * error. `Take` bounds the per-item breakdowns (subjects, exams).
 */
export type AnalyticsPeriod =
  | 'Today'
  | 'Last7Days'
  | 'Last30Days'
  | 'Last90Days'
  | 'Last12Months'
  | 'AllTime'
  | 'Custom'

export interface AnalyticsQuery {
  Period?: AnalyticsPeriod
  /** ISO timestamps. Required by the server when `Period` is `Custom`. */
  From?: string
  To?: string
  Take?: number
}

export const DEFAULT_PAGE_SIZE = 12

/** An empty page — lets a screen render its empty state without a null check. */
export function emptyPage<T>(pageSize = DEFAULT_PAGE_SIZE): Paginated<T> {
  return {
    items: [],
    pageNumber: 1,
    pageSize,
    totalCount: 0,
    totalPages: 0,
    hasPrevious: false,
    hasNext: false,
  }
}

/** Drops undefined/empty entries so the query string stays clean. */
export function params(input: object): QueryParams {
  const out: QueryParams = {}
  for (const [key, value] of Object.entries(input)) {
    if (value === undefined || value === null || value === '') continue
    if (Array.isArray(value)) {
      if (value.length) out[key] = value as (string | number)[]
      continue
    }
    out[key] = value as string | number | boolean
  }
  return out
}
