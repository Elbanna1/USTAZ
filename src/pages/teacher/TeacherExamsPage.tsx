import * as React from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { CalendarClock, Eye, MoreHorizontal, Pencil, Plus, Timer, Trash2, Trophy } from 'lucide-react'
import { teacherExamsApi, teacherGroupsApi, type TeacherExamQuery } from '@/api/endpoints/teacher'
import type { ExamDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { applyServerErrors } from '@/lib/form'
import { cn } from '@/lib/utils'
import { subjectKeyFor } from '@/lib/identity'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { FormDialog } from '@/components/common/FormDialog'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import { Switch } from '@/components/ui/primitives'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 12

const EXAM_TYPES = [
  { value: 'Quiz', ar: 'كويز', en: 'Quiz' },
  { value: 'Monthly', ar: 'شهري', en: 'Monthly' },
  { value: 'Midterm', ar: 'نصف الترم', en: 'Midterm' },
  { value: 'Final', ar: 'نهائي', en: 'Final' },
  { value: 'Practice', ar: 'تدريبي', en: 'Practice' },
] as const

export default function TeacherExamsPage() {
  const { t, locale, formatNumber, formatDateTime } = useI18n()
  usePageTitle(t('exams.title'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [published, setPublished] = React.useState<boolean | undefined>()
  const [formOpen, setFormOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<ExamDto | null>(null)
  const [deleting, setDeleting] = React.useState<ExamDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query: TeacherExamQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      IsPublished: published,
    }),
    [page, debounced, published],
  )

  const exams = useQuery({
    queryKey: qk.teacher.exams(query),
    queryFn: () => teacherExamsApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, published])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher'] })

  const publish = useMutation({
    mutationFn: ({ id, results }: { id: string; results: boolean }) =>
      teacherExamsApi.publish(id, results),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.published'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teacherExamsApi.remove(id),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = exams.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('exams.title')}
        description={
          locale === 'ar-EG'
            ? 'أنشئ امتحانات أونلاين أو سجّل درجات امتحانات ورقية.'
            : 'Create online exams or record the results of paper ones.'
        }
        actions={
          <Button
            size="sm"
            onClick={() => {
              setEditing(null)
              setFormOpen(true)
            }}
          >
            <Plus className="size-4" />
            {locale === 'ar-EG' ? 'امتحان جديد' : 'New exam'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-exams-search"
          />
          <div className="flex gap-2">
            <FilterChip
              label={t('common.all')}
              active={published === undefined}
              onClick={() => setPublished(undefined)}
            />
            <FilterChip
              label={t('common.published')}
              active={published === true}
              onClick={() => setPublished(true)}
            />
            <FilterChip
              label={t('common.draft')}
              active={published === false}
              onClick={() => setPublished(false)}
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
            title={locale === 'ar-EG' ? 'لسه مفيش امتحانات' : 'No exams yet'}
            description={
              locale === 'ar-EG'
                ? 'الامتحان الأونلاين بيتصحّح تلقائي، والورقي بتسجّل درجاته بنفسك.'
                : 'Online exams grade themselves; for paper exams you record the scores.'
            }
            action={{
              label: locale === 'ar-EG' ? 'أنشئ امتحان' : 'Create an exam',
              onClick: () => {
                setEditing(null)
                setFormOpen(true)
              },
            }}
          />
        ) : (
          <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
            {items.map((exam) => (
              <RevealItem key={exam.id} className="h-full">
                <Card className="flex h-full flex-col p-5">
                  <div className="flex items-start gap-3">
                    <SubjectTile
                      subject={subjectKeyFor({
                        nameAr: exam.subjectNameAr,
                        nameEn: exam.subjectNameEn,
                      })}
                      size="sm"
                    />
                    <div className="min-w-0 flex-1">
                      <Link
                        to={`/teacher/exams/${exam.id}`}
                        className="line-clamp-2-rtl font-display text-body font-bold leading-snug text-foreground hover:text-primary"
                      >
                        {exam.title}
                      </Link>
                      {exam.groupName && (
                        <p className="mt-0.5 truncate text-caption text-muted-foreground">
                          {exam.groupName}
                        </p>
                      )}
                    </div>

                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon-sm" aria-label={t('common.actions')}>
                          <MoreHorizontal className="size-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem
                          onSelect={() => {
                            setEditing(exam)
                            setFormOpen(true)
                          }}
                        >
                          <Pencil className="size-4" />
                          {t('common.edit')}
                        </DropdownMenuItem>
                        {!exam.isPublished && (
                          <DropdownMenuItem
                            onSelect={() => exam.id && publish.mutate({ id: exam.id, results: false })}
                          >
                            <Eye className="size-4" />
                            {locale === 'ar-EG' ? 'نشر الامتحان' : 'Publish exam'}
                          </DropdownMenuItem>
                        )}
                        {exam.isPublished && !exam.resultsPublished && (
                          <DropdownMenuItem
                            onSelect={() => exam.id && publish.mutate({ id: exam.id, results: true })}
                          >
                            <Trophy className="size-4" />
                            {t('exams.publishResults')}
                          </DropdownMenuItem>
                        )}
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                          className="text-destructive-strong"
                          onSelect={() => setDeleting(exam)}
                        >
                          <Trash2 className="size-4" />
                          {t('common.delete')}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>

                  <div className="mt-3 flex flex-wrap gap-1.5">
                    <Badge variant={exam.isPublished ? 'success' : 'outline'} size="sm">
                      {exam.isPublished ? t('common.published') : t('common.draft')}
                    </Badge>
                    {exam.resultsPublished && (
                      <Badge variant="accent" size="sm">
                        {locale === 'ar-EG' ? 'النتائج منشورة' : 'Results published'}
                      </Badge>
                    )}
                    <Badge variant="outline" size="sm">
                      {exam.typeName ?? exam.type}
                    </Badge>
                    {exam.isOnline && (
                      <Badge variant="primary" size="sm">
                        {locale === 'ar-EG' ? 'أونلاين' : 'Online'}
                      </Badge>
                    )}
                  </div>

                  <ul className="mt-4 space-y-2 text-body-sm text-muted-foreground">
                    {exam.examDate && (
                      <li className="flex items-center gap-2">
                        <CalendarClock className="size-4 shrink-0 text-primary" />
                        <span className="tnum">{formatDateTime(exam.examDate)}</span>
                      </li>
                    )}
                    <li className="flex items-center gap-2">
                      <Timer className="size-4 shrink-0 text-primary" />
                      <span className="tnum">
                        {formatNumber(exam.durationInMinutes ?? 0)}{' '}
                        {locale === 'ar-EG' ? 'دقيقة' : 'min'}
                      </span>
                    </li>
                    <li className="flex items-center gap-2">
                      <Trophy className="size-4 shrink-0 text-primary" />
                      <span className="tnum">
                        {formatNumber(exam.questionsCount ?? 0)}{' '}
                        {locale === 'ar-EG' ? 'سؤال' : 'questions'} ·{' '}
                        {formatNumber(exam.resultsCount ?? 0)}{' '}
                        {locale === 'ar-EG' ? 'نتيجة' : 'results'}
                      </span>
                    </li>
                  </ul>

                  <div className="mt-auto border-t border-border pt-4">
                    <Button asChild size="sm" variant="outline" block>
                      <Link to={`/teacher/exams/${exam.id}`}>{t('common.details')}</Link>
                    </Button>
                  </div>
                </Card>
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>

      {exams.data && items.length > 0 && (
        <Pagination page={exams.data} onChange={setPage} className="mt-8" />
      )}

      <ExamFormDialog open={formOpen} onOpenChange={setFormOpen} exam={editing} onSaved={invalidate} />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف الامتحان؟' : 'Delete this exam?'}
        description={
          locale === 'ar-EG'
            ? `هتحذف «${deleting?.title}» وكل أسئلته ونتايجه.`
            : `This deletes "${deleting?.title}" with all its questions and results.`
        }
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                 Exam form                                  */
/* -------------------------------------------------------------------------- */

interface ExamValues {
  title: string
  description: string
  subjectId: string
  groupId: string
  type: (typeof EXAM_TYPES)[number]['value']
  examDate: string
  durationInMinutes: string
  maximumScore: string
  passingScore: string
  maxAttempts: string
}

function ExamFormDialog({
  open,
  onOpenChange,
  exam,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  exam: ExamDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const subjects = useSubjects()
  const isEdit = Boolean(exam?.id)
  const [formError, setFormError] = React.useState<string | null>(null)
  const [isOnline, setIsOnline] = React.useState(true)
  const [showResults, setShowResults] = React.useState(false)

  const groups = useQuery({
    queryKey: qk.teacher.groups({ PageSize: 100 }),
    queryFn: () => teacherGroupsApi.list({ PageSize: 100 }),
    enabled: open,
  })

  const schema = React.useMemo(
    () =>
      z.object({
        title: z.string().min(3, t('validation.required')),
        description: z.string().optional().default(''),
        subjectId: z.string().min(1, t('validation.selectOne')),
        groupId: z.string().optional().default(''),
        type: z.enum(['Quiz', 'Monthly', 'Midterm', 'Final', 'Practice']),
        examDate: z.string().min(1, t('validation.required')),
        durationInMinutes: z.string().refine((v) => Number(v) > 0, t('validation.number')),
        maximumScore: z.string().refine((v) => Number(v) > 0, t('validation.number')),
        passingScore: z.string().refine((v) => Number(v) >= 0, t('validation.number')),
        maxAttempts: z.string().refine((v) => Number(v) > 0, t('validation.number')),
      }),
    [t],
  )

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setError,
    watch,
    formState: { errors },
  } = useForm<ExamValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      title: '',
      description: '',
      subjectId: '',
      groupId: '',
      type: 'Quiz',
      examDate: new Date(Date.now() + 86_400_000).toISOString().slice(0, 16),
      durationInMinutes: '60',
      maximumScore: '100',
      passingScore: '50',
      maxAttempts: '1',
    },
  })

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    if (exam) {
      reset({
        title: exam.title ?? '',
        description: exam.description ?? '',
        subjectId: exam.subjectId ?? '',
        groupId: exam.groupId ?? '',
        type: exam.type ?? 'Quiz',
        examDate: exam.examDate?.slice(0, 16) ?? '',
        durationInMinutes: String(exam.durationInMinutes ?? 60),
        maximumScore: String(exam.maximumScore ?? 100),
        passingScore: String(exam.passingScore ?? 50),
        maxAttempts: String(exam.maxAttempts ?? 1),
      })
      setIsOnline(Boolean(exam.isOnline))
      setShowResults(Boolean(exam.showResultsImmediately))
    } else {
      reset()
      setIsOnline(true)
      setShowResults(false)
    }
  }, [open, exam, reset])

  const save = useMutation({
    mutationFn: (values: ExamValues) => {
      const payload = {
        subjectId: values.subjectId,
        groupId: values.groupId || null,
        title: values.title.trim(),
        description: values.description.trim() || null,
        type: values.type,
        examDate: new Date(values.examDate).toISOString(),
        durationInMinutes: Number(values.durationInMinutes),
        maximumScore: Number(values.maximumScore),
        passingScore: Number(values.passingScore),
        isOnline,
        maxAttempts: Number(values.maxAttempts),
        showResultsImmediately: showResults,
      }
      return isEdit && exam?.id
        ? teacherExamsApi.update(exam.id, payload)
        : teacherExamsApi.create(payload)
    },
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<ExamValues>(error, setError, [
          'title',
          'subjectId',
          'groupId',
          'examDate',
          'durationInMinutes',
          'maximumScore',
          'passingScore',
          'maxAttempts',
        ]),
      ),
  })

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={
        isEdit
          ? locale === 'ar-EG'
            ? 'تعديل الامتحان'
            : 'Edit exam'
          : locale === 'ar-EG'
            ? 'امتحان جديد'
            : 'New exam'
      }
      error={formError}
      submitting={save.isPending}
      onSubmit={handleSubmit((values) => save.mutate(values))}
    >
      <Field
        label={locale === 'ar-EG' ? 'عنوان الامتحان' : 'Exam title'}
        htmlFor="exam-title"
        required
        error={errors.title?.message}
      >
        <Input id="exam-title" invalid={!!errors.title} {...register('title')} />
      </Field>

      <Field label={locale === 'ar-EG' ? 'الوصف' : 'Description'} htmlFor="exam-description">
        <Textarea id="exam-description" rows={3} {...register('description')} />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={t('common.subject')}
          htmlFor="exam-subject"
          required
          error={errors.subjectId?.message}
        >
          <Select
            value={watch('subjectId')}
            onValueChange={(v) => setValue('subjectId', v, { shouldValidate: true })}
          >
            <SelectTrigger id="exam-subject">
              <SelectValue placeholder={t('common.subject')} />
            </SelectTrigger>
            <SelectContent>
              {subjects.subjects.map((s) => (
                <SelectItem key={s.id} value={s.id!}>
                  {subjects.label(s)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field label={t('common.group')} htmlFor="exam-group">
          <Select
            value={watch('groupId') || 'none'}
            onValueChange={(v) => setValue('groupId', v === 'none' ? '' : v)}
          >
            <SelectTrigger id="exam-group">
              <SelectValue placeholder={t('common.group')} />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="none">{locale === 'ar-EG' ? 'كل الطلاب' : 'All students'}</SelectItem>
              {(groups.data?.items ?? []).map((g) => (
                <SelectItem key={g.id} value={g.id!}>
                  {g.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={locale === 'ar-EG' ? 'نوع الامتحان' : 'Exam type'} htmlFor="exam-type">
          <Select value={watch('type')} onValueChange={(v) => setValue('type', v as ExamValues['type'])}>
            <SelectTrigger id="exam-type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {EXAM_TYPES.map((type) => (
                <SelectItem key={type.value} value={type.value}>
                  {type[lang]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field
          label={locale === 'ar-EG' ? 'ميعاد الامتحان' : 'Exam date'}
          htmlFor="exam-date"
          required
          error={errors.examDate?.message}
        >
          <DateField kind="datetime-local"
 id="exam-date"
 invalid={!!errors.examDate}
 {...register('examDate')}
 />
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-4">
        <Field
          label={locale === 'ar-EG' ? 'المدة (د)' : 'Duration'}
          htmlFor="exam-duration"
          required
          error={errors.durationInMinutes?.message}
        >
          <Input
            id="exam-duration"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            {...register('durationInMinutes')}
          />
        </Field>
        <Field
          label={t('exams.maximumScore')}
          htmlFor="exam-max"
          required
          error={errors.maximumScore?.message}
        >
          <Input
            id="exam-max"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            {...register('maximumScore')}
          />
        </Field>
        <Field
          label={t('exams.passingScore')}
          htmlFor="exam-pass"
          required
          error={errors.passingScore?.message}
        >
          <Input
            id="exam-pass"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            {...register('passingScore')}
          />
        </Field>
        <Field
          label={t('exams.attempts')}
          htmlFor="exam-attempts"
          required
          error={errors.maxAttempts?.message}
        >
          <Input
            id="exam-attempts"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            {...register('maxAttempts')}
          />
        </Field>
      </div>

      <div className="space-y-3 rounded-xl border border-border bg-muted/20 p-4">
        <label className="flex items-center justify-between gap-4">
          <span>
            <span className="block font-display text-body-sm font-semibold text-foreground">
              {locale === 'ar-EG' ? 'امتحان أونلاين' : 'Online exam'}
            </span>
            <span className="block text-caption text-muted-foreground">
              {locale === 'ar-EG'
                ? 'الطالب بيحلّه على المنصة والتصحيح تلقائي.'
                : 'Students answer on the platform and grading is automatic.'}
            </span>
          </span>
          <Switch checked={isOnline} onCheckedChange={setIsOnline} />
        </label>

        <label className="flex items-center justify-between gap-4">
          <span>
            <span className="block font-display text-body-sm font-semibold text-foreground">
              {locale === 'ar-EG' ? 'إظهار النتيجة فورًا' : 'Show results immediately'}
            </span>
            <span className="block text-caption text-muted-foreground">
              {locale === 'ar-EG'
                ? 'الطالب يشوف درجته بمجرد التسليم.'
                : 'Students see their score right after submitting.'}
            </span>
          </span>
          <Switch checked={showResults} onCheckedChange={setShowResults} />
        </label>
      </div>
    </FormDialog>
  )
}
