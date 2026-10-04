import * as React from 'react'
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { AnimatePresence, motion } from 'framer-motion'
import { ChevronLeft, ChevronRight, LogOut, Menu, Settings, UserRound } from 'lucide-react'
import { Logo, LogoMark } from '@/components/brand/Logo'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { SearchTrigger } from '@/components/search/SearchTrigger'
import { LanguageToggle } from '@/components/layout/LanguageToggle'
import { NotificationCenter } from '@/components/notifications/NotificationCenter'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useScrollToTop } from '@/hooks/use-ui'
import { useAuth, ROLE_HOME, type AppRole } from '@/hooks/use-auth'
import { useUnreadCounts } from '@/hooks/use-notifications'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { pageTransition } from '@/lib/motion'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import {
  MOBILE_PRIMARY,
  ROLE_ICONS,
  ROLE_LABELS,
  navFor,
  type NavEntry,
} from '@/lib/navigation'

export function DashboardLayout({ role }: { role: AppRole }) {
  const location = useLocation()
  const navigate = useNavigate()
  const { t, locale, isRtl } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { user, roles, hasRole, permissions, logout } = useAuth()
  const { toast } = useToast()
  const unread = useUnreadCounts()
  const [mobileOpen, setMobileOpen] = React.useState(false)
  useScrollToTop()

  React.useEffect(() => setMobileOpen(false), [location.pathname])

  const isAssistant = role === 'teacher' && hasRole('assistant') && !hasRole('teacher')
  const sections = React.useMemo(
    () => navFor(role, { permissions, isAssistant }),
    [role, permissions, isAssistant],
  )
  const flatItems = React.useMemo(() => sections.flatMap((s) => s.items), [sections])
  const mobileItems = MOBILE_PRIMARY[role]
    .map((href) => flatItems.find((item) => item.href === href))
    .filter((item): item is NavEntry => Boolean(item))
    .slice(0, 4)

  const displayName = user?.displayName?.trim() || user?.firstName || '—'
  const roleLabel = ROLE_LABELS[isAssistant ? 'assistant' : role][lang]
  const photo = imageUrl(user?.profileImageUrl)
  const traits = avatarTraitsFor(user?.id ?? 'anonymous')

  /** Other consoles this same account can open. */
  const otherRoles = roles.filter((r) => r !== role && !(r === 'assistant' && role === 'teacher'))

  const badgeFor = (item: NavEntry) =>
    item.badge === 'notifications'
      ? unread.notifications
      : item.badge === 'messages'
        ? unread.messages
        : undefined

  async function handleLogout() {
    await logout()
    toast({ title: t('auth.logoutDone'), variant: 'success' })
    navigate('/login', { replace: true })
  }

  const notificationsHref = flatItems.find((i) => i.badge === 'notifications')?.href ?? '/'

  return (
    <div className="min-h-dvh bg-background">
      {/* --------------------------- Desktop sidebar --------------------------- */}
      <aside className="fixed inset-y-0 start-0 z-40 hidden w-64 flex-col border-e border-border bg-surface lg:flex">
        <div className="flex h-16 shrink-0 items-center border-b border-border px-5">
          <Link to="/" aria-label={t('common.appName')}>
            <Logo size="sm" />
          </Link>
        </div>

        <div className="shrink-0 border-b border-border p-4">
          <ConsoleBadge role={isAssistant ? 'assistant' : role} lang={lang} />
        </div>

        <nav
          className="flex-1 space-y-5 overflow-y-auto p-3"
          aria-label={roleLabel}
        >
          {sections.map((section, i) => (
            <div key={section.title?.en ?? `section-${i}`} className="space-y-1">
              {section.title && (
                <p className="px-3 pb-1 text-label uppercase tracking-wide text-muted-foreground">
                  {section.title[lang]}
                </p>
              )}
              {section.items.map((item) => (
                <SidebarLink
                  key={item.href}
                  item={item}
                  lang={lang}
                  badge={badgeFor(item)}
                  isRtl={isRtl}
                />
              ))}
            </div>
          ))}
        </nav>

        <div className="shrink-0 border-t border-border p-3">
          <Link
            to="/"
            className="flex items-center gap-2.5 rounded-lg px-3 py-2.5 text-body-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
          >
            <LogOut className="size-[1.125rem] flip-rtl" />
            {t('nav.backToSite')}
          </Link>
        </div>
      </aside>

      {/* ----------------------------- Content column ---------------------------- */}
      <div className="lg:ps-64">
        <header className="sticky top-0 z-30 border-b border-border bg-background/85 backdrop-blur-xl">
          <div className="flex h-16 items-center gap-3 px-4 sm:px-6">
            <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
              <SheetTrigger asChild>
                <Button variant="outline" size="icon" className="lg:hidden" aria-label="Menu">
                  <Menu className="size-5" />
                </Button>
              </SheetTrigger>
              <SheetContent side="start" className="w-[min(18rem,86vw)] overflow-y-auto">
                <SheetHeader>
                  <SheetTitle className="text-start">
                    <Logo size="sm" />
                  </SheetTitle>
                </SheetHeader>
                <div className="p-4">
                  <ConsoleBadge role={isAssistant ? 'assistant' : role} lang={lang} />
                </div>
                <nav className="space-y-5 px-3 pb-8" aria-label={roleLabel}>
                  {sections.map((section, i) => (
                    <div key={section.title?.en ?? `m-section-${i}`} className="space-y-1">
                      {section.title && (
                        <p className="px-3 pb-1 text-label uppercase tracking-wide text-muted-foreground">
                          {section.title[lang]}
                        </p>
                      )}
                      {section.items.map((item) => (
                        <SidebarLink
                          key={item.href}
                          item={item}
                          lang={lang}
                          badge={badgeFor(item)}
                          isRtl={isRtl}
                        />
                      ))}
                    </div>
                  ))}
                </nav>
              </SheetContent>
            </Sheet>

            <LogoMark className="size-8 lg:hidden" />

            {/* Search rests in the header's open run, collapsing to an icon
                once the row gets tight. */}
            <SearchTrigger variant="responsive" />

            <div className="ms-auto flex items-center gap-2">
              <LanguageToggle className="hidden sm:grid" />
              <ThemeToggle />
              <NotificationCenter allHref={notificationsHref} />

              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <button
                    type="button"
                    className="flex items-center gap-2.5 rounded-lg border border-border bg-card p-1 ps-3 transition-colors hover:border-foreground/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <span className="hidden text-start sm:block">
                      <span
                        dir="auto"
                        className="block max-w-[10rem] truncate font-display text-caption font-bold text-foreground"
                      >
                        {displayName}
                      </span>
                      <span className="block text-[0.6875rem] text-muted-foreground">
                        {roleLabel}
                      </span>
                    </span>
                    <Avatar
                      traits={traits}
                      seed={user?.id ?? 'anonymous'}
                      name={displayName}
                      src={photo}
                      size="sm"
                    />
                  </button>
                </DropdownMenuTrigger>
                <DropdownMenuContent className="w-60">
                  <DropdownMenuLabel>
                    <span className="block font-display text-body-sm font-bold text-foreground">
                      {displayName}
                    </span>
                    <span className="block break-all text-caption font-normal normal-case tracking-normal text-muted-foreground">
                      {user?.email}
                    </span>
                  </DropdownMenuLabel>
                  <DropdownMenuSeparator />

                  {user?.teacherId && (
                    <DropdownMenuItem asChild>
                      <Link to={`/teachers/${user.teacherId}`}>
                        <UserRound className="size-4" />
                        {locale === 'ar-EG' ? 'شوف بروفايلك العام' : 'View public profile'}
                      </Link>
                    </DropdownMenuItem>
                  )}
                  <DropdownMenuItem asChild>
                    <Link to={`${ROLE_HOME[role].replace('/dashboard', '')}/settings`}>
                      <Settings className="size-4" />
                      {t('nav.settings')}
                    </Link>
                  </DropdownMenuItem>

                  {otherRoles.length > 0 && (
                    <>
                      <DropdownMenuSeparator />
                      {otherRoles.map((other) => (
                        <DropdownMenuItem key={other} asChild>
                          <Link to={ROLE_HOME[other]}>
                            {React.createElement(ROLE_ICONS[other], { className: 'size-4' })}
                            {ROLE_LABELS[other][lang]}
                          </Link>
                        </DropdownMenuItem>
                      ))}
                    </>
                  )}

                  <DropdownMenuSeparator />
                  <DropdownMenuItem onSelect={() => void handleLogout()}>
                    <LogOut className="size-4 flip-rtl" />
                    {t('nav.logout')}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            </div>
          </div>
        </header>

        <main id="main" className="px-4 pb-24 pt-6 sm:px-6 lg:pb-12">
          <AnimatePresence mode="wait" initial={false}>
            <motion.div
              key={location.pathname}
              variants={pageTransition}
              initial="initial"
              animate="animate"
              exit="exit"
            >
              <Outlet />
            </motion.div>
          </AnimatePresence>
        </main>
      </div>

      {/* ------------------------ Mobile bottom navigation ----------------------- */}
      {mobileItems.length > 0 && (
        <nav
          className="safe-bottom fixed inset-x-0 bottom-0 z-40 border-t border-border bg-background/95 backdrop-blur-xl lg:hidden"
          aria-label={roleLabel}
        >
          <ul className="grid" style={{ gridTemplateColumns: `repeat(${mobileItems.length}, 1fr)` }}>
            {mobileItems.map((item) => {
              const badge = badgeFor(item)
              return (
                <li key={item.href}>
                  <NavLink
                    to={item.href}
                    end
                    className={({ isActive }) =>
                      cn(
                        'flex flex-col items-center gap-1 px-1 py-2.5 text-[0.6875rem] font-semibold transition-colors',
                        isActive ? 'text-primary' : 'text-muted-foreground',
                      )
                    }
                  >
                    {({ isActive }) => (
                      <>
                        <span
                          className={cn(
                            'relative grid size-8 place-items-center rounded-lg transition-colors',
                            isActive && 'bg-primary/12',
                          )}
                        >
                          <item.icon className="size-[1.125rem]" />
                          {badge ? (
                            <span className="absolute -end-0.5 -top-0.5 size-2 rounded-full bg-destructive" />
                          ) : null}
                        </span>
                        <span className="w-full truncate text-center">{item.label[lang]}</span>
                      </>
                    )}
                  </NavLink>
                </li>
              )
            })}
          </ul>
        </nav>
      )}
    </div>
  )
}

