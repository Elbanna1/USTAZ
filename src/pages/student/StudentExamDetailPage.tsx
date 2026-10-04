import * as React from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft,
  CalendarClock,
  CheckCircle2,
  FileText,
  Play,
  Timer,
  Trophy,
  XCircle,
} from 'lucide-react'
import { motion } from 'framer-motion'
import { studentApi } from '@/api/endpoints/student'
import type { ExamAttemptDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { subjectKeyFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { StatusBadge } from '@/components/common/StatusBadge'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { RowSkeleton } from '@/components/ui/skeleton'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function StudentExamDetailPage() {
  const { examId = '' } = useParams()
  const { t, locale, pick, formatNumber, formatDateTime } = useI18n()
  const navigate = useNavigate()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const exam = useQuery({
    queryKey: qk.student.exam(examId),
    queryFn: () => studentApi.exam(examId),
    enabled: Boolean(examId),
  })

  const attempts = useQuery({
    queryKey: qk.student.examAttempts(examId),
    queryFn: () => studentApi.examAttempts(examId),
    enabled: Boolean(examId),
  })

  usePageTitle(exam.data?.title ?? t('exams.title'))

  const start = useMutation({
    mutationFn: () => studentApi.startAttempt(examId),
    onSuccess: (session) => {
      // The questions only come back from this call, so hand them to the runner.
      queryClient.setQueryData(['student', 'exam-session', session.attemptId], session)
      queryClient.invalidateQueries({ queryKey: qk.student.examAttempts(examId) })
      navigate(`/student/exams/${examId}/attempt/${session.attemptId}`)
    },
    onError: (error) =>
      showApiError(error),
  })

  if (exam.isPending) {
    return (
      <>
        <div className="mb-6 h-10 w-64 animate-pulse rounded-lg bg-muted" />
        <RowSkeleton count={4} />
      </>
    )
  }

  if (exam.isError) {
    return <QueryError error={exam.error} onRetry={() => exam.refetch()} />
  }

  const data = exam.data!
  const list = attempts.data ?? []
  const inProgress = list.find((a) => a.status === 'InProgress')
  const attemptsUsed = list.length
  const maxAttempts = data.maxAttempts ?? 1
  const attemptsLeft = Math.max(0, maxAttempts - attemptsUsed)
  const bestAttempt = list
    .filter((a) => a.status === 'Graded' || a.status === 'Submitted')
    .sort((a, b) => (b.percentage ?? 0) - (a.percentage ?? 0))[0]
  const canStart = data.isOnline && (inProgress || attemptsLeft > 0)

  return (
    <>
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to="/student/exams">
          <ArrowLeft className="size-4 flip-rtl" />
          {t('exams.title')}
        </Link>
      </Button>

      <DashboardHeader
        title={data.title ?? ''}
        description={data.description ?? undefined}
        actions={
          canStart ? (
            <Button
              size="lg"
              loading={start.isPending}
              onClick={() =>
                inProgress
                  ? navigate(`/student/exams/${examId}/attempt/${inProgress.id}`)
                  : start.mutate()
              }
            >
              <Play className="size-4 flip-rtl" />
              {inProgress ? t('exams.resumeAttempt') : t('exams.startAttempt')}
            </Button>
          ) : undefined
        }
      />

      <div className="grid gap-6 lg:grid-cols-[1.5fr,1fr]">
        {/* ----------------------------- Overview ---------------------------- */}
        <div className="space-y-6">
          <Card className="p-5">
            <div className="flex items-start gap-4">
              <SubjectTile
                subject={subjectKeyFor({
                  nameAr: data.subjectNameAr,
                  nameEn: data.subjectNameEn,
                })}
                size="md"
              />
              <div className="min-w-0 flex-1 space-y-3">
                <div className="flex flex-wrap items-center gap-2">
                  <Badge variant="primary" size="sm">
                    {data.typeName ?? data.type}
                  </Badge>
                  {pick(data.subjectNameAr, data.subjectNameEn) && (
                    <Badge variant="outline" size="sm">
                      {pick(data.subjectNameAr, data.subjectNameEn)}
                    </Badge>
                  )}
                  {data.groupName && (
                    <Badge variant="outline" size="sm">
                      {data.groupName}
                    </Badge>
                  )}
                  {data.isOnline && (
                    <Badge variant="accent" size="sm">
                      {locale === 'ar-EG' ? 'أونلاين' : 'Online'}
                    </Badge>
                  )}
                </div>

                <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
                  <Fact
                    icon={<CalendarClock className="size-4" />}
                    label={t('common.date')}
                    value={data.examDate ? formatDateTime(data.examDate) : '—'}
                  />
                  <Fact
                    icon={<Timer className="size-4" />}
                    label={t('courses.duration')}
                    value={`${formatNumber(data.durationInMinutes ?? 0)} ${locale === 'ar-EG' ? 'د' : 'min'}`}
                  />
                  <Fact
                    icon={<Trophy className="size-4" />}
                    label={t('exams.maximumScore')}
                    value={formatNumber(data.maximumScore ?? 0)}
                  />
                  <Fact
                    icon={<FileText className="size-4" />}
                    label={t('exams.passingScore')}
                    value={formatNumber(data.passingScore ?? 0)}
                  />
                </dl>
              </div>
            </div>
          </Card>

          {/* ---------------------- Best result banner --------------------- */}
          {bestAttempt && bestAttempt.scoresVisible && (
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={transitions.base}
            >
              <Card
                className={cn(
                  'overflow-hidden p-5',
                  bestAttempt.isPassed
                    ? 'border-success/30 bg-success/5'
                    : 'border-destructive/30 bg-destructive/5',
                )}
              >
                <div className="flex flex-wrap items-center gap-4">
                  <span
                    className={cn(
                      'grid size-14 place-items-center rounded-2xl',
                      bestAttempt.isPassed
                        ? 'bg-success/15 text-success-strong'
                        : 'bg-destructive/15 text-destructive-strong',
                    )}
                  >
                    {bestAttempt.isPassed ? (
                      <CheckCircle2 className="size-7" />
                    ) : (
                      <XCircle className="size-7" />
                    )}
                  </span>
                  <div className="min-w-0 flex-1">
                    <p className="font-display text-heading-sm font-bold text-foreground">
                      {bestAttempt.isPassed ? t('exams.passed') : t('exams.failed')}
                    </p>
                    <p className="tnum text-body-sm text-muted-foreground">
                      {formatNumber(bestAttempt.score ?? 0)} / {formatNumber(bestAttempt.maximumScore ?? 0)}
                      {' · '}
                      {Math.round(bestAttempt.percentage ?? 0)}%
                    </p>
                  </div>
                  <Button asChild variant="outline" size="sm">
                    <Link to={`/student/exams/${examId}/attempt/${bestAttempt.id}?review=1`}>
                      {t('exams.result')}
                    </Link>
                  </Button>
                </div>
                <Progress
                  value={bestAttempt.percentage ?? 0}
                  className="mt-4 h-2"
                  indicatorClassName={bestAttempt.isPassed ? 'bg-success' : 'bg-destructive'}
                />
              </Card>
            </motion.div>
          )}
        </div>

        {/* ----------------------------- Attempts ---------------------------- */}
        <div className="space-y-4">
          <Card className="p-5">
            <h2 className="font-display text-heading-sm text-foreground">{t('exams.attempts')}</h2>
            <p className="tnum mt-1 text-body-sm text-muted-foreground">
              {attemptsLeft > 0
                ? t('exams.attemptsLeft', { count: attemptsLeft })
                : t('exams.noAttempts')}
            </p>
            <Progress
              value={maxAttempts ? (attemptsUsed / maxAttempts) * 100 : 0}
              className="mt-3 h-1.5"
            />
          </Card>

          <Card className="p-2">
            <h3 className="px-3 pb-2 pt-3 font-display text-body-sm font-bold text-foreground">
              {t('exams.previousAttempts')}
            </h3>
            {attempts.isPending ? (
              <div className="p-2">
                <RowSkeleton count={2} />
              </div>
            ) : list.length === 0 ? (
              <EmptyState
                spot="exams"
                compact
                className="border-0 bg-transparent"
                title={locale === 'ar-EG' ? 'لسه مجربتش' : 'No attempts yet'}
              />
            ) : (
              <ul className="space-y-1">
                {list.map((attempt) => (
                  <AttemptRow key={attempt.id} attempt={attempt} examId={examId} />
                ))}
              </ul>
            )}
          </Card>
        </div>
      </div>
    </>
  )
}

function Fact({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode
  label: string
  value: string
}) {
  return (
    <div>
      <dt className="flex items-center gap-1.5 text-caption text-muted-foreground">
        <span className="text-primary [&_svg]:size-4">{icon}</span>
        {label}
      </dt>
      <dd className="tnum mt-1 font-display text-body-sm font-bold text-foreground">{value}</dd>
    </div>
  )
}

function AttemptRow({ attempt, examId }: { attempt: ExamAttemptDto; examId: string }) {
  const { t, formatNumber, formatDateTime } = useI18n()

  return (
    <li>
      <Link
        to={`/student/exams/${examId}/attempt/${attempt.id}${attempt.status === 'InProgress' ? '' : '?review=1'}`}
        className="flex items-center gap-3 rounded-lg p-3 transition-colors hover:bg-muted"
      >
        <span className="tnum grid size-9 shrink-0 place-items-center rounded-lg bg-muted font-display text-body-sm font-bold text-foreground">
          {formatNumber(attempt.attemptNumber ?? 0)}
        </span>
        <span className="min-w-0 flex-1">
          <span className="block font-display text-body-sm font-semibold text-foreground">
            {attempt.submittedAt
              ? formatDateTime(attempt.submittedAt)
              : attempt.startedAt
                ? formatDateTime(attempt.startedAt)
                : '—'}
          </span>
          {attempt.scoresVisible && attempt.status !== 'InProgress' && (
            <span className="tnum block text-caption text-muted-foreground">
              {formatNumber(attempt.score ?? 0)} / {formatNumber(attempt.maximumScore ?? 0)}
            </span>
          )}
          {attempt.requiresManualGrading && attempt.status === 'Submitted' && (
            <span className="block text-caption text-muted-foreground">
              {t('exams.awaitingGrading')}
            </span>
          )}
        </span>
        <StatusBadge status={attempt.status} fallbackLabel={attempt.statusName} />
      </Link>
    </li>
  )
}
