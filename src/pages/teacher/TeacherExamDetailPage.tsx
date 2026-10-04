import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import {
  ArrowLeft,
  Check,
  Eye,
  GripVertical,
  Plus,
  Trash2,
  Trophy,
  X,
} from 'lucide-react'
import { teacherExamsApi } from '@/api/endpoints/teacher'
import type { ExamAttemptDto, ExamQuestionDto, ExamQuestionOptionInput } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { cn } from '@/lib/utils'
import { avatarTraitsFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Progress } from '@/components/ui/primitives'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { FormDialog } from '@/components/common/FormDialog'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { StatusBadge } from '@/components/common/StatusBadge'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Pagination } from '@/components/common/Pagination'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const QUESTION_TYPES = [
  { value: 'MultipleChoice', ar: 'اختيار من متعدد', en: 'Multiple choice' },
  { value: 'TrueFalse', ar: 'صح وخطأ', en: 'True / false' },
  { value: 'ShortAnswer', ar: 'إجابة قصيرة', en: 'Short answer' },
  { value: 'Essay', ar: 'مقالي', en: 'Essay' },
] as const

export default function TeacherExamDetailPage() {
  const { examId = '' } = useParams()
  const { t, locale, formatNumber, formatDateTime } = useI18n()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const exam = useQuery({
    queryKey: qk.teacher.exam(examId),
    queryFn: () => teacherExamsApi.details(examId),
    enabled: Boolean(examId),
  })

  usePageTitle(exam.data?.title ?? t('exams.title'))

  const publish = useMutation({
    mutationFn: (results: boolean) => teacherExamsApi.publish(examId, results),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({ title: t('common.published'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  if (exam.isPending) return <RowSkeleton count={5} />
  if (exam.isError) return <QueryError error={exam.error} onRetry={() => exam.refetch()} />

  const data = exam.data!

  return (
    <>
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to="/teacher/exams">
          <ArrowLeft className="size-4 flip-rtl" />
          {t('exams.title')}
        </Link>
      </Button>

      <DashboardHeader
        title={data.title ?? ''}
        description={[data.groupName, data.typeName].filter(Boolean).join(' · ')}
        actions={
          <>
            {!data.isPublished && (
              <Button size="sm" loading={publish.isPending} onClick={() => publish.mutate(false)}>
                <Eye className="size-4" />
                {locale === 'ar-EG' ? 'نشر الامتحان' : 'Publish exam'}
              </Button>
            )}
            {data.isPublished && !data.resultsPublished && (
              <Button size="sm" loading={publish.isPending} onClick={() => publish.mutate(true)}>
                <Trophy className="size-4" />
                {t('exams.publishResults')}
              </Button>
            )}
          </>
        }
      >
        <div className="flex flex-wrap gap-2">
          <Badge variant={data.isPublished ? 'success' : 'outline'} size="md">
            {data.isPublished ? t('common.published') : t('common.draft')}
          </Badge>
          <Badge variant="outline" size="md" className="tnum">
            {formatNumber(data.maximumScore ?? 0)} {t('exams.score')}
          </Badge>
          <Badge variant="outline" size="md" className="tnum">
            {formatNumber(data.durationInMinutes ?? 0)} {locale === 'ar-EG' ? 'دقيقة' : 'min'}
          </Badge>
          {data.examDate && (
            <Badge variant="outline" size="md" className="tnum">
              {formatDateTime(data.examDate)}
            </Badge>
          )}
        </div>
      </DashboardHeader>

      <Tabs defaultValue={data.isOnline ? 'questions' : 'attempts'}>
        <TabsList>
          {data.isOnline && (
            <TabsTrigger value="questions">
              {locale === 'ar-EG' ? 'الأسئلة' : 'Questions'}
            </TabsTrigger>
          )}
          <TabsTrigger value="attempts">{t('exams.attempts')}</TabsTrigger>
        </TabsList>

        {data.isOnline && (
          <TabsContent value="questions" className="mt-6">
            <QuestionsPanel examId={examId} />
          </TabsContent>
        )}

        <TabsContent value="attempts" className="mt-6">
          <AttemptsPanel examId={examId} />
        </TabsContent>
      </Tabs>
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                 Questions                                  */
/* -------------------------------------------------------------------------- */

function QuestionsPanel({ examId }: { examId: string }) {
  const { t, locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()
  const [formOpen, setFormOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<ExamQuestionDto | null>(null)
  const [deleting, setDeleting] = React.useState<ExamQuestionDto | null>(null)

  const questions = useQuery({
    queryKey: qk.teacher.examQuestions(examId),
    queryFn: () => teacherExamsApi.questions(examId),
    enabled: Boolean(examId),
  })

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: qk.teacher.examQuestions(examId) })
    queryClient.invalidateQueries({ queryKey: qk.teacher.exam(examId) })
  }

  const remove = useMutation({
    mutationFn: (questionId: string) => teacherExamsApi.removeQuestion(examId, questionId),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = questions.data ?? []
  const totalPoints = items.reduce((sum, q) => sum + (q.points ?? 0), 0)

  return (
    <>
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <p className="text-body-sm text-muted-foreground">
          <span className="tnum font-display font-bold text-foreground">
            {formatNumber(items.length)}
          </span>{' '}
          {locale === 'ar-EG' ? 'سؤال' : 'questions'} ·{' '}
          <span className="tnum font-display font-bold text-foreground">
            {formatNumber(totalPoints)}
          </span>{' '}
          {t('exams.points')}
        </p>
        <Button
          size="sm"
          onClick={() => {
            setEditing(null)
            setFormOpen(true)
          }}
        >
          <Plus className="size-4" />
          {t('exams.addQuestion')}
        </Button>
      </div>

      {questions.isPending ? (
        <RowSkeleton count={4} />
      ) : questions.isError ? (
        <QueryError error={questions.error} onRetry={() => questions.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          spot="exams"
          title={locale === 'ar-EG' ? 'لسه مفيش أسئلة' : 'No questions yet'}
          description={
            locale === 'ar-EG'
              ? 'ضيف أسئلة عشان الطلاب يقدروا يحلّوا الامتحان أونلاين.'
              : 'Add questions so students can take this exam online.'
          }
          action={{
            label: t('exams.addQuestion'),
            onClick: () => {
              setEditing(null)
              setFormOpen(true)
            },
          }}
        />
      ) : (
        <div className="space-y-3">
          <AnimatePresence initial={false}>
            {items.map((question, index) => (
              <motion.div
                key={question.id}
                layout
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, x: -20 }}
                transition={transitions.fast}
              >
                <Card className="p-5">
                  <div className="flex items-start gap-3">
                    <span className="tnum mt-0.5 grid size-7 shrink-0 place-items-center rounded-lg bg-muted font-display text-caption font-bold text-muted-foreground">
                      {index + 1}
                    </span>

                    <div className="min-w-0 flex-1">
                      <p className="font-display text-body-sm font-semibold leading-relaxed text-foreground">
                        {question.text}
                      </p>

                      <div className="mt-2 flex flex-wrap items-center gap-2">
                        <Badge variant="outline" size="sm">
                          {QUESTION_TYPES.find((q) => q.value === question.type)?.[lang] ??
                            question.typeName}
                        </Badge>
                        <Badge variant="primary" size="sm" className="tnum">
                          {formatNumber(question.points ?? 0)} {t('exams.points')}
                        </Badge>
                      </div>

                      {(question.options?.length ?? 0) > 0 && (
                        <ul className="mt-3 space-y-1.5">
                          {question.options!.map((option) => (
                            <li
                              key={option.id}
                              className={cn(
                                'flex items-center gap-2 rounded-lg px-3 py-2 text-body-sm',
                                option.isCorrect
                                  ? 'bg-success/8 text-foreground'
                                  : 'bg-muted/40 text-muted-foreground',
                              )}
                            >
                              {option.isCorrect ? (
                                <Check className="size-4 shrink-0 text-success-strong" />
                              ) : (
                                <X className="size-4 shrink-0 opacity-30" />
                              )}
                              {option.text}
                            </li>
                          ))}
                        </ul>
                      )}

                      {question.correctAnswer && (
                        <p className="mt-3 rounded-lg border border-success/25 bg-success/6 p-2.5 text-caption text-foreground">
                          <span className="font-semibold">{t('exams.correctAnswer')}: </span>
                          {question.correctAnswer}
                        </p>
                      )}
                    </div>

                    <div className="flex shrink-0 gap-1">
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={t('common.edit')}
                        onClick={() => {
                          setEditing(question)
                          setFormOpen(true)
                        }}
                      >
                        <GripVertical className="size-4" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        aria-label={t('common.delete')}
                        className="text-muted-foreground hover:text-destructive-strong"
                        onClick={() => setDeleting(question)}
                      >
                        <Trash2 className="size-4" />
                      </Button>
                    </div>
                  </div>
                </Card>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>
      )}

      <QuestionFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        examId={examId}
        question={editing}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف السؤال؟' : 'Delete this question?'}
        description={deleting?.text ?? ''}
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}

interface QuestionValues {
  text: string
  type: (typeof QUESTION_TYPES)[number]['value']
  points: string
  correctAnswer: string
  explanation: string
}

function QuestionFormDialog({
  open,
  onOpenChange,
  examId,
  question,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  examId: string
  question: ExamQuestionDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const isEdit = Boolean(question?.id)
  const [formError, setFormError] = React.useState<string | null>(null)
  const [values, setValues] = React.useState<QuestionValues>({
    text: '',
    type: 'MultipleChoice',
    points: '5',
    correctAnswer: '',
    explanation: '',
  })
  const [options, setOptions] = React.useState<ExamQuestionOptionInput[]>([])

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    if (question) {
      setValues({
        text: question.text ?? '',
        type: question.type ?? 'MultipleChoice',
        points: String(question.points ?? 5),
        correctAnswer: question.correctAnswer ?? '',
        explanation: question.explanation ?? '',
      })
      setOptions(
        (question.options ?? []).map((o, i) => ({
          text: o.text ?? '',
          isCorrect: o.isCorrect,
          sortOrder: o.sortOrder ?? i,
        })),
      )
    } else {
      setValues({ text: '', type: 'MultipleChoice', points: '5', correctAnswer: '', explanation: '' })
      setOptions([
        { text: '', isCorrect: true, sortOrder: 0 },
        { text: '', isCorrect: false, sortOrder: 1 },
      ])
    }
  }, [open, question])

  /* True/false questions always carry exactly the two fixed options. */
  React.useEffect(() => {
    if (values.type !== 'TrueFalse') return
    setOptions([
      { text: locale === 'ar-EG' ? 'صح' : 'True', isCorrect: true, sortOrder: 0 },
      { text: locale === 'ar-EG' ? 'خطأ' : 'False', isCorrect: false, sortOrder: 1 },
    ])
  }, [values.type, locale])

  const needsOptions = values.type === 'MultipleChoice' || values.type === 'TrueFalse'

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        text: values.text.trim(),
        type: values.type,
        points: Number(values.points) || 1,
        correctAnswer: needsOptions ? null : values.correctAnswer.trim() || null,
        explanation: values.explanation.trim() || null,
        options: needsOptions
          ? options
              .filter((o) => o.text.trim())
              .map((o, i) => ({ text: o.text.trim(), isCorrect: o.isCorrect, sortOrder: i }))
          : [],
      }
      return isEdit && question?.id
        ? teacherExamsApi.updateQuestion(examId, question.id, payload)
        : teacherExamsApi.addQuestion(examId, payload)
    },
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  const canSave =
    values.text.trim().length > 0 &&
    (!needsOptions || options.filter((o) => o.text.trim()).length >= 2)

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={isEdit ? t('common.edit') : t('exams.addQuestion')}
      error={formError}
      submitting={save.isPending}
      onSubmit={(e) => {
        e.preventDefault()
        if (canSave) save.mutate()
      }}
    >
      <Field label={t('exams.questionText')} htmlFor="q-text" required>
        <Textarea
          id="q-text"
          rows={3}
          value={values.text}
          onChange={(e) => setValues((v) => ({ ...v, text: e.target.value }))}
        />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t('exams.questionType')} htmlFor="q-type">
          <Select
            value={values.type}
            onValueChange={(v) => setValues((prev) => ({ ...prev, type: v as QuestionValues['type'] }))}
          >
            <SelectTrigger id="q-type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {QUESTION_TYPES.map((type) => (
                <SelectItem key={type.value} value={type.value}>
                  {type[lang]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field label={t('exams.points')} htmlFor="q-points" required>
          <Input
            id="q-points"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            value={values.points}
            onChange={(e) => setValues((v) => ({ ...v, points: e.target.value }))}
          />
        </Field>
      </div>

      {needsOptions ? (
        <div className="space-y-3 rounded-xl border border-border bg-muted/20 p-4">
          <div className="flex items-center justify-between gap-3">
            <p className="font-display text-body-sm font-bold text-foreground">
              {t('exams.options')}
            </p>
            {values.type === 'MultipleChoice' && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() =>
                  setOptions((prev) => [
                    ...prev,
                    { text: '', isCorrect: false, sortOrder: prev.length },
                  ])
                }
              >
                <Plus className="size-4" />
                {t('common.add')}
              </Button>
            )}
          </div>

          {options.map((option, index) => (
            <div key={index} className="flex items-center gap-2">
              <button
                type="button"
                onClick={() =>
                  setOptions((prev) => prev.map((o, i) => ({ ...o, isCorrect: i === index })))
                }
                aria-label={t('exams.correctAnswer')}
                aria-pressed={option.isCorrect}
                className={cn(
                  'grid size-9 shrink-0 place-items-center rounded-lg border transition-colors',
                  option.isCorrect
                    ? 'border-success bg-success/12 text-success-strong'
                    : 'border-border text-muted-foreground hover:border-foreground/25',
                )}
              >
                <Check className="size-4" />
              </button>
              <Input
                value={option.text}
                onChange={(e) =>
                  setOptions((prev) =>
                    prev.map((o, i) => (i === index ? { ...o, text: e.target.value } : o)),
                  )
                }
                readOnly={values.type === 'TrueFalse'}
                placeholder={`${t('exams.options')} ${index + 1}`}
                aria-label={`${t('exams.options')} ${index + 1}`}
              />
              {values.type === 'MultipleChoice' && options.length > 2 && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  className="text-muted-foreground hover:text-destructive-strong"
                  onClick={() => setOptions((prev) => prev.filter((_, i) => i !== index))}
                  aria-label={t('common.remove')}
                >
                  <Trash2 className="size-4" />
                </Button>
              )}
            </div>
          ))}
        </div>
      ) : (
        <Field
          label={t('exams.correctAnswer')}
          htmlFor="q-answer"
          hint={
            locale === 'ar-EG'
              ? 'للأسئلة المقالية سيبها فاضية وصحّح بنفسك.'
              : 'Leave empty for essay questions and grade them yourself.'
          }
        >
          <Input
            id="q-answer"
            value={values.correctAnswer}
            onChange={(e) => setValues((v) => ({ ...v, correctAnswer: e.target.value }))}
          />
        </Field>
      )}

      <Field label={t('exams.explanation')} htmlFor="q-explanation">
        <Textarea
          id="q-explanation"
          rows={2}
          value={values.explanation}
          onChange={(e) => setValues((v) => ({ ...v, explanation: e.target.value }))}
        />
      </Field>
    </FormDialog>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Attempts                                  */
/* -------------------------------------------------------------------------- */

function AttemptsPanel({ examId }: { examId: string }) {
  const { t, locale, formatNumber, formatDateTime } = useI18n()
  const [page, setPage] = React.useState(1)
  const [awaiting, setAwaiting] = React.useState(false)
  const [grading, setGrading] = React.useState<ExamAttemptDto | null>(null)

  const query = React.useMemo(
    () => ({ Page: page, PageSize: 20, AwaitingGrading: awaiting || undefined }),
    [page, awaiting],
  )

  const attempts = useQuery({
    queryKey: qk.teacher.examAttempts(examId, query),
    queryFn: () => teacherExamsApi.attempts(examId, query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [awaiting])

  const items = attempts.data?.items ?? []

  return (
    <>
      <div className="mb-5 flex flex-wrap items-center gap-2">
        <Button
          variant={awaiting ? 'primary' : 'outline'}
          size="sm"
          onClick={() => setAwaiting((v) => !v)}
        >
          {t('exams.awaitingGrading')}
        </Button>
      </div>

      {attempts.isPending ? (
        <RowSkeleton count={5} />
      ) : attempts.isError ? (
        <QueryError error={attempts.error} onRetry={() => attempts.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          spot="exams"
          title={locale === 'ar-EG' ? 'مفيش محاولات' : 'No attempts yet'}
          description={
            locale === 'ar-EG'
              ? 'أول ما طالب يحلّ الامتحان هتلاقي محاولته هنا.'
              : 'Student attempts appear here as soon as they take the exam.'
          }
        />
      ) : (
        <div className="space-y-3">
          {items.map((attempt) => (
            <Card key={attempt.id} className="flex flex-wrap items-center gap-4 p-4">
              <Avatar
                traits={avatarTraitsFor(attempt.studentId ?? '')}
                seed={attempt.studentId ?? ''}
                name={attempt.studentName ?? ''}
                size="sm"
              />
              <div className="min-w-0 flex-1">
                <p className="truncate font-display text-body-sm font-bold text-foreground">
                  {attempt.studentName}
                </p>
                <p className="tnum truncate text-caption text-muted-foreground">
                  {locale === 'ar-EG' ? 'محاولة' : 'Attempt'}{' '}
                  {formatNumber(attempt.attemptNumber ?? 0)}
                  {attempt.submittedAt ? ` · ${formatDateTime(attempt.submittedAt)}` : ''}
                </p>
              </div>

              {attempt.status !== 'InProgress' && (
                <div className="flex min-w-[8rem] items-center gap-2">
                  <Progress
                    value={attempt.percentage ?? 0}
                    className="h-1.5 flex-1"
                    indicatorClassName={attempt.isPassed ? 'bg-success' : 'bg-destructive'}
                  />
                  <span className="tnum text-caption font-bold text-foreground">
                    {formatNumber(attempt.score ?? 0)}/{formatNumber(attempt.maximumScore ?? 0)}
                  </span>
                </div>
              )}

              <StatusBadge status={attempt.status} fallbackLabel={attempt.statusName} />

              {attempt.requiresManualGrading && attempt.status === 'Submitted' && (
                <Button size="sm" onClick={() => setGrading(attempt)}>
                  {t('exams.gradeAttempt')}
                </Button>
              )}
            </Card>
          ))}
        </div>
      )}

      {attempts.data && items.length > 0 && (
        <Pagination page={attempts.data} onChange={setPage} className="mt-6" />
      )}

      {grading && (
        <GradeAttemptDialog attempt={grading} onClose={() => setGrading(null)} />
      )}
    </>
  )
}

function GradeAttemptDialog({
  attempt,
  onClose,
}: {
  attempt: ExamAttemptDto
  onClose: () => void
}) {
  const { t, locale, formatNumber } = useI18n()
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [formError, setFormError] = React.useState<string | null>(null)
  const [comment, setComment] = React.useState(attempt.teacherComment ?? '')
  const [scores, setScores] = React.useState<Record<string, { points: string; feedback: string }>>(
    () =>
      Object.fromEntries(
        (attempt.answers ?? []).map((a) => [
          a.id ?? '',
          { points: String(a.awardedPoints ?? 0), feedback: a.teacherFeedback ?? '' },
        ]),
      ),
  )

  const full = useQuery({
    queryKey: qk.teacher.attempt(attempt.id ?? ''),
    queryFn: () => teacherExamsApi.attempt(attempt.id!),
    enabled: Boolean(attempt.id),
  })

  React.useEffect(() => {
    if (!full.data?.answers) return
    setScores(
      Object.fromEntries(
        full.data.answers.map((a) => [
          a.id ?? '',
          { points: String(a.awardedPoints ?? 0), feedback: a.teacherFeedback ?? '' },
        ]),
      ),
    )
  }, [full.data])

  const save = useMutation({
    mutationFn: () =>
      teacherExamsApi.gradeAttempt(attempt.id!, {
        answers: Object.entries(scores).map(([answerId, value]) => ({
          answerId,
          awardedPoints: Number(value.points) || 0,
          feedback: value.feedback.trim() || null,
        })),
        teacherComment: comment.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({ title: t('common.saved'), variant: 'success' })
      onClose()
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  const answers = full.data?.answers ?? attempt.answers ?? []

  return (
    <FormDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={`${t('exams.gradeAttempt')} — ${attempt.studentName ?? ''}`}
      error={formError}
      submitting={save.isPending}
      submitLabel={t('common.save')}
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
      size="xl"
    >
      {full.isPending ? (
        <RowSkeleton count={3} />
      ) : (
        <>
          {answers.map((answer, index) => (
            <Card key={answer.id} className="space-y-3 p-4">
              <div className="flex items-start justify-between gap-3">
                <p className="font-display text-body-sm font-semibold leading-relaxed text-foreground">
                  {index + 1}. {answer.questionText}
                </p>
                <Badge variant="outline" size="sm" className="tnum shrink-0">
                  {formatNumber(answer.maximumPoints ?? 0)} {t('exams.points')}
                </Badge>
              </div>

              {answer.textAnswer && (
                <p className="whitespace-pre-wrap rounded-lg bg-muted/50 p-3 text-body-sm text-foreground">
                  {answer.textAnswer}
                </p>
              )}

              <div className="grid gap-3 sm:grid-cols-[8rem,1fr]">
                <Field label={t('exams.score')} htmlFor={`score-${answer.id}`}>
                  <Input
                    id={`score-${answer.id}`}
                    type="number"
                    min={0}
                    max={answer.maximumPoints}
                    dir="ltr"
                    className="text-start"
                    value={scores[answer.id ?? '']?.points ?? '0'}
                    onChange={(e) =>
                      setScores((prev) => ({
                        ...prev,
                        [answer.id ?? '']: {
                          ...prev[answer.id ?? ''],
                          points: e.target.value,
                        },
                      }))
                    }
                  />
                </Field>
                <Field label={t('homework.feedback')} htmlFor={`fb-${answer.id}`}>
                  <Input
                    id={`fb-${answer.id}`}
                    value={scores[answer.id ?? '']?.feedback ?? ''}
                    onChange={(e) =>
                      setScores((prev) => ({
                        ...prev,
                        [answer.id ?? '']: {
                          ...prev[answer.id ?? ''],
                          feedback: e.target.value,
                        },
                      }))
                    }
                  />
                </Field>
              </div>
            </Card>
          ))}

          <Field
            label={locale === 'ar-EG' ? 'ملاحظة عامة للطالب' : 'Overall comment'}
            htmlFor="attempt-comment"
          >
            <Textarea
              id="attempt-comment"
              rows={3}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
            />
          </Field>
        </>
      )}
    </FormDialog>
  )
}
