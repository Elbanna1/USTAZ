import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { Bell, CheckCheck, Settings2, Trash2 } from 'lucide-react'
import { notificationsApi, type NotificationQuery } from '@/api/endpoints/platform'
import type { NotificationPreferenceDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useNotificationActions, useUnreadCounts } from '@/hooks/use-notifications'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { NotificationRow } from '@/components/notifications/NotificationCenter'
import { FilterChip } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Switch } from '@/components/ui/primitives'
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog'
import { useMutation, useQueryClient } from '@tanstack/react-query'

const CATEGORIES = [
  { value: undefined, ar: 'الكل', en: 'All' },
  { value: 'Attendance', ar: 'الحضور', en: 'Attendance' },
  { value: 'Grade', ar: 'الدرجات', en: 'Grades' },
  { value: 'Homework', ar: 'الواجبات', en: 'Homework' },
  { value: 'Exam', ar: 'الامتحانات', en: 'Exams' },
  { value: 'Payment', ar: 'المدفوعات', en: 'Payments' },
  { value: 'Announcement', ar: 'الإعلانات', en: 'Announcements' },
  { value: 'Message', ar: 'الرسائل', en: 'Messages' },
] as const

const PAGE_SIZE = 20

export default function NotificationsPage() {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  usePageTitle(t('notifications.title'))

  const [page, setPage] = React.useState(1)
  const [category, setCategory] = React.useState<string>()
  const [unreadOnly, setUnreadOnly] = React.useState(false)
  const [prefsOpen, setPrefsOpen] = React.useState(false)

  const actions = useNotificationActions()
  const unread = useUnreadCounts()

  const query: NotificationQuery = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Category: category, UnreadOnly: unreadOnly || undefined }),
    [page, category, unreadOnly],
  )

  const list = useQuery({
    queryKey: qk.notifications.list(query),
    queryFn: () => notificationsApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [category, unreadOnly])

  const items = list.data?.items ?? []

  return (
    <>
      <DashboardHeader
        title={t('notifications.title')}
        description={
          unread.notifications > 0
            ? t('notifications.unreadCount', { count: unread.notifications })
            : locale === 'ar-EG'
              ? 'كل التحديثات المهمة في مكان واحد.'
              : 'Every important update in one place.'
        }
        actions={
          <>
            <Button variant="outline" size="sm" onClick={() => setPrefsOpen(true)}>
              <Settings2 className="size-4" />
              {t('notifications.preferences')}
            </Button>
            <Button
              variant="outline"
              size="sm"
              loading={actions.clearRead.isPending}
              onClick={() => actions.clearRead.mutate()}
            >
              <Trash2 className="size-4" />
              {t('notifications.clearRead')}
            </Button>
            {unread.notifications > 0 && (
              <Button
                size="sm"
                loading={actions.markAllRead.isPending}
                onClick={() => actions.markAllRead.mutate()}
              >
                <CheckCheck className="size-4" />
                {t('notifications.markAllRead')}
              </Button>
            )}
          </>
        }
      >
        <div className="no-scrollbar flex gap-2 overflow-x-auto pb-1">
          {CATEGORIES.map((c) => (
            <FilterChip
              key={c.value ?? 'all'}
              label={c[lang]}
              active={category === c.value}
              onClick={() => setCategory(c.value)}
            />
          ))}
          <span className="mx-1 h-8 w-px shrink-0 bg-border" />
          <FilterChip
            label={t('messages.unread')}
            active={unreadOnly}
            count={unread.notifications || undefined}
            onClick={() => setUnreadOnly((v) => !v)}
          />
        </div>
      </DashboardHeader>

      <Card className={cn('p-2 transition-opacity', list.isFetching && 'opacity-60')}>
        {list.isPending ? (
          <div className="p-2">
            <RowSkeleton count={6} />
          </div>
        ) : list.isError ? (
          <QueryError error={list.error} onRetry={() => list.refetch()} compact />
        ) : items.length === 0 ? (
          <EmptyState
            spot="inbox"
            compact
            title={t('notifications.empty')}
            description={t('notifications.emptyHint')}
          />
        ) : (
          <motion.div
            initial="hidden"
            animate="visible"
            variants={{ visible: { transition: { staggerChildren: 0.03 } } }}
            className="space-y-1"
          >
            <AnimatePresence initial={false}>
              {items.map((notification) => (
                <motion.div
                  key={notification.id}
                  layout
                  variants={{
                    hidden: { opacity: 0, y: 8 },
                    visible: { opacity: 1, y: 0, transition: transitions.fast },
                  }}
                  exit={{ opacity: 0, x: -20, transition: { duration: 0.15 } }}
                  className="group relative"
                >
                  <NotificationRow
                    notification={notification}
                    onRead={(id) => actions.markRead.mutate(id)}
                  />
                  <button
                    type="button"
                    onClick={() => notification.id && actions.remove.mutate(notification.id)}
                    aria-label={t('common.delete')}
                    className="absolute end-2 top-2 rounded-md p-1.5 text-muted-foreground opacity-0 transition-opacity hover:bg-destructive/10 hover:text-destructive-strong focus-visible:opacity-100 group-hover:opacity-100"
                  >
                    <Trash2 className="size-4" />
                  </button>
                </motion.div>
              ))}
            </AnimatePresence>
          </motion.div>
        )}
      </Card>

      {list.data && items.length > 0 && (
        <Pagination page={list.data} onChange={setPage} className="mt-6" />
      )}

      <PreferencesDialog open={prefsOpen} onOpenChange={setPrefsOpen} />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                Preferences                                 */
/* -------------------------------------------------------------------------- */

const CATEGORY_LABELS: Record<string, { ar: string; en: string }> = {
  Attendance: { ar: 'الحضور', en: 'Attendance' },
  Grade: { ar: 'الدرجات', en: 'Grades' },
  Homework: { ar: 'الواجبات', en: 'Homework' },
  Exam: { ar: 'الامتحانات', en: 'Exams' },
  Schedule: { ar: 'الجدول', en: 'Schedule' },
  Payment: { ar: 'المدفوعات', en: 'Payments' },
  Subscription: { ar: 'الاشتراك', en: 'Subscription' },
  Announcement: { ar: 'الإعلانات', en: 'Announcements' },
  Message: { ar: 'الرسائل', en: 'Messages' },
  System: { ar: 'النظام', en: 'System' },
  Course: { ar: 'الكورسات', en: 'Courses' },
  Security: { ar: 'الأمان', en: 'Security' },
  Enrollment: { ar: 'الاشتراكات', en: 'Enrollments' },
  Certificate: { ar: 'الشهادات', en: 'Certificates' },
}

function PreferencesDialog({
  open,
  onOpenChange,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [draft, setDraft] = React.useState<NotificationPreferenceDto[]>([])

  const prefs = useQuery({
    queryKey: qk.notifications.preferences,
    queryFn: notificationsApi.preferences,
    enabled: open,
  })

  React.useEffect(() => {
    if (prefs.data) setDraft(prefs.data)
  }, [prefs.data])

  const save = useMutation({
    mutationFn: () =>
      notificationsApi.updatePreferences({
        preferences: draft.map((p) => ({
          category: p.category,
          isEnabled: p.isEnabled,
          inApp: p.inApp,
          email: p.email,
          whatsApp: p.whatsApp,
        })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.notifications.preferences })
      toast({ title: t('common.saved'), variant: 'success' })
      onOpenChange(false)
    },
    onError: () => toast({ title: t('states.errorTitle'), variant: 'error' }),
  })

  function toggle(index: number, key: 'isEnabled' | 'inApp' | 'email' | 'whatsApp') {
    setDraft((prev) =>
      prev.map((p, i) => (i === index ? { ...p, [key]: !p[key] } : p)),
    )
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t('notifications.preferences')}</DialogTitle>
        </DialogHeader>

        {prefs.isPending ? (
          <RowSkeleton count={5} />
        ) : prefs.isError ? (
          <QueryError error={prefs.error} onRetry={() => prefs.refetch()} compact />
        ) : (
          <>
            <div className="max-h-[60vh] space-y-2 overflow-y-auto pe-1">
              <div className="sticky top-0 z-10 grid grid-cols-[1fr_repeat(3,3.5rem)] gap-2 border-b border-border bg-popover px-3 pb-2 pt-1 text-[0.6875rem] font-semibold text-muted-foreground">
                <span>{locale === 'ar-EG' ? 'النوع' : 'Category'}</span>
                <span className="text-center">{t('notifications.inApp')}</span>
                <span className="text-center">{t('notifications.emailChannel')}</span>
                <span className="text-center">{t('notifications.whatsAppChannel')}</span>
              </div>

              {draft.map((pref, index) => (
                <div
                  key={pref.category}
                  className={cn(
                    'grid grid-cols-[1fr_repeat(3,3.5rem)] items-center gap-2 rounded-lg px-3 py-2.5 transition-colors',
                    pref.isEnabled ? 'bg-muted/30' : 'opacity-60',
                  )}
                >
                  <button
                    type="button"
                    onClick={() => toggle(index, 'isEnabled')}
                    className="flex items-center gap-2 text-start font-display text-body-sm font-semibold text-foreground"
                  >
                    <Bell
                      className={cn(
                        'size-4 shrink-0',
                        pref.isEnabled ? 'text-primary' : 'text-muted-foreground',
                      )}
                    />
                    {CATEGORY_LABELS[pref.category ?? '']?.[lang] ?? pref.categoryName ?? pref.category}
                  </button>
                  {(['inApp', 'email', 'whatsApp'] as const).map((channel) => (
                    <span key={channel} className="flex justify-center">
                      <Switch
                        checked={Boolean(pref[channel])}
                        disabled={!pref.isEnabled}
                        onCheckedChange={() => toggle(index, channel)}
                        aria-label={`${pref.category} — ${channel}`}
                      />
                    </span>
                  ))}
                </div>
              ))}
            </div>

            <div className="flex justify-end gap-2 pt-2">
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                {t('common.cancel')}
              </Button>
              <Button loading={save.isPending} onClick={() => save.mutate()}>
                {t('common.save')}
              </Button>
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  )
}
