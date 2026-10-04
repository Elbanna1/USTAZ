import * as React from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { CalendarClock, Eye, MoreHorizontal, Pencil, Plus, Trash2, Users } from 'lucide-react'
import { teacherGroupsApi, teacherHomeworkApi, type TeacherHomeworkQuery } from '@/api/endpoints/teacher'
import type { HomeworkDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { applyServerErrors } from '@/lib/form'
import { cn } from '@/lib/utils'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { FormDialog } from '@/components/common/FormDialog'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import { Switch, Progress } from '@/components/ui/primitives'
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
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 12

export default function TeacherHomeworkPage() {
  const { t, locale, formatNumber, formatDate } = useI18n()
  usePageTitle(t('homework.title'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [published, setPublished] = React.useState<boolean | undefined>()
  const [formOpen, setFormOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<HomeworkDto | null>(null)
  const [deleting, setDeleting] = React.useState<HomeworkDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query: TeacherHomeworkQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      IsPublished: published,
    }),
    [page, debounced, published],
  )

  const homework = useQuery({
    queryKey: qk.teacher.homework(query),
    queryFn: () => teacherHomeworkApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, published])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher'] })

  const publish = useMutation({
    mutationFn: (id: string) => teacherHomeworkApi.publish(id),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.published'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teacherHomeworkApi.remove(id),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = homework.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('homework.title')}
        description={
          locale === 'ar-EG'
            ? 'اعمل واجبات لمجموعاتك، وتابع التسليمات وصحّحها.'
            : 'Assign homework to your groups, then track and grade submissions.'
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
            {locale === 'ar-EG' ? 'واجب جديد' : 'New homework'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-homework-search"
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

      <div className={cn('transition-opacity', homework.isFetching && 'opacity-60')}>
        {homework.isPending ? (
          <RowSkeleton count={5} />
        ) : homework.isError ? (
          <QueryError error={homework.error} onRetry={() => homework.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="homework"
            title={locale === 'ar-EG' ? 'لسه مفيش واجبات' : 'No homework yet'}
            description={
              locale === 'ar-EG'
                ? 'الواجب بيظهر للطلاب بعد ما تنشره.'
                : 'Homework becomes visible to students once you publish it.'
            }
            action={{
              label: locale === 'ar-EG' ? 'أنشئ واجب' : 'Create homework',
              onClick: () => {
                setEditing(null)
                setFormOpen(true)
              },
            }}
          />
        ) : (
          <RevealGroup className="space-y-3">
            {items.map((item) => {
              const gradedRate = item.submissionsCount
                ? Math.round(((item.gradedCount ?? 0) / item.submissionsCount) * 100)
                : 0

              return (
                <RevealItem key={item.id}>
                  <Card className="p-5">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                      <div className="min-w-0 flex-1">
                        <Link
                          to={`/teacher/homework/${item.id}`}
                          className="font-display text-body font-bold text-foreground hover:text-primary"
                        >
                          {item.title}
                        </Link>
                        <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-caption text-muted-foreground">
                          {item.groupName && <span>{item.groupName}</span>}
                          {item.dueAt && (
                            <span className="tnum inline-flex items-center gap-1.5">
                              <CalendarClock className="size-3.5" />
                              {t('homework.dueAt')} {formatDate(item.dueAt, 'd MMM yyyy')}
                            </span>
                          )}
                          <span className="tnum inline-flex items-center gap-1.5">
                            <Users className="size-3.5" />
                            {formatNumber(item.submissionsCount ?? 0)} {t('homework.submissions')}
                          </span>
                        </div>
                      </div>

                      <div className="flex items-center gap-2">
                        <Badge variant={item.isPublished ? 'success' : 'outline'} size="sm">
                          {item.isPublished ? t('common.published') : t('common.draft')}
                        </Badge>

                        <DropdownMenu>
                          <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon-sm" aria-label={t('common.actions')}>
                              <MoreHorizontal className="size-4" />
                            </Button>
                          </DropdownMenuTrigger>
                          <DropdownMenuContent align="end">
                            <DropdownMenuItem
                              onSelect={() => {
                                setEditing(item)
                                setFormOpen(true)
                              }}
                            >
                              <Pencil className="size-4" />
                              {t('common.edit')}
                            </DropdownMenuItem>
                            {!item.isPublished && (
                              <DropdownMenuItem onSelect={() => item.id && publish.mutate(item.id)}>
                                <Eye className="size-4" />
                                {locale === 'ar-EG' ? 'نشر' : 'Publish'}
                              </DropdownMenuItem>
                            )}
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                              className="text-destructive-strong"
                              onSelect={() => setDeleting(item)}
                            >
                              <Trash2 className="size-4" />
                              {t('common.delete')}
                            </DropdownMenuItem>
                          </DropdownMenuContent>
                        </DropdownMenu>
                      </div>
                    </div>

                    {(item.submissionsCount ?? 0) > 0 && (
                      <div className="mt-4 flex items-center gap-3">
                        <Progress value={gradedRate} className="h-1.5 flex-1" />
                        <span className="tnum shrink-0 text-caption text-muted-foreground">
                          {formatNumber(item.gradedCount ?? 0)}/
                          {formatNumber(item.submissionsCount ?? 0)} {t('homework.graded')}
                        </span>
                        <Button asChild size="sm" variant="outline">
                          <Link to={`/teacher/homework/${item.id}`}>{t('homework.grade')}</Link>
                        </Button>
                      </div>
                    )}
                  </Card>
                </RevealItem>
              )
            })}
          </RevealGroup>
        )}
      </div>

      {homework.data && items.length > 0 && (
        <Pagination page={homework.data} onChange={setPage} className="mt-8" />
      )}

      <HomeworkFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        homework={editing}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف الواجب؟' : 'Delete this homework?'}
        description={deleting?.title ?? ''}
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */

interface HomeworkValues {
  title: string
  description: string
  instructions: string
  groupId: string
  dueAt: string
  maximumScore: string
}

function HomeworkFormDialog({
  open,
  onOpenChange,
  homework,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  homework: HomeworkDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const isEdit = Boolean(homework?.id)
  const [formError, setFormError] = React.useState<string | null>(null)
  const [allowLate, setAllowLate] = React.useState(true)

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
        instructions: z.string().optional().default(''),
        groupId: z.string().min(1, t('validation.selectOne')),
        dueAt: z.string().min(1, t('validation.required')),
        maximumScore: z.string().refine((v) => Number(v) > 0, t('validation.number')),
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
  } = useForm<HomeworkValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      title: '',
      description: '',
      instructions: '',
      groupId: '',
      dueAt: new Date(Date.now() + 7 * 86_400_000).toISOString().slice(0, 16),
      maximumScore: '10',
    },
  })

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    if (homework) {
      reset({
        title: homework.title ?? '',
        description: homework.description ?? '',
        instructions: homework.instructions ?? '',
        groupId: homework.groupId ?? '',
        dueAt: homework.dueAt?.slice(0, 16) ?? '',
        maximumScore: String(homework.maximumScore ?? 10),
      })
      setAllowLate(Boolean(homework.allowLateSubmission))
    } else {
      reset()
      setAllowLate(true)
    }
  }, [open, homework, reset])

  const save = useMutation({
    mutationFn: (values: HomeworkValues) => {
      const payload = {
        groupId: values.groupId,
        title: values.title.trim(),
        description: values.description.trim() || null,
        instructions: values.instructions.trim() || null,
        dueAt: new Date(values.dueAt).toISOString(),
        maximumScore: Number(values.maximumScore),
        allowLateSubmission: allowLate,
      }
      return isEdit && homework?.id
        ? teacherHomeworkApi.update(homework.id, payload)
        : teacherHomeworkApi.create(payload)
    },
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<HomeworkValues>(error, setError, [
          'title',
          'groupId',
          'dueAt',
          'maximumScore',
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
            ? 'تعديل الواجب'
            : 'Edit homework'
          : locale === 'ar-EG'
            ? 'واجب جديد'
            : 'New homework'
      }
      error={formError}
      submitting={save.isPending}
      onSubmit={handleSubmit((values) => save.mutate(values))}
    >
      <Field
        label={locale === 'ar-EG' ? 'عنوان الواجب' : 'Homework title'}
        htmlFor="hw-title"
        required
        error={errors.title?.message}
      >
        <Input id="hw-title" invalid={!!errors.title} {...register('title')} />
      </Field>

      <Field label={t('common.group')} htmlFor="hw-group" required error={errors.groupId?.message}>
        <Select
          value={watch('groupId')}
          onValueChange={(v) => setValue('groupId', v, { shouldValidate: true })}
        >
          <SelectTrigger id="hw-group">
            <SelectValue placeholder={t('common.group')} />
          </SelectTrigger>
          <SelectContent>
            {(groups.data?.items ?? []).map((g) => (
              <SelectItem key={g.id} value={g.id!}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field label={locale === 'ar-EG' ? 'الوصف' : 'Description'} htmlFor="hw-description">
        <Textarea id="hw-description" rows={3} {...register('description')} />
      </Field>

      <Field label={t('homework.instructions')} htmlFor="hw-instructions">
        <Textarea id="hw-instructions" rows={4} {...register('instructions')} />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={t('homework.dueAt')}
          htmlFor="hw-due"
          required
          error={errors.dueAt?.message}
        >
          <DateField kind="datetime-local"
 id="hw-due"
 invalid={!!errors.dueAt}
 {...register('dueAt')}
 />
        </Field>
        <Field
          label={t('exams.maximumScore')}
          htmlFor="hw-score"
          required
          error={errors.maximumScore?.message}
        >
          <Input
            id="hw-score"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            {...register('maximumScore')}
          />
        </Field>
      </div>

      <label className="flex items-center justify-between gap-4 rounded-xl border border-border bg-muted/20 p-4">
        <span>
          <span className="block font-display text-body-sm font-semibold text-foreground">
            {t('homework.allowLate')}
          </span>
          <span className="block text-caption text-muted-foreground">
            {locale === 'ar-EG'
              ? 'الطالب يقدر يسلّم بعد الميعاد وهيتسجّل كمتأخر.'
              : 'Students can still submit after the deadline, marked as late.'}
          </span>
        </span>
        <Switch checked={allowLate} onCheckedChange={setAllowLate} />
      </label>
    </FormDialog>
  )
}
