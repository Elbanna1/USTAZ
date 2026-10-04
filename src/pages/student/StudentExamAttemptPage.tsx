import * as React from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import {
  AlertTriangle,
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  Circle,
  Send,
  Timer,
  XCircle,
} from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import type { ExamAnswerInput, StudentExamQuestionDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/input'
import { Progress } from '@/components/ui/primitives'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import { LogoMark } from '@/components/brand/Logo'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function StudentExamAttemptPage() {
  const { examId = '', attemptId = '' } = useParams()
  const [searchParams] = useSearchParams()
  const reviewMode = searchParams.get('review') === '1'

  return reviewMode ? (
    <AttemptReview examId={examId} attemptId={attemptId} />
  ) : (
    <AttemptRunner examId={examId} attemptId={attemptId} />
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Runner                                   */
/* -------------------------------------------------------------------------- */

function AttemptRunner({ examId, attemptId }: { examId: string; attemptId: string }) {
  const { t, locale, formatNumber } = useI18n()
  const navigate = useNavigate()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  /**
   * The session (questions + deadline) comes back from `POST /attempts`.
   * Re-opening the page reads it from the cache the detail page primed; if the
   * cache is cold we fall back to the attempt record, which carries the answers.
   */
  const session = useQuery({
    queryKey: ['student', 'attempt-session', attemptId],
    queryFn: () => studentApi.attempt(attemptId),
    enabled: Boolean(attemptId),
    staleTime: Infinity,
    gcTime: Infinity,
  })

  const cachedSession = queryClient.getQueryData<{
    attemptId?: string
    examTitle?: string
    questions?: StudentExamQuestionDto[]
    expiresAt?: string | null
    durationInMinutes?: number
    maximumScore?: number
  }>(['student', 'exam-session', attemptId])

  const questions: StudentExamQuestionDto[] = React.useMemo(() => {
    if (cachedSession?.questions?.length) return cachedSession.questions
    // The attempt record exposes the same questions through its answer rows.
    return (session.data?.answers ?? []).map((answer) => ({
      id: answer.questionId,
      text: answer.questionText,
      type: answer.questionType,
      points: answer.maximumPoints,
      selectedOptionId: answer.selectedOptionId,
      textAnswer: answer.textAnswer,
      options: [],
    }))
  }, [cachedSession, session.data])

  const title = cachedSession?.examTitle ?? session.data?.examTitle ?? t('exams.title')
  const expiresAt = cachedSession?.expiresAt ?? session.data?.expiresAt ?? null

  usePageTitle(title)

  const [answers, setAnswers] = React.useState<Record<string, ExamAnswerInput>>({})
  const [index, setIndex] = React.useState(0)
  const [confirmOpen, setConfirmOpen] = React.useState(false)
  const submittedRef = React.useRef(false)

  /* Seed any answers the server already recorded for this attempt. */
  React.useEffect(() => {
    if (!questions.length) return
    setAnswers((prev) => {
      if (Object.keys(prev).length) return prev
      const seeded: Record<string, ExamAnswerInput> = {}
      questions.forEach((q) => {
        if (!q.id) return
        if (q.selectedOptionId || q.textAnswer) {
          seeded[q.id] = {
            questionId: q.id,
            selectedOptionId: q.selectedOptionId ?? null,
            textAnswer: q.textAnswer ?? null,
          }
        }
      })
      return seeded
    })
  }, [questions])

  const submit = useMutation({
    mutationFn: () =>
      studentApi.submitAttempt(attemptId, { answers: Object.values(answers) }),
    onSuccess: () => {
      submittedRef.current = true
      queryClient.invalidateQueries({ queryKey: ['student'] })
      toast({ title: t('common.saved'), variant: 'success' })
      navigate(`/student/exams/${examId}/attempt/${attemptId}?review=1`, { replace: true })
    },
    onError: (error) =>
      showApiError(error),
  })

  /* ------------------------------- Countdown ------------------------------ */
  const [remaining, setRemaining] = React.useState<number | null>(null)

  React.useEffect(() => {
    if (!expiresAt) return
    const deadline = new Date(expiresAt).getTime()
    const tick = () => setRemaining(Math.max(0, Math.round((deadline - Date.now()) / 1000)))
    tick()
    const id = setInterval(tick, 1000)
    return () => clearInterval(id)
  }, [expiresAt])

  /* Time is up — hand in whatever is answered rather than losing the attempt. */
  React.useEffect(() => {
    if (remaining !== 0 || submittedRef.current || submit.isPending) return
    submittedRef.current = true
    toast({ title: t('exams.timeUp'), variant: 'warning' })
    submit.mutate()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [remaining])

  /* Warn before a refresh or a tab close loses unsaved answers. */
  React.useEffect(() => {
    const handler = (event: BeforeUnloadEvent) => {
      if (submittedRef.current) return
      event.preventDefault()
      event.returnValue = ''
    }
    window.addEventListener('beforeunload', handler)
    return () => window.removeEventListener('beforeunload', handler)
  }, [])

  if (session.isPending) {
    return <RowSkeleton count={5} />
  }
  if (session.isError) {
    return <QueryError error={session.error} onRetry={() => session.refetch()} />
  }
  if (!questions.length) {
    return (
      <QueryError
        error={new Error('no-questions')}
        onRetry={() => session.refetch()}
      />
    )
  }

  const current = questions[index]
  const answeredCount = Object.values(answers).filter(
    (a) => a.selectedOptionId || (a.textAnswer ?? '').trim(),
  ).length

  function setAnswer(questionId: string, patch: Partial<ExamAnswerInput>) {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: {
        questionId,
        selectedOptionId: null,
        textAnswer: null,
        ...prev[questionId],
        ...patch,
      },
    }))
  }

  const isAnswered = (q: StudentExamQuestionDto) => {
    const a = q.id ? answers[q.id] : undefined
    return Boolean(a?.selectedOptionId || (a?.textAnswer ?? '').trim())
  }

  const lowTime = remaining !== null && remaining <= 120

  return (
    <div className="mx-auto max-w-4xl">
      {/* --------------------------- Focused header -------------------------- */}
      <header className="sticky top-16 z-20 -mx-4 mb-6 border-b border-border bg-background/90 px-4 py-3 backdrop-blur-xl sm:-mx-6 sm:px-6">
        <div className="flex flex-wrap items-center gap-3">
          <LogoMark className="size-8 shrink-0" />
          <div className="min-w-0 flex-1">
            <p className="truncate font-display text-body-sm font-bold text-foreground">{title}</p>
            <p className="tnum text-caption text-muted-foreground">
              {t('exams.question', {
                current: formatNumber(index + 1),
                total: formatNumber(questions.length),
              })}
            </p>
          </div>

          {remaining !== null && (
            <motion.div
              animate={lowTime ? { scale: [1, 1.04, 1] } : {}}
              transition={{ repeat: lowTime ? Infinity : 0, duration: 1.4 }}
              className={cn(
                'flex items-center gap-2 rounded-lg border px-3 py-1.5',
                lowTime
                  ? 'border-destructive/40 bg-destructive/10 text-destructive-strong'
                  : 'border-border bg-card text-foreground',
              )}
            >
              <Timer className="size-4" />
              <span className="tnum font-display text-body-sm font-bold">
                {formatClock(remaining)}
              </span>
            </motion.div>
          )}

          <Button size="sm" onClick={() => setConfirmOpen(true)} loading={submit.isPending}>
            <Send className="size-4 flip-rtl" />
            {t('exams.submit')}
          </Button>
        </div>

        <Progress
          value={(answeredCount / questions.length) * 100}
          className="mt-3 h-1"
          indicatorClassName={lowTime ? 'bg-destructive' : undefined}
        />
      </header>

      <div className="grid gap-6 lg:grid-cols-[1fr,13rem]">
        {/* ----------------------------- Question ---------------------------- */}
        <div>
          <AnimatePresence mode="wait">
            <motion.div
              key={current.id}
              initial={{ opacity: 0, x: 24 }}
              animate={{ opacity: 1, x: 0 }}
              exit={{ opacity: 0, x: -24 }}
              transition={transitions.base}
            >
              <Card className="p-6">
                <div className="flex items-start justify-between gap-3">
                  <h2 className="font-display text-heading-sm leading-relaxed text-foreground">
                    {current.text}
                  </h2>
                  <Badge variant="outline" size="sm" className="tnum shrink-0">
                    {formatNumber(current.points ?? 0)} {t('exams.points')}
                  </Badge>
                </div>

                <div className="mt-6 space-y-3">
                  {current.type === 'ShortAnswer' || current.type === 'Essay' ? (
                    <Textarea
                      value={(current.id && answers[current.id]?.textAnswer) || ''}
                      onChange={(e) =>
                        current.id && setAnswer(current.id, { textAnswer: e.target.value })
                      }
                      rows={current.type === 'Essay' ? 8 : 3}
                      placeholder={locale === 'ar-EG' ? 'اكتب إجابتك هنا…' : 'Type your answer…'}
                      aria-label={t('homework.yourAnswer')}
                    />
                  ) : (
                    (current.options ?? []).map((option) => {
                      const selected = Boolean(
                        current.id && answers[current.id]?.selectedOptionId === option.id,
                      )
                      return (
                        <button
                          key={option.id}
                          type="button"
                          onClick={() => {
                            if (!current.id) return
                            setAnswer(current.id, {
                              selectedOptionId: option.id ?? null,
                              textAnswer: null,
                            })
                          }}
                          aria-pressed={selected}
                          className={cn(
                            'flex w-full items-center gap-3 rounded-xl border p-4 text-start transition-all duration-200',
                            selected
                              ? 'border-primary bg-primary/8 shadow-soft'
                              : 'border-border bg-card hover:border-foreground/25 hover:bg-muted/40',
                          )}
                        >
                          <span
                            className={cn(
                              'grid size-5 shrink-0 place-items-center rounded-full border-2 transition-colors',
                              selected ? 'border-primary bg-primary' : 'border-muted-foreground/40',
                            )}
                          >
                            {selected && (
                              <motion.span
                                initial={{ scale: 0 }}
                                animate={{ scale: 1 }}
                                className="size-2 rounded-full bg-primary-foreground"
                              />
                            )}
                          </span>
                          <span className="flex-1 text-body-sm leading-relaxed text-foreground">
                            {option.text}
                          </span>
                        </button>
                      )
                    })
                  )}
                </div>
              </Card>
            </motion.div>
          </AnimatePresence>

          <div className="mt-5 flex items-center justify-between gap-3">
            <Button
              variant="outline"
              disabled={index === 0}
              onClick={() => setIndex((i) => Math.max(0, i - 1))}
            >
              <ArrowLeft className="size-4 flip-rtl" />
              {t('common.previous')}
            </Button>

            {index === questions.length - 1 ? (
              <Button onClick={() => setConfirmOpen(true)} loading={submit.isPending}>
                <Send className="size-4 flip-rtl" />
                {t('exams.submit')}
              </Button>
            ) : (
              <Button onClick={() => setIndex((i) => Math.min(questions.length - 1, i + 1))}>
                {t('common.next')}
                <ArrowRight className="size-4 flip-rtl" />
              </Button>
            )}
          </div>
        </div>

        {/* --------------------------- Question map -------------------------- */}
        <Card className="h-fit p-4 lg:sticky lg:top-40">
          <p className="mb-3 font-display text-caption font-bold text-foreground">
            {t('exams.answered')}:{' '}
            <span className="tnum">
              {formatNumber(answeredCount)}/{formatNumber(questions.length)}
            </span>
          </p>
          <div className="grid grid-cols-6 gap-1.5 lg:grid-cols-5">
            {questions.map((q, i) => {
              const answered = isAnswered(q)
              return (
                <button
                  key={q.id}
                  type="button"
                  onClick={() => setIndex(i)}
                  aria-current={i === index ? 'true' : undefined}
                  aria-label={`${t('exams.question', { current: i + 1, total: questions.length })}`}
                  className={cn(
                    'tnum grid aspect-square place-items-center rounded-md border text-caption font-bold transition-all duration-200',
                    i === index
                      ? 'border-primary bg-primary text-primary-foreground'
                      : answered
                        ? 'border-success/40 bg-success/12 text-success-strong'
                        : 'border-border bg-card text-muted-foreground hover:border-foreground/25',
                  )}
                >
                  {i + 1}
                </button>
              )
            })}
          </div>
        </Card>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title={t('exams.submitConfirmTitle')}
        description={
          answeredCount < questions.length
            ? `${t('exams.submitConfirmBody')} ${
                locale === 'ar-EG'
                  ? `فاضل ${formatNumber(questions.length - answeredCount)} سؤال من غير إجابة.`
                  : `${formatNumber(questions.length - answeredCount)} questions are still unanswered.`
              }`
            : t('exams.submitConfirmBody')
        }
        confirmLabel={t('exams.submit')}
        onConfirm={() => submit.mutate()}
        loading={submit.isPending}
      />
    </div>
  )
}

function formatClock(totalSeconds: number): string {
  const h = Math.floor(totalSeconds / 3600)
  const m = Math.floor((totalSeconds % 3600) / 60)
  const s = totalSeconds % 60
  const pad = (n: number) => String(n).padStart(2, '0')
  return h > 0 ? `${pad(h)}:${pad(m)}:${pad(s)}` : `${pad(m)}:${pad(s)}`
}

/* -------------------------------------------------------------------------- */
/*                                   Review                                   */
/* -------------------------------------------------------------------------- */

function AttemptReview({ examId, attemptId }: { examId: string; attemptId: string }) {
  const { t, locale, formatNumber } = useI18n()

  const attempt = useQuery({
    queryKey: qk.student.attempt(attemptId),
    queryFn: () => studentApi.attempt(attemptId),
    enabled: Boolean(attemptId),
  })

  usePageTitle(attempt.data?.examTitle ?? t('exams.result'))

  if (attempt.isPending) return <RowSkeleton count={5} />
  if (attempt.isError) return <QueryError error={attempt.error} onRetry={() => attempt.refetch()} />

  const data = attempt.data!
  const graded = data.status === 'Graded' || data.scoresVisible
  const pending = data.requiresManualGrading && data.status === 'Submitted'

  return (
    <div className="mx-auto max-w-4xl">
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to={`/student/exams/${examId}`}>
          <ArrowLeft className="size-4 flip-rtl" />
          {data.examTitle}
        </Link>
      </Button>

      {/* ----------------------------- Score card ---------------------------- */}
      <motion.div
        initial={{ opacity: 0, y: 12 }}
        animate={{ opacity: 1, y: 0 }}
        transition={transitions.base}
      >
        <Card
          className={cn(
            'overflow-hidden p-6',
            pending
              ? 'border-accent/30 bg-accent/5'
              : graded
                ? data.isPassed
                  ? 'border-success/30 bg-success/5'
                  : 'border-destructive/30 bg-destructive/5'
                : undefined,
          )}
        >
          <div className="flex flex-wrap items-center gap-5">
            <span
              className={cn(
                'grid size-16 place-items-center rounded-2xl',
                pending
                  ? 'bg-accent/15 text-accent-strong'
                  : graded
                    ? data.isPassed
                      ? 'bg-success/15 text-success-strong'
                      : 'bg-destructive/15 text-destructive-strong'
                    : 'bg-muted text-muted-foreground',
              )}
            >
              {pending ? (
                <AlertTriangle className="size-8" />
              ) : data.isPassed ? (
                <CheckCircle2 className="size-8" />
              ) : (
                <XCircle className="size-8" />
              )}
            </span>

            <div className="min-w-0 flex-1">
              <p className="font-display text-heading-md font-extrabold text-foreground">
                {pending
                  ? t('exams.awaitingGrading')
                  : data.isPassed
                    ? t('exams.passed')
                    : t('exams.failed')}
              </p>
              {graded && !pending && (
                <p className="tnum mt-1 text-body text-muted-foreground">
                  {formatNumber(data.score ?? 0)} / {formatNumber(data.maximumScore ?? 0)} ·{' '}
                  {Math.round(data.percentage ?? 0)}%
                </p>
              )}
            </div>
          </div>

          {graded && !pending && (
            <Progress
              value={data.percentage ?? 0}
              className="mt-5 h-2.5"
              indicatorClassName={data.isPassed ? 'bg-success' : 'bg-destructive'}
            />
          )}

          {data.teacherComment && (
            <p className="mt-4 rounded-lg border border-border bg-card p-4 text-body-sm leading-relaxed text-muted-foreground">
              {data.teacherComment}
            </p>
          )}
        </Card>
      </motion.div>

      {/* ------------------------------ Answers ------------------------------ */}
      <div className="mt-6 space-y-4">
        {(data.answers ?? []).map((answer, i) => {
          const correct = answer.isCorrect === true
          const wrong = answer.isCorrect === false

          return (
            <motion.div
              key={answer.id ?? i}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ ...transitions.base, delay: Math.min(i * 0.04, 0.3) }}
            >
              <Card
                className={cn(
                  'p-5',
                  correct && 'border-success/25',
                  wrong && 'border-destructive/25',
                )}
              >
                <div className="flex items-start gap-3">
                  <span
                    className={cn(
                      'mt-0.5 grid size-7 shrink-0 place-items-center rounded-lg',
                      correct
                        ? 'bg-success/12 text-success-strong'
                        : wrong
                          ? 'bg-destructive/12 text-destructive-strong'
                          : 'bg-muted text-muted-foreground',
                    )}
                  >
                    {correct ? (
                      <CheckCircle2 className="size-4" />
                    ) : wrong ? (
                      <XCircle className="size-4" />
                    ) : (
                      <Circle className="size-4" />
                    )}
                  </span>

                  <div className="min-w-0 flex-1 space-y-3">
                    <div className="flex items-start justify-between gap-3">
                      <p className="font-display text-body-sm font-semibold leading-relaxed text-foreground">
                        {answer.questionText}
                      </p>
                      <Badge variant="outline" size="sm" className="tnum shrink-0">
                        {formatNumber(answer.awardedPoints ?? 0)}/
                        {formatNumber(answer.maximumPoints ?? 0)}
                      </Badge>
                    </div>

                    {answer.textAnswer && (
                      <div className="rounded-lg bg-muted/50 p-3">
                        <p className="text-caption font-semibold text-muted-foreground">
                          {t('homework.yourAnswer')}
                        </p>
                        <p className="mt-1 whitespace-pre-wrap text-body-sm text-foreground">
                          {answer.textAnswer}
                        </p>
                      </div>
                    )}

                    {answer.correctAnswer && (
                      <div className="rounded-lg border border-success/25 bg-success/6 p-3">
                        <p className="text-caption font-semibold text-success-strong">
                          {t('exams.correctAnswer')}
                        </p>
                        <p className="mt-1 text-body-sm text-foreground">{answer.correctAnswer}</p>
                      </div>
                    )}

                    {answer.explanation && (
                      <p className="text-body-sm leading-relaxed text-muted-foreground">
                        <span className="font-semibold text-foreground">
                          {t('exams.explanation')}:{' '}
                        </span>
                        {answer.explanation}
                      </p>
                    )}

                    {answer.teacherFeedback && (
                      <p className="rounded-lg border border-border bg-card p-3 text-body-sm leading-relaxed text-muted-foreground">
                        <span className="font-semibold text-foreground">
                          {t('homework.feedback')}:{' '}
                        </span>
                        {answer.teacherFeedback}
                      </p>
                    )}
                  </div>
                </div>
              </Card>
            </motion.div>
          )
        })}

        {(data.answers ?? []).length === 0 && (
          <Card className="p-6 text-center text-body-sm text-muted-foreground">
            {locale === 'ar-EG'
              ? 'النتيجة التفصيلية هتظهر بعد ما المدرّس ينشرها.'
              : 'Detailed results appear once your teacher publishes them.'}
          </Card>
        )}
      </div>
    </div>
  )
}
