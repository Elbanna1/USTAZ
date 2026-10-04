import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  AlertTriangle,
  ArrowRight,
  CircleAlert,
  Info,
  Medal,
  Target,
  TrendingDown,
  Users,
} from 'lucide-react'
import {
  examAnalyticsApi,
  type ExamAnalytics,
  type ExamInsight,
  type ExamStudentStanding,
  type QuestionAnalytics,
  type QuestionDifficulty,
} from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { CategoryBarChart } from '@/components/dashboard/charts'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'

/* -------------------------------------------------------------------------- */
/*                              Copy + mappings                               */
/* -------------------------------------------------------------------------- */

type Bi = { ar: string; en: string }

/**
 * The API ships a ready Arabic sentence per insight. These supply the English
 * equivalent and let us pick an icon — the `code` is the contract, not the prose.
 */
const INSIGHT_EN: Record<string, string> = {
  NO_RESULTS: 'No graded attempts yet, so there is nothing to analyse.',
  AVERAGE_BELOW_PASS_MARK: 'The class average is below the pass mark.',
  LOW_PASS_RATE: 'Fewer students passed than you would normally expect.',
  WIDE_SPREAD: 'Scores are spread very widely — the class is not moving together.',
  QUESTIONS_NEED_REVIEW: 'Some questions were missed by most students and are worth revisiting.',
  AWAITING_GRADING: 'Some answers still need manual grading before results are final.',
  EXPIRED_ATTEMPTS: 'Some attempts ran out of time before they were submitted.',
  MISSING_PARTICIPANTS: 'Some students have not attempted this exam.',
  RESULTS_NOT_PUBLISHED: 'Results are not published yet, so students cannot see their scores.',
}

const DIFFICULTY: Record<QuestionDifficulty, { label: Bi; variant: 'success' | 'warning' | 'destructive' | 'outline' }> = {
  Easy: { label: { ar: 'سهل', en: 'Easy' }, variant: 'success' },
  Medium: { label: { ar: 'متوسط', en: 'Medium' }, variant: 'warning' },
  Hard: { label: { ar: 'صعب', en: 'Hard' }, variant: 'destructive' },
  Unknown: { label: { ar: 'لسه مش واضح', en: 'Not yet clear' }, variant: 'outline' },
}

const SEVERITY_STYLE = {
  Info: { icon: Info, wrap: 'border-info/25 bg-info/[0.06]', tone: 'text-info' },
  Warning: { icon: AlertTriangle, wrap: 'border-warning/30 bg-warning/[0.07]', tone: 'text-warning-strong' },
  Critical: { icon: CircleAlert, wrap: 'border-destructive/30 bg-destructive/[0.06]', tone: 'text-destructive-strong' },
} as const

/* -------------------------------------------------------------------------- */
/*                                    Page                                    */
/* -------------------------------------------------------------------------- */

