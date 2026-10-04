import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft,
  CalendarClock,
  Download,
  Eye,
  Lock,
  LockOpen,
  Trophy,
  Undo2,
  Users,
} from 'lucide-react'
import { teacherHomeworkApi } from '@/api/endpoints/teacher'
import { resolveMediaUrl } from '@/api/config'
import type { HomeworkSubmissionDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { avatarTraitsFor } from '@/lib/identity'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { FilterChip } from '@/components/common/controls'
import { StatusBadge } from '@/components/common/StatusBadge'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { Pagination } from '@/components/common/Pagination'
import { FormDialog } from '@/components/common/FormDialog'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Progress } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const STATUSES = [
  { value: undefined, ar: 'الكل', en: 'All' },
  { value: 'Submitted', ar: 'مستني تصحيح', en: 'Awaiting grading' },
  { value: 'Graded', ar: 'اتصحّح', en: 'Graded' },
  { value: 'Returned', ar: 'اترجّع للتعديل', en: 'Returned' },
  { value: 'Late', ar: 'متأخر', en: 'Late' },
  { value: 'NotSubmitted', ar: 'لسه متسلّمش', en: 'Not submitted' },
] as const

export default function TeacherHomeworkDetailPage() {
  const { homeworkId = '' } = useParams()
  const { t, locale, formatNumber, formatDateTime } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [status, setStatus] = React.useState<string>()
  const [confirmClose, setConfirmClose] = React.useState(false)
  const [returning, setReturning] = React.useState<HomeworkSubmissionDto | null>(null)
  const [grading, setGrading] = React.useState<HomeworkSubmissionDto | null>(null)

  const homework = useQuery({
    queryKey: qk.teacher.homeworkItem(homeworkId),
    queryFn: () => teacherHomeworkApi.details(homeworkId),
    enabled: Boolean(homeworkId),
  })

  const query = React.useMemo(() => ({ Page: page, PageSize: 20, Status: status }), [page, status])
  const submissions = useQuery({
    queryKey: qk.teacher.homeworkSubmissions(homeworkId, query),
    queryFn: () => teacherHomeworkApi.submissions(homeworkId, query),
    enabled: Boolean(homeworkId),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [status])

  usePageTitle(homework.data?.title ?? t('homework.title'))

  const publish = useMutation({
    mutationFn: () => teacherHomeworkApi.publish(homeworkId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({ title: t('common.published'), variant: 'success' })
    },
    onError: (error) => showApiError(error),
  })

  // Closing stops new submissions; reopening lets late work back in. Both are
  // reversible, so only closing asks for confirmation.
  const close = useMutation({
    mutationFn: () => teacherHomeworkApi.close(homeworkId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      setConfirmClose(false)
      toast({
        title: locale === 'ar-EG' ? 'الواجب اتقفل' : 'Homework closed',
        description:
          locale === 'ar-EG'
            ? 'مش هيقدر حد يسلّم تاني لحد ما تفتحه.'
            : 'No further submissions until you reopen it.',
        variant: 'success',
      })
    },
    onError: (error) => showApiError(error),
  })

  const reopen = useMutation({
    mutationFn: () => teacherHomeworkApi.reopen(homeworkId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({
        title: locale === 'ar-EG' ? 'الواجب اتفتح تاني' : 'Homework reopened',
        variant: 'success',
      })
    },
    onError: (error) => showApiError(error),
  })

  if (homework.isPending) return <RowSkeleton count={5} />
  if (homework.isError)
    return <QueryError error={homework.error} onRetry={() => homework.refetch()} />

  const data = homework.data!
  const items = submissions.data?.items ?? []
  const gradedRate = data.submissionsCount
    ? Math.round(((data.gradedCount ?? 0) / data.submissionsCount) * 100)
    : 0

  return (
    <>
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to="/teacher/homework">
          <ArrowLeft className="size-4 flip-rtl" />
          {t('homework.title')}
        </Link>
      </Button>

      <DashboardHeader
        title={data.title ?? ''}
        description={data.description ?? undefined}
        actions={
          !data.isPublished ? (
            <Button size="sm" loading={publish.isPending} onClick={() => publish.mutate()}>
              <Eye className="size-4" />
              {locale === 'ar-EG' ? 'نشر الواجب' : 'Publish homework'}
            </Button>
          ) : data.isClosed ? (
            <Button
              size="sm"
              variant="outline"
              loading={reopen.isPending}
              onClick={() => reopen.mutate()}
            >
              <LockOpen className="size-4" />
              {locale === 'ar-EG' ? 'افتح الواجب' : 'Reopen'}
            </Button>
          ) : (
            <Button size="sm" variant="outline" onClick={() => setConfirmClose(true)}>
              <Lock className="size-4" />
              {locale === 'ar-EG' ? 'اقفل الواجب' : 'Close'}
            </Button>
          )
        }
      >
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant={!data.isPublished ? 'outline' : data.isClosed ? 'warning' : 'success'}
            size="md"
          >
            {!data.isPublished
              ? t('common.draft')
              : data.isClosed
                ? locale === 'ar-EG'
                  ? 'مقفول'
                  : 'Closed'
                : t('common.published')}
          </Badge>
          {(data.pendingGradingCount ?? 0) > 0 && (
            <Badge variant="warning" size="md" className="tnum">
              {formatNumber(data.pendingGradingCount!)}{' '}
              {locale === 'ar-EG' ? 'مستني تصحيح' : 'awaiting grading'}
            </Badge>
          )}
          {(data.returnedCount ?? 0) > 0 && (
            <Badge variant="outline" size="md" className="tnum">
              <Undo2 className="size-3.5" />
              {formatNumber(data.returnedCount!)}{' '}
              {locale === 'ar-EG' ? 'اترجّع' : 'returned'}
            </Badge>
          )}
          {data.allowResubmission && (
            <Badge variant="outline" size="md">
              {locale === 'ar-EG' ? 'إعادة التسليم مسموحة' : 'Resubmission allowed'}
            </Badge>
          )}
          {data.groupName && (
            <Badge variant="outline" size="md">
              {data.groupName}
            </Badge>
          )}
          {data.dueAt && (
            <Badge variant="outline" size="md" className="tnum">
              <CalendarClock className="size-3.5" />
              {formatDateTime(data.dueAt)}
            </Badge>
          )}
          <Badge variant="outline" size="md" className="tnum">
            <Trophy className="size-3.5" />
            {formatNumber(data.maximumScore ?? 0)}
          </Badge>
        </div>
      </DashboardHeader>

      <Card className="mb-6 p-5">
        <div className="flex flex-wrap items-center gap-4">
          <span className="grid size-11 place-items-center rounded-xl bg-primary/10 text-primary">
            <Users className="size-5" />
          </span>
          <div className="min-w-0 flex-1">
            <p className="tnum font-display text-heading-sm font-bold text-foreground">
              {formatNumber(data.gradedCount ?? 0)} / {formatNumber(data.submissionsCount ?? 0)}{' '}
              {t('homework.graded')}
            </p>
            <Progress value={gradedRate} className="mt-2 h-1.5" />
          </div>
        </div>
      </Card>

      <div className="mb-5 no-scrollbar flex gap-2 overflow-x-auto">
        {STATUSES.map((s) => (
          <FilterChip
            key={s.value ?? 'all'}
            label={s[lang]}
            active={status === s.value}
            onClick={() => setStatus(s.value)}
          />
        ))}
      </div>

      {submissions.isPending ? (
        <RowSkeleton count={5} />
      ) : submissions.isError ? (
        <QueryError error={submissions.error} onRetry={() => submissions.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          spot="homework"
          title={locale === 'ar-EG' ? 'مفيش تسليمات' : 'No submissions'}
          description={
            locale === 'ar-EG'
              ? 'أول ما طالب يسلّم هتلاقي تسليمه هنا.'
              : 'Submissions appear here as soon as a student hands in.'
          }
        />
      ) : (
        <div className="space-y-3">
          {items.map((submission) => (
            <Card key={submission.id} className="p-4">
              <div className="flex flex-wrap items-center gap-4">
                <Avatar
                  traits={avatarTraitsFor(submission.studentId ?? '')}
                  seed={submission.studentId ?? ''}
                  name={submission.studentName ?? ''}
                  size="sm"
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-display text-body-sm font-bold text-foreground">
                    {submission.studentName}
                  </p>
                  <p className="tnum truncate text-caption text-muted-foreground">
                    {submission.submittedAt ? formatDateTime(submission.submittedAt) : '—'}
                    {submission.isLate ? ` · ${t('homework.overdue')}` : ''}
                  </p>
                </div>

                {submission.status === 'Graded' && (
                  <Badge variant="success" size="md" className="tnum">
                    {formatNumber(submission.score ?? 0)} /{' '}
                    {formatNumber(submission.maximumScore ?? data.maximumScore ?? 0)}
                  </Badge>
                )}

                <StatusBadge status={submission.status} fallbackLabel={submission.statusName} />

                {submission.fileUrl && (
                  <Button asChild variant="outline" size="icon-sm">
                    <a
                      href={resolveMediaUrl(submission.fileUrl)}
                      target="_blank"
                      rel="noopener noreferrer"
                      aria-label={t('common.download')}
                    >
                      <Download className="size-4" />
                    </a>
                  </Button>
                )}

                {submission.status !== 'NotSubmitted' && submission.status !== 'Returned' && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setReturning(submission)}
                  >
                    <Undo2 className="size-4" />
                    {locale === 'ar-EG' ? 'رجّعه' : 'Return'}
                  </Button>
                )}

                {submission.status !== 'NotSubmitted' && (
                  <Button size="sm" onClick={() => setGrading(submission)}>
                    {submission.status === 'Graded' ? t('common.edit') : t('homework.grade')}
                  </Button>
                )}
              </div>

              {submission.content && (
                <p className="mt-3 whitespace-pre-wrap rounded-lg bg-muted/40 p-3 text-body-sm text-foreground">
                  {submission.content}
                </p>
              )}

              {submission.status === 'Returned' && submission.returnReason && (
                <p className="mt-3 rounded-lg border border-warning/25 bg-warning/[0.07] p-3 text-body-sm text-foreground">
                  <span className="font-semibold">
                    {locale === 'ar-EG' ? 'سبب الإرجاع: ' : 'Returned because: '}
                  </span>
                  {submission.returnReason}
                </p>
              )}

              {(submission.submissionCount ?? 0) > 1 && (
                <p className="mt-2 text-caption text-muted-foreground">
                  {locale === 'ar-EG'
                    ? `تسليم رقم ${formatNumber(submission.submissionCount!)}`
                    : `Attempt ${formatNumber(submission.submissionCount!)}`}
                </p>
              )}
            </Card>
          ))}
        </div>
      )}

      {submissions.data && items.length > 0 && (
        <Pagination page={submissions.data} onChange={setPage} className="mt-6" />
      )}

      {grading && (
        <GradeSubmissionDialog
          submission={grading}
          maximumScore={data.maximumScore ?? 10}
          onClose={() => setGrading(null)}
        />
      )}

      {returning && (
        <ReturnSubmissionDialog
          submission={returning}
          onClose={() => setReturning(null)}
        />
      )}

      <ConfirmDialog
        open={confirmClose}
        onOpenChange={setConfirmClose}
        title={locale === 'ar-EG' ? 'تقفل الواجب؟' : 'Close this homework?'}
        description={
          locale === 'ar-EG'
            ? 'مش هيقدر حد يسلّم بعد كده. تقدر تفتحه تاني في أي وقت.'
            : 'Students will not be able to submit. You can reopen it at any time.'
        }
        confirmLabel={locale === 'ar-EG' ? 'اقفل' : 'Close'}
        loading={close.isPending}
        onConfirm={() => close.mutate()}
      />
    </>
  )
}

