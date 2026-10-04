import * as React from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQueries, useQuery } from '@tanstack/react-query'
import {
  Award,
  BadgeCheck,
  Clock,
  Facebook,
  Globe,
  Heart,
  Instagram,
  MapPin,
  Send,
  Sparkles,
  Star,
  Target,
  Trophy,
  Users,
  Youtube,
} from 'lucide-react'
import { teachersApi } from '@/api/endpoints/catalog'
import type { TeacherAchievementDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useFavoriteTeachers } from '@/hooks/use-favorites'
import { useI18n } from '@/i18n'
import { cn, formatCompactNumber, hashSeed } from '@/lib/utils'
import { avatarTraitsFor, coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { WEEK_ORDER, dayLabel, slotsByDay } from '@/lib/schedule'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Separator } from '@/components/ui/primitives'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { CoverImage, GoldRule } from '@/components/illustrations/CoverArt'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { Rating } from '@/components/common/Rating'
import { Breadcrumbs, ShareButton } from '@/components/common/controls'
import { PanelHeading } from '@/components/common/SectionHeading'
import { Reveal, RevealGroup, RevealItem } from '@/components/common/Reveal'
import { ReviewCard } from '@/components/common/ReviewCard'
import { GroupCard } from '@/components/groups/GroupCard'
import { CourseRow } from '@/components/courses/CourseCard'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { CardGridSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { WhatsAppButton, FloatingWhatsApp } from '@/components/whatsapp/WhatsAppButton'
import NotFoundPage from '@/pages/NotFoundPage'
import { isApiError } from '@/api/errors'

const ACHIEVEMENT_ICONS: Record<string, typeof Trophy> = {
  trophy: Trophy,
  star: Star,
  users: Users,
  award: Award,
  sparkles: Sparkles,
  target: Target,
}

const SOCIAL_ICONS: Record<string, typeof Facebook> = {
  Facebook,
  Instagram,
  YouTube: Youtube,
  Youtube,
  Telegram: Send,
  Website: Globe,
}

export default function TeacherProfilePage() {
  const { teacherId = '' } = useParams()
  const { t, locale, pick, formatNumber } = useI18n()
  const favorites = useFavoriteTeachers()
  const [activeSection, setActiveSection] = React.useState('about')

  const SECTIONS = React.useMemo(
    () => [
      { id: 'about', label: locale === 'ar-EG' ? 'عن المدرّس' : 'About' },
      { id: 'groups', label: t('common.groups') },
      { id: 'courses', label: t('common.courses') },
      { id: 'schedule', label: t('nav.schedule') },
      { id: 'reviews', label: t('common.reviews') },
    ],
    [locale, t],
  )

  const teacher = useQuery({
    queryKey: qk.teachers.details(teacherId),
    queryFn: () => teachersApi.details(teacherId),
    enabled: Boolean(teacherId),
    retry: false,
  })

  const [groups, courses, schedule, reviews] = useQueries({
    queries: [
      {
        queryKey: qk.teachers.groups(teacherId, { PageSize: 12 }),
        queryFn: () => teachersApi.groups(teacherId, { PageSize: 12 }),
        enabled: Boolean(teacherId),
      },
      {
        queryKey: qk.teachers.courses(teacherId, { PageSize: 12 }),
        queryFn: () => teachersApi.courses(teacherId, { PageSize: 12 }),
        enabled: Boolean(teacherId),
      },
      {
        queryKey: qk.teachers.schedule(teacherId),
        queryFn: () => teachersApi.schedule(teacherId),
        enabled: Boolean(teacherId),
      },
      {
        queryKey: qk.teachers.reviews(teacherId, { PageSize: 6 }),
        queryFn: () => teachersApi.reviews(teacherId, { PageSize: 6 }),
        enabled: Boolean(teacherId),
      },
    ],
  })

  const data = teacher.data

  usePageTitle(
    data ? `${data.displayName ?? ''}${data.headline ? ` — ${data.headline}` : ''}` : t('common.teacher'),
    data?.headline ?? undefined,
  )

  /* Highlights whichever profile section is currently in view. */
  React.useEffect(() => {
    if (!data) return
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0]
        if (visible) setActiveSection(visible.target.id)
      },
      { rootMargin: '-140px 0px -60% 0px', threshold: [0.1, 0.4] },
    )
    SECTIONS.forEach((s) => {
      const el = document.getElementById(s.id)
      if (el) observer.observe(el)
    })
    return () => observer.disconnect()
  }, [data, SECTIONS])

  if (teacher.isPending) {
    return (
      <div className="container-page space-y-8 py-12">
        <div className="h-56 animate-pulse rounded-2xl bg-muted" />
        <CardGridSkeleton count={3} />
      </div>
    )
  }

  if (teacher.isError) {
    if (isApiError(teacher.error) && teacher.error.kind === 'notFound') return <NotFoundPage />
    return (
      <div className="container-page py-16">
        <QueryError error={teacher.error} onRetry={() => teacher.refetch()} />
      </div>
    )
  }

  const profile = data!
  const primarySubject = profile.subjects?.[0]
  const subjectKey = subjectKeyFor(primarySubject)
  const saved = favorites.isSaved(teacherId)
  const groupItems = groups.data?.items ?? []
  const courseItems = courses.data?.items ?? []
  const slots = schedule.data ?? []
  const reviewItems = reviews.data?.items ?? []
  const byDay = slotsByDay(slots)

  return (
    <>
      {/* ------------------------------- Hero ------------------------------- */}
      <section className="relative">
        <div className="relative h-48 overflow-hidden sm:h-64">
          <CoverImage
            src={imageUrl(profile.coverImageUrl)}
            pattern={coverPatternFor(teacherId)}
            subject={subjectKey}
            intensity="bold"
          />
          <div className="absolute inset-0 bg-gradient-to-t from-background via-background/40 to-transparent" />
        </div>

        <div className="container-page">
          <div className="-mt-20 flex flex-col gap-6 sm:-mt-24 lg:flex-row lg:items-end">
            <Avatar
              traits={avatarTraitsFor(teacherId)}
              seed={teacherId}
              name={profile.displayName ?? ''}
              src={imageUrl(profile.profileImageUrl)}
              size="3xl"
              className="shrink-0 ring-4 ring-background"
            />

            <div className="min-w-0 flex-1 pb-2">
              <Breadcrumbs
                items={[
                  { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
                  { label: t('nav.teachers'), href: '/teachers' },
                  { label: profile.displayName ?? '' },
                ]}
                className="mb-3"
              />

              <h1 className="flex flex-wrap items-center gap-2.5 text-display-lg text-foreground">
                {profile.displayName}
                {profile.isVerified && (
                  <BadgeCheck className="size-7 shrink-0 text-primary" aria-label="verified" />
                )}
                {profile.isFeatured && (
                  <Badge variant="gold" size="md">
                    <Sparkles className="size-3.5" />
                    {locale === 'ar-EG' ? 'مميّز' : 'Featured'}
                  </Badge>
                )}
              </h1>

              {profile.headline && (
                <p className="mt-2 text-body-lg text-muted-foreground">{profile.headline}</p>
              )}

              <div className="mt-4 flex flex-wrap items-center gap-x-5 gap-y-2 text-body-sm text-muted-foreground">
                <Rating value={profile.rating ?? 0} count={profile.reviewCount} variant="stars" />
                {profile.city && (
                  <span className="inline-flex items-center gap-1.5">
                    <MapPin className="size-4" />
                    {profile.city}
                  </span>
                )}
                <span className="inline-flex items-center gap-1.5">
                  <Users className="size-4" />
                  <span className="tnum">{formatCompactNumber(profile.studentsCount ?? 0)}</span>{' '}
                  {t('common.students')}
                </span>
                <span className="tnum inline-flex items-center gap-1.5">
                  <Clock className="size-4" />
                  {formatNumber(profile.yearsOfExperience ?? 0)}{' '}
                  {locale === 'ar-EG' ? 'سنة خبرة' : 'years'}
                </span>
              </div>
            </div>

            <div className="flex shrink-0 flex-wrap items-center gap-2.5 pb-2">
              {favorites.enabled && (
                <Button
                  variant="outline"
                  size="lg"
                  aria-pressed={saved}
                  disabled={favorites.isToggling}
                  onClick={() => favorites.toggle(teacherId)}
                >
                  <Heart className={cn('size-4', saved && 'fill-accent text-accent')} />
                  {saved
                    ? locale === 'ar-EG'
                      ? 'محفوظ'
                      : 'Saved'
                    : locale === 'ar-EG'
                      ? 'احفظ'
                      : 'Save'}
                </Button>
              )}
              <ShareButton
                title={profile.displayName ?? ''}
                text={profile.headline ?? ''}
              />
              {profile.whatsAppLink && (
                <WhatsAppButton href={profile.whatsAppLink} size="lg">
                  {locale === 'ar-EG' ? 'كلّمه على واتساب' : 'Message on WhatsApp'}
                </WhatsAppButton>
              )}
            </div>
          </div>
        </div>
      </section>

      {/* ---------------------------- Section nav --------------------------- */}
      <nav className="sticky top-16 z-20 mt-8 border-y border-border bg-background/90 backdrop-blur-xl">
        <div className="container-page">
          <ul className="no-scrollbar flex gap-1 overflow-x-auto">
            {SECTIONS.map((section) => (
              <li key={section.id}>
                <a
                  href={`#${section.id}`}
                  className={cn(
                    'relative block whitespace-nowrap px-4 py-3.5 font-display text-body-sm font-semibold transition-colors',
                    activeSection === section.id
                      ? 'text-primary'
                      : 'text-muted-foreground hover:text-foreground',
                  )}
                >
                  {section.label}
                  {activeSection === section.id && (
                    <span className="absolute inset-x-3 bottom-0 h-0.5 rounded-full bg-primary" />
                  )}
                </a>
              </li>
            ))}
          </ul>
        </div>
      </nav>

      <div className="container-page grid gap-10 py-10 lg:grid-cols-[1fr,20rem]">
        <div className="min-w-0 space-y-12">
          {/* ------------------------------ About ----------------------------- */}
          <section id="about" className="scroll-mt-32">
            <PanelHeading title={locale === 'ar-EG' ? 'عن المدرّس' : 'About'} />
            {profile.bio ? (
              <p className="mt-4 whitespace-pre-wrap text-body leading-[1.9] text-muted-foreground">
                {profile.bio}
              </p>
            ) : (
              <p className="mt-4 text-body-sm text-muted-foreground">
                {locale === 'ar-EG'
                  ? 'المدرّس لسه مكتبش نبذة عن نفسه.'
                  : 'This teacher has not written a bio yet.'}
              </p>
            )}

            {(profile.subjects?.length ?? 0) > 0 && (
              <>
                <GoldRule className="my-8 max-w-xs" />
                <h3 className="font-display text-heading-sm text-foreground">
                  {locale === 'ar-EG' ? 'المواد والصفوف' : 'Subjects & grades'}
                </h3>
                <div className="mt-4 flex flex-wrap gap-2">
                  {profile.subjects!.map((subject) => (
                    <Badge key={subject.id} variant="primary" size="lg">
                      {pick(subject.nameAr, subject.nameEn)}
                    </Badge>
                  ))}
                  {(profile.grades ?? []).map((grade) => (
                    <Badge key={grade.id} variant="outline" size="lg">
                      {pick(grade.nameAr, grade.nameEn)}
                    </Badge>
                  ))}
                </div>
              </>
            )}

            {(profile.achievements?.length ?? 0) > 0 && (
              <>
                <GoldRule className="my-8 max-w-xs" />
                <h3 className="font-display text-heading-sm text-foreground">
                  {locale === 'ar-EG' ? 'إنجازات' : 'Achievements'}
                </h3>
                <RevealGroup className="mt-4 grid gap-4 sm:grid-cols-2">
                  {profile.achievements!.map((achievement) => (
                    <RevealItem key={achievement.id}>
                      <AchievementCard achievement={achievement} />
                    </RevealItem>
                  ))}
                </RevealGroup>
              </>
            )}
          </section>

          {/* ------------------------------ Groups ---------------------------- */}
          <section id="groups" className="scroll-mt-32">
            <PanelHeading
              title={t('common.groups')}
              action={
                groupItems.length > 0 ? (
                  <Button asChild variant="ghost" size="sm">
                    <Link to={`/groups?teacher=${teacherId}`}>{t('common.viewAll')}</Link>
                  </Button>
                ) : undefined
              }
            />
            <div className="mt-5">
              {groups.isPending ? (
                <CardGridSkeleton count={2} className="lg:grid-cols-2" />
              ) : groupItems.length === 0 ? (
                <EmptyState
                  spot="groups"
                  compact
                  title={locale === 'ar-EG' ? 'مفيش مجموعات متاحة' : 'No groups available'}
                  description={
                    locale === 'ar-EG'
                      ? 'كلّم المدرّس على واتساب وهو هيقولك أول ما تفتح مجموعة.'
                      : 'Message the teacher — they will let you know when a group opens.'
                  }
                />
              ) : (
                <RevealGroup className="grid gap-5 lg:grid-cols-2">
                  {groupItems.map((group) => (
                    <RevealItem key={group.id} className="h-full">
                      <GroupCard group={group} />
                    </RevealItem>
                  ))}
                </RevealGroup>
              )}
            </div>
          </section>

          {/* ------------------------------ Courses --------------------------- */}
          <section id="courses" className="scroll-mt-32">
            <PanelHeading title={t('common.courses')} />
            <div className="mt-5">
              {courses.isPending ? (
                <RowSkeleton count={3} />
              ) : courseItems.length === 0 ? (
                <EmptyState
                  spot="courses"
                  compact
                  title={locale === 'ar-EG' ? 'مفيش كورسات' : 'No courses yet'}
                />
              ) : (
                <RevealGroup className="space-y-3">
                  {courseItems.map((course) => (
                    <RevealItem key={course.id}>
                      <CourseRow course={course} />
                    </RevealItem>
                  ))}
                </RevealGroup>
              )}
            </div>
          </section>

          {/* ----------------------------- Schedule --------------------------- */}
          <section id="schedule" className="scroll-mt-32">
            <PanelHeading title={t('nav.schedule')} />
            <div className="mt-5">
              {schedule.isPending ? (
                <RowSkeleton count={3} />
              ) : slots.length === 0 ? (
                <EmptyState
                  spot="calendar"
                  compact
                  title={locale === 'ar-EG' ? 'مفيش مواعيد معلنة' : 'No published schedule'}
                />
              ) : (
                <div className="grid gap-3 sm:grid-cols-2">
                  {WEEK_ORDER.filter((day) => byDay[day].length > 0).map((day) => (
                    <Card key={day} className="p-4">
                      <p className="font-display text-body-sm font-bold text-foreground">
                        {dayLabel(day, locale)}
                      </p>
                      <ul className="mt-2.5 space-y-2">
                        {byDay[day].map((slot) => (
                          <li
                            key={slot.id}
                            className="flex items-center gap-2.5 text-body-sm text-muted-foreground"
                          >
                            <SubjectTile
                              subject={subjectKeyFor({
                                nameAr: slot.subjectNameAr,
                                nameEn: slot.subjectNameEn,
                              })}
                              size="sm"
                            />
                            <span className="min-w-0 flex-1 truncate">{slot.groupName}</span>
                            <span className="tnum shrink-0 text-caption">
                              {slot.startTime?.slice(0, 5)}
                            </span>
                          </li>
                        ))}
                      </ul>
                    </Card>
                  ))}
                </div>
              )}
            </div>
          </section>

          {/* ------------------------------ Reviews --------------------------- */}
          <section id="reviews" className="scroll-mt-32">
            <PanelHeading
              title={t('common.reviews')}
              description={
                profile.reviewCount
                  ? `${formatNumber(profile.reviewCount)} ${t('common.reviews')}`
                  : undefined
              }
            />
            <div className="mt-5">
              {reviews.isPending ? (
                <RowSkeleton count={3} />
              ) : reviewItems.length === 0 ? (
                <EmptyState
                  spot="inbox"
                  compact
                  title={locale === 'ar-EG' ? 'مفيش تقييمات لسه' : 'No reviews yet'}
                />
              ) : (
                <RevealGroup className="space-y-4">
                  {reviewItems.map((review) => (
                    <RevealItem key={review.id}>
                      <ReviewCard review={review} />
                    </RevealItem>
                  ))}
                </RevealGroup>
              )}
            </div>
          </section>
        </div>

        {/* ------------------------------ Sidebar ----------------------------- */}
        <aside className="space-y-5 lg:sticky lg:top-32 lg:h-fit">
          <Reveal>
            <Card className="p-5">
              <h3 className="font-display text-heading-sm text-foreground">
                {locale === 'ar-EG' ? 'نظرة سريعة' : 'At a glance'}
              </h3>
              <dl className="mt-4 space-y-3">
                <Stat
                  label={t('common.students')}
                  value={formatCompactNumber(profile.studentsCount ?? 0)}
                />
                <Separator />
                <Stat
                  label={t('common.groups')}
                  value={formatNumber(profile.groupsCount ?? 0)}
                />
                <Separator />
                <Stat
                  label={t('common.courses')}
                  value={formatNumber(profile.coursesCount ?? 0)}
                />
                <Separator />
                <Stat
                  label={t('common.rating')}
                  value={`${(profile.rating ?? 0).toFixed(1)} / 5`}
                />
              </dl>
            </Card>
          </Reveal>

          {(profile.socialLinks?.length ?? 0) > 0 && (
            <Reveal delay={0.05}>
              <Card className="p-5">
                <h3 className="font-display text-heading-sm text-foreground">
                  {locale === 'ar-EG' ? 'تابعه' : 'Follow'}
                </h3>
                <ul className="mt-4 space-y-2">
                  {profile.socialLinks!.map((link) => {
                    const Icon = SOCIAL_ICONS[link.platform ?? ''] ?? Globe
                    return (
                      <li key={link.id}>
                        <a
                          href={link.url}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="flex items-center gap-3 rounded-lg border border-border bg-surface p-3 text-body-sm transition-colors hover:border-foreground/20 hover:bg-muted"
                        >
                          <Icon className="size-4 shrink-0 text-primary" />
                          <span className="min-w-0 flex-1 truncate font-latin">
                            {link.platformName ?? link.platform}
                          </span>
                        </a>
                      </li>
                    )
                  })}
                </ul>
              </Card>
            </Reveal>
          )}
        </aside>
      </div>

      {profile.whatsAppLink && (
        <FloatingWhatsApp
          phone=""
          contactName={profile.displayName}
          contactRole={profile.headline ?? undefined}
        />
      )}
    </>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-body-sm text-muted-foreground">{label}</dt>
      <dd className="tnum font-display text-body-sm font-bold text-foreground">{value}</dd>
    </div>
  )
}

/** The API stores no icon, so pick a stable one from the achievement's id. */
const ACHIEVEMENT_ICON_KEYS = Object.keys(ACHIEVEMENT_ICONS)

function AchievementCard({ achievement }: { achievement: TeacherAchievementDto }) {
  const key = ACHIEVEMENT_ICON_KEYS[hashSeed(achievement.id ?? '') % ACHIEVEMENT_ICON_KEYS.length]
  const Icon = ACHIEVEMENT_ICONS[key] ?? Trophy

  return (
    <Card className="flex h-full gap-3.5 p-4">
      <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-accent/12 text-accent-strong">
        <Icon className="size-5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="font-display text-body-sm font-bold text-foreground">{achievement.title}</p>
        {achievement.description && (
          <p className="mt-1 text-caption leading-relaxed text-muted-foreground">
            {achievement.description}
          </p>
        )}
        {achievement.year && (
          <p className="tnum mt-1 text-[0.6875rem] text-muted-foreground/70">{achievement.year}</p>
        )}
      </div>
    </Card>
  )
}
