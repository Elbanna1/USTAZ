import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Eye, Megaphone, MoreHorizontal, Pencil, Pin, Plus, Trash2 } from 'lucide-react'
import { teacherAnnouncementsApi, teacherGroupsApi } from '@/api/endpoints/teacher'
import type { AnnouncementDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
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
import { useApiErrorToast } from '@/hooks/use-api-toast'

const AUDIENCES = [
  { value: 'TenantWide', ar: 'الكل', en: 'Everyone' },
  { value: 'Group', ar: 'مجموعة محددة', en: 'A specific group' },
  { value: 'Students', ar: 'الطلاب', en: 'Students' },
  { value: 'Parents', ar: 'أولياء الأمور', en: 'Parents' },
] as const

const PAGE_SIZE = 12

export default function TeacherAnnouncementsPage() {
  const { t, locale, formatDateTime } = useI18n()
  usePageTitle(t('dashboard.announcements'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [published, setPublished] = React.useState<boolean | undefined>()
  const [formOpen, setFormOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<AnnouncementDto | null>(null)
  const [deleting, setDeleting] = React.useState<AnnouncementDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      IsPublished: published,
    }),
    [page, debounced, published],
  )

  const announcements = useQuery({
    queryKey: qk.teacher.announcements(query),
    queryFn: () => teacherAnnouncementsApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, published])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher', 'announcements'] })

  const publish = useMutation({
    mutationFn: (id: string) => teacherAnnouncementsApi.publish(id),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.published'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teacherAnnouncementsApi.remove(id),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = announcements.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('dashboard.announcements')}
        description={
          locale === 'ar-EG'
            ? 'ابعت إعلان لكل طلابك أو لمجموعة واحدة.'
            : 'Post an announcement to all your students or to one group.'
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
            {locale === 'ar-EG' ? 'إعلان جديد' : 'New announcement'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-announcements-search"
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

      <div className={cn('transition-opacity', announcements.isFetching && 'opacity-60')}>
        {announcements.isPending ? (
          <RowSkeleton count={4} />
        ) : announcements.isError ? (
          <QueryError error={announcements.error} onRetry={() => announcements.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="inbox"
            title={locale === 'ar-EG' ? 'مفيش إعلانات' : 'No announcements'}
            description={
              locale === 'ar-EG'
                ? 'الإعلان بيوصل لطلابك في الإشعارات وفي لوحتهم.'
                : 'Announcements reach students through notifications and their dashboard.'
            }
            action={{
              label: locale === 'ar-EG' ? 'إعلان جديد' : 'New announcement',
              onClick: () => {
                setEditing(null)
                setFormOpen(true)
              },
            }}
          />
        ) : (
          <div className="space-y-3">
            {items.map((item, i) => (
              <motion.div
                key={item.id}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ ...transitions.fast, delay: Math.min(i * 0.04, 0.3) }}
              >
                <Card className={cn('p-5', item.isPinned && 'border-accent/35 bg-accent/5')}>
                  <div className="flex items-start gap-4">
                    <span
                      className={cn(
                        'grid size-10 shrink-0 place-items-center rounded-xl',
                        item.isPinned
                          ? 'bg-accent/15 text-accent-strong'
                          : 'bg-primary/10 text-primary',
                      )}
                    >
                      {item.isPinned ? <Pin className="size-5" /> : <Megaphone className="size-5" />}
                    </span>

                    <div className="min-w-0 flex-1">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="font-display text-body font-bold text-foreground">
                          {item.title}
                        </p>
                        <Badge variant={item.isPublished ? 'success' : 'outline'} size="sm">
                          {item.isPublished ? t('common.published') : t('common.draft')}
                        </Badge>
                        {item.audienceName && (
                          <Badge variant="outline" size="sm">
                            {item.groupName ?? item.audienceName}
                          </Badge>
                        )}
                      </div>

                      <p className="mt-2 whitespace-pre-wrap text-body-sm leading-relaxed text-muted-foreground">
                        {item.body}
                      </p>

                      {item.publishedAt && (
                        <p className="tnum mt-2 text-caption text-muted-foreground/70">
                          {formatDateTime(item.publishedAt)}
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
                </Card>
              </motion.div>
            ))}
          </div>
        )}
      </div>

      {announcements.data && items.length > 0 && (
        <Pagination page={announcements.data} onChange={setPage} className="mt-8" />
      )}

      <AnnouncementFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        announcement={editing}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف الإعلان؟' : 'Delete this announcement?'}
        description={deleting?.title ?? ''}
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}

function AnnouncementFormDialog({
  open,
  onOpenChange,
  announcement,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  announcement: AnnouncementDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const isEdit = Boolean(announcement?.id)

  const [title, setTitle] = React.useState('')
  const [body, setBody] = React.useState('')
  const [audience, setAudience] = React.useState<(typeof AUDIENCES)[number]['value']>('TenantWide')
  const [groupId, setGroupId] = React.useState('')
  const [isPinned, setIsPinned] = React.useState(false)
  const [expiresAt, setExpiresAt] = React.useState('')
  const [formError, setFormError] = React.useState<string | null>(null)

  const groups = useQuery({
    queryKey: qk.teacher.groups({ PageSize: 100 }),
    queryFn: () => teacherGroupsApi.list({ PageSize: 100 }),
    enabled: open,
  })

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    setTitle(announcement?.title ?? '')
    setBody(announcement?.body ?? '')
    setAudience(announcement?.audience ?? 'TenantWide')
    setGroupId(announcement?.groupId ?? '')
    setIsPinned(Boolean(announcement?.isPinned))
    setExpiresAt(announcement?.expiresAt?.slice(0, 10) ?? '')
  }, [open, announcement])

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        title: title.trim(),
        body: body.trim(),
        audience,
        groupId: audience === 'Group' ? groupId || null : null,
        isPinned,
        expiresAt: expiresAt ? new Date(expiresAt).toISOString() : null,
      }
      return isEdit && announcement?.id
        ? teacherAnnouncementsApi.update(announcement.id, payload)
        : teacherAnnouncementsApi.create(payload)
    },
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={
        isEdit
          ? locale === 'ar-EG'
            ? 'تعديل الإعلان'
            : 'Edit announcement'
          : locale === 'ar-EG'
            ? 'إعلان جديد'
            : 'New announcement'
      }
      error={formError}
      submitting={save.isPending}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        if (title.trim() && body.trim()) save.mutate()
      }}
    >
      <Field label={locale === 'ar-EG' ? 'العنوان' : 'Title'} htmlFor="ann-title" required>
        <Input id="ann-title" value={title} onChange={(e) => setTitle(e.target.value)} />
      </Field>

      <Field label={locale === 'ar-EG' ? 'النص' : 'Body'} htmlFor="ann-body" required>
        <Textarea id="ann-body" rows={5} value={body} onChange={(e) => setBody(e.target.value)} />
      </Field>

      <Field label={locale === 'ar-EG' ? 'الجمهور' : 'Audience'} htmlFor="ann-audience">
        <Select
          value={audience}
          onValueChange={(v) => setAudience(v as (typeof AUDIENCES)[number]['value'])}
        >
          <SelectTrigger id="ann-audience">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {AUDIENCES.map((a) => (
              <SelectItem key={a.value} value={a.value}>
                {a[lang]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      {audience === 'Group' && (
        <Field label={t('common.group')} htmlFor="ann-group" required>
          <Select value={groupId} onValueChange={setGroupId}>
            <SelectTrigger id="ann-group">
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
      )}

      <Field
        label={locale === 'ar-EG' ? 'ينتهي في' : 'Expires on'}
        htmlFor="ann-expires"
        hint={t('common.optional')}
      >
        <DateField
          id="ann-expires"
          value={expiresAt}
          onChange={(e) => setExpiresAt(e.target.value)}
        />
      </Field>

      <label className="flex items-center justify-between gap-4 rounded-xl border border-border bg-muted/20 p-4">
        <span>
          <span className="block font-display text-body-sm font-semibold text-foreground">
            {locale === 'ar-EG' ? 'تثبيت الإعلان' : 'Pin announcement'}
          </span>
          <span className="block text-caption text-muted-foreground">
            {locale === 'ar-EG' ? 'هيفضل فوق القائمة.' : 'Keeps it at the top of the list.'}
          </span>
        </span>
        <Switch checked={isPinned} onCheckedChange={setIsPinned} />
      </label>
    </FormDialog>
  )
}
