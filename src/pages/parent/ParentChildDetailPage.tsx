import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { keepPreviousData, useQueries, useQuery } from '@tanstack/react-query'
import { ArrowLeft, CalendarDays, ClipboardCheck, FileText, GraduationCap, Wallet } from 'lucide-react'
import { parentApi } from '@/api/endpoints/parent'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatusBadge } from '@/components/common/StatusBadge'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { Pagination } from '@/components/common/Pagination'
import { RowSkeleton, StatSkeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { CategoryBarChart } from '@/components/dashboard/charts'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

export default function ParentChildDetailPage() {
  const { studentId = '' } = useParams()
  const { t, locale } = useI18n()

  const [child, overview] = useQueries({
    queries: [
      {
        queryKey: qk.parent.child(studentId),
        queryFn: () => parentApi.child(studentId),
        enabled: Boolean(studentId),
      },
      {
        queryKey: qk.parent.progress(studentId),
        queryFn: () => parentApi.progress(studentId),
        enabled: Boolean(studentId),
      },
    ],
  })

  usePageTitle(child.data?.displayName ?? t('parent.children'))

  if (child.isPending) return <RowSkeleton count={5} />
  if (child.isError) return <QueryError error={child.error} onRetry={() => child.refetch()} />

  const data = child.data!
  const stats = overview.data

  return (
    <>
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to="/parent/children">
          <ArrowLeft className="size-4 flip-rtl" />
          {t('parent.children')}
        </Link>
      </Button>

      <DashboardHeader
        title={data.displayName ?? ''}
        description={[data.gradeName, data.schoolName].filter(Boolean).join(' · ')}
      >
        <div className="flex flex-wrap items-center gap-4">
          <Avatar
            traits={avatarTraitsFor(studentId)}
            seed={studentId}
            name={data.displayName ?? ''}
            src={imageUrl(data.profileImageUrl)}
            size="lg"
          />
          <div className="flex flex-wrap gap-1.5">
            {data.relationship && (
              <Badge variant="outline" size="md">
                {data.relationship}
              </Badge>
            )}
            {data.isConfirmed ? (
              <Badge variant="success" size="md">
                {locale === 'ar-EG' ? 'مؤكد' : 'Confirmed'}
              </Badge>
            ) : (
              <Badge variant="warning" size="md">
                {locale === 'ar-EG' ? 'مستني موافقة الطالب' : 'Awaiting student approval'}
              </Badge>
            )}
          </div>
        </div>
      </DashboardHeader>

      {overview.isPending ? (
        <StatSkeleton count={4} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('dashboard.attendanceRate')}
              value={Math.round(stats?.attendanceRate ?? 0)}
              suffix="%"
              icon={<CalendarDays />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'متوسط الدرجات' : 'Average grade'}
              value={Math.round(stats?.averageGradePercentage ?? 0)}
              suffix="%"
              icon={<GraduationCap />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'واجبات مستحقة' : 'Pending homework'}
              value={stats?.pendingHomeworkCount ?? 0}
              icon={<ClipboardCheck />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'امتحانات قادمة' : 'Upcoming exams'}
              value={stats?.upcomingExamsCount ?? 0}
              icon={<FileText />}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <Tabs defaultValue="grades" className="mt-8">
        <TabsList>
          <TabsTrigger value="grades">{t('dashboard.grades')}</TabsTrigger>
          <TabsTrigger value="attendance">{t('dashboard.attendance')}</TabsTrigger>
          <TabsTrigger value="homework">{t('dashboard.homework')}</TabsTrigger>
          <TabsTrigger value="exams">{t('dashboard.exams')}</TabsTrigger>
          <TabsTrigger value="payments">{t('dashboard.payments')}</TabsTrigger>
        </TabsList>

        <TabsContent value="grades" className="mt-6">
          <GradesPanel studentId={studentId} />
        </TabsContent>
        <TabsContent value="attendance" className="mt-6">
          <AttendancePanel studentId={studentId} />
        </TabsContent>
        <TabsContent value="homework" className="mt-6">
          <HomeworkPanel studentId={studentId} />
        </TabsContent>
        <TabsContent value="exams" className="mt-6">
          <ExamsPanel studentId={studentId} />
        </TabsContent>
        <TabsContent value="payments" className="mt-6">
          <PaymentsPanel studentId={studentId} />
        </TabsContent>
      </Tabs>
    </>
  )
}

/* -------------------------------------------------------------------------- */

function GradesPanel({ studentId }: { studentId: string }) {
  const { t, locale, pick, formatNumber } = useI18n()
  const grades = useQuery({
    queryKey: qk.parent.grades(studentId),
    queryFn: () => parentApi.grades(studentId),
  })

  if (grades.isPending) return <RowSkeleton count={4} />
  if (grades.isError) return <QueryError error={grades.error} onRetry={() => grades.refetch()} />

  const data = grades.data!
  const results = data.results ?? []

  if (results.length === 0) {
    return (
      <EmptyState
        spot="exams"
        title={locale === 'ar-EG' ? 'لسه مفيش نتائج' : 'No results yet'}
        description={
          locale === 'ar-EG'
            ? 'نتائج الامتحانات هتظهر هنا أول ما تتنشر.'
            : 'Exam results appear here once published.'
        }
      />
    )
  }

  return (
    <div className="space-y-6">
      {(data.bySubject?.length ?? 0) > 0 && (
        <Card className="p-5">
          <h3 className="mb-4 font-display text-heading-sm text-foreground">
            {locale === 'ar-EG' ? 'المتوسط لكل مادة' : 'Average by subject'}
          </h3>
          <CategoryBarChart
            data={data.bySubject!.map((s) => ({
              label: pick(s.subjectNameAr, s.subjectNameEn),
              value: Math.round(s.averagePercentage ?? 0),
            }))}
            height={Math.max(180, (data.bySubject?.length ?? 1) * 52)}
            formatter={(v) => `${v}%`}
            seriesName={locale === 'ar-EG' ? 'المتوسط' : 'Average'}
          />
        </Card>
      )}

      <div className="space-y-3">
        {results.map((result) => (
          <Card key={result.id} className="flex flex-wrap items-center gap-4 p-4">
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {result.examTitle}
              </p>
              <p className="truncate text-caption text-muted-foreground">{result.groupName}</p>
            </div>
            <div className="flex min-w-[8rem] items-center gap-2">
              <Progress
                value={result.percentage ?? 0}
                className="h-1.5 flex-1"
                indicatorClassName={result.isPassed ? 'bg-success' : 'bg-destructive'}
              />
              <span className="tnum text-caption font-bold text-foreground">
                {formatNumber(result.score ?? 0)}/{formatNumber(result.maximumScore ?? 0)}
              </span>
            </div>
            <Badge variant={result.isPassed ? 'success' : 'destructive'} size="sm">
              {result.isPassed ? t('exams.passed') : t('exams.failed')}
            </Badge>
          </Card>
        ))}
      </div>
    </div>
  )
}

function AttendancePanel({ studentId }: { studentId: string }) {
  const { locale, formatDate } = useI18n()
  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: 15 }), [page])

  const attendance = useQuery({
    queryKey: qk.parent.attendance(studentId, query),
    queryFn: () => parentApi.attendance(studentId, query),
    placeholderData: keepPreviousData,
  })

  if (attendance.isPending) return <RowSkeleton count={5} />
  if (attendance.isError)
    return <QueryError error={attendance.error} onRetry={() => attendance.refetch()} />

  const items = attendance.data?.items ?? []
  if (items.length === 0) {
    return (
      <EmptyState
        spot="calendar"
        title={locale === 'ar-EG' ? 'مفيش سجل حضور' : 'No attendance records'}
      />
    )
  }

  return (
    <>
      <div className="space-y-2.5">
        {items.map((record) => (
          <Card key={record.id} className="flex flex-wrap items-center gap-3 p-4">
            <span className="tnum min-w-[6rem] font-display text-body-sm font-bold text-foreground">
              {record.sessionDate ? formatDate(record.sessionDate, 'd MMM yyyy') : '—'}
            </span>
            <span className="min-w-0 flex-1 truncate text-body-sm text-muted-foreground">
              {record.groupName}
            </span>
            {record.minutesLate ? (
              <span className="tnum text-caption text-warning-strong">
                {locale === 'ar-EG' ? `تأخير ${record.minutesLate} د` : `${record.minutesLate} min late`}
              </span>
            ) : null}
            <StatusBadge status={record.status} fallbackLabel={record.statusName} />
          </Card>
        ))}
      </div>
      {attendance.data && <Pagination page={attendance.data} onChange={setPage} className="mt-6" />}
    </>
  )
}

