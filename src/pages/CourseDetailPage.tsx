import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  BadgeCheck,
  Clock,
  Heart,
  Lock,
  PlayCircle,
  ShieldCheck,
  Sparkles,
  Users,
} from 'lucide-react'
import { coursesApi, enrollmentsApi } from '@/api/endpoints/catalog'
import type { CourseLessonDto } from '@/api/schema'
import { isApiError } from '@/api/errors'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useFavoriteCourses } from '@/hooks/use-favorites'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn, formatCompactNumber } from '@/lib/utils'
import { pricingToBreakdown } from '@/lib/pricing'
import { avatarTraitsFor, coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Progress } from '@/components/ui/primitives'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { CoverImage } from '@/components/illustrations/CoverArt'
import { Rating } from '@/components/common/Rating'
import { PriceTag } from '@/components/common/pricing-bits'
import { Breadcrumbs, ShareButton } from '@/components/common/controls'
import { PanelHeading } from '@/components/common/SectionHeading'
import { Reveal, RevealGroup, RevealItem } from '@/components/common/Reveal'
import { ReviewCard } from '@/components/common/ReviewCard'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { CardGridSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { CourseCard } from '@/components/courses/CourseCard'
import NotFoundPage from '@/pages/NotFoundPage'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function CourseDetailPage() {
  const { courseId = '' } = useParams()
  const navigate = useNavigate()
  const { t, locale, pick, formatNumber } = useI18n()
  const { isAuthenticated, hasRole } = useAuth()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()
  const favorites = useFavoriteCourses()

  const course = useQuery({
    queryKey: qk.courses.details(courseId),
    queryFn: () => coursesApi.details(courseId),
    enabled: Boolean(courseId),
    retry: false,
  })

  const reviews = useQuery({
    queryKey: qk.courses.reviews(courseId, { PageSize: 6 }),
    queryFn: () => coursesApi.reviews(courseId, { PageSize: 6 }),
    enabled: Boolean(courseId),
  })

  const data = course.data

  const related = useQuery({
    queryKey: qk.courses.list({ SubjectId: data?.subject?.id, PageSize: 3 }),
    queryFn: () => coursesApi.list({ SubjectId: data?.subject?.id, PageSize: 4 }),
    enabled: Boolean(data?.subject?.id),
  })

  usePageTitle(data?.title ?? t('common.course'), data?.description ?? undefined)

  const enroll = useMutation({
    mutationFn: () => enrollmentsApi.enrollInCourse({ courseId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.courses.details(courseId) })
      queryClient.invalidateQueries({ queryKey: ['student'] })
      toast({ title: t('courses.enrolled'), variant: 'success' })
      navigate(`/student/courses/${courseId}`)
    },
    onError: (error) =>
      showApiError(error),
  })

  if (course.isPending) {
    return (
      <div className="container-page space-y-8 py-12">
        <div className="h-72 animate-pulse rounded-2xl bg-muted" />
        <CardGridSkeleton count={3} />
      </div>
    )
  }

  if (course.isError) {
    if (isApiError(course.error) && course.error.kind === 'notFound') return <NotFoundPage />
    return (
      <div className="container-page py-16">
        <QueryError error={course.error} onRetry={() => course.refetch()} />
      </div>
    )
  }

  const details = data!
  const price = pricingToBreakdown(details.pricing)
  const subjectKey = subjectKeyFor(details.subject)
  const sections = details.sections ?? []
  const allLessons = sections.flatMap((s) => s.lessons ?? [])
  const previewLesson = allLessons.find((l) => l.isPreview)
  const hours = Math.round(((details.durationInMinutes ?? 0) / 60) * 10) / 10
  const saved = favorites.isSaved(courseId)
  const canEnroll = isAuthenticated && hasRole('student') && !details.isEnrolled
  const relatedItems = (related.data?.items ?? []).filter((c) => c.id !== courseId).slice(0, 3)

  return (
    <>
      {/* ------------------------------- Hero ------------------------------- */}
      <section className="relative overflow-hidden border-b border-border bg-primary text-primary-foreground">
        <div className="absolute inset-0 opacity-30">
          <CoverImage
            src={imageUrl(details.thumbnailUrl)}
            pattern={coverPatternFor(courseId)}
            subject={subjectKey}
            intensity="bold"
          />
        </div>
        <div className="absolute inset-0 bg-gradient-to-t from-primary via-primary/85 to-primary/70" />

        <div className="container-page relative py-12 lg:py-16">
          <Breadcrumbs
            items={[
              { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
              { label: t('nav.courses'), href: '/courses' },
              { label: details.title ?? '' },
            ]}
            className="mb-5 [&_*]:text-primary-foreground/70"
          />

          <div className="grid gap-10 lg:grid-cols-[1fr,22rem]">
            <div className="min-w-0">
              <div className="flex flex-wrap gap-2">
                {details.subject && (
                  <Badge variant="gold" size="md">
                    {pick(details.subject.nameAr, details.subject.nameEn)}
                  </Badge>
                )}
                {details.grade && (
                  <Badge
                    variant="outline"
                    size="md"
                    className="border-primary-foreground/25 text-primary-foreground"
                  >
                    {pick(details.grade.nameAr, details.grade.nameEn)}
                  </Badge>
                )}
                {details.isFeatured && (
                  <Badge variant="gold" size="md">
                    <Sparkles className="size-3.5" />
                    {locale === 'ar-EG' ? 'الأكثر طلبًا' : 'Popular'}
                  </Badge>
                )}
              </div>

              <h1 className="mt-4 text-display-xl text-primary-foreground">{details.title}</h1>

              {details.description && (
                <p className="mt-4 max-w-2xl text-body-lg leading-[1.9] text-primary-foreground/80">
                  {details.description}
                </p>
              )}

              <div className="mt-6 flex flex-wrap items-center gap-x-6 gap-y-3 text-body-sm text-primary-foreground/80">
                <Rating value={details.rating ?? 0} count={details.reviewCount} size="md" />
                <span className="inline-flex items-center gap-1.5">
                  <Users className="size-4" />
                  <span className="tnum">{formatCompactNumber(details.studentCount ?? 0)}</span>{' '}
                  {t('common.students')}
                </span>
                <span className="tnum inline-flex items-center gap-1.5">
                  <PlayCircle className="size-4" />
                  {formatNumber(details.lessonsCount ?? 0)}{' '}
                  {locale === 'ar-EG' ? 'درس' : 'lessons'}
                </span>
                {hours > 0 && (
                  <span className="tnum inline-flex items-center gap-1.5">
                    <Clock className="size-4" />
                    {hours} {locale === 'ar-EG' ? 'ساعة' : 'hours'}
                  </span>
                )}
              </div>

              {details.teacher?.id && (
                <Link
                  to={`/teachers/${details.teacher.id}`}
                  className="mt-6 inline-flex items-center gap-3 rounded-xl border border-primary-foreground/15 bg-primary-foreground/8 p-3 pe-5 transition-colors hover:bg-primary-foreground/12"
                >
                  <Avatar
                    traits={avatarTraitsFor(details.teacher.id)}
                    seed={details.teacher.id}
                    name={details.teacher.displayName ?? ''}
                    src={imageUrl(details.teacher.profileImageUrl)}
                    size="md"
                  />
                  <span className="min-w-0">
                    <span className="flex items-center gap-1.5 font-display text-body-sm font-bold text-primary-foreground">
                      {details.teacher.displayName}
                      {details.teacher.isVerified && <BadgeCheck className="size-4 text-accent-strong" />}
                    </span>
                    <span className="block truncate text-caption text-primary-foreground/60">
                      {details.teacher.headline}
                    </span>
                  </span>
                </Link>
              )}
            </div>

            {/* ------------------------- Purchase card ------------------------ */}
            <div className="lg:-mb-24">
              <Reveal>
                <Card className="overflow-hidden text-foreground shadow-lift">
                  <div className="relative aspect-video overflow-hidden">
                    <CoverImage
                      src={imageUrl(details.thumbnailUrl)}
                      pattern={coverPatternFor(courseId)}
                      subject={subjectKey}
                      intensity="bold"
                    />
                    {previewLesson && (
                      <button
                        type="button"
                        className="absolute inset-0 grid place-items-center bg-black/25 transition-colors hover:bg-black/35"
                        onClick={() =>
                          document
                            .getElementById('curriculum')
                            ?.scrollIntoView({ behavior: 'smooth' })
                        }
                      >
                        <span className="grid size-14 place-items-center rounded-full border border-white/30 bg-black/40 text-white backdrop-blur-sm">
                          <PlayCircle className="size-7" />
                        </span>
                      </button>
                    )}
                  </div>

                  <div className="space-y-5 p-5">
                    <PriceTag
                      breakdown={price}
                      size="lg"
                      currency={details.pricing?.currency}
                    />

                    {details.isEnrolled ? (
                      <>
                        <Progress value={details.progressPercentage ?? 0} className="h-2" />
                        <Button asChild size="lg" block>
                          <Link to={`/student/courses/${courseId}`}>
                            {(details.progressPercentage ?? 0) > 0
                              ? t('courses.continueCourse')
                              : t('courses.startCourse')}
                          </Link>
                        </Button>
                      </>
                    ) : canEnroll ? (
                      <Button
                        size="lg"
                        block
                        loading={enroll.isPending}
                        onClick={() => enroll.mutate()}
                      >
                        {enroll.isPending ? t('courses.enrolling') : t('courses.enroll')}
                      </Button>
                    ) : (
                      <Button asChild size="lg" block>
                        <Link to={isAuthenticated ? '/courses' : '/login'}>
                          {isAuthenticated ? t('common.details') : t('nav.login')}
                        </Link>
                      </Button>
                    )}

                    <div className="flex items-center gap-2">
                      {favorites.enabled && (
                        <Button
                          variant="outline"
                          block
                          className="flex-1"
                          aria-pressed={saved}
                          disabled={favorites.isToggling}
                          onClick={() => favorites.toggle(courseId)}
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
                      <ShareButton title={details.title ?? ''} />
                    </div>

                    <ul className="space-y-2.5 border-t border-border pt-4 text-body-sm text-muted-foreground">
                      <li className="flex items-center gap-2.5">
                        <PlayCircle className="size-4 shrink-0 text-primary" />
                        <span className="tnum">
                          {formatNumber(details.lessonsCount ?? 0)}{' '}
                          {locale === 'ar-EG' ? 'درس مسجّل' : 'recorded lessons'}
                        </span>
                      </li>
                      {hours > 0 && (
                        <li className="flex items-center gap-2.5">
                          <Clock className="size-4 shrink-0 text-primary" />
                          <span className="tnum">
                            {hours} {locale === 'ar-EG' ? 'ساعة محتوى' : 'hours of content'}
                          </span>
                        </li>
                      )}
                      <li className="flex items-center gap-2.5">
                        <ShieldCheck className="size-4 shrink-0 text-primary" />
                        {locale === 'ar-EG' ? 'وصول مدى الحياة' : 'Lifetime access'}
                      </li>
                    </ul>
                  </div>
                </Card>
              </Reveal>
            </div>
          </div>
        </div>
      </section>

      {/* ---------------------------- Curriculum ---------------------------- */}
      <section className="section-y !pt-12">
        <div className="container-page grid gap-10 lg:grid-cols-[1fr,22rem]">
          <div className="min-w-0 space-y-12">
            <div id="curriculum" className="scroll-mt-24">
              <PanelHeading
                title={t('courses.curriculum')}
                description={
                  sections.length
                    ? `${formatNumber(sections.length)} ${t('courses.sections')} · ${formatNumber(details.lessonsCount ?? 0)} ${t('courses.lessons')}`
                    : undefined
                }
              />

              <div className="mt-5">
                {sections.length === 0 ? (
                  <EmptyState spot="courses" compact title={t('courses.noLessons')} />
                ) : (
                  <Card className="overflow-hidden p-2">
                    <Accordion type="multiple" defaultValue={[sections[0]?.id ?? '']}>
                      {sections.map((section) => (
                        <AccordionItem
                          key={section.id}
                          value={section.id ?? ''}
                          className="border-b border-border last:border-b-0"
                        >
                          <AccordionTrigger className="px-3 hover:no-underline">
                            <span className="flex-1 text-start font-display text-body-sm font-bold text-foreground">
                              {section.title}
                            </span>
                            <span className="tnum me-2 shrink-0 text-caption text-muted-foreground">
                              {formatNumber(section.lessonsCount ?? section.lessons?.length ?? 0)}
                            </span>
                          </AccordionTrigger>
                          <AccordionContent className="px-1 pb-2">
                            <ul className="space-y-0.5">
                              {(section.lessons ?? []).map((lesson) => (
                                <LessonRow
                                  key={lesson.id}
                                  lesson={lesson}
                                  enrolled={Boolean(details.isEnrolled)}
                                  courseId={courseId}
                                />
                              ))}
                            </ul>
                          </AccordionContent>
                        </AccordionItem>
                      ))}
                    </Accordion>
                  </Card>
                )}
              </div>
            </div>

            {/* ------------------------- Locked notice ---------------------- */}
            {!details.isEnrolled && allLessons.some((l) => !l.isPreview) && (
              <motion.div
                initial={{ opacity: 0, y: 10 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true }}
                transition={transitions.base}
              >
                <Card className="flex flex-col items-center gap-4 border-accent/25 bg-accent/5 p-8 text-center">
                  <span className="grid size-14 place-items-center rounded-2xl bg-accent/15 text-accent-strong">
                    <Lock className="size-7" />
                  </span>
                  <div className="max-w-md space-y-2">
                    <h3 className="font-display text-heading-sm text-foreground">
                      {t('courses.lockedTitle')}
                    </h3>
                    <p className="text-body-sm text-muted-foreground">
                      {t('courses.lockedDescription')}
                    </p>
                  </div>
                  {canEnroll && (
                    <Button loading={enroll.isPending} onClick={() => enroll.mutate()}>
                      {t('courses.enroll')}
                    </Button>
                  )}
                </Card>
              </motion.div>
            )}

            {/* ----------------------------- Reviews ------------------------ */}
            <div id="reviews" className="scroll-mt-24">
              <PanelHeading title={t('common.reviews')} />
              <div className="mt-5">
                {reviews.isPending ? (
                  <RowSkeleton count={3} />
                ) : (reviews.data?.items ?? []).length === 0 ? (
                  <EmptyState
                    spot="inbox"
                    compact
                    title={locale === 'ar-EG' ? 'مفيش تقييمات لسه' : 'No reviews yet'}
                  />
                ) : (
                  <RevealGroup className="space-y-4">
                    {reviews.data!.items.map((review) => (
                      <RevealItem key={review.id}>
                        <ReviewCard review={review} />
                      </RevealItem>
                    ))}
                  </RevealGroup>
                )}
              </div>
            </div>
          </div>

          {/* ------------------------------ Sidebar --------------------------- */}
          <aside className="space-y-5">
            {details.teacher?.id && (
              <Reveal>
                <Card className="p-5">
                  <h3 className="font-display text-heading-sm text-foreground">
                    {t('common.teacher')}
                  </h3>
                  <Link
                    to={`/teachers/${details.teacher.id}`}
                    className="mt-4 flex items-center gap-3"
                  >
                    <Avatar
                      traits={avatarTraitsFor(details.teacher.id)}
                      seed={details.teacher.id}
                      name={details.teacher.displayName ?? ''}
                      src={imageUrl(details.teacher.profileImageUrl)}
                      size="lg"
                    />
                    <span className="min-w-0">
                      <span className="block truncate font-display text-body-sm font-bold text-foreground">
                        {details.teacher.displayName}
                      </span>
                      <span className="block truncate text-caption text-muted-foreground">
                        {details.teacher.headline}
                      </span>
                      <Rating
                        value={details.teacher.rating ?? 0}
                        count={details.teacher.reviewCount}
                        size="sm"
                        className="mt-1"
                      />
                    </span>
                  </Link>
                </Card>
              </Reveal>
            )}
          </aside>
        </div>
      </section>

      {/* ---------------------------- Related ------------------------------- */}
      {relatedItems.length > 0 && (
        <section className="section-y !pt-0">
          <div className="container-page">
            <PanelHeading
              title={locale === 'ar-EG' ? 'كورسات ممكن تعجبك' : 'You might also like'}
            />
            <RevealGroup className="mt-6 grid gap-5 sm:grid-cols-2 xl:grid-cols-3">
              {relatedItems.map((item) => (
                <RevealItem key={item.id} className="h-full">
                  <CourseCard course={item} />
                </RevealItem>
              ))}
            </RevealGroup>
          </div>
        </section>
      )}
    </>
  )
}

function LessonRow({
  lesson,
  enrolled,
  courseId,
}: {
  lesson: CourseLessonDto
  enrolled: boolean
  courseId: string
}) {
  const { t, locale, formatNumber } = useI18n()
  const unlocked = enrolled || lesson.isPreview

  const content = (
    <>
      <span
        className={cn(
          'shrink-0',
          unlocked ? 'text-primary' : 'text-muted-foreground/60',
        )}
      >
        {unlocked ? <PlayCircle className="size-4" /> : <Lock className="size-4" />}
      </span>
      <span className="min-w-0 flex-1 truncate text-body-sm text-foreground/90">
        {lesson.title}
      </span>
      {lesson.isPreview && !enrolled && (
        <Badge variant="accent" size="sm" className="shrink-0">
          {t('courses.previewAvailable')}
        </Badge>
      )}
      <span className="tnum shrink-0 text-caption text-muted-foreground">
        {formatNumber(lesson.durationInMinutes ?? 0)} {locale === 'ar-EG' ? 'د' : 'min'}
      </span>
    </>
  )

  if (enrolled) {
    return (
      <li>
        <Link
          to={`/student/courses/${courseId}`}
          className="flex items-center gap-2.5 rounded-lg px-3 py-2.5 transition-colors hover:bg-muted"
        >
          {content}
        </Link>
      </li>
    )
  }

  return (
    <li
      className={cn(
        'flex items-center gap-2.5 rounded-lg px-3 py-2.5',
        !unlocked && 'opacity-70',
      )}
    >
      {content}
    </li>
  )
}