/**
 * Hands a submission back for another attempt.
 *
 * The reason is mandatory by contract and by intent: "returned" is only
 * actionable if the student is told what to change, so the submit stays
 * disabled until something is written.
 */
function ReturnSubmissionDialog({
  submission,
  onClose,
}: {
  submission: HomeworkSubmissionDto
  onClose: () => void
}) {
  const { t, locale } = useI18n()
  const isAr = locale === 'ar-EG'
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [reason, setReason] = React.useState('')
  const [formError, setFormError] = React.useState<string | null>(null)

  const trimmed = reason.trim()

  const send = useMutation({
    mutationFn: () => teacherHomeworkApi.returnSubmission(submission.id!, trimmed),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({
        title: isAr ? 'رجّعنا التسليم للطالب' : 'Sent back to the student',
        description: isAr
          ? 'هيوصله إشعار بالسبب ويقدر يسلّم تاني.'
          : 'They are notified with your reason and can submit again.',
        variant: 'success',
      })
      onClose()
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={`${isAr ? 'رجّع التسليم' : 'Return submission'} — ${submission.studentName ?? ''}`}
      error={formError}
      submitting={send.isPending}
      submitLabel={isAr ? 'رجّعه للطالب' : 'Return to student'}
      submitDisabled={trimmed.length === 0}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        if (!trimmed) return
        send.mutate()
      }}
    >
      <Field
        label={isAr ? 'إيه اللي محتاج يتعدّل؟' : 'What needs changing?'}
        htmlFor="return-reason"
        required
        hint={
          isAr
            ? 'الطالب هيشوف الكلام ده، فاكتب المطلوب بوضوح.'
            : 'The student sees this, so be specific about what to fix.'
        }
      >
        <Textarea
          id="return-reason"
          rows={4}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder={
            isAr
              ? 'مثال: الحل ناقص الخطوة الأخيرة في السؤال الثالث.'
              : 'e.g. The final step of question 3 is missing.'
          }
        />
      </Field>
    </FormDialog>
  )
}