function HomeworkPanel({ studentId }: { studentId: string }) {
  const { t, locale, formatNumber, formatDate } = useI18n()
  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: 15 }), [page])

  const homework = useQuery({
    queryKey: qk.parent.homework(studentId, query),
    queryFn: () => parentApi.homework(studentId, query),
    placeholderData: keepPreviousData,
  })

  if (homework.isPending) return <RowSkeleton count={5} />
  if (homework.isError)
    return <QueryError error={homework.error} onRetry={() => homework.refetch()} />

  const items = homework.data?.items ?? []
  if (items.length === 0) {
    return (
      <EmptyState spot="homework" title={locale === 'ar-EG' ? 'مفيش واجبات' : 'No homework'} />
    )
  }

  return (
    <>
      <div className="space-y-2.5">
        {items.map((submission) => (
          <Card key={submission.id} className="flex flex-wrap items-center gap-3 p-4">
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {submission.homeworkTitle}
              </p>
              <p className="tnum truncate text-caption text-muted-foreground">
                {submission.dueAt
                  ? `${t('homework.dueAt')} ${formatDate(submission.dueAt, 'd MMM')}`
                  : ''}
                {submission.isLate ? ` · ${t('homework.overdue')}` : ''}
              </p>
            </div>
            {submission.status === 'Graded' && (
              <Badge variant="success" size="sm" className="tnum">
                {formatNumber(submission.score ?? 0)}/{formatNumber(submission.maximumScore ?? 0)}
              </Badge>
            )}
            <StatusBadge status={submission.status} fallbackLabel={submission.statusName} />
          </Card>
        ))}
      </div>
      {homework.data && <Pagination page={homework.data} onChange={setPage} className="mt-6" />}
    </>
  )
}

