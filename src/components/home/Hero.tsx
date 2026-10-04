import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useQuery } from '@tanstack/react-query'
import {
  ArrowRight,
  BadgeCheck,
  BookOpen,
  CalendarDays,
  ClipboardCheck,
  MapPin,
  Sparkles,
  TrendingUp,
  Users,
} from 'lucide-react'
import { scheduleApi, teachersApi } from '@/api/endpoints/catalog'
import { qk } from '@/lib/query-client'
import { useI18n } from '@/i18n'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { AmbientGlow, CoverImage, PaperTexture } from '@/components/illustrations/CoverArt'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { Rating } from '@/components/common/Rating'
import { WhatsAppButton } from '@/components/whatsapp/WhatsAppButton'
import { formatCompactNumber } from '@/lib/utils'
import { avatarTraitsFor, coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { SITE } from '@/lib/constants'

const container = {
  hidden: {},
  visible: { transition: { staggerChildren: 0.08, delayChildren: 0.05 } },
}

const item = {
  hidden: { opacity: 0, y: 22 },
  visible: { opacity: 1, y: 0, transition: { duration: 0.55, ease: [0.22, 1, 0.36, 1] as const } },
}

export function Hero() {
  const { t, locale, formatNumber, formatRelativeDay, formatTime, pick } = useI18n()

  /* The hero showcases a real featured teacher when the platform has one. */
  const featured = useQuery({
    queryKey: qk.teachers.list({ PageSize: 5, Sort: 'featured' }),
    queryFn: () => teachersApi.list({ PageSize: 5, Sort: 'featured' }),
  })

  const upcoming = useQuery({
    queryKey: qk.schedule.upcoming,
    queryFn: () => scheduleApi.upcoming(1),
  })

  const teachers = featured.data?.items ?? []
  const hero = teachers[0]
  const socialProof = teachers.slice(1, 5)
  const nextSession = upcoming.data?.[0]
  const heroSubject = hero?.subjects?.[0]

  return (
    <section className="relative overflow-hidden pb-16 pt-10 sm:pb-24 sm:pt-16 lg:pb-28 lg:pt-20">
      <AmbientGlow />
      <PaperTexture className="opacity-40 dark:opacity-20" />

      <div className="container-page relative">
        <motion.div
          variants={container}
          initial="hidden"
          animate="visible"
          className="grid items-center gap-14 lg:grid-cols-[1.15fr,1fr] lg:gap-10 xl:gap-16"
        >
          {/* ---------------------------- Copy ---------------------------- */}
          <div className="max-w-2xl">
            <motion.div variants={item}>
              <span className="eyebrow">
                <span className="relative flex size-1.5">
                  <span className="absolute inline-flex size-full animate-ping rounded-full bg-accent opacity-75" />
                  <span className="relative inline-flex size-1.5 rounded-full bg-accent" />
                </span>
                {locale === 'ar-EG' ? 'منصة مصرية للمدرّسين' : 'An Egyptian platform for teachers'}
              </span>
            </motion.div>

            <motion.h1 variants={item} className="mt-6 text-display-2xl text-foreground">
              {locale === 'ar-EG' ? (
                <>
                  كل اللي محتاجه عشان تبني{' '}
                  <span className="relative inline-block">
                    <span className="text-gradient-gold">اسمك</span>
                    <Underline />
                  </span>{' '}
                  كمدرّس.
                </>
              ) : (
                <>
                  Everything you need to build your{' '}
                  <span className="relative inline-block">
                    <span className="text-gradient-gold">name</span>
                    <Underline />
                  </span>{' '}
                  as a teacher.
                </>
              )}
            </motion.h1>

            <motion.p variants={item} className="mt-6 max-w-xl text-body-lg text-muted-foreground">
              {locale === 'ar-EG'
                ? 'اعرض خبرتك، نظّم مجموعاتك، تابع حضور طلابك وواجباتهم وامتحاناتهم — كل تفاصيل شغلك التعليمي في مكان واحد.'
                : 'Show your experience, run your groups, and track attendance, homework, and exams — your whole practice in one place.'}
            </motion.p>

            <motion.div variants={item} className="mt-8 flex flex-wrap items-center gap-3">
              <Button asChild size="xl">
                <Link to="/register">
                  {locale === 'ar-EG' ? 'ابدأ مجانًا' : 'Start free'}
                  <ArrowRight className="size-5 flip-rtl" />
                </Link>
              </Button>
              <Button asChild size="xl" variant="outline">
                <Link to="/teachers">{t('nav.teachers')}</Link>
              </Button>
              <WhatsAppButton
                phone={SITE.phone}
                intent="send-inquiry"
                size="xl"
                variant="whatsapp-outline"
              >
                {locale === 'ar-EG' ? 'كلّمنا' : 'Talk to us'}
              </WhatsAppButton>
            </motion.div>

            {/* Social proof row — only once real teachers exist. */}
            {teachers.length > 0 && (
              <motion.div
                variants={item}
                className="mt-10 flex flex-wrap items-center gap-x-8 gap-y-5"
              >
                <div className="flex items-center gap-3">
                  <div className="flex -space-x-3 rtl:space-x-reverse">
                    {socialProof.map((teacher) => (
                      <Avatar
                        key={teacher.id}
                        traits={avatarTraitsFor(teacher.id ?? '')}
                        seed={teacher.id ?? ''}
                        name={teacher.displayName ?? ''}
                        src={imageUrl(teacher.profileImageUrl)}
                        size="sm"
                        className="ring-2 ring-background"
                      />
                    ))}
                  </div>
                  <div>
                    <p className="tnum font-display text-body-sm font-bold text-foreground">
                      {formatNumber(featured.data?.totalCount ?? 0)} {t('common.teachers')}
                    </p>
                    <p className="text-caption text-muted-foreground">
                      {locale === 'ar-EG' ? 'بنوا بروفايلهم على أستاذ' : 'built their profile on USTAZ'}
                    </p>
                  </div>
                </div>

                {hero && (hero.rating ?? 0) > 0 && (
                  <>
                    <div className="h-10 w-px bg-border max-sm:hidden" />
                    <div>
                      <Rating value={hero.rating ?? 0} variant="stars" size="sm" />
                      <p className="mt-1 text-caption text-muted-foreground">
                        {locale === 'ar-EG'
                          ? 'تقييمات من طلاب حقيقيين'
                          : 'ratings from real students'}
                      </p>
                    </div>
                  </>
                )}
              </motion.div>
            )}
          </div>

          {/* --------------------------- Visual --------------------------- */}
          {/* The chips are anchored to this box and the card is inset within it,
              so they break the card's edge instead of covering its content. */}
          <motion.div variants={item} className="relative mx-auto w-full max-w-md lg:max-w-none">
            <motion.div
              initial={{ opacity: 0, scale: 0.94, y: 28 }}
              animate={{ opacity: 1, scale: 1, y: 0 }}
              transition={{ ...transitions.slow, delay: 0.2 }}
              className="relative overflow-hidden rounded-2xl border border-border bg-card shadow-lift sm:mx-14 lg:mx-10 xl:mx-14"
            >
              {hero ? (
                <>
                  <div className="relative h-32">
                    <CoverImage
                      src={imageUrl(hero.coverImageUrl)}
                      pattern={coverPatternFor(hero.id ?? '')}
                      subject={subjectKeyFor(heroSubject)}
                      intensity="bold"
                    />
                    {hero.isFeatured && (
                      <div className="absolute inset-x-5 top-4 flex items-center justify-between">
                        <Badge variant="gold" size="sm" className="shadow-soft">
                          <Sparkles className="size-3" />
                          {locale === 'ar-EG' ? 'مدرّس مميّز' : 'Featured teacher'}
                        </Badge>
                      </div>
                    )}
                  </div>

                  <div className="p-5 pt-0 sm:p-6 sm:pt-0">
                    <div className="-mt-11 flex items-end justify-between gap-3">
                      <Avatar
                        traits={avatarTraitsFor(hero.id ?? '')}
                        seed={hero.id ?? ''}
                        name={hero.displayName ?? ''}
                        src={imageUrl(hero.profileImageUrl)}
                        size="2xl"
                        className="ring-4 ring-card"
                      />
                      <Rating
                        value={hero.rating ?? 0}
                        count={hero.reviewCount}
                        size="sm"
                        className="pb-1.5"
                      />
                    </div>

                    <h2 className="mt-4 flex items-center gap-2 font-display text-heading-md text-foreground">
                      {hero.displayName}
                      {hero.isVerified && <BadgeCheck className="size-5 shrink-0 text-primary" />}
                    </h2>
                    {hero.headline && (
                      <p className="mt-1 line-clamp-2-rtl text-body-sm text-muted-foreground">
                        {hero.headline}
                      </p>
                    )}

                    <div className="mt-4 flex flex-wrap gap-1.5">
                      {heroSubject && (
                        <Badge variant="primary" size="sm">
                          {pick(heroSubject.nameAr, heroSubject.nameEn)}
                        </Badge>
                      )}
                      {hero.city && (
                        <Badge variant="outline" size="sm">
                          <MapPin className="size-3" />
                          {hero.city}
                        </Badge>
                      )}
                    </div>

                    <dl className="mt-5 grid grid-cols-3 gap-2 rounded-xl border border-border bg-muted/45 p-3.5 text-center">
                      <div>
                        <dt className="text-[0.6875rem] text-muted-foreground">
                          {locale === 'ar-EG' ? 'سنة خبرة' : 'Years'}
                        </dt>
                        <dd className="tnum font-display text-body font-extrabold text-foreground">
                          {formatNumber(hero.yearsOfExperience ?? 0)}
                        </dd>
                      </div>
                      <div className="border-x border-border">
                        <dt className="text-[0.6875rem] text-muted-foreground">
                          {t('common.students')}
                        </dt>
                        <dd className="tnum font-display text-body font-extrabold text-foreground">
                          {formatCompactNumber(hero.studentsCount ?? 0)}
                        </dd>
                      </div>
                      <div>
                        <dt className="text-[0.6875rem] text-muted-foreground">
                          {t('common.groups')}
                        </dt>
                        <dd className="tnum font-display text-body font-extrabold text-foreground">
                          {formatNumber(hero.groupsCount ?? 0)}
                        </dd>
                      </div>
                    </dl>

                    <div className="mt-5 flex gap-2">
                      <Button asChild block className="flex-1">
                        <Link to={`/teachers/${hero.id}`}>
                          {locale === 'ar-EG' ? 'شوف البروفايل' : 'View profile'}
                        </Link>
                      </Button>
                      {hero.whatsAppLink && (
                        <WhatsAppButton href={hero.whatsAppLink} size="md">
                          {locale === 'ar-EG' ? 'واتساب' : 'WhatsApp'}
                        </WhatsAppButton>
                      )}
                    </div>
                  </div>
                </>
              ) : (
                /* Nothing published yet — show what a workspace gives a teacher. */
                <FeaturePreview />
              )}
            </motion.div>

            {/* Floating: next class */}
            {nextSession && (
              <motion.div
                initial={{ opacity: 0, x: -24, y: 12 }}
                animate={{ opacity: 1, x: 0, y: 0 }}
                transition={{ ...transitions.slow, delay: 0.55 }}
                className="absolute -end-1 top-10 hidden w-56 sm:block lg:-end-2 xl:-end-4"
              >
                <div className="animate-float-slow rounded-xl border border-border bg-card/95 p-3.5 shadow-lift backdrop-blur-sm">
                  <p className="flex items-center gap-1.5 text-[0.6875rem] font-semibold uppercase tracking-wide text-muted-foreground">
                    <CalendarDays className="size-3.5" aria-hidden />
                    {locale === 'ar-EG' ? 'الحصة الجاية' : 'Next class'}
                  </p>
                  <div className="mt-2.5 flex items-center gap-2.5">
                    <SubjectTile
                      subject={subjectKeyFor({
                        nameAr: nextSession.subjectNameAr,
                        nameEn: nextSession.subjectNameEn,
                      })}
                      size="sm"
                    />
                    <div className="min-w-0">
                      <p className="truncate font-display text-caption font-bold text-foreground">
                        {nextSession.groupName}
                      </p>
                      <p className="tnum truncate text-[0.6875rem] text-muted-foreground">
                        {nextSession.nextOccurrenceUtc
                          ? formatRelativeDay(nextSession.nextOccurrenceUtc)
                          : nextSession.dayName}
                        {nextSession.startTime ? ` · ${formatTime(nextSession.startTime)}` : ''}
                      </p>
                    </div>
                  </div>
                </div>
              </motion.div>
            )}

          </motion.div>
        </motion.div>
      </div>
    </section>
  )
}

function Underline() {
  return (
    <svg
      className="absolute -bottom-1 start-0 h-3 w-full text-accent/45"
      viewBox="0 0 200 12"
      preserveAspectRatio="none"
      aria-hidden
    >
      <path
        d="M2 8C40 3 80 2 118 5s60 4 80 2"
        stroke="currentColor"
        strokeWidth="3"
        strokeLinecap="round"
        fill="none"
      />
    </svg>
  )
}

/** Shown before any teacher is published — the product, not a fake profile. */
function FeaturePreview() {
  const { locale } = useI18n()

  const features =
    locale === 'ar-EG'
      ? [
          { icon: Users, title: 'مجموعاتك', body: 'مواعيد وأسعار وسعة وأماكن متاحة' },
          { icon: ClipboardCheck, title: 'الحضور والواجبات', body: 'سجّل وتابع من غير ورق' },
          { icon: BookOpen, title: 'الامتحانات', body: 'أونلاين بتصحيح تلقائي' },
          { icon: TrendingUp, title: 'الإيرادات', body: 'تعرف بالظبط إنت واصل لفين' },
        ]
      : [
          { icon: Users, title: 'Your groups', body: 'Times, pricing, capacity, seats left' },
          { icon: ClipboardCheck, title: 'Attendance & homework', body: 'Tracked without paper' },
          { icon: BookOpen, title: 'Exams', body: 'Online, graded automatically' },
          { icon: TrendingUp, title: 'Revenue', body: 'Know exactly where you stand' },
        ]

  return (
    <div className="p-6">
      <span className="eyebrow">{locale === 'ar-EG' ? 'مساحة عملك' : 'Your workspace'}</span>
      <ul className="mt-5 space-y-3">
        {features.map((feature) => (
          <li
            key={feature.title}
            className="flex items-start gap-3 rounded-xl border border-border bg-surface p-3.5"
          >
            <span className="grid size-10 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
              <feature.icon className="size-5" />
            </span>
            <span className="min-w-0">
              <span className="block font-display text-body-sm font-bold text-foreground">
                {feature.title}
              </span>
              <span className="block text-caption text-muted-foreground">{feature.body}</span>
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}
