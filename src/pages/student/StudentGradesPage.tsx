import { useQuery } from '@tanstack/react-query'
import { Award, GraduationCap, TrendingDown, TrendingUp } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { subjectKeyFor } from '@/lib/identity'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { CategoryBarChart } from '@/components/dashboard/charts'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Progress } from '@/components/ui/primitives'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import type { ExamResultDto } from '@/api/schema'

export default function StudentGradesPage() {
  const { t, locale, pick, formatNumber, formatDate } = useI18n()
  usePageTitle(t('dashboard.grades'))

  const grades = useQuery({
    queryKey: qk.student.grades,
    queryFn: studentApi.grades,
  })

  const data = grades.data
  const results = data?.results ?? []
  const bySubject = data?.bySubject ?? []

  const columns: Column<ExamResultDto>[] = [
    {
      id: 'exam',
      header: t('exams.title'),
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-display text-body-sm font-semibold text-foreground">
            {row.examTitle}
          </p>
          {row.groupName && (
            <p className="truncate text-caption text-muted-foreground">{row.groupName}</p>
          )}
        </div>
      ),
      sortValue: (row) => row.examTitle ?? '',
    },
    {
      id: 'date',
      header: t('common.date'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {row.examDate ? formatDate(row.examDate, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.examDate ?? '',
    },
    {
      id: 'score',
      header: t('exams.score'),
      cell: (row) =>
        row.isAbsent ? (
          <Badge variant="outline" size="sm">
            {locale === 'ar-EG' ? 'غايب' : 'Absent'}
          </Badge>
        ) : (
          <span className="tnum font-display text-body-sm font-bold text-foreground">
            {formatNumber(row.score ?? 0)} / {formatNumber(row.maximumScore ?? 0)}
          </span>
        ),
      sortValue: (row) => row.percentage ?? 0,
    },
    {
      id: 'percentage',
      header: '%',
      cell: (row) => (
        <div className="flex min-w-[6rem] items-center gap-2">
          <Progress
            value={row.percentage ?? 0}
            className="h-1.5 flex-1"
            indicatorClassName={
              row.isPassed ? 'bg-success' : row.isAbsent ? 'bg-muted-foreground' : 'bg-destructive'
            }
          />
          <span className="tnum w-10 text-end text-caption font-semibold text-foreground">
            {Math.round(row.percentage ?? 0)}%
          </span>
        </div>
      ),
      sortValue: (row) => row.percentage ?? 0,
    },
    {
      id: 'result',
      header: t('exams.result'),
      cell: (row) => (
        <Badge variant={row.isPassed ? 'success' : 'destructive'} size="sm">
          {row.isPassed ? t('exams.passed') : t('exams.failed')}
        </Badge>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('dashboard.grades')}
        description={
          locale === 'ar-EG'
            ? 'ملخّص أدائك في كل الامتحانات، ومتوسطك في كل مادة.'
            : 'A summary of your exam performance and your average per subject.'
        }
      />

      {grades.isPending ? (
        <StatSkeleton count={4} />
      ) : grades.isError ? (
        <QueryError error={grades.error} onRetry={() => grades.refetch()} />
      ) : (
        <>
          <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <RevealItem>
              <StatCard
                label={locale === 'ar-EG' ? 'امتحانات' : 'Exams taken'}
                value={data?.examsTaken ?? 0}
                icon={<GraduationCap />}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={t('exams.passed')}
                value={data?.examsPassed ?? 0}
                icon={<Award />}
                description={
                  data?.examsTaken
                    ? `${Math.round(((data.examsPassed ?? 0) / data.examsTaken) * 100)}%`
                    : undefined
                }
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={locale === 'ar-EG' ? 'المتوسط' : 'Average'}
                value={Math.round(data?.averagePercentage ?? 0)}
                suffix="%"
                icon={<TrendingUp />}
              />
            </RevealItem>
            <RevealItem>
              <StatCard
                label={locale === 'ar-EG' ? 'الأعلى / الأقل' : 'Best / worst'}
                value={Math.round(data?.highestPercentage ?? 0)}
                suffix="%"
                icon={<TrendingDown />}
                description={`${Math.round(data?.lowestPercentage ?? 0)}% ${locale === 'ar-EG' ? 'أقل درجة' : 'lowest'}`}
              />
            </RevealItem>
          </RevealGroup>

          <div className="mt-8 grid gap-8 xl:grid-cols-[1fr,1.4fr]">
            {/* -------------------------- By subject -------------------------- */}
            <DashboardSection title={locale === 'ar-EG' ? 'المتوسط لكل مادة' : 'Average by subject'}>
              {bySubject.length === 0 ? (
                <EmptyState
                  spot="exams"
                  compact
                  title={locale === 'ar-EG' ? 'لسه مفيش نتائج' : 'No results yet'}
                />
              ) : (
                <>
                  <Card className="p-4">
                    <CategoryBarChart
                      data={bySubject.map((s) => ({
                        label: pick(s.subjectNameAr, s.subjectNameEn),
                        value: Math.round(s.averagePercentage ?? 0),
                      }))}
                      height={Math.max(180, bySubject.length * 52)}
                      formatter={(v) => `${v}%`}
                    />
                  </Card>

                  <div className="space-y-2.5">
                    {bySubject.map((subject) => (
                      <Card key={subject.subjectId} className="flex items-center gap-3.5 p-4">
                        <SubjectTile
                          subject={subjectKeyFor({
                            nameAr: subject.subjectNameAr,
                            nameEn: subject.subjectNameEn,
                          })}
                          size="sm"
                        />
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-display text-body-sm font-semibold text-foreground">
                            {pick(subject.subjectNameAr, subject.subjectNameEn)}
                          </p>
                          <p className="tnum text-caption text-muted-foreground">
                            {formatNumber(subject.examsTaken ?? 0)}{' '}
                            {locale === 'ar-EG' ? 'امتحان' : 'exams'}
                          </p>
                        </div>
                        <span
                          className={cn(
                            'tnum font-display text-heading-sm font-extrabold',
                            (subject.averagePercentage ?? 0) >= 60
                              ? 'text-success-strong'
                              : 'text-destructive-strong',
                          )}
                        >
                          {Math.round(subject.averagePercentage ?? 0)}%
                        </span>
                      </Card>
                    ))}
                  </div>
                </>
              )}
            </DashboardSection>

            {/* --------------------------- All results ------------------------ */}
            <DashboardSection title={locale === 'ar-EG' ? 'كل النتائج' : 'All results'}>
              {grades.isPending ? (
                <RowSkeleton count={5} />
              ) : (
                <DataTable
                  rows={results}
                  columns={columns}
                  rowKey={(row) => row.id ?? `${row.examId}-${row.studentId}`}
                  pageSize={10}
                  empty={{
                    spot: 'exams',
                    title: locale === 'ar-EG' ? 'لسه مفيش نتائج' : 'No results yet',
                    description:
                      locale === 'ar-EG'
                        ? 'نتائجك هتظهر هنا أول ما مدرّسك ينشرها.'
                        : 'Your results appear here once your teacher publishes them.',
                  }}
                  mobileCard={(row) => (
                    <Card className="space-y-3 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <p className="min-w-0 flex-1 font-display text-body-sm font-bold text-foreground">
                          {row.examTitle}
                        </p>
                        <Badge variant={row.isPassed ? 'success' : 'destructive'} size="sm">
                          {row.isPassed ? t('exams.passed') : t('exams.failed')}
                        </Badge>
                      </div>
                      <div className="flex items-center gap-3">
                        <Progress
                          value={row.percentage ?? 0}
                          className="h-1.5 flex-1"
                          indicatorClassName={row.isPassed ? 'bg-success' : 'bg-destructive'}
                        />
                        <span className="tnum text-caption font-bold text-foreground">
                          {formatNumber(row.score ?? 0)}/{formatNumber(row.maximumScore ?? 0)}
                        </span>
                      </div>
                    </Card>
                  )}
                />
              )}
            </DashboardSection>
          </div>
        </>
      )}
    </>
  )
}