function ExamsPanel({ studentId }: { studentId: string }) {
  const { locale, formatNumber, formatDateTime } = useI18n()
  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: 15 }), [page])

  const exams = useQuery({
    queryKey: qk.parent.exams(studentId, query),
    queryFn: () => parentApi.exams(studentId, query),
    placeholderData: keepPreviousData,
  })

  if (exams.isPending) return <RowSkeleton count={5} />
  if (exams.isError) return <QueryError error={exams.error} onRetry={() => exams.refetch()} />

  const items = exams.data?.items ?? []
  if (items.length === 0) {
    return <EmptyState spot="exams" title={locale === 'ar-EG' ? 'مفيش امتحانات' : 'No exams'} />
  }

  return (
    <>
      <div className="space-y-2.5">
        {items.map((exam) => (
          <Card key={exam.id} className="flex flex-wrap items-center gap-3 p-4">
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {exam.title}
              </p>
              <p className="tnum truncate text-caption text-muted-foreground">
                {exam.groupName}
                {exam.examDate ? ` · ${formatDateTime(exam.examDate)}` : ''}
              </p>
            </div>
            <Badge variant="outline" size="sm" className="tnum">
              {formatNumber(exam.maximumScore ?? 0)}
            </Badge>
            <Badge variant={exam.isPublished ? 'success' : 'outline'} size="sm">
              {exam.typeName ?? exam.type}
            </Badge>
          </Card>
        ))}
      </div>
      {exams.data && <Pagination page={exams.data} onChange={setPage} className="mt-6" />}
    </>
  )
}

function PaymentsPanel({ studentId }: { studentId: string }) {
  const { locale, formatCurrency, formatDate } = useI18n()
  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: 15 }), [page])

  const payments = useQuery({
    queryKey: qk.parent.payments(studentId, query),
    queryFn: () => parentApi.payments(studentId, query),
    placeholderData: keepPreviousData,
  })

  if (payments.isPending) return <RowSkeleton count={5} />
  if (payments.isError)
    return <QueryError error={payments.error} onRetry={() => payments.refetch()} />

  const items = payments.data?.items ?? []
  if (items.length === 0) {
    return (
      <EmptyState spot="payments" title={locale === 'ar-EG' ? 'مفيش مدفوعات' : 'No payments'} />
    )
  }

  return (
    <>
      <div className="space-y-2.5">
        {items.map((payment) => (
          <Card key={payment.id} className="flex flex-wrap items-center gap-3 p-4">
            <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
              <Wallet className="size-5" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {payment.groupName || payment.courseTitle || payment.typeName}
              </p>
              <p className="tnum truncate text-caption text-muted-foreground">
                {payment.periodMonth ? formatDate(payment.periodMonth, 'MMMM yyyy') : ''}
                {payment.dueAt ? ` · ${formatDate(payment.dueAt, 'd MMM')}` : ''}
              </p>
            </div>
            <span className="tnum font-display text-body-sm font-bold text-foreground">
              {formatCurrency(payment.netAmount ?? 0, payment.currency)}
            </span>
            <StatusBadge status={payment.status} fallbackLabel={payment.statusName} />
          </Card>
        ))}
      </div>
      {payments.data && <Pagination page={payments.data} onChange={setPage} className="mt-6" />}
    </>
  )
}