function GradeSubmissionDialog({
  submission,
  maximumScore,
  onClose,
}: {
  submission: HomeworkSubmissionDto
  maximumScore: number
  onClose: () => void
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [score, setScore] = React.useState(String(submission.score ?? ''))
  const [feedback, setFeedback] = React.useState(submission.teacherFeedback ?? '')
  const [formError, setFormError] = React.useState<string | null>(null)

  const save = useMutation({
    mutationFn: () =>
      teacherHomeworkApi.grade(submission.id!, {
        score: Number(score) || 0,
        feedback: feedback.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({ title: t('common.saved'), variant: 'success' })
      onClose()
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={`${t('homework.grade')} — ${submission.studentName ?? ''}`}
      error={formError}
      submitting={save.isPending}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      {submission.content && (
        <div>
          <p className="mb-2 font-display text-body-sm font-semibold text-foreground">
            {t('homework.yourAnswer')}
          </p>
          <p className="whitespace-pre-wrap rounded-lg bg-muted/50 p-3 text-body-sm text-foreground">
            {submission.content}
          </p>
        </div>
      )}

      <Field
        label={`${t('exams.score')} (${locale === 'ar-EG' ? 'من' : 'out of'} ${maximumScore})`}
        htmlFor="grade-score"
        required
      >
        <Input
          id="grade-score"
          type="number"
          min={0}
          max={maximumScore}
          dir="ltr"
          className="text-start"
          value={score}
          onChange={(e) => setScore(e.target.value)}
        />
      </Field>

      <Field label={t('homework.feedback')} htmlFor="grade-feedback">
        <Textarea
          id="grade-feedback"
          rows={4}
          value={feedback}
          onChange={(e) => setFeedback(e.target.value)}
        />
      </Field>
    </FormDialog>
  )
}
