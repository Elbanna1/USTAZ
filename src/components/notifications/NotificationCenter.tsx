import * as React from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import {
  Award,
  Bell,
  BookOpen,
  CalendarClock,
  CheckCheck,
  CreditCard,
  FileText,
  GraduationCap,
  Megaphone,
  MessageSquare,
  ShieldAlert,
  UserPlus,
  type LucideIcon,
} from 'lucide-react'
import { notificationsApi } from '@/api/endpoints/platform'
import type { NotificationDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { cn } from '@/lib/utils'
import { useI18n } from '@/i18n'
import { useAuth } from '@/hooks/use-auth'
import { useNotificationActions, useUnreadCounts } from '@/hooks/use-notifications'
import { Button } from '@/components/ui/button'
import { Spot } from '@/components/illustrations/Spot'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { transitions } from '@/lib/motion'

/** One glyph per notification category, so the list is scannable at a glance. */
const CATEGORY_ICONS: Record<string, LucideIcon> = {
  Attendance: CalendarClock,
  Grade: GraduationCap,
  Homework: FileText,
  Exam: FileText,
  Schedule: CalendarClock,
  Payment: CreditCard,
  Subscription: CreditCard,
  Announcement: Megaphone,
  Message: MessageSquare,
  System: ShieldAlert,
  Course: BookOpen,
  Security: ShieldAlert,
  Enrollment: UserPlus,
  Certificate: Award,
}

const PRIORITY_STYLES: Record<string, string> = {
  Critical: 'bg-destructive/12 text-destructive-strong',
  High: 'bg-warning/15 text-warning-strong',
  Normal: 'bg-primary/10 text-primary',
  Low: 'bg-muted text-muted-foreground',
}

export function notificationTitle(
  notification: NotificationDto,
  locale: 'ar-EG' | 'en',
): { title: string; body: string } {
  const useEnglish = locale === 'en'
  return {
    title: (useEnglish ? notification.titleEn : notification.title) || notification.title || '',
    body: (useEnglish ? notification.messageEn : notification.message) || notification.message || '',
  }
}

/** Single row, shared by the dropdown and the full notifications page. */
export function NotificationRow({
  notification,
  onRead,
  dense = false,
}: {
  notification: NotificationDto
  onRead?: (id: string) => void
  dense?: boolean
}) {
  const { locale, formatTimeAgo } = useI18n()
  const copy = notificationTitle(notification, locale)
  const Icon = CATEGORY_ICONS[notification.category ?? ''] ?? Bell
  const unread = !notification.isRead

  const content = (
    <>
      <span
        className={cn(
          'grid size-9 shrink-0 place-items-center rounded-lg',
          PRIORITY_STYLES[notification.priority ?? 'Normal'],
        )}
      >
        <Icon className="size-[1.125rem]" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="flex items-start gap-2">
          <span
            className={cn(
              'block flex-1 font-display text-body-sm leading-snug',
              unread ? 'font-bold text-foreground' : 'font-semibold text-muted-foreground',
            )}
          >
            {copy.title}
          </span>
          {unread && <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" />}
        </span>
        {copy.body && (
          <span
            className={cn(
              'mt-1 block text-caption leading-relaxed text-muted-foreground',
              dense && 'line-clamp-2-rtl',
            )}
          >
            {copy.body}
          </span>
        )}
        {notification.createdAt && (
          <span className="mt-1.5 block text-[0.6875rem] text-muted-foreground/70">
            {formatTimeAgo(notification.createdAt)}
          </span>
        )}
      </span>
    </>
  )

  const className = cn(
    'flex w-full items-start gap-3 rounded-xl p-3 text-start transition-colors',
    unread ? 'bg-primary/[0.04] hover:bg-primary/[0.08]' : 'hover:bg-muted',
  )

  const handleClick = () => {
    if (unread && notification.id) onRead?.(notification.id)
  }

  if (notification.actionUrl) {
    return (
      <Link to={notification.actionUrl} className={className} onClick={handleClick}>
        {content}
      </Link>
    )
  }

  return (
    <button type="button" className={className} onClick={handleClick}>
      {content}
    </button>
  )
}

/** Bell + dropdown used in every dashboard topbar. */
export function NotificationCenter({ allHref }: { allHref: string }) {
  const { t } = useI18n()
  const { isAuthenticated } = useAuth()
  const [open, setOpen] = React.useState(false)
  const { notifications: unread } = useUnreadCounts()
  const actions = useNotificationActions()

  const query = useQuery({
    queryKey: qk.notifications.list({ PageSize: 6 }),
    queryFn: () => notificationsApi.list({ PageSize: 6 }),
    enabled: isAuthenticated && open,
  })

  const items = query.data?.items ?? []

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={
            unread ? `${t('notifications.title')} — ${t('notifications.unreadCount', { count: unread })}` : t('notifications.title')
          }
          className="relative grid size-10 place-items-center rounded-lg border border-border bg-card text-muted-foreground transition-colors hover:border-foreground/20 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <Bell className="size-[1.125rem]" />
          <AnimatePresence>
            {unread > 0 && (
              <motion.span
                initial={{ scale: 0 }}
                animate={{ scale: 1 }}
                exit={{ scale: 0 }}
                transition={transitions.spring}
                className="tnum absolute -end-1 -top-1 grid h-[1.125rem] min-w-[1.125rem] place-items-center rounded-full bg-destructive px-1 text-[0.625rem] font-bold text-destructive-foreground"
              >
                {unread > 99 ? '99+' : unread}
              </motion.span>
            )}
          </AnimatePresence>
        </button>
      </DropdownMenuTrigger>

      <DropdownMenuContent align="end" className="w-[min(22rem,calc(100vw-2rem))] p-0">
        <div className="flex items-center justify-between gap-2 border-b border-border px-4 py-3">
          <p className="font-display text-body-sm font-bold text-foreground">
            {t('notifications.title')}
          </p>
          {unread > 0 && (
            <button
              type="button"
              onClick={() => actions.markAllRead.mutate()}
              disabled={actions.markAllRead.isPending}
              className="inline-flex items-center gap-1.5 text-caption font-semibold text-primary transition-opacity hover:opacity-80 disabled:opacity-50"
            >
              <CheckCheck className="size-3.5" />
              {t('notifications.markAllRead')}
            </button>
          )}
        </div>

        <div className="max-h-[22rem] overflow-y-auto p-2">
          {query.isPending ? (
            <div className="space-y-2 p-1">
              {[0, 1, 2].map((i) => (
                <div key={i} className="flex gap-3 rounded-xl p-3">
                  <span className="size-9 shrink-0 animate-pulse rounded-lg bg-muted" />
                  <span className="flex-1 space-y-2">
                    <span className="block h-3 w-2/3 animate-pulse rounded bg-muted" />
                    <span className="block h-3 w-1/2 animate-pulse rounded bg-muted" />
                  </span>
                </div>
              ))}
            </div>
          ) : items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 px-4 py-8 text-center">
              <Spot name="inbox" className="h-20" />
              <p className="font-display text-body-sm font-bold text-foreground">
                {t('notifications.empty')}
              </p>
              <p className="text-caption text-muted-foreground">{t('notifications.emptyHint')}</p>
            </div>
          ) : (
            <motion.div
              initial="hidden"
              animate="visible"
              variants={{ visible: { transition: { staggerChildren: 0.04 } } }}
              className="space-y-1"
            >
              {items.map((notification) => (
                <motion.div
                  key={notification.id}
                  variants={{
                    hidden: { opacity: 0, y: 6 },
                    visible: { opacity: 1, y: 0, transition: transitions.fast },
                  }}
                >
                  <NotificationRow
                    notification={notification}
                    dense
                    onRead={(id) => actions.markRead.mutate(id)}
                  />
                </motion.div>
              ))}
            </motion.div>
          )}
        </div>

        <div className="border-t border-border p-2">
          <Button asChild variant="ghost" block size="sm" onClick={() => setOpen(false)}>
            <Link to={allHref}>{t('common.viewAll')}</Link>
          </Button>
        </div>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
