import * as React from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { CalendarClock, FileText, Timer, Trophy } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import type { ExamDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useI18n } from '@/i18n'
import { subjectKeyFor } from '@/lib/identity'
import { cn } from '@/lib/utils'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { SearchBar, FilterChip } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const PAGE_SIZE = 12

export default function StudentExamsPage() {
  const { t, locale, formatNumber, formatDateTime } = useI18n()
  usePageTitle(t('exams.title'))

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [upcoming, setUpcoming] = React.useState<boolean | undefined>(undefined)
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      Upcoming: upcoming,
    }),
    [page, debounced, upcoming],
  )

  const exams = useQuery({
    queryKey: qk.student.exams(query),
    queryFn: () => studentApi.exams(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, upcoming])

  const items = exams.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('exams.title')}
        description={
          locale === 'ar-EG'
            ? 'امتحاناتك القادمة والسابقة، ونتايجك أول ما تتنشر.'
            : 'Your upcoming and past exams — with results as soon as they are published.'
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="student-exams-search"
          />
          <div className="flex gap-2">
            <FilterChip
              label={t('common.all')}
              active={upcoming === undefined}
              onClick={() => setUpcoming(undefined)}
            />
            <FilterChip
              label={t('exams.upcoming')}
              active={upcoming === true}
              onClick={() => setUpcoming(true)}
            />
          </div>
        </div>
      </DashboardHeader>

      <div className={cn('transition-opacity', exams.isFetching && 'opacity-60')}>
        {exams.isPending ? (
          <CardGridSkeleton count={6} />
        ) : exams.isError ? (
          <QueryError error={exams.error} onRetry={() => exams.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="exams"
            title={locale === 'ar-EG' ? 'مفيش امتحانات' : 'No exams yet'}
            description={
              locale === 'ar-EG'
                ? 'أول ما مدرّسك ينشر امتحان هتلاقيه هنا.'
                : 'Exams your teacher publishes will appear here.'
            }
          />
        ) : (
          <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {items.map((exam) => (
              <RevealItem key={exam.id} className="h-full">
                <ExamCard exam={exam} formatDateTime={formatDateTime} formatNumber={formatNumber} />
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>

      {exams.data && items.length > 0 && (
        <Pagination page={exams.data} onChange={setPage} className="mt-8" />
      )}
    </>
  )
}

function ExamCard({
  exam,
  formatDateTime,
  formatNumber,
}: {
  exam: ExamDto
  formatDateTime: (value: string) => string
  formatNumber: (value: number) => string
}) {
  const { t, locale, pick } = useI18n()
  const subjectName = pick(exam.subjectNameAr, exam.subjectNameEn)
  const isPast = exam.examDate ? new Date(exam.examDate) < new Date() : false

  return (
    <Card className="flex h-full flex-col gap-4 p-5" interactive>
      <div className="flex items-start gap-3">
        <SubjectTile
          subject={subjectKeyFor({ nameAr: exam.subjectNameAr, nameEn: exam.subjectNameEn })}
          size="sm"
        />
        <div className="min-w-0 flex-1">
          <h3 className="line-clamp-2-rtl font-display text-body font-bold leading-snug text-foreground">
            {exam.title}
          </h3>
          {exam.groupName && (
            <p className="mt-0.5 truncate text-caption text-muted-foreground">{exam.groupName}</p>
          )}
        </div>
        <Badge variant={isPast ? 'default' : 'primary'} size="sm">
          {exam.typeName ?? exam.type}
        </Badge>
      </div>

      <ul className="space-y-2 text-body-sm text-muted-foreground">
        {exam.examDate && (
          <li className="flex items-center gap-2">
            <CalendarClock className="size-4 shrink-0 text-primary" />
            <span className="tnum">{formatDateTime(exam.examDate)}</span>
          </li>
        )}
        <li className="flex items-center gap-2">
          <Timer className="size-4 shrink-0 text-primary" />
          <span className="tnum">
            {formatNumber(exam.durationInMinutes ?? 0)} {locale === 'ar-EG' ? 'دقيقة' : 'min'}
          </span>
        </li>
        <li className="flex items-center gap-2">
          <Trophy className="size-4 shrink-0 text-primary" />
          <span className="tnum">
            {formatNumber(exam.maximumScore ?? 0)} {t('exams.score')}
          </span>
        </li>
        {(exam.questionsCount ?? 0) > 0 && (
          <li className="flex items-center gap-2">
            <FileText className="size-4 shrink-0 text-primary" />
            <span className="tnum">
              {formatNumber(exam.questionsCount ?? 0)}{' '}
              {locale === 'ar-EG' ? 'سؤال' : 'questions'}
            </span>
          </li>
        )}
      </ul>

      <div className="mt-auto flex items-center gap-2 border-t border-border pt-4">
        {subjectName && (
          <Badge variant="outline" size="sm">
            {subjectName}
          </Badge>
        )}
        <Button asChild size="sm" className="ms-auto">
          <Link to={`/student/exams/${exam.id}`}>{t('common.details')}</Link>
        </Button>
      </div>
    </Card>
  )
}
