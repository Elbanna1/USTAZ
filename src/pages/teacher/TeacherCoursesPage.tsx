import * as React from 'react'
import { Link } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Eye, EyeOff, MoreHorizontal, Pencil, Plus, Trash2, Users } from 'lucide-react'
import { teacherCoursesApi, type TeacherCourseQuery } from '@/api/endpoints/teacher'
import type { CourseListItemDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useGrades, useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { applyServerErrors } from '@/lib/form'
import { cn } from '@/lib/utils'
import { pricingToBreakdown } from '@/lib/pricing'
import { coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
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
import { Field } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { CoverImage } from '@/components/illustrations/CoverArt'
import { PriceTag } from '@/components/common/pricing-bits'
import { Rating } from '@/components/common/Rating'
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

export default function TeacherCoursesPage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('dashboard.myCourses'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [published, setPublished] = React.useState<boolean | undefined>()
  const [formOpen, setFormOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<CourseListItemDto | null>(null)
  const [deleting, setDeleting] = React.useState<CourseListItemDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query: TeacherCourseQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      IsPublished: published,
    }),
    [page, debounced, published],
  )

  const courses = useQuery({
    queryKey: qk.teacher.courses(query),
    queryFn: () => teacherCoursesApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, published])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher'] })

  const publish = useMutation({
    mutationFn: ({ id, next }: { id: string; next: boolean }) =>
      next ? teacherCoursesApi.publish(id) : teacherCoursesApi.unpublish(id),
    onSuccess: (_r, { next }) => {
      invalidate()
      toast({ title: next ? t('common.published') : t('common.draft'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teacherCoursesApi.remove(id),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = courses.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('dashboard.myCourses')}
        description={
          locale === 'ar-EG'
            ? 'كورسات مسجّلة بيشتريها الطالب مرة وتفضل معاه.'
            : 'Recorded courses students buy once and keep forever.'
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
            {locale === 'ar-EG' ? 'كورس جديد' : 'New course'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-courses-search"
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

      <div className={cn('transition-opacity', courses.isFetching && 'opacity-60')}>
        {courses.isPending ? (
          <CardGridSkeleton count={6} />
        ) : courses.isError ? (
          <QueryError error={courses.error} onRetry={() => courses.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="courses"
            title={locale === 'ar-EG' ? 'لسه مفيش كورسات' : 'No courses yet'}
            description={
              locale === 'ar-EG'
                ? 'ابدأ كورس، ضيف أقسامه ودروسه، وانشره للطلاب.'
                : 'Create a course, add its sections and lessons, then publish it.'
            }
            action={{
              label: locale === 'ar-EG' ? 'أنشئ كورس' : 'Create a course',
              onClick: () => {
                setEditing(null)
                setFormOpen(true)
              },
            }}
          />
        ) : (
          <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
            {items.map((course) => (
              <RevealItem key={course.id} className="h-full">
                <Card className="flex h-full flex-col overflow-hidden">
                  <div className="relative aspect-[16/9] overflow-hidden">
                    <CoverImage
                      src={imageUrl(course.thumbnailUrl)}
                      pattern={coverPatternFor(course.id ?? '')}
                      subject={subjectKeyFor(course.subject)}
                      intensity="bold"
                    />
                    <div className="absolute inset-x-4 top-4 flex items-start justify-between gap-2">
                      <Badge variant={course.isPublished ? 'success' : 'outline'} size="sm">
                        {course.isPublished ? t('common.published') : t('common.draft')}
                      </Badge>
                      <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                          <button
                            type="button"
                            aria-label={t('common.actions')}
                            className="grid size-8 place-items-center rounded-full border border-white/25 bg-black/30 text-white backdrop-blur-sm transition-colors hover:bg-black/50"
                          >
                            <MoreHorizontal className="size-4" />
                          </button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end">
                          <DropdownMenuItem
                            onSelect={() => {
                              setEditing(course)
                              setFormOpen(true)
                            }}
                          >
                            <Pencil className="size-4" />
                            {t('common.edit')}
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onSelect={() =>
                              course.id &&
                              publish.mutate({ id: course.id, next: !course.isPublished })
                            }
                          >
                            {course.isPublished ? (
                              <EyeOff className="size-4" />
                            ) : (
                              <Eye className="size-4" />
                            )}
                            {course.isPublished
                              ? locale === 'ar-EG'
                                ? 'إلغاء النشر'
                                : 'Unpublish'
                              : locale === 'ar-EG'
                                ? 'نشر'
                                : 'Publish'}
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                          <DropdownMenuItem
                            className="text-destructive-strong"
                            onSelect={() => setDeleting(course)}
                          >
                            <Trash2 className="size-4" />
                            {t('common.delete')}
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>
                  </div>

                  <div className="flex flex-1 flex-col gap-3 p-5">
                    <Link
                      to={`/teacher/courses/${course.id}`}
                      className="line-clamp-2-rtl font-display text-body font-bold leading-snug text-foreground hover:text-primary"
                    >
                      {course.title}
                    </Link>

                    <div className="flex flex-wrap items-center gap-x-4 gap-y-1.5 text-caption text-muted-foreground">
                      <Rating value={course.rating ?? 0} count={course.reviewCount} size="sm" />
                      <span className="inline-flex items-center gap-1.5">
                        <Users className="size-3.5" />
                        <span className="tnum">{formatNumber(course.studentCount ?? 0)}</span>
                      </span>
                      <span className="tnum">
                        {formatNumber(course.lessonsCount ?? 0)}{' '}
                        {locale === 'ar-EG' ? 'درس' : 'lessons'}
                      </span>
                    </div>

                    <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-4">
                      <PriceTag
                        breakdown={pricingToBreakdown(course.pricing)}
                        size="sm"
                        currency={course.pricing?.currency}
                      />
                      <Button asChild size="sm" variant="outline">
                        <Link to={`/teacher/courses/${course.id}`}>
                          {locale === 'ar-EG' ? 'المحتوى' : 'Content'}
                        </Link>
                      </Button>
                    </div>
                  </div>
                </Card>
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>

      {courses.data && items.length > 0 && (
        <Pagination page={courses.data} onChange={setPage} className="mt-8" />
      )}

      <CourseFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        course={editing}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف الكورس؟' : 'Delete this course?'}
        description={
          locale === 'ar-EG'
            ? `هتحذف «${deleting?.title}» وكل دروسه.`
            : `This deletes "${deleting?.title}" and all of its lessons.`
        }
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                Course form                                 */
/* -------------------------------------------------------------------------- */

interface CourseValues {
  title: string
  description: string
  subjectId: string
  gradeId: string
  price: string
  originalPrice: string
  thumbnailUrl: string
}

function CourseFormDialog({
  open,
  onOpenChange,
  course,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  course: CourseListItemDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const subjects = useSubjects()
  const grades = useGrades()
  const isEdit = Boolean(course?.id)
  const [formError, setFormError] = React.useState<string | null>(null)

  const schema = React.useMemo(
    () =>
      z.object({
        title: z.string().min(3, t('validation.required')),
        description: z.string().optional().default(''),
        subjectId: z.string().min(1, t('validation.selectOne')),
        gradeId: z.string().min(1, t('validation.selectOne')),
        price: z.string().refine((v) => Number(v) >= 0, t('validation.number')),
        originalPrice: z.string().optional().default(''),
        thumbnailUrl: z.string().optional().default(''),
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
  } = useForm<CourseValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      title: '',
      description: '',
      subjectId: '',
      gradeId: '',
      price: '0',
      originalPrice: '',
      thumbnailUrl: '',
    },
  })

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    reset(
      course
        ? {
            title: course.title ?? '',
            description: course.description ?? '',
            subjectId: course.subject?.id ?? '',
            gradeId: course.grade?.id ?? '',
            price: String(course.pricing?.price ?? 0),
            originalPrice: course.pricing?.originalPrice
              ? String(course.pricing.originalPrice)
              : '',
            thumbnailUrl: course.thumbnailUrl ?? '',
          }
        : undefined,
    )
  }, [open, course, reset])

  const save = useMutation({
    mutationFn: (values: CourseValues) => {
      const payload = {
        subjectId: values.subjectId,
        gradeId: values.gradeId,
        title: values.title.trim(),
        description: values.description.trim() || null,
        thumbnailUrl: values.thumbnailUrl.trim() || null,
        price: Number(values.price) || 0,
        originalPrice: values.originalPrice ? Number(values.originalPrice) : null,
      }
      return isEdit && course?.id
        ? teacherCoursesApi.update(course.id, payload)
        : teacherCoursesApi.create(payload)
    },
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<CourseValues>(error, setError, [
          'title',
          'subjectId',
          'gradeId',
          'price',
          'thumbnailUrl',
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
            ? 'تعديل الكورس'
            : 'Edit course'
          : locale === 'ar-EG'
            ? 'كورس جديد'
            : 'New course'
      }
      description={
        locale === 'ar-EG'
          ? 'بعد الحفظ تقدر تضيف أقسام ودروس من صفحة الكورس.'
          : 'After saving you can add sections and lessons from the course page.'
      }
      error={formError}
      submitting={save.isPending}
      onSubmit={handleSubmit((values) => save.mutate(values))}
    >
      <Field
        label={locale === 'ar-EG' ? 'عنوان الكورس' : 'Course title'}
        htmlFor="course-title"
        required
        error={errors.title?.message}
      >
        <Input id="course-title" invalid={!!errors.title} {...register('title')} />
      </Field>

      <Field label={locale === 'ar-EG' ? 'الوصف' : 'Description'} htmlFor="course-description">
        <Textarea id="course-description" rows={4} {...register('description')} />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={t('common.subject')}
          htmlFor="course-subject"
          required
          error={errors.subjectId?.message}
        >
          <Select
            value={watch('subjectId')}
            onValueChange={(v) => setValue('subjectId', v, { shouldValidate: true })}
          >
            <SelectTrigger id="course-subject">
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

        <Field
          label={t('common.grade')}
          htmlFor="course-grade"
          required
          error={errors.gradeId?.message}
        >
          <Select
            value={watch('gradeId')}
            onValueChange={(v) => setValue('gradeId', v, { shouldValidate: true })}
          >
            <SelectTrigger id="course-grade">
              <SelectValue placeholder={t('common.grade')} />
            </SelectTrigger>
            <SelectContent>
              {grades.grades.map((g) => (
                <SelectItem key={g.id} value={g.id!}>
                  {grades.label(g)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={t('common.price')}
          htmlFor="course-price"
          required
          error={errors.price?.message}
        >
          <Input
            id="course-price"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            invalid={!!errors.price}
            {...register('price')}
          />
        </Field>
        <Field
          label={locale === 'ar-EG' ? 'السعر قبل الخصم' : 'Original price'}
          htmlFor="course-original"
        >
          <Input
            id="course-original"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            {...register('originalPrice')}
          />
        </Field>
      </div>

      <Field
        label={locale === 'ar-EG' ? 'رابط صورة الغلاف' : 'Cover image URL'}
        htmlFor="course-thumb"
        hint={
          locale === 'ar-EG'
            ? 'سيبه فاضي وهنرسم غلاف تلقائي.'
            : 'Leave empty and we draw a cover for you.'
        }
      >
        <Input
          id="course-thumb"
          type="url"
          dir="ltr"
          className="text-start"
          {...register('thumbnailUrl')}
        />
      </Field>
    </FormDialog>
  )
}
