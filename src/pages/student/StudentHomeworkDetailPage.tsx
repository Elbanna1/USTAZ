import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { ArrowLeft, CalendarClock, CheckCircle2, Download, Paperclip, Send, Trophy, Undo2, Upload, X } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import { resolveMediaUrl } from '@/api/config'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/input'
import { Progress } from '@/components/ui/primitives'
import { StatusBadge } from '@/components/common/StatusBadge'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function StudentHomeworkDetailPage() {
  const { homeworkId = '' } = useParams()
  const { t, locale, formatNumber, formatDateTime } = useI18n()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const homework = useQuery({
    queryKey: qk.student.homeworkItem(homeworkId),
    queryFn: () => studentApi.homeworkDetails(homeworkId),
    enabled: Boolean(homeworkId),
  })

  usePageTitle(homework.data?.title ?? t('homework.title'))

  const submission = homework.data?.mySubmission
  const [content, setContent] = React.useState('')
  const [file, setFile] = React.useState<File | null>(null)
  const fileInputRef = React.useRef<HTMLInputElement>(null)

  React.useEffect(() => {
    if (submission?.content) setContent(submission.content)
  }, [submission?.content])

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: qk.student.homeworkItem(homeworkId) })
    queryClient.invalidateQueries({ queryKey: ['student', 'homework'] })
  }

  const submit = useMutation({
    mutationFn: async () => {
      // Attachments upload first so the submission records the stored file URL.
      if (file) await studentApi.attachHomeworkFile(homeworkId, file)
      return studentApi.submitHomework(homeworkId, { content: content.trim() || null })
    },
    onSuccess: () => {
      setFile(null)
      if (fileInputRef.current) fileInputRef.current.value = ''
      invalidate()
      toast({ title: t('homework.submitted'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  if (homework.isPending) return <RowSkeleton count={5} />
  if (homework.isError)
    return <QueryError error={homework.error} onRetry={() => homework.refetch()} />

  const data = homework.data!
  const dueDate = data.dueAt ? new Date(data.dueAt) : null
  const overdue = dueDate ? dueDate < new Date() : false
  const graded = submission?.status === 'Graded'
  const returned = submission?.status === 'Returned'
  const hasSubmitted = Boolean(submission?.submittedAt)

  // A returned submission is an explicit invitation to try again, so it
  // outranks the "already submitted" rule.
  const attemptsUsed = submission?.submissionCount ?? 0
  const attemptsAllowed = data.maxSubmissions ?? null
  const outOfAttempts =
    attemptsAllowed !== null && attemptsUsed >= attemptsAllowed && !returned

  const blockedReason: string | null = data.isClosed
    ? locale === 'ar-EG'
      ? 'المدرّس قفل الواجب، فمفيش تسليم جديد.'
      : 'Your teacher closed this homework, so it no longer accepts submissions.'
    : graded
      ? locale === 'ar-EG'
        ? 'الواجب اتصحّح خلاص.'
        : 'This homework has already been graded.'
      : outOfAttempts
        ? locale === 'ar-EG'
          ? 'استهلكت كل محاولات التسليم المتاحة.'
          : 'You have used all of your submission attempts.'
        : hasSubmitted && !data.allowResubmission && !returned
          ? locale === 'ar-EG'
            ? 'سلّمت الواجب، وإعادة التسليم مش مسموحة فيه.'
            : 'You have submitted, and this homework does not allow resubmission.'
          : overdue && !data.allowLateSubmission
            ? locale === 'ar-EG'
              ? 'الموعد النهائي عدّى، والتسليم المتأخر مش مسموح.'
              : 'The deadline has passed and late submission is not allowed.'
            : null

  const canSubmit = blockedReason === null

  return (
    <div className="mx-auto max-w-4xl">
      <Button asChild variant="ghost" size="sm" className="mb-4">
        <Link to="/student/homework">
          <ArrowLeft className="size-4 flip-rtl" />
          {t('homework.title')}
        </Link>
      </Button>

      <DashboardHeader
        title={data.title ?? ''}
        description={data.description ?? undefined}
        actions={<StatusBadge status={submission?.status ?? 'NotSubmitted'} size="md" />}
      />

      {returned && submission?.returnReason && (
        <Card className="mb-6 border-warning/30 bg-warning/[0.07] p-5">
          <div className="flex items-start gap-3">
            <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-warning/15 text-warning-strong">
              <Undo2 className="size-4" />
            </span>
            <div className="min-w-0 flex-1">
              <h2 className="font-display text-body font-bold text-foreground">
                {locale === 'ar-EG'
                  ? 'المدرّس رجّعلك الواجب للتعديل'
                  : 'Your teacher sent this back for changes'}
              </h2>
              <p className="mt-1.5 whitespace-pre-wrap text-body-sm leading-relaxed text-foreground/90">
                {submission.returnReason}
              </p>
              {submission.returnedAt && (
                <p className="mt-2 text-caption text-muted-foreground">
                  {formatDateTime(submission.returnedAt)}
                </p>
              )}
            </div>
          </div>
        </Card>
      )}

      {blockedReason && !returned && (
        <Card className="mb-6 border-dashed p-4">
          <p className="text-body-sm text-muted-foreground">{blockedReason}</p>
        </Card>
      )}

      {/* ------------------------------- Facts ------------------------------- */}
      <Card className="mb-6 p-5">
        <dl className="grid grid-cols-2 gap-5 sm:grid-cols-4">
          <div>
            <dt className="flex items-center gap-1.5 text-caption text-muted-foreground">
              <CalendarClock className="size-4 text-primary" />
              {t('homework.dueAt')}
            </dt>
            <dd
              className={cn(
                'tnum mt-1 font-display text-body-sm font-bold',
                overdue && !submission?.submittedAt ? 'text-destructive-strong' : 'text-foreground',
              )}
            >
              {dueDate ? formatDateTime(dueDate.toISOString()) : '—'}
            </dd>
          </div>
          <div>
            <dt className="flex items-center gap-1.5 text-caption text-muted-foreground">
              <Trophy className="size-4 text-primary" />
              {t('exams.maximumScore')}
            </dt>
            <dd className="tnum mt-1 font-display text-body-sm font-bold text-foreground">
              {formatNumber(data.maximumScore ?? 0)}
            </dd>
          </div>
          {data.groupName && (
            <div>
              <dt className="text-caption text-muted-foreground">{t('common.group')}</dt>
              <dd className="mt-1 truncate font-display text-body-sm font-bold text-foreground">
                {data.groupName}
              </dd>
            </div>
          )}
          <div>
            <dt className="text-caption text-muted-foreground">{t('homework.allowLate')}</dt>
            <dd className="mt-1 font-display text-body-sm font-bold text-foreground">
              {data.allowLateSubmission ? t('common.yes') : t('common.no')}
            </dd>
          </div>
          {attemptsAllowed !== null && (
            <div>
              <dt className="text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'المحاولات' : 'Attempts'}
              </dt>
              <dd className="tnum mt-1 font-display text-body-sm font-bold text-foreground">
                {formatNumber(attemptsUsed)} / {formatNumber(attemptsAllowed)}
              </dd>
            </div>
          )}
        </dl>
      </Card>

      {/* ---------------------------- Instructions --------------------------- */}
      {data.instructions && (
        <Card className="mb-6 p-5">
          <h2 className="font-display text-heading-sm text-foreground">
            {t('homework.instructions')}
          </h2>
          <p className="mt-3 whitespace-pre-wrap text-body-sm leading-relaxed text-muted-foreground">
            {data.instructions}
          </p>
        </Card>
      )}

      {/* ---------------------------- Attachments ---------------------------- */}
      {(data.attachments?.length ?? 0) > 0 && (
        <Card className="mb-6 p-5">
          <h2 className="font-display text-heading-sm text-foreground">
            {t('homework.attachments')}
          </h2>
          <ul className="mt-3 space-y-2">
            {data.attachments!.map((attachment) => (
              <li key={attachment.id}>
                <a
                  href={resolveMediaUrl(attachment.fileUrl)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3 transition-colors hover:border-foreground/20 hover:bg-muted"
                >
                  <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                  <span className="min-w-0 flex-1 truncate text-body-sm text-foreground">
                    {attachment.fileName}
                  </span>
                  <Download className="size-4 shrink-0 text-muted-foreground" />
                </a>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {/* ------------------------------- Grade ------------------------------- */}
      {graded && submission && (
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={transitions.base}
          className="mb-6"
        >
          <Card className="border-success/30 bg-success/5 p-5">
            <div className="flex flex-wrap items-center gap-4">
              <span className="grid size-12 place-items-center rounded-2xl bg-success/15 text-success-strong">
                <CheckCircle2 className="size-6" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="font-display text-heading-sm font-bold text-foreground">
                  {t('homework.graded')}
                </p>
                <p className="tnum text-body-sm text-muted-foreground">
                  {formatNumber(submission.score ?? 0)} /{' '}
                  {formatNumber(submission.maximumScore ?? data.maximumScore ?? 0)}
                  {submission.percentage != null && ` · ${Math.round(submission.percentage)}%`}
                </p>
              </div>
            </div>
            <Progress
              value={submission.percentage ?? 0}
              className="mt-4 h-2"
              indicatorClassName="bg-success"
            />
            {submission.teacherFeedback && (
              <div className="mt-4 rounded-lg border border-border bg-card p-4">
                <p className="text-caption font-semibold text-muted-foreground">
                  {t('homework.feedback')}
                </p>
                <p className="mt-1.5 whitespace-pre-wrap text-body-sm leading-relaxed text-foreground">
                  {submission.teacherFeedback}
                </p>
              </div>
            )}
          </Card>
        </motion.div>
      )}

      {/* ----------------------------- Submission ---------------------------- */}
      <Card className="p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="font-display text-heading-sm text-foreground">
            {t('homework.yourAnswer')}
          </h2>
          {submission?.submittedAt && (
            <Badge variant={submission.isLate ? 'warning' : 'success'} size="sm">
              {submission.isLate ? t('homework.overdue') : t('homework.submitted')} ·{' '}
              {formatDateTime(submission.submittedAt)}
            </Badge>
          )}
        </div>

        {canSubmit ? (
          <form
            onSubmit={(e) => {
              e.preventDefault()
              submit.mutate()
            }}
            className="mt-4 space-y-4"
          >
            <Textarea
              value={content}
              onChange={(e) => setContent(e.target.value)}
              rows={6}
              placeholder={
                locale === 'ar-EG' ? 'اكتب إجابتك أو ملاحظاتك هنا…' : 'Write your answer here…'
              }
              aria-label={t('homework.yourAnswer')}
            />

            <input
              ref={fileInputRef}
              type="file"
              className="hidden"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />

            {file ? (
              <motion.div
                initial={{ opacity: 0, y: 6 }}
                animate={{ opacity: 1, y: 0 }}
                className="flex items-center gap-2 rounded-lg border border-border bg-muted/40 px-3 py-2.5 text-body-sm"
              >
                <Paperclip className="size-4 shrink-0 text-muted-foreground" />
                <span className="min-w-0 flex-1 truncate">{file.name}</span>
                <button
                  type="button"
                  onClick={() => {
                    setFile(null)
                    if (fileInputRef.current) fileInputRef.current.value = ''
                  }}
                  aria-label={t('common.remove')}
                  className="rounded p-1 text-muted-foreground transition-colors hover:text-destructive-strong"
                >
                  <X className="size-4" />
                </button>
              </motion.div>
            ) : (
              <Button
                type="button"
                variant="outline"
                onClick={() => fileInputRef.current?.click()}
              >
                <Upload className="size-4" />
                {t('homework.attachFile')}
              </Button>
            )}

            {overdue && data.allowLateSubmission && (
              <p className="rounded-lg border border-warning/30 bg-warning/8 p-3 text-body-sm text-warning-strong">
                {locale === 'ar-EG'
                  ? 'الميعاد عدّى — التسليم ده هيتسجّل كتسليم متأخر.'
                  : 'The deadline has passed — this will be recorded as a late submission.'}
              </p>
            )}

            <Button
              type="submit"
              size="lg"
              loading={submit.isPending}
              disabled={!content.trim() && !file}
            >
              <Send className="size-4 flip-rtl" />
              {submission?.submittedAt ? t('homework.resubmit') : t('homework.submit')}
            </Button>
          </form>
        ) : (
          <div className="mt-4 space-y-3">
            {submission?.content ? (
              <p className="whitespace-pre-wrap rounded-lg bg-muted/40 p-4 text-body-sm leading-relaxed text-foreground">
                {submission.content}
              </p>
            ) : (
              <p className="text-body-sm text-muted-foreground">
                {graded
                  ? locale === 'ar-EG'
                    ? 'الواجب اتصحّح، مش ممكن تعدّله.'
                    : 'This homework is graded and can no longer be edited.'
                  : locale === 'ar-EG'
                    ? 'ميعاد التسليم عدّى والمدرّس مش مفعّل التسليم المتأخر.'
                    : 'The deadline has passed and late submissions are not allowed.'}
              </p>
            )}
            {submission?.fileUrl && (
              <a
                href={resolveMediaUrl(submission.fileUrl)}
                target="_blank"
                rel="noopener noreferrer"
                className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-body-sm transition-colors hover:bg-muted"
              >
                <Download className="size-4" />
                {t('common.download')}
              </a>
            )}
          </div>
        )}
      </Card>
    </div>
  )
}
