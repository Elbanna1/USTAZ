import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import {
  Check,
  KeyRound,

  MoreVertical,
  Power,
  ShieldCheck,
  Trash2,
  UserPlus,
} from 'lucide-react'
import { assistantsApi, type MemberQuery } from '@/api/endpoints/tenant'
import type { TenantMemberDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { StatusBadge } from '@/components/common/StatusBadge'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { FormDialog } from '@/components/common/FormDialog'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
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
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const ROLES = [
  { value: 'Assistant', ar: 'مساعد', en: 'Assistant' },
  { value: 'Moderator', ar: 'مشرف', en: 'Moderator' },
  { value: 'ContentManager', ar: 'مسؤول محتوى', en: 'Content manager' },
  { value: 'Admin', ar: 'أدمن', en: 'Admin' },
] as const

/** Arabic labels for the permission groups the catalog returns. */
const GROUP_LABELS: Record<string, { ar: string; en: string }> = {
  Analytics: { ar: 'التحليلات', en: 'Analytics' },
  Announcements: { ar: 'الإعلانات', en: 'Announcements' },
  Attendance: { ar: 'الحضور', en: 'Attendance' },
  Courses: { ar: 'الكورسات', en: 'Courses' },
  Exams: { ar: 'الامتحانات', en: 'Exams' },
  Groups: { ar: 'المجموعات', en: 'Groups' },
  Homework: { ar: 'الواجبات', en: 'Homework' },
  Messages: { ar: 'الرسائل', en: 'Messages' },
  Payments: { ar: 'المدفوعات', en: 'Payments' },
  Schedule: { ar: 'الجدول', en: 'Schedule' },
  Students: { ar: 'الطلاب', en: 'Students' },
  Settings: { ar: 'الإعدادات', en: 'Settings' },
  Team: { ar: 'الفريق', en: 'Team' },
}

/** `Groups.Manage` → "إدارة" / "Manage". */
function actionLabel(permission: string, lang: 'ar' | 'en'): string {
  const action = permission.split('.').pop() ?? permission
  const map: Record<string, { ar: string; en: string }> = {
    View: { ar: 'عرض', en: 'View' },
    Manage: { ar: 'إدارة', en: 'Manage' },
    Create: { ar: 'إنشاء', en: 'Create' },
    Edit: { ar: 'تعديل', en: 'Edit' },
    Delete: { ar: 'حذف', en: 'Delete' },
    Grade: { ar: 'تصحيح', en: 'Grade' },
    Record: { ar: 'تسجيل', en: 'Record' },
    Publish: { ar: 'نشر', en: 'Publish' },
  }
  return map[action]?.[lang] ?? action
}

const PAGE_SIZE = 20

export default function TeacherAssistantsPage() {
  const { t, locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  usePageTitle(t('assistants.title'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [inviteOpen, setInviteOpen] = React.useState(false)
  const [permissionsFor, setPermissionsFor] = React.useState<TenantMemberDto | null>(null)
  const [removing, setRemoving] = React.useState<TenantMemberDto | null>(null)

  const query: MemberQuery = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, AssistantsOnly: true }),
    [page],
  )

  const assistants = useQuery({
    queryKey: qk.tenant.assistants(query),
    queryFn: () => assistantsApi.list(query),
    placeholderData: keepPreviousData,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['tenant'] })

  const setStatus = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) =>
      assistantsApi.setStatus(id, active),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => assistantsApi.remove(id),
    onSuccess: () => {
      invalidate()
      setRemoving(null)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const items = assistants.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('assistants.title')}
        description={
          locale === 'ar-EG'
            ? 'ادعُ مساعدين يشاركوك الشغل، وحدّد بالظبط كل واحد يقدر يعمل إيه.'
            : 'Invite assistants to share the workload and control exactly what each one can do.'
        }
        actions={
          <Button size="sm" onClick={() => setInviteOpen(true)}>
            <UserPlus className="size-4" />
            {t('assistants.invite')}
          </Button>
        }
      />

      {assistants.isPending ? (
        <RowSkeleton count={4} />
      ) : assistants.isError ? (
        <QueryError error={assistants.error} onRetry={() => assistants.refetch()} />
      ) : items.length === 0 ? (
        <EmptyState
          spot="teachers"
          title={t('assistants.empty')}
          description={t('assistants.emptyHint')}
          action={{ label: t('assistants.invite'), onClick: () => setInviteOpen(true) }}
        />
      ) : (
        <div className="space-y-3">
          {items.map((member, i) => (
            <motion.div
              key={member.id}
              initial={{ opacity: 0, y: 8 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ ...transitions.fast, delay: Math.min(i * 0.04, 0.3) }}
            >
              <Card className="p-5">
                <div className="flex flex-wrap items-center gap-4">
                  <Avatar
                    traits={avatarTraitsFor(member.userId ?? member.id ?? '')}
                    seed={member.userId ?? member.id ?? ''}
                    name={member.displayName ?? member.email ?? ''}
                    src={imageUrl(member.profileImageUrl)}
                    size="md"
                  />

                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-display text-body font-bold text-foreground">
                        {member.displayName || member.email}
                      </p>
                      <Badge variant="primary" size="sm">
                        {ROLES.find((r) => r.value === member.role)?.[lang] ?? member.roleName}
                      </Badge>
                      <StatusBadge status={member.status} fallbackLabel={member.statusName} />
                    </div>
                    <p className="mt-0.5 truncate font-latin text-caption text-muted-foreground">
                      {member.email}
                    </p>
                    {member.title && (
                      <p className="mt-0.5 text-caption text-muted-foreground">{member.title}</p>
                    )}
                  </div>

                  <div className="flex items-center gap-2">
                    <Badge variant="outline" size="md" className="tnum">
                      <KeyRound className="size-3.5" />
                      {formatNumber(member.permissions?.length ?? 0)}
                    </Badge>
                    <Button size="sm" variant="outline" onClick={() => setPermissionsFor(member)}>
                      {t('assistants.permissions')}
                    </Button>

                    <DropdownMenu>
                      <DropdownMenuTrigger asChild>
                        <Button variant="ghost" size="icon-sm" aria-label={t('common.actions')}>
                          <MoreVertical className="size-4" />
                        </Button>
                      </DropdownMenuTrigger>
                      <DropdownMenuContent align="end">
                        <DropdownMenuItem
                          onSelect={() =>
                            member.id &&
                            setStatus.mutate({
                              id: member.id,
                              active: member.status !== 'Active',
                            })
                          }
                        >
                          <Power className="size-4" />
                          {member.status === 'Active'
                            ? t('assistants.deactivate')
                            : t('assistants.activate')}
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                          className="text-destructive-strong"
                          onSelect={() => setRemoving(member)}
                        >
                          <Trash2 className="size-4" />
                          {t('common.remove')}
                        </DropdownMenuItem>
                      </DropdownMenuContent>
                    </DropdownMenu>
                  </div>
                </div>
              </Card>
            </motion.div>
          ))}
        </div>
      )}

      {assistants.data && items.length > 0 && (
        <Pagination page={assistants.data} onChange={setPage} className="mt-8" />
      )}

      <InviteDialog open={inviteOpen} onOpenChange={setInviteOpen} onSaved={invalidate} />

      {permissionsFor && (
        <PermissionsDialog
          member={permissionsFor}
          onClose={() => setPermissionsFor(null)}
          onSaved={invalidate}
        />
      )}

      <ConfirmDialog
        open={Boolean(removing)}
        onOpenChange={(open) => !open && setRemoving(null)}
        title={t('assistants.removeConfirm')}
        description={removing?.displayName ?? removing?.email ?? ''}
        confirmLabel={t('common.remove')}
        loading={remove.isPending}
        onConfirm={() => removing?.id && remove.mutate(removing.id)}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Invite                                   */
/* -------------------------------------------------------------------------- */

function InviteDialog({
  open,
  onOpenChange,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()

  const [email, setEmail] = React.useState('')
  const [role, setRole] = React.useState<(typeof ROLES)[number]['value']>('Assistant')
  const [title, setTitle] = React.useState('')
  const [selected, setSelected] = React.useState<string[]>([])
  const [formError, setFormError] = React.useState<string | null>(null)

  const catalog = useQuery({
    queryKey: qk.tenant.permissionCatalog,
    queryFn: assistantsApi.permissionCatalog,
    enabled: open,
    staleTime: 30 * 60_000,
  })

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    setEmail('')
    setRole('Assistant')
    setTitle('')
    setSelected([])
  }, [open])

  const invite = useMutation({
    mutationFn: () =>
      assistantsApi.invite({ email: email.trim(), role, title: title.trim() || null, permissions: selected }),
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('assistants.invited'), variant: 'success' })
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={t('assistants.inviteTitle')}
      description={
        locale === 'ar-EG'
          ? 'هيوصله بريد فيه دعوة. الصلاحيات تقدر تعدّلها في أي وقت.'
          : 'They receive an email invitation. Permissions can be changed any time.'
      }
      error={formError}
      submitting={invite.isPending}
      submitLabel={t('assistants.invite')}
      onSubmit={(e) => {
        e.preventDefault()
        if (email.trim()) invite.mutate()
      }}
    >
      <Field label={t('auth.email')} htmlFor="invite-email" required>
        <Input
          id="invite-email"
          type="email"
          dir="ltr"
          className="text-start"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          placeholder="name@example.com"
        />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t('assistants.role')} htmlFor="invite-role">
          <Select value={role} onValueChange={(v) => setRole(v as typeof role)}>
            <SelectTrigger id="invite-role">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {ROLES.map((r) => (
                <SelectItem key={r.value} value={r.value}>
                  {r[lang]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field label={locale === 'ar-EG' ? 'المسمى الوظيفي' : 'Title'} htmlFor="invite-title">
          <Input id="invite-title" value={title} onChange={(e) => setTitle(e.target.value)} />
        </Field>
      </div>

      <PermissionPicker
        catalog={catalog.data?.groups ?? []}
        loading={catalog.isPending}
        selected={selected}
        onChange={setSelected}
      />
    </FormDialog>
  )
}

/* -------------------------------------------------------------------------- */
/*                                Permissions                                 */
/* -------------------------------------------------------------------------- */

function PermissionsDialog({
  member,
  onClose,
  onSaved,
}: {
  member: TenantMemberDto
  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useI18n()
  const { toast } = useToast()
  const [selected, setSelected] = React.useState<string[]>(
    member.explicitPermissions?.length ? member.explicitPermissions : (member.permissions ?? []),
  )
  const [formError, setFormError] = React.useState<string | null>(null)

  const catalog = useQuery({
    queryKey: qk.tenant.permissionCatalog,
    queryFn: assistantsApi.permissionCatalog,
    staleTime: 30 * 60_000,
  })

  const save = useMutation({
    mutationFn: () => assistantsApi.setPermissions(member.id!, selected),
    onSuccess: () => {
      onSaved()
      onClose()
      toast({ title: t('assistants.permissionsSaved'), variant: 'success' })
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={`${t('assistants.permissions')} — ${member.displayName ?? member.email ?? ''}`}
      error={formError}
      submitting={save.isPending}
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      <PermissionPicker
        catalog={catalog.data?.groups ?? []}
        loading={catalog.isPending}
        selected={selected}
        onChange={setSelected}
      />
    </FormDialog>
  )
}

/**
 * Grouped permission cards with a per-group master toggle — far easier to reason
 * about than a wall of checkboxes.
 */
function PermissionPicker({
  catalog,
  loading,
  selected,
  onChange,
}: {
  catalog: { name?: string; permissions?: string[] }[]
  loading: boolean
  selected: string[]
  onChange: (next: string[]) => void
}) {
  const { t, locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  if (loading) return <RowSkeleton count={4} />

  const toggle = (permission: string) =>
    onChange(
      selected.includes(permission)
        ? selected.filter((p) => p !== permission)
        : [...selected, permission],
    )

  const toggleGroup = (permissions: string[], allOn: boolean) =>
    onChange(
      allOn
        ? selected.filter((p) => !permissions.includes(p))
        : [...new Set([...selected, ...permissions])],
    )

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between gap-3">
        <p className="font-display text-body-sm font-bold text-foreground">
          {t('assistants.permissions')}
        </p>
        <Badge variant="primary" size="sm" className="tnum">
          <ShieldCheck className="size-3.5" />
          {formatNumber(selected.length)}
        </Badge>
      </div>

      <div className="grid gap-3 sm:grid-cols-2">
        {catalog.map((group) => {
          const permissions = group.permissions ?? []
          const activeCount = permissions.filter((p) => selected.includes(p)).length
          const allOn = activeCount === permissions.length && permissions.length > 0

          return (
            <Card
              key={group.name}
              className={cn(
                'p-4 transition-colors',
                activeCount > 0 && 'border-primary/30 bg-primary/[0.03]',
              )}
            >
              <div className="flex items-center justify-between gap-3">
                <p className="font-display text-body-sm font-bold text-foreground">
                  {GROUP_LABELS[group.name ?? '']?.[lang] ?? group.name}
                </p>
                <Switch
                  checked={allOn}
                  onCheckedChange={() => toggleGroup(permissions, allOn)}
                  aria-label={group.name}
                />
              </div>

              <div className="mt-3 flex flex-wrap gap-1.5">
                <AnimatePresence initial={false}>
                  {permissions.map((permission) => {
                    const on = selected.includes(permission)
                    return (
                      <motion.button
                        key={permission}
                        layout
                        type="button"
                        onClick={() => toggle(permission)}
                        aria-pressed={on}
                        className={cn(
                          'inline-flex items-center gap-1 rounded-full border px-2.5 py-1 text-caption font-semibold transition-colors',
                          on
                            ? 'border-primary bg-primary text-primary-foreground'
                            : 'border-border bg-card text-muted-foreground hover:border-foreground/25',
                        )}
                      >
                        {on && <Check className="size-3" />}
                        {actionLabel(permission, lang)}
                      </motion.button>
                    )
                  })}
                </AnimatePresence>
              </div>
            </Card>
          )
        })}
      </div>
    </div>
  )
}