function SidebarLink({
  item,
  lang,
  badge,
  isRtl,
}: {
  item: NavEntry
  lang: 'ar' | 'en'
  badge?: number
  isRtl: boolean
}) {
  const Chevron = isRtl ? ChevronLeft : ChevronRight

  return (
    <NavLink
      to={item.href}
      end
      className={({ isActive }) =>
        cn(
          'group relative flex items-center gap-3 rounded-lg px-3 py-2.5 font-display text-body-sm font-semibold transition-colors duration-200',
          isActive
            ? 'text-primary-foreground'
            : 'text-muted-foreground hover:bg-muted hover:text-foreground',
        )
      }
    >
      {({ isActive }) => (
        <>
          {isActive && (
            <motion.span
              layoutId="sidebar-active"
              className="absolute inset-0 rounded-lg bg-primary shadow-soft"
              transition={{ type: 'spring', stiffness: 420, damping: 34 }}
            />
          )}
          <item.icon className="relative size-[1.125rem] shrink-0" />
          <span className="relative flex-1 truncate">{item.label[lang]}</span>
          {badge ? (
            <span
              className={cn(
                'tnum relative rounded-full px-1.5 py-0.5 text-[0.625rem] font-bold',
                isActive ? 'bg-primary-foreground/20' : 'bg-destructive text-destructive-foreground',
              )}
            >
              {badge > 99 ? '99+' : badge}
            </span>
          ) : (
            <Chevron
              className={cn(
                'relative size-4 shrink-0 opacity-0 transition-opacity duration-200 group-hover:opacity-60',
                isActive && 'opacity-70',
              )}
            />
          )}
        </>
      )}
    </NavLink>
  )
}

function ConsoleBadge({ role, lang }: { role: AppRole; lang: 'ar' | 'en' }) {
  const { user } = useAuth()
  const Icon = ROLE_ICONS[role]
  const workspace = user?.tenantName

  return (
    <div className="flex items-center gap-3 rounded-xl border border-border bg-card p-3">
      <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/12 text-primary">
        <Icon className="size-[1.125rem]" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate font-display text-caption font-bold text-foreground">
          {ROLE_LABELS[role][lang]}
        </p>
        {workspace && (
          <p dir="auto" className="truncate text-[0.6875rem] text-muted-foreground">
            {workspace}
          </p>
        )}
      </div>
      {user && !user.emailConfirmed && (
        <Badge variant="warning" size="sm">
          {lang === 'ar' ? 'الإيميل' : 'Email'}
        </Badge>
      )}
    </div>
  )
}
