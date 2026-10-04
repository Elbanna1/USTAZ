import * as React from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  CalendarDays,
  Eye,
  EyeOff,
  MoreHorizontal,
  Pencil,
  Plus,
  Trash2,
  Users,
} from 'lucide-react'
import { teacherGroupsApi, type TeacherGroupQuery } from '@/api/endpoints/teacher'
import type { GroupListItemDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { pricingToBreakdown } from '@/lib/pricing'
import { formatScheduleDays } from '@/lib/schedule'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { CardGridSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { PriceTag } from '@/components/common/pricing-bits'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import { GroupFormDialog } from '@/components/teacher/GroupFormDialog'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 12

export default function TeacherGroupsPage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('dashboard.myGroups'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()
  const [searchParams, setSearchParams] = useSearchParams()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [published, setPublished] = React.useState<boolean | undefined>()
  const [editing, setEditing] = React.useState<GroupListItemDto | null>(null)
  const [formOpen, setFormOpen] = React.useState(searchParams.get('new') === '1')
  const [deleting, setDeleting] = React.useState<GroupListItemDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query: TeacherGroupQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      IsPublished: published,
    }),
    [page, debounced, published],
  )

  const groups = useQuery({
    queryKey: qk.teacher.groups(query),
    queryFn: () => teacherGroupsApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, published])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher'] })

  const publish = useMutation({
    mutationFn: ({ id, next }: { id: string; next: boolean }) =>
      next ? teacherGroupsApi.publish(id) : teacherGroupsApi.unpublish(id),
    onSuccess: (_r, { next }) => {
      invalidate()
      toast({ title: next ? t('common.published') : t('common.draft'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teacherGroupsApi.remove(id),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  function openCreate() {
    setEditing(null)
    setFormOpen(true)
  }

  function closeForm(open: boolean) {
    setFormOpen(open)
    if (!open && searchParams.get('new')) setSearchParams({}, { replace: true })
  }

  const items = groups.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('dashboard.myGroups')}
        description={
          locale === 'ar-EG'
            ? 'أنشئ مجموعاتك، حدّد المواعيد والسعر والسعة، وانشرها لطلابك.'
            : 'Create groups, set the schedule, price and capacity, then publish them.'
        }
        actions={
          <Button size="sm" onClick={openCreate}>
            <Plus className="size-4" />
            {locale === 'ar-EG' ? 'مجموعة جديدة' : 'New group'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-groups-search"
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

      <div className={cn('transition-opacity', groups.isFetching && 'opacity-60')}>
        {groups.isPending ? (
          <CardGridSkeleton count={6} />
        ) : groups.isError ? (
          <QueryError error={groups.error} onRetry={() => groups.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="groups"
            title={locale === 'ar-EG' ? 'لسه مفيش مجموعات' : 'No groups yet'}
            description={
              locale === 'ar-EG'
                ? 'المجموعة هي حصصك الأسبوعية بمواعيد ثابتة وسعر شهري.'
                : 'A group is your weekly live sessions with a fixed schedule and monthly price.'
            }
            action={{ label: locale === 'ar-EG' ? 'أنشئ مجموعة' : 'Create a group', onClick: openCreate }}
          />
        ) : (
          <RevealGroup className="grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
            {items.map((group) => {
              const price = pricingToBreakdown(group.pricing)
              const fillRate = group.capacity
                ? Math.round(((group.enrolledStudentsCount ?? 0) / group.capacity) * 100)
                : 0

              return (
                <RevealItem key={group.id} className="h-full">
                  <Card className="flex h-full flex-col p-5">
                    <div className="flex items-start justify-between gap-3">
                      <div className="min-w-0">
                        <Link
                          to={`/teacher/groups/${group.id}`}
                          className="line-clamp-2-rtl font-display text-body font-bold leading-snug text-foreground hover:text-primary"
                        >
                          {group.name}
                        </Link>
                        <p className="mt-1 truncate text-caption text-muted-foreground">
                          {[group.subject?.nameAr, group.grade?.nameAr].filter(Boolean).join(' · ')}
                        </p>
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
                              setEditing(group)
                              setFormOpen(true)
                            }}
                          >
                            <Pencil className="size-4" />
                            {t('common.edit')}
                          </DropdownMenuItem>
                          <DropdownMenuItem
                            onSelect={() =>
                              group.id &&
                              publish.mutate({ id: group.id, next: !group.isPublished })
                            }
                          >
                            {group.isPublished ? (
                              <EyeOff className="size-4" />
                            ) : (
                              <Eye className="size-4" />
                            )}
                            {group.isPublished
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
                            onSelect={() => setDeleting(group)}
                          >
                            <Trash2 className="size-4" />
                            {t('common.delete')}
                          </DropdownMenuItem>
                        </DropdownMenuContent>
                      </DropdownMenu>
                    </div>

                    <div className="mt-3 flex flex-wrap gap-1.5">
                      <Badge variant={group.isPublished ? 'success' : 'outline'} size="sm">
                        {group.isPublished ? t('common.published') : t('common.draft')}
                      </Badge>
                      {group.typeName && (
                        <Badge variant="outline" size="sm">
                          {group.typeName}
                        </Badge>
                      )}
                      {group.teachingModeName && (
                        <Badge variant="outline" size="sm">
                          {group.teachingModeName}
                        </Badge>
                      )}
                    </div>

                    <ul className="mt-4 space-y-2 text-body-sm text-muted-foreground">
                      <li className="flex items-center gap-2">
                        <CalendarDays className="size-4 shrink-0 text-primary" />
                        {formatScheduleDays(group.schedules, locale) ||
                          (locale === 'ar-EG' ? 'مفيش مواعيد' : 'No schedule yet')}
                      </li>
                      <li className="flex items-center gap-2">
                        <Users className="size-4 shrink-0 text-primary" />
                        <span className="tnum">
                          {formatNumber(group.enrolledStudentsCount ?? 0)} /{' '}
                          {formatNumber(group.capacity ?? 0)}
                        </span>
                      </li>
                    </ul>

                    <Progress value={fillRate} className="mt-3 h-1.5" />

                    <div className="mt-auto flex items-end justify-between gap-3 border-t border-border pt-4">
                      <PriceTag
                        breakdown={price}
                        period={locale === 'ar-EG' ? '/ شهريًا' : '/ month'}
                        size="sm"
                        currency={group.pricing?.currency}
                      />
                      <Button asChild size="sm" variant="outline">
                        <Link to={`/teacher/groups/${group.id}`}>{t('common.details')}</Link>
                      </Button>
                    </div>
                  </Card>
                </RevealItem>
              )
            })}
          </RevealGroup>
        )}
      </div>

      {groups.data && items.length > 0 && (
        <Pagination page={groups.data} onChange={setPage} className="mt-8" />
      )}

      <GroupFormDialog
        open={formOpen}
        onOpenChange={closeForm}
        group={editing}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف المجموعة؟' : 'Delete this group?'}
        description={
          locale === 'ar-EG'
            ? `هتحذف «${deleting?.name}». الطلاب المشتركين مش هيقدروا يوصلوا لها بعد كده.`
            : `This deletes "${deleting?.name}". Enrolled students will lose access.`
        }
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}
