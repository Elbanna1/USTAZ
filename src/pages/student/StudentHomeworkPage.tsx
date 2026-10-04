import * as React from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { CalendarClock, ClipboardCheck, Paperclip } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import type { HomeworkDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { StatusBadge } from '@/components/common/StatusBadge'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const PAGE_SIZE = 12

const STATUSES = [
  { value: undefined, ar: 'الكل', en: 'All' },
  { value: 'NotSubmitted', ar: 'لسه متسلّمش', en: 'Not submitted' },
  { value: 'Submitted', ar: 'اتسلّم', en: 'Submitted' },
  { value: 'Graded', ar: 'اتصحّح', en: 'Graded' },
] as const

export default function StudentHomeworkPage() {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  usePageTitle(t('homework.title'))

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [status, setStatus] = React.useState<string>()
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Search: debounced.trim() || undefined, Status: status }),
    [page, debounced, status],
  )

  const homework = useQuery({
    queryKey: qk.student.homework(query),
    queryFn: () => studentApi.homework(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, status])

  const items = homework.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('homework.title')}
        description={
          locale === 'ar-EG'
            ? 'واجباتك ومواعيد التسليم ودرجاتك.'
            : 'Your assignments, deadlines, and grades.'
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="student-homework-search"
          />
          <div className="no-scrollbar flex gap-2 overflow-x-auto">
            {STATUSES.map((s) => (
              <FilterChip
                key={s.value ?? 'all'}
                label={s[lang]}
                active={status === s.value}
                onClick={() => setStatus(s.value)}
              />
            ))}
          </div>
        </div>
      </DashboardHeader>

      <div className={cn('transition-opacity', homework.isFetching && 'opacity-60')}>
        {homework.isPending ? (
          <RowSkeleton count={5} />
        ) : homework.isError ? (
          <QueryError error={homework.error} onRetry={() => homework.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="homework"
            title={locale === 'ar-EG' ? 'مفيش واجبات' : 'No homework'}
            description={
              locale === 'ar-EG'
                ? 'أول ما مدرّسك ينشر واجب هتلاقيه هنا.'
                : 'Assignments your teacher publishes will appear here.'
            }
          />
        ) : (
          <RevealGroup className="space-y-3">
            {items.map((item) => (
              <RevealItem key={item.id}>
                <HomeworkRow homework={item} />
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>

      {homework.data && items.length > 0 && (
        <Pagination page={homework.data} onChange={setPage} className="mt-8" />
      )}
    </>
  )
}

function HomeworkRow({ homework }: { homework: HomeworkDto }) {
  const { t, formatNumber, formatDate } = useI18n()
  const submission = homework.mySubmission
  const status = submission?.status ?? 'NotSubmitted'
  const overdue =
    status === 'NotSubmitted' && homework.dueAt ? new Date(homework.dueAt) < new Date() : false

  return (
    <Card className="overflow-hidden" interactive>
      <Link
        to={`/student/homework/${homework.id}`}
        className="flex flex-col gap-4 p-5 sm:flex-row sm:items-center"
      >
        <span
          className={cn(
            'grid size-11 shrink-0 place-items-center rounded-xl',
            status === 'Graded'
              ? 'bg-success/12 text-success-strong'
              : status === 'Submitted'
                ? 'bg-primary/12 text-primary'
                : overdue
                  ? 'bg-destructive/12 text-destructive-strong'
                  : 'bg-muted text-muted-foreground',
          )}
        >
          <ClipboardCheck className="size-5" />
        </span>

        <div className="min-w-0 flex-1">
          <p className="truncate font-display text-body font-bold text-foreground">
            {homework.title}
          </p>
          <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-caption text-muted-foreground">
            {homework.groupName && <span>{homework.groupName}</span>}
            {homework.dueAt && (
              <span className="tnum inline-flex items-center gap-1.5">
                <CalendarClock className="size-3.5" />
                {t('homework.dueAt')} {formatDate(homework.dueAt, 'd MMM')}
              </span>
            )}
            {(homework.attachments?.length ?? 0) > 0 && (
              <span className="inline-flex items-center gap-1.5">
                <Paperclip className="size-3.5" />
                {formatNumber(homework.attachments!.length)}
              </span>
            )}
          </div>
        </div>

        <div className="flex shrink-0 items-center gap-3">
          {status === 'Graded' && submission?.score != null && (
            <Badge variant="success" size="md" className="tnum">
              {formatNumber(submission.score)} / {formatNumber(submission.maximumScore ?? homework.maximumScore ?? 0)}
            </Badge>
          )}
          {overdue ? (
            <Badge variant="destructive" size="sm">
              {t('homework.overdue')}
            </Badge>
          ) : (
            <StatusBadge status={status} fallbackLabel={submission?.statusName} />
          )}
        </div>
      </Link>
    </Card>
  )
}
