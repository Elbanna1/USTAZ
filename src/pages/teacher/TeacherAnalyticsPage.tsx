import * as React from 'react'
import { useQueries, useQuery } from '@tanstack/react-query'
import { Star, TrendingUp, Users, Wallet } from 'lucide-react'
import { teacherAnalyticsApi } from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn, percent } from '@/lib/utils'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import {
  CategoryBarChart,
  DualLineChart,
  RevenueAreaChart,
  SplitDonutChart,
} from '@/components/dashboard/charts'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Progress } from '@/components/ui/primitives'
import { FilterChip } from '@/components/common/controls'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const RANGES = [3, 6, 12] as const

export default function TeacherAnalyticsPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  usePageTitle(t('dashboard.analytics'))

  const [months, setMonths] = React.useState<number>(6)

  const overview = useQuery({
    queryKey: qk.teacher.analytics(months),
    queryFn: () => teacherAnalyticsApi.overview(months),
  })

  const [revenue, growth, coursePerf, groupPerf, attendance, grades] = useQueries({
    queries: [
      { queryKey: qk.teacher.revenue(months), queryFn: () => teacherAnalyticsApi.revenue(months) },
      {
        queryKey: qk.teacher.studentGrowth(months),
        queryFn: () => teacherAnalyticsApi.studentGrowth(months),
      },
      { queryKey: qk.teacher.coursePerformance, queryFn: teacherAnalyticsApi.courses },
      { queryKey: qk.teacher.groupPerformance, queryFn: teacherAnalyticsApi.groups },
      { queryKey: qk.teacher.attendanceStats({}), queryFn: () => teacherAnalyticsApi.attendance() },
      { queryKey: qk.teacher.gradeAnalytics({}), queryFn: () => teacherAnalyticsApi.grades() },
    ],
  })

  const data = overview.data
  const revenueSeries = (revenue.data ?? []).map((p) => ({
    label: p.label ?? '',
    value: p.totalRevenue ?? 0,
    secondary: p.courseRevenue ?? 0,
  }))
  const growthSeries = (growth.data ?? []).map((p) => ({
    label: p.label ?? '',
    value: p.newStudents ?? 0,
    secondary: p.cumulativeStudents ?? 0,
  }))
  const hasRevenue = revenueSeries.some((p) => p.value > 0)
  const attendanceStats = attendance.data
  const gradeStats = grades.data

  return (
    <>
      <DashboardHeader
        title={t('dashboard.analytics')}
        description={
          locale === 'ar-EG'
            ? 'أرقام شغلك: الإيرادات، نمو الطلاب، الحضور، والدرجات.'
            : 'The numbers behind your work: revenue, student growth, attendance, and grades.'
        }
        actions={
          <div className="flex gap-2">
            {RANGES.map((value) => (
              <FilterChip
                key={value}
                label={
                  locale === 'ar-EG' ? `${formatNumber(value)} شهور` : `${formatNumber(value)} months`
                }
                active={months === value}
                onClick={() => setMonths(value)}
              />
            ))}
          </div>
        }
      />

      {overview.isPending ? (
        <StatSkeleton count={4} />
      ) : overview.isError ? (
        <QueryError error={overview.error} onRetry={() => overview.refetch()} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('dashboard.revenue')}
              value={data?.totalRevenue ?? 0}
              icon={<Wallet />}
              description={`${t('dashboard.monthlyRevenue')}: ${formatCurrency(data?.monthlyRevenue ?? 0)}`}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.totalStudents')}
              value={data?.totalStudents ?? 0}
              icon={<Users />}
              description={
                locale === 'ar-EG'
                  ? `+${formatNumber(data?.newStudentsThisMonth ?? 0)} الشهر ده`
                  : `+${formatNumber(data?.newStudentsThisMonth ?? 0)} this month`
              }
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={t('dashboard.averageRating')}
              value={data?.averageRating ?? 0}
              decimals={1}
              icon={<Star />}
              description={`${formatNumber(data?.totalReviews ?? 0)} ${t('common.reviews')}`}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'نسبة التحويل' : 'Conversion'}
              value={Math.round(data?.conversionPercentage ?? 0)}
              suffix="%"
              icon={<TrendingUp />}
              description={
                locale === 'ar-EG' ? 'من استفسار لاشتراك' : 'inquiries that become enrollments'
              }
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8 grid gap-8 xl:grid-cols-2">
        <DashboardSection title={t('dashboard.revenue')}>
          <Card className="p-5">
            {revenue.isPending ? (
              <div className="h-[260px] animate-pulse rounded-lg bg-muted" />
            ) : !hasRevenue ? (
              <EmptyState
                spot="payments"
                compact
                className="border-0 bg-transparent"
                title={locale === 'ar-EG' ? 'لسه مفيش إيرادات' : 'No revenue yet'}
              />
            ) : (
              <RevenueAreaChart data={revenueSeries} />
            )}
          </Card>
        </DashboardSection>

        <DashboardSection title={locale === 'ar-EG' ? 'نمو الطلاب' : 'Student growth'}>
          <Card className="p-5">
            {growth.isPending ? (
              <div className="h-[260px] animate-pulse rounded-lg bg-muted" />
            ) : growthSeries.every((p) => p.value === 0 && (p.secondary ?? 0) === 0) ? (
              <EmptyState
                spot="teachers"
                compact
                className="border-0 bg-transparent"
                title={locale === 'ar-EG' ? 'لسه مفيش طلاب' : 'No students yet'}
              />
            ) : (
              <DualLineChart
                data={growthSeries}
                seriesName={locale === 'ar-EG' ? 'طلاب جدد' : 'New students'}
                secondaryName={locale === 'ar-EG' ? 'الإجمالي' : 'Cumulative'}
              />
            )}
          </Card>
        </DashboardSection>

        <DashboardSection title={t('dashboard.attendance')}>
          <Card className="p-5">
            {attendance.isPending ? (
              <div className="h-[220px] animate-pulse rounded-lg bg-muted" />
            ) : (attendanceStats?.totalSessions ?? 0) === 0 ? (
              <EmptyState
                spot="calendar"
                compact
                className="border-0 bg-transparent"
                title={locale === 'ar-EG' ? 'مفيش حضور مسجّل' : 'No attendance recorded'}
              />
            ) : (
              <>
                <SplitDonutChart
                  data={[
                    {
                      label: locale === 'ar-EG' ? 'حاضر' : 'Present',
                      value: attendanceStats?.present ?? 0,
                    },
                    {
                      label: locale === 'ar-EG' ? 'متأخر' : 'Late',
                      value: attendanceStats?.late ?? 0,
                    },
                    {
                      label: locale === 'ar-EG' ? 'غايب' : 'Absent',
                      value: attendanceStats?.absent ?? 0,
                    },
                    {
                      label: locale === 'ar-EG' ? 'بعذر' : 'Excused',
                      value: attendanceStats?.excused ?? 0,
                    },
                  ]}
                  height={220}
                />
                <div className="mt-4 space-y-2.5 border-t border-border pt-4">
                  {(attendanceStats?.byGroup ?? []).map((group) => (
                    <div key={group.groupId} className="flex items-center gap-3">
                      <span className="min-w-0 flex-1 truncate text-body-sm text-muted-foreground">
                        {group.groupName}
                      </span>
                      <Progress
                        value={group.attendanceRate ?? 0}
                        className="h-1.5 w-24 shrink-0"
                      />
                      <span className="tnum w-10 shrink-0 text-end text-caption font-bold text-foreground">
                        {Math.round(group.attendanceRate ?? 0)}%
                      </span>
                    </div>
                  ))}
                </div>
              </>
            )}
          </Card>
        </DashboardSection>

        <DashboardSection title={t('dashboard.grades')}>
          <Card className="p-5">
            {grades.isPending ? (
              <div className="h-[220px] animate-pulse rounded-lg bg-muted" />
            ) : (gradeStats?.totalResults ?? 0) === 0 ? (
              <EmptyState
                spot="exams"
                compact
                className="border-0 bg-transparent"
                title={locale === 'ar-EG' ? 'لسه مفيش نتائج' : 'No results yet'}
              />
            ) : (
              <>
                <div className="grid grid-cols-3 gap-4">
                  <Metric
                    label={locale === 'ar-EG' ? 'نسبة النجاح' : 'Pass rate'}
                    value={`${Math.round(gradeStats?.passRate ?? 0)}%`}
                    tone={(gradeStats?.passRate ?? 0) >= 60 ? 'success' : 'destructive'}
                  />
                  <Metric
                    label={locale === 'ar-EG' ? 'المتوسط' : 'Average'}
                    value={`${Math.round(gradeStats?.averagePercentage ?? 0)}%`}
                  />
                  <Metric
                    label={locale === 'ar-EG' ? 'الأعلى' : 'Highest'}
                    value={`${Math.round(gradeStats?.highestPercentage ?? 0)}%`}
                    tone="success"
                  />
                </div>

                {(gradeStats?.bySubject?.length ?? 0) > 0 && (
                  <div className="mt-5 border-t border-border pt-4">
                    <CategoryBarChart
                      data={(gradeStats?.bySubject ?? []).map((s) => ({
                        label: locale === 'ar-EG' ? (s.subjectNameAr ?? '') : (s.subjectNameEn ?? ''),
                        value: Math.round(s.averagePercentage ?? 0),
                      }))}
                      height={180}
                      formatter={(v) => `${v}%`}
                      seriesName={locale === 'ar-EG' ? 'المتوسط' : 'Average'}
                    />
                  </div>
                )}
              </>
            )}
          </Card>
        </DashboardSection>
      </div>

      {/* ------------------------- Group performance ------------------------- */}
      <DashboardSection
        title={locale === 'ar-EG' ? 'أداء المجموعات' : 'Group performance'}
        className="mt-10"
      >
        {groupPerf.isPending ? (
          <RowSkeleton count={4} />
        ) : (groupPerf.data ?? []).length === 0 ? (
          <EmptyState
            spot="groups"
            compact
            title={locale === 'ar-EG' ? 'لسه مفيش مجموعات' : 'No groups yet'}
          />
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {(groupPerf.data ?? []).map((group) => (
              <Card key={group.groupId} className="p-5">
                <div className="flex items-start justify-between gap-3">
                  <p className="min-w-0 flex-1 truncate font-display text-body-sm font-bold text-foreground">
                    {group.name}
                  </p>
                  <Badge variant={group.isPublished ? 'success' : 'outline'} size="sm">
                    {group.isPublished ? t('common.published') : t('common.draft')}
                  </Badge>
                </div>

                <p className="tnum mt-3 font-display text-heading-sm font-extrabold text-foreground">
                  {formatCurrency(group.monthlyRevenue ?? 0)}
                </p>

                <div className="mt-3 flex items-center gap-2.5">
                  <Progress value={group.fillRatePercentage ?? 0} className="h-1.5 flex-1" />
                  <span className="tnum text-caption text-muted-foreground">
                    {formatNumber(group.enrolledStudentsCount ?? 0)}/
                    {formatNumber(group.capacity ?? 0)}
                  </span>
                </div>
              </Card>
            ))}
          </div>
        )}
      </DashboardSection>

      {/* ------------------------- Course performance ------------------------ */}
      <DashboardSection
        title={locale === 'ar-EG' ? 'أداء الكورسات' : 'Course performance'}
        className="mt-10"
      >
        {coursePerf.isPending ? (
          <RowSkeleton count={4} />
        ) : (coursePerf.data ?? []).length === 0 ? (
          <EmptyState
            spot="courses"
            compact
            title={locale === 'ar-EG' ? 'لسه مفيش كورسات' : 'No courses yet'}
          />
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
            {(coursePerf.data ?? []).map((course) => (
              <Card key={course.courseId} className="p-5">
                <p className="truncate font-display text-body-sm font-bold text-foreground">
                  {course.title}
                </p>

                <dl className="mt-3 grid grid-cols-3 gap-3 text-center">
                  <div>
                    <dt className="text-[0.6875rem] text-muted-foreground">
                      {t('common.students')}
                    </dt>
                    <dd className="tnum font-display text-body-sm font-bold text-foreground">
                      {formatNumber(course.enrollmentsCount ?? 0)}
                    </dd>
                  </div>
                  <div className="border-x border-border">
                    <dt className="text-[0.6875rem] text-muted-foreground">
                      {t('dashboard.completionRate')}
                    </dt>
                    <dd className="tnum font-display text-body-sm font-bold text-foreground">
                      {percent(course.completedCount ?? 0, course.enrollmentsCount || 1)}%
                    </dd>
                  </div>
                  <div>
                    <dt className="text-[0.6875rem] text-muted-foreground">
                      {t('dashboard.revenue')}
                    </dt>
                    <dd className="tnum font-display text-body-sm font-bold text-foreground">
                      {formatCurrency(course.totalRevenue ?? 0)}
                    </dd>
                  </div>
                </dl>

                <Progress
                  value={course.averageProgressPercentage ?? 0}
                  className="mt-3 h-1.5"
                />
              </Card>
            ))}
          </div>
        )}
      </DashboardSection>
    </>
  )
}

function Metric({
  label,
  value,
  tone,
}: {
  label: string
  value: string
  tone?: 'success' | 'destructive'
}) {
  return (
    <div className="rounded-xl border border-border bg-surface p-3 text-center">
      <p className="text-caption text-muted-foreground">{label}</p>
      <p
        className={cn(
          'tnum mt-1 font-display text-heading-sm font-extrabold',
          tone === 'success'
            ? 'text-success-strong'
            : tone === 'destructive'
              ? 'text-destructive-strong'
              : 'text-foreground',
        )}
      >
        {value}
      </p>
    </div>
  )
}