export default function TeacherExamAnalyticsPage() {
  const { examId = '' } = useParams()
  const { locale, formatNumber, formatDate } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const isAr = lang === 'ar'

  const analytics = useQuery({
    queryKey: qk.teacher.examAnalytics(examId, 5),
    queryFn: () => examAnalyticsApi.get(examId, 5),
    enabled: Boolean(examId),
  })

  usePageTitle(
    analytics.data?.overview.title ?? (isAr ? 'تحليل الامتحان' : 'Exam analytics'),
  )

  if (analytics.isPending) return <AnalyticsSkeleton />
  if (analytics.isError) {
    return (
      <div className="space-y-6">
        <DashboardHeader title={isAr ? 'تحليل الامتحان' : 'Exam analytics'} />
        <QueryError error={analytics.error} onRetry={() => analytics.refetch()} />
      </div>
    )
  }

  const data = analytics.data
  if (!data) return null

  const { overview, performance } = data
  const subject = isAr ? overview.subjectNameAr : overview.subjectNameEn
  const graded = performance.scoredAttempts > 0

  return (
    <div className="space-y-6">
      <DashboardHeader
        title={overview.title}
        description={[subject, overview.groupName, formatDate(overview.examDate)]
          .filter(Boolean)
          .join(' · ')}
        actions={
          <Button asChild variant="outline" size="sm">
            <Link to={`/teacher/exams/${overview.examId}`}>
              {isAr ? 'رجوع للامتحان' : 'Back to exam'}
              <ArrowRight className="size-4 flip-rtl" />
            </Link>
          </Button>
        }
      />

      {/* Insights first: what the teacher should do, before the numbers. */}
      {data.insights.length > 0 && (
        <ul className="space-y-2.5">
          {data.insights.map((insight, index) => (
            <InsightRow key={insight.code} insight={insight} index={index} lang={lang} />
          ))}
        </ul>
      )}

      {/* ----------------------------- Overview ---------------------------- */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          label={isAr ? 'عدد اللي دخلوا' : 'Participants'}
          value={formatNumber(performance.totalAttempts)}
          hint={
            overview.expectedParticipants
              ? isAr
                ? `من ${formatNumber(overview.expectedParticipants)} طالب`
                : `of ${formatNumber(overview.expectedParticipants)} students`
              : undefined
          }
          icon={<Users className="size-5" />}
        />
        <MetricCard
          label={isAr ? 'متوسط الدرجات' : 'Average score'}
          value={graded ? `${formatNumber(Math.round(performance.averagePercentage))}%` : '—'}
          hint={
            graded
              ? isAr
                ? `${formatNumber(performance.averageScore)} من ${formatNumber(overview.maximumScore)}`
                : `${formatNumber(performance.averageScore)} of ${formatNumber(overview.maximumScore)}`
              : undefined
          }
          icon={<Target className="size-5" />}
        />
        <MetricCard
          label={isAr ? 'نسبة النجاح' : 'Pass rate'}
          value={graded ? `${formatNumber(Math.round(performance.passRate))}%` : '—'}
          hint={
            graded
              ? isAr
                ? `نجح ${formatNumber(performance.passedCount)} · رسب ${formatNumber(performance.failedCount)}`
                : `${formatNumber(performance.passedCount)} passed · ${formatNumber(performance.failedCount)} failed`
              : undefined
          }
          icon={<Medal className="size-5" />}
          tone={graded && performance.passRate < 50 ? 'warning' : 'default'}
        />
        <MetricCard
          label={isAr ? 'الوسيط' : 'Median'}
          value={graded ? `${formatNumber(Math.round(performance.medianPercentage))}%` : '—'}
          hint={
            graded
              ? isAr
                ? `تشتّت ${formatNumber(Math.round(performance.standardDeviation))} نقطة`
                : `spread ${formatNumber(Math.round(performance.standardDeviation))} pts`
              : undefined
          }
          icon={<TrendingDown className="size-5" />}
        />
      </div>

      {/* Secondary counts only when they are non-zero — no rows of zeros. */}
      <SecondaryCounts performance={performance} lang={lang} />

      {/* --------------------------- Distribution -------------------------- */}
      <DashboardSection
        title={isAr ? 'توزيع الدرجات' : 'Score distribution'}
        description={
          isAr
            ? 'كل عمود بيوضّح عدد الطلاب اللي درجتهم في النطاق ده.'
            : 'Each bar is the number of students whose score fell in that band.'
        }
      >
        {graded ? (
          <Card className="p-5">
            <CategoryBarChart
              data={data.scoreDistribution.map((bucket) => ({
                label: bucket.label,
                value: bucket.count,
              }))}
              seriesName={isAr ? 'عدد الطلاب' : 'Students'}
              height={280}
            />
          </Card>
        ) : (
          <EmptyState
            spot="exams"
            compact
            title={isAr ? 'مفيش درجات لسه' : 'No scores yet'}
            description={
              isAr
                ? 'أول ما الطلاب يخلّصوا ويتصحّح الامتحان هتلاقي التوزيع هنا.'
                : 'Once students finish and the exam is graded, the distribution appears here.'
            }
          />
        )}
      </DashboardSection>

      {/* ---------------------------- Questions ---------------------------- */}
      {data.questions.length > 0 && (
        <DashboardSection
          title={isAr ? 'تحليل الأسئلة' : 'Question analysis'}
          description={
            isAr
              ? 'نسبة الإجابات الصحيحة لكل سؤال — السهل فوق والصعب تحت.'
              : 'The share of correct answers per question — easiest first.'
          }
        >
          <QuestionTable questions={data.questions} lang={lang} />
        </DashboardSection>
      )}

      {/* --------------------------- Standings ----------------------------- */}
      <div className="grid gap-5 lg:grid-cols-2">
        <StandingsCard
          title={isAr ? 'الأعلى درجة' : 'Top performers'}
          students={data.topPerformers}
          lang={lang}
          empty={isAr ? 'مفيش نتائج لسه.' : 'No results yet.'}
          rank
        />
        <StandingsCard
          title={isAr ? 'محتاجين متابعة' : 'Needs follow-up'}
          students={data.lowestPerformers}
          lang={lang}
          empty={isAr ? 'مفيش نتائج لسه.' : 'No results yet.'}
          tone="warning"
        />
      </div>

      {data.notAttempted.length > 0 && (
        <DashboardSection
          title={isAr ? 'مدخلوش الامتحان' : 'Not attempted'}
          description={
            isAr
              ? 'الطلاب دول متسجلين بس لسه مدخلوش.'
              : 'These students are enrolled but have not attempted yet.'
          }
        >
          <Card className="p-5">
            <ul className="flex flex-wrap gap-2">
              {data.notAttempted.map((student) => (
                <li key={student.studentId}>
                  <Badge variant="outline" size="sm">
                    {student.studentName}
                  </Badge>
                </li>
              ))}
            </ul>
          </Card>
        </DashboardSection>
      )}
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Pieces                                    */
/* -------------------------------------------------------------------------- */

function InsightRow({
  insight,
  index,
  lang,
}: {
  insight: ExamInsight
  index: number
  lang: 'ar' | 'en'
}) {
  const style = SEVERITY_STYLE[insight.severityName] ?? SEVERITY_STYLE.Info
  const Icon = style.icon
  // The server's sentence is Arabic; fall back to it if we have no English copy.
  const text = lang === 'ar' ? insight.message : (INSIGHT_EN[insight.code] ?? insight.message)

  return (
    <motion.li
      initial={{ opacity: 0, y: 6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ ...transitions.base, delay: index * 0.04 }}
      className={cn('flex items-start gap-3 rounded-xl border px-4 py-3', style.wrap)}
    >
      <Icon className={cn('mt-0.5 size-4 shrink-0', style.tone)} />
      <p className="text-body-sm text-foreground">{text}</p>
    </motion.li>
  )
}

function MetricCard({
  label,
  value,
  hint,
  icon,
  tone = 'default',
}: {
  label: string
  value: string
  hint?: string
  icon: React.ReactNode
  tone?: 'default' | 'warning'
}) {
  return (
    <Card className="p-5">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-body-sm font-medium text-muted-foreground">{label}</p>
          <p
            className={cn(
              'tnum mt-2 font-display text-[1.75rem] font-extrabold leading-none',
              tone === 'warning' ? 'text-warning-strong' : 'text-foreground',
            )}
          >
            {value}
          </p>
          {hint && <p className="tnum mt-1.5 text-caption text-muted-foreground">{hint}</p>}
        </div>
        <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
          {icon}
        </span>
      </div>
    </Card>
  )
}

function SecondaryCounts({
  performance,
  lang,
}: {
  performance: ExamAnalytics['performance']
  lang: 'ar' | 'en'
}) {
  const { formatNumber } = useI18n()
  const isAr = lang === 'ar'

  const rows = [
    { key: 'awaiting', label: { ar: 'مستني تصحيح', en: 'Awaiting grading' }, value: performance.awaitingGradingCount },
    { key: 'progress', label: { ar: 'لسه بيحل', en: 'In progress' }, value: performance.inProgressCount },
    { key: 'expired', label: { ar: 'الوقت خلص', en: 'Timed out' }, value: performance.expiredCount },
    { key: 'absent', label: { ar: 'غايب', en: 'Absent' }, value: performance.absentCount },
  ].filter((row) => row.value > 0)

  if (rows.length === 0) return null

  return (
    <Card className="flex flex-wrap items-center gap-x-8 gap-y-3 p-4">
      {rows.map((row) => (
        <div key={row.key} className="flex items-baseline gap-2">
          <span className="tnum font-display text-body font-bold text-foreground">
            {formatNumber(row.value)}
          </span>
          <span className="text-body-sm text-muted-foreground">{row.label[isAr ? 'ar' : 'en']}</span>
        </div>
      ))}
    </Card>
  )
}

function QuestionTable({
  questions,
  lang,
}: {
  questions: QuestionAnalytics[]
  lang: 'ar' | 'en'
}) {
  const { formatNumber } = useI18n()
  const isAr = lang === 'ar'
  const sorted = React.useMemo(
    () => [...questions].sort((a, b) => b.correctPercentage - a.correctPercentage),
    [questions],
  )

  return (
    <Card className="overflow-hidden p-0">
      {/* Scrolls inside itself so the page never scrolls sideways on mobile. */}
      <div className="overflow-x-auto">
        <table className="w-full min-w-[34rem] text-body-sm">
          <thead>
            <tr className="border-b border-border bg-muted/40">
              <th scope="col" className="p-4 text-start font-display font-bold text-foreground">
                {isAr ? 'السؤال' : 'Question'}
              </th>
              <th scope="col" className="p-4 text-center font-display font-bold text-foreground">
                {isAr ? 'إجابات صح' : 'Correct'}
              </th>
              <th scope="col" className="p-4 text-center font-display font-bold text-foreground">
                {isAr ? 'المستوى' : 'Difficulty'}
              </th>
            </tr>
          </thead>
          <tbody>
            {sorted.map((question) => {
              const difficulty = DIFFICULTY[question.difficultyName] ?? DIFFICULTY.Unknown
              const pct = Math.round(question.correctPercentage)
              return (
                <tr key={question.questionId} className="border-b border-border last:border-b-0">
                  <td className="max-w-[24rem] p-4">
                    <p className="line-clamp-2 text-foreground">{question.text}</p>
                    <p className="mt-1 text-caption text-muted-foreground">
                      {question.typeName} · {formatNumber(question.points)}{' '}
                      {isAr ? 'درجة' : 'pts'}
                      {question.awaitingGradingCount > 0 && (
                        <>
                          {' · '}
                          {isAr
                            ? `${formatNumber(question.awaitingGradingCount)} مستني تصحيح`
                            : `${formatNumber(question.awaitingGradingCount)} awaiting grading`}
                        </>
                      )}
                    </p>
                  </td>
                  <td className="p-4">
                    {/* Bar plus number: the share is readable without relying on colour. */}
                    <div className="flex items-center justify-center gap-2.5">
                      <span
                        className="h-1.5 w-20 overflow-hidden rounded-full bg-muted"
                        role="img"
                        aria-label={isAr ? `${pct}% إجابات صحيحة` : `${pct}% correct`}
                      >
                        <span
                          className={cn(
                            'block h-full rounded-full',
                            pct >= 80 ? 'bg-success' : pct >= 50 ? 'bg-warning' : 'bg-destructive',
                          )}
                          style={{ width: `${pct}%` }}
                        />
                      </span>
                      <span className="tnum font-semibold text-foreground">{pct}%</span>
                    </div>
                  </td>
                  <td className="p-4 text-center">
                    <Badge variant={difficulty.variant} size="sm">
                      {difficulty.label[isAr ? 'ar' : 'en']}
                    </Badge>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </Card>
  )
}

function StandingsCard({
  title,
  students,
  lang,
  empty,
  rank = false,
  tone = 'default',
}: {
  title: string
  students: ExamStudentStanding[]
  lang: 'ar' | 'en'
  empty: string
  rank?: boolean
  tone?: 'default' | 'warning'
}) {
  const { formatNumber } = useI18n()
  const isAr = lang === 'ar'

  return (
    <Card className="p-5">
      <h3 className="font-display text-heading-sm text-foreground">{title}</h3>

      {students.length === 0 ? (
        <p className="mt-4 text-body-sm text-muted-foreground">{empty}</p>
      ) : (
        <ol className="mt-4 space-y-2.5">
          {students.map((student, index) => (
            <li key={student.studentId} className="flex items-center gap-3">
              {rank ? (
                <span className="tnum grid size-7 shrink-0 place-items-center rounded-lg bg-muted font-display text-caption font-bold text-foreground">
                  {formatNumber(index + 1)}
                </span>
              ) : (
                <span
                  className={cn(
                    'size-2 shrink-0 rounded-full',
                    tone === 'warning' ? 'bg-warning' : 'bg-primary',
                  )}
                />
              )}
              <span className="min-w-0 flex-1 truncate text-body-sm text-foreground">
                {student.studentName}
              </span>
              <span className="tnum shrink-0 font-display text-body-sm font-bold text-foreground">
                {student.percentage == null
                  ? isAr
                    ? 'غايب'
                    : 'Absent'
                  : `${formatNumber(Math.round(student.percentage))}%`}
              </span>
            </li>
          ))}
        </ol>
      )}
    </Card>
  )
}

function AnalyticsSkeleton() {
  return (
    <div className="space-y-6">
      <div className="space-y-2">
        <Skeleton className="h-7 w-64" />
        <Skeleton className="h-4 w-80" />
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {[0, 1, 2, 3].map((i) => (
          <Card key={i} className="p-5">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="mt-3 h-8 w-20" />
            <Skeleton className="mt-2 h-3 w-28" />
          </Card>
        ))}
      </div>
      <Card className="p-5">
        <Skeleton className="h-[280px] w-full" />
      </Card>
    </div>
  )
}
