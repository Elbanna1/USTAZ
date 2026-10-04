import { Link } from 'react-router-dom'
import { ArrowRight, Facebook, Instagram, Linkedin, Mail, MapPin, Youtube } from 'lucide-react'
import { Logo } from '@/components/brand/Logo'
import { WhatsAppIcon } from '@/components/whatsapp/WhatsAppButton'
import { Button } from '@/components/ui/button'
import { SITE } from '@/lib/constants'
import { useSubjects } from '@/hooks/use-taxonomy'
import { buildWhatsAppUrl } from '@/lib/whatsapp'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'

type Bilingual = { ar: string; en: string }

interface FooterColumn {
  title: Bilingual
  links: { label: Bilingual; href: string }[]
}

/** Every href here is a route that exists — the footer is not a wish list. */
const COLUMNS: FooterColumn[] = [
  {
    title: { ar: 'المنصة', en: 'Platform' },
    links: [
      { label: { ar: 'المدرّسين', en: 'Teachers' }, href: '/teachers' },
      { label: { ar: 'المجموعات', en: 'Groups' }, href: '/groups' },
      { label: { ar: 'الكورسات', en: 'Courses' }, href: '/courses' },
      { label: { ar: 'الجدول الأسبوعي', en: 'Weekly schedule' }, href: '/schedule' },
      { label: { ar: 'الأسعار', en: 'Pricing' }, href: '/pricing' },
    ],
  },
  {
    title: { ar: 'للمدرّسين', en: 'For teachers' },
    links: [
      { label: { ar: 'ابدأ بروفايلك', en: 'Start your profile' }, href: '/register' },
      { label: { ar: 'لوحة تحكم المدرّس', en: 'Teacher console' }, href: '/teacher/dashboard' },
      { label: { ar: 'إدارة المجموعات', en: 'Manage groups' }, href: '/teacher/groups' },
      { label: { ar: 'الخصومات والعروض', en: 'Discounts' }, href: '/teacher/discounts' },
      { label: { ar: 'التقييمات', en: 'Reviews' }, href: '/teacher/reviews' },
    ],
  },
  {
    title: { ar: 'للطلاب', en: 'For students' },
    links: [
      { label: { ar: 'لوحة الطالب', en: 'Student console' }, href: '/student/dashboard' },
      { label: { ar: 'جدولي', en: 'My schedule' }, href: '/student/schedule' },
      { label: { ar: 'المفضّلة', en: 'Favourites' }, href: '/student/favorites' },
      { label: { ar: 'الشهادات', en: 'Certificates' }, href: '/student/certificates' },
      { label: { ar: 'تسجيل حساب', en: 'Create an account' }, href: '/register' },
    ],
  },
  {
    title: { ar: 'أستاذ', en: 'USTAZ' },
    links: [
      { label: { ar: 'عن المنصة', en: 'About' }, href: '/about' },
      { label: { ar: 'الأسئلة الشائعة', en: 'FAQ' }, href: '/faq' },
      { label: { ar: 'تواصل معنا', en: 'Contact' }, href: '/contact' },
      { label: { ar: 'تسجيل الدخول', en: 'Log in' }, href: '/login' },
    ],
  },
]

const SOCIALS = [
  { label: { ar: 'فيسبوك', en: 'Facebook' }, href: SITE.social.facebook, Icon: Facebook },
  { label: { ar: 'إنستجرام', en: 'Instagram' }, href: SITE.social.instagram, Icon: Instagram },
  { label: { ar: 'يوتيوب', en: 'YouTube' }, href: SITE.social.youtube, Icon: Youtube },
  { label: { ar: 'لينكدإن', en: 'LinkedIn' }, href: SITE.social.linkedin, Icon: Linkedin },
]

