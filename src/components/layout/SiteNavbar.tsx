import * as React from 'react'
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { motion } from 'framer-motion'
import {
  BookOpen,
  CalendarDays,
  ChevronDown,
  GraduationCap,
  LayoutDashboard,
  LogIn,
  LogOut,
  Menu,
  Settings,
  Users,
} from 'lucide-react'
import { Logo } from '@/components/brand/Logo'
import { ThemeToggle } from '@/components/layout/ThemeToggle'
import { LanguageToggle } from '@/components/layout/LanguageToggle'
import { SearchTrigger } from '@/components/search/SearchTrigger'
import { Button } from '@/components/ui/button'
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetTrigger,
} from '@/components/ui/sheet'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useScrollY } from '@/hooks/use-ui'
import { useAuth, ROLE_HOME } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { subjectByKey } from '@/data/taxonomy'
import { avatarTraitsFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { ROLE_ICONS, ROLE_LABELS } from '@/lib/navigation'
import { SubjectIcon } from '@/components/illustrations/SubjectIcon'
import { Avatar } from '@/components/illustrations/PersonAvatar'

const NAV_ICONS: Record<string, typeof Users> = {
  '/teachers': Users,
  '/groups': GraduationCap,
  '/courses': BookOpen,
  '/schedule': CalendarDays,
}

/** Nav entries are built from the dictionary so they follow the active locale. */
function useMainNav() {
  const { t, locale } = useI18n()
  return React.useMemo(
    () => [
      {
        href: '/teachers',
        label: t('nav.teachers'),
        description:
          locale === 'ar-EG' ? 'اكتشف مدرّسين حسب المادة والمحافظة' : 'Find teachers by subject and city',
      },
      {
        href: '/groups',
        label: t('nav.groups'),
        description: locale === 'ar-EG' ? 'مجموعات شهرية بمواعيد ثابتة' : 'Monthly groups on a fixed schedule',
      },
      {
        href: '/courses',
        label: t('nav.courses'),
        description: locale === 'ar-EG' ? 'كورسات مسجّلة ومراجعات نهائية' : 'Recorded courses and revisions',
      },
      {
        href: '/schedule',
        label: t('nav.schedule'),
        description: locale === 'ar-EG' ? 'حصص الأسبوع كلها في مكان واحد' : 'The whole week in one view',
      },
      { href: '/pricing', label: t('nav.pricing') },
    ],
    [t, locale],
  )
}

function useSecondaryNav() {
  const { t } = useI18n()
  return React.useMemo(
    () => [
      { href: '/about', label: t('nav.about') },
      { href: '/faq', label: t('nav.faq') },
      { href: '/contact', label: t('nav.contact') },
      { href: '/verify', label: t('certificates.verify') },
    ],
    [t],
  )
}

export function SiteNavbar() {
  const scrollY = useScrollY()
  const { pathname } = useLocation()
  const { t, locale } = useI18n()
  const [mobileOpen, setMobileOpen] = React.useState(false)
  const mainNav = useMainNav()

  const scrolled = scrollY > 12

  /* The sheet must close on navigation or it stays open behind the new page. */
  React.useEffect(() => {
    setMobileOpen(false)
  }, [pathname])

  return (
    <>
      <a
        href="#main"
        className="sr-only focus:not-sr-only focus:absolute focus:start-4 focus:top-4 focus:z-[60] focus:rounded-lg focus:bg-primary focus:px-4 focus:py-2.5 focus:font-display focus:text-body-sm focus:font-semibold focus:text-primary-foreground"
      >
        {locale === 'ar-EG' ? 'تخطَّ إلى المحتوى' : 'Skip to content'}
      </a>

      <header
        className={cn(
          'sticky top-0 z-50 w-full transition-all duration-300 ease-premium',
          scrolled
            ? 'border-b border-border bg-background/85 backdrop-blur-xl'
            : 'border-b border-transparent bg-transparent',
        )}
      >
        <div className="container-page">
          <div
            className={cn(
              'flex items-center gap-4 transition-all duration-300',
              scrolled ? 'h-16' : 'h-18',
            )}
          >
            <Link to="/" className="shrink-0 rounded-lg" aria-label={t('common.appName')}>
              <Logo size={scrolled ? 'sm' : 'md'} />
            </Link>

            {/* Desktop nav */}
            <nav
              className="hidden flex-1 items-center justify-center gap-1 lg:flex"
              aria-label={t('nav.dashboard')}
            >
              <SubjectsMenu />
              {mainNav.map((item) => (
                <NavLink
                  key={item.href}
                  to={item.href}
                  className={({ isActive }) =>
                    cn(
                      'relative rounded-lg px-3.5 py-2 font-display text-body-sm font-semibold transition-colors',
                      isActive ? 'text-foreground' : 'text-muted-foreground hover:text-foreground',
                    )
                  }
                >
                  {({ isActive }) => (
                    <>
                      {item.label}
                      {isActive && (
                        <motion.span
                          layoutId="nav-active"
                          className="absolute inset-x-2.5 -bottom-0.5 h-0.5 rounded-full bg-accent"
                          transition={{ duration: 0.28, ease: [0.22, 1, 0.36, 1] }}
                        />
                      )}
                    </>
                  )}
                </NavLink>
              ))}
            </nav>

            <div className="ms-auto flex items-center gap-2 lg:ms-0">
              <SearchTrigger className="hidden sm:grid" />

              <LanguageToggle className="hidden sm:grid" />
              <ThemeToggle />

              <AccountMenu />

              {/* Mobile menu */}
              <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
                <SheetTrigger asChild>
                  <Button
                    variant="outline"
                    size="icon"
                    className="lg:hidden"
                    aria-label={t('nav.dashboard')}
                  >
                    <Menu className="size-5" />
                  </Button>
                </SheetTrigger>
                <SheetContent side="start" className="w-[min(22rem,88vw)] max-w-none">
                  <SheetHeader>
                    <SheetTitle className="text-start">
                      <Logo size="sm" />
                    </SheetTitle>
                  </SheetHeader>
                  <MobileNavBody />
                </SheetContent>
              </Sheet>
            </div>
          </div>
        </div>
      </header>
    </>
  )
}

/* -------------------------------------------------------------------------- */

function SubjectsMenu() {
  const { t, locale, formatNumber } = useI18n()
  const subjects = useSubjects()

  if (!subjects.subjects.length) return null

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="group inline-flex items-center gap-1.5 rounded-lg px-3.5 py-2 font-display text-body-sm font-semibold text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {t('common.subject')}
          <ChevronDown className="size-4 transition-transform duration-200 group-data-[state=open]:rotate-180" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="center" className="w-[min(34rem,90vw)] p-3">
        <DropdownMenuLabel className="px-1.5">
          {locale === 'ar-EG' ? 'اختار مادتك' : 'Pick your subject'}
        </DropdownMenuLabel>
        <div className="grid grid-cols-2 gap-1 pt-1">
          {subjects.subjects.map((subject) => {
            const key = subjectKeyFor(subject)
            const hue = subjectByKey(key).hue
            return (
              <DropdownMenuItem key={subject.id} asChild className="px-2.5 py-2.5">
                <Link to={`/teachers?subject=${subject.slug ?? subject.id}`}>
                  <span
                    className="grid size-8 shrink-0 place-items-center rounded-md border border-[hsl(var(--sh)_55%_45%/0.25)] bg-[hsl(var(--sh)_60%_50%/0.12)] text-[hsl(var(--sh)_62%_32%)] dark:text-[hsl(var(--sh)_70%_68%)]"
                    style={{ '--sh': hue } as React.CSSProperties}
                  >
                    <SubjectIcon subject={key} className="size-4" />
                  </span>
                  <span className="min-w-0">
                    <span className="block font-display text-body-sm font-semibold text-foreground">
                      {subjects.label(subject)}
                    </span>
                    <span className="block truncate text-caption text-muted-foreground">
                      {formatNumber(subject.teachersCount ?? 0)} {t('common.teacher')}
                    </span>
                  </span>
                </Link>
              </DropdownMenuItem>
            )
          })}
        </div>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to="/teachers" className="justify-center font-semibold text-primary">
            {t('common.viewAll')}
          </Link>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

/* -------------------------------------------------------------------------- */

/** Signed out: log in + sign up. Signed in: a menu into the user's consoles. */
function AccountMenu() {
  const { t, locale } = useI18n()
  const { isAuthenticated, user, roles, home, logout } = useAuth()
  const { toast } = useToast()
  const navigate = useNavigate()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  if (!isAuthenticated) {
    return (
      <>
        <Button asChild variant="outline" size="md" className="hidden lg:inline-flex">
          <Link to="/login">
            <LogIn className="size-4" />
            {t('nav.login')}
          </Link>
        </Button>
        <Button asChild size="md" className="hidden lg:inline-flex">
          <Link to="/register">
            {locale === 'ar-EG' ? 'ابدأ كمدرّس' : 'Start as a teacher'}
          </Link>
        </Button>
      </>
    )
  }

  const displayName = user?.displayName?.trim() || user?.firstName || ''

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex items-center gap-2 rounded-lg border border-border bg-card p-1 ps-3 transition-colors hover:border-foreground/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          <span className="hidden max-w-[8rem] truncate font-display text-caption font-bold text-foreground lg:block">
            {displayName}
          </span>
          <Avatar
            traits={avatarTraitsFor(user?.id ?? '')}
            seed={user?.id ?? ''}
            name={displayName}
            src={imageUrl(user?.profileImageUrl)}
            size="sm"
          />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuLabel>
          <span className="block font-display text-body-sm font-bold text-foreground">
            {displayName}
          </span>
          <span className="block break-all text-caption font-normal normal-case tracking-normal text-muted-foreground">
            {user?.email}
          </span>
        </DropdownMenuLabel>
        <DropdownMenuSeparator />

        <DropdownMenuItem asChild>
          <Link to={home}>
            <LayoutDashboard className="size-4" />
            {t('nav.dashboard')}
          </Link>
        </DropdownMenuItem>

        {roles.length > 1 && (
          <>
            <DropdownMenuSeparator />
            {roles.map((role) => (
              <DropdownMenuItem key={role} asChild>
                <Link to={ROLE_HOME[role]}>
                  {React.createElement(ROLE_ICONS[role], { className: 'size-4' })}
                  {ROLE_LABELS[role][lang]}
                </Link>
              </DropdownMenuItem>
            ))}
          </>
        )}

        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link to={`${home.replace('/dashboard', '')}/settings`}>
            <Settings className="size-4" />
            {t('nav.settings')}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem
          onSelect={async () => {
            await logout()
            toast({ title: t('auth.logoutDone'), variant: 'success' })
            navigate('/')
          }}
        >
          <LogOut className="size-4 flip-rtl" />
          {t('nav.logout')}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}

/* -------------------------------------------------------------------------- */

function MobileNavBody() {
  const { t, locale } = useI18n()
  const { isAuthenticated, home, logout } = useAuth()
  const { toast } = useToast()
  const navigate = useNavigate()
  const mainNav = useMainNav()
  const secondaryNav = useSecondaryNav()

  return (
    <div className="flex min-h-0 flex-1 flex-col overflow-y-auto">
      <nav className="flex-1 space-y-1 p-4" aria-label={t('nav.dashboard')}>
        {mainNav.map((item) => {
          const Icon = NAV_ICONS[item.href] ?? BookOpen
          return (
            <NavLink
              key={item.href}
              to={item.href}
              className={({ isActive }) =>
                cn(
                  'flex items-start gap-3 rounded-xl border px-3.5 py-3 transition-colors',
                  isActive ? 'border-primary/25 bg-primary/8' : 'border-transparent hover:bg-muted',
                )
              }
            >
              <span className="mt-0.5 grid size-9 shrink-0 place-items-center rounded-lg border border-border bg-card text-primary">
                <Icon className="size-[1.125rem]" />
              </span>
              <span className="min-w-0">
                <span className="block font-display text-body-sm font-semibold text-foreground">
                  {item.label}
                </span>
                {item.description && (
                  <span className="block text-caption text-muted-foreground">
                    {item.description}
                  </span>
                )}
              </span>
            </NavLink>
          )
        })}

        <div className="rule-fade my-4" />

        {secondaryNav.map((item) => (
          <NavLink
            key={item.href}
            to={item.href}
            className="block rounded-xl px-3.5 py-2.5 text-body-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
          >
            {item.label}
          </NavLink>
        ))}

        <div className="rule-fade my-4" />

        <div className="flex items-center justify-between gap-3 px-1">
          <span className="text-body-sm text-muted-foreground">{t('nav.language')}</span>
          <LanguageToggle variant="inline" />
        </div>
      </nav>

      <div className="safe-bottom sticky bottom-0 space-y-2 border-t border-border bg-card p-4">
        {isAuthenticated ? (
          <>
            <Button asChild block>
              <Link to={home}>
                <LayoutDashboard className="size-4" />
                {t('nav.dashboard')}
              </Link>
            </Button>
            <Button
              variant="outline"
              block
              onClick={async () => {
                await logout()
                toast({ title: t('auth.logoutDone'), variant: 'success' })
                navigate('/')
              }}
            >
              <LogOut className="size-4 flip-rtl" />
              {t('nav.logout')}
            </Button>
          </>
        ) : (
          <>
            <Button asChild block>
              <Link to="/register">
                {locale === 'ar-EG' ? 'ابدأ كمدرّس مجانًا' : 'Start free as a teacher'}
              </Link>
            </Button>
            <Button asChild variant="outline" block>
              <Link to="/login">{t('nav.login')}</Link>
            </Button>
          </>
        )}
      </div>
    </div>
  )
}