export function SiteFooter() {
  const subjects = useSubjects()
  const { isAuthenticated, home } = useAuth()
  const { locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const isAr = lang === 'ar'

  return (
    <footer className="relative mt-auto overflow-hidden border-t border-border bg-surface">
      <div className="pointer-events-none absolute inset-0 bg-dot-grid opacity-40" aria-hidden />

      <div className="container-page relative">
        {/* Closing call to action. There is no newsletter endpoint, so there is no
            newsletter form here — only destinations that actually exist. */}
        <div className="grid gap-8 border-b border-border py-12 lg:grid-cols-[1.1fr,1fr] lg:items-center lg:gap-16">
          <div>
            <h2 className="text-heading-lg text-foreground">
              {isAr
                ? 'خلي طلابك يلاقوك من غير ما يدوّروا كتير.'
                : 'Let your students find you without hunting for you.'}
            </h2>
            <p className="mt-3 max-w-lg text-body text-muted-foreground">
              {isAr
                ? 'ابدأ بروفايلك، ضيف مجموعاتك ومواعيدك، وخلي كل تفاصيل شغلك التعليمي في مكان واحد.'
                : 'Start your profile, add your groups and times, and keep every detail of your teaching in one place.'}
            </p>
          </div>

          <div className="flex flex-col gap-3 sm:flex-row lg:justify-end">
            <Button asChild size="lg" className="shrink-0">
              <Link to={isAuthenticated ? home : '/register'}>
                {isAuthenticated
                  ? isAr
                    ? 'روح للوحة التحكم'
                    : 'Go to your console'
                  : isAr
                    ? 'ابدأ مجانًا'
                    : 'Start free'}
                <ArrowRight className="size-4 flip-rtl" />
              </Link>
            </Button>
            <Button asChild variant="outline" size="lg" className="shrink-0">
              <Link to="/contact">{isAr ? 'كلّمنا' : 'Talk to us'}</Link>
            </Button>
          </div>
        </div>

        {/* Link columns */}
        <div className="grid gap-10 py-12 md:grid-cols-2 lg:grid-cols-[1.4fr,repeat(4,1fr)]">
          <div className="space-y-5">
            <Link to="/" aria-label={isAr ? 'أستاذ' : 'USTAZ'}>
              <Logo size="lg" />
            </Link>
            <p className="max-w-xs text-body-sm leading-relaxed text-muted-foreground">
              {isAr ? SITE.description : SITE.descriptionEn}
            </p>

            <ul className="space-y-2.5 text-body-sm text-muted-foreground">
              <li>
                <a
                  href={buildWhatsAppUrl({ phone: SITE.phone, intent: 'support' })}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-2.5 transition-colors hover:text-whatsapp"
                >
                  <WhatsAppIcon className="size-4 shrink-0" />
                  <span dir="ltr" className="tnum">
                    {SITE.phone}
                  </span>
                </a>
              </li>
              <li>
                <a
                  href={`mailto:${SITE.email}`}
                  className="inline-flex items-center gap-2.5 transition-colors hover:text-foreground"
                >
                  <Mail className="size-4 shrink-0" />
                  <span dir="ltr">{SITE.email}</span>
                </a>
              </li>
              <li className="inline-flex items-center gap-2.5">
                <MapPin className="size-4 shrink-0" />
                {isAr ? SITE.addressAr : SITE.addressEn}
              </li>
            </ul>

            <div className="flex items-center gap-2">
              {SOCIALS.map(({ label, href, Icon }) => (
                <a
                  key={label.en}
                  href={href}
                  target="_blank"
                  rel="noopener noreferrer"
                  aria-label={label[lang]}
                  className="grid size-9 place-items-center rounded-lg border border-border bg-card text-muted-foreground transition-all duration-200 hover:border-foreground/20 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                >
                  <Icon className="size-4" />
                </a>
              ))}
            </div>
          </div>

          {COLUMNS.map((col) => (
            <nav key={col.title.en} aria-label={col.title[lang]}>
              <h3 className="font-display text-body-sm font-bold text-foreground">
                {col.title[lang]}
              </h3>
              <ul className="mt-4 space-y-2.5">
                {col.links.map((link) => (
                  <li key={link.href + link.label.en}>
                    <Link
                      to={link.href}
                      className="link-underline text-body-sm text-muted-foreground transition-colors hover:text-foreground"
                    >
                      {link.label[lang]}
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
          ))}
        </div>

        {/* Subject cloud — driven by the live taxonomy, hidden when it is empty. */}
        {subjects.subjects.length > 0 && (
          <div className="border-t border-border py-8">
            <h3 className="font-display text-body-sm font-bold text-foreground">
              {isAr ? 'تصفّح حسب المادة' : 'Browse by subject'}
            </h3>
            <ul className="mt-4 flex flex-wrap gap-2">
              {subjects.subjects.map((subject) => (
                <li key={subject.id}>
                  <Link
                    to={`/teachers?subject=${subject.slug ?? subject.id}`}
                    className="inline-flex rounded-full border border-border bg-card px-3 py-1.5 text-caption text-muted-foreground transition-colors hover:border-foreground/20 hover:text-foreground"
                  >
                    {subjects.label(subject)}
                  </Link>
                </li>
              ))}
            </ul>
          </div>
        )}

        {/* Legal */}
        <div className="flex flex-col gap-4 border-t border-border py-7 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-caption text-muted-foreground">
            {isAr
              ? `© ${new Date().getFullYear()} أستاذ — Ustaz. كل الحقوق محفوظة.`
              : `© ${new Date().getFullYear()} USTAZ. All rights reserved.`}
          </p>
          <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-caption text-muted-foreground/80">
            <Link to="/faq" className="transition-colors hover:text-foreground">
              {isAr ? 'الأسئلة الشائعة' : 'FAQ'}
            </Link>
            <Link to="/contact" className="transition-colors hover:text-foreground">
              {isAr ? 'تواصل معنا' : 'Contact'}
            </Link>
            <Link to="/about" className="transition-colors hover:text-foreground">
              {isAr ? 'عن المنصة' : 'About'}
            </Link>
          </div>
        </div>
      </div>
    </footer>
  )
}
