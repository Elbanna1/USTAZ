import { Link, useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import {
  BadgeCheck,
  BookOpen,
  CalendarDays,
  Check,
  Clock,
  Download,
  Lock,
  MapPin,
  Monitor,
  Sparkles,
  Users,
} from 'lucide-react'
import { enrollmentsApi, groupsApi } from '@/api/endpoints/catalog'
import { resolveMediaUrl } from '@/api/config'
import { isApiError } from '@/api/errors'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { pricingToBreakdown } from '@/lib/pricing'
import { coverPatternFor, imageUrl, subjectKeyFor } from '@/lib/identity'
import { WEEK_ORDER, apiDayLabel, formatScheduleDays } from '@/lib/schedule'
import { transitions } from '@/lib/motion'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Card } from '@/components/ui/card'
import { Separator } from '@/components/ui/primitives'
import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/accordion'
import { CoverArt, GoldRule } from '@/components/illustrations/CoverArt'
import { SubjectChip } from '@/components/illustrations/SubjectIcon'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Rating } from '@/components/common/Rating'
import { Breadcrumbs, ShareButton } from '@/components/common/controls'
import { PanelHeading } from '@/components/common/SectionHeading'
import { Reveal, RevealGroup, RevealItem } from '@/components/common/Reveal'
import { PriceTag, SeatsMeter } from '@/components/common/pricing-bits'
import { ReviewCard } from '@/components/common/ReviewCard'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { CardGridSkeleton, RowSkeleton } from '@/components/ui/skeleton'
import { FloatingWhatsApp, WhatsAppButton } from '@/components/whatsapp/WhatsAppButton'
import { avatarTraitsFor } from '@/lib/identity'
import NotFoundPage from '@/pages/NotFoundPage'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const TYPE_LABELS: Record<string, { ar: string; en: string }> = {
  Regular: { ar: 'منتظمة', en: 'Regular' },
  Revision: { ar: 'مراجعة نهائية', en: 'Revision' },
  Intensive: { ar: 'مكثّفة', en: 'Intensive' },
  ExamPreparation: { ar: 'استعداد للامتحان', en: 'Exam prep' },
}

const MODE_LABELS: Record<string, { ar: string; en: string }> = {
  Online: { ar: 'أونلاين', en: 'Online' },
  Offline: { ar: 'حضوري', en: 'In person' },
  Hybrid: { ar: 'أونلاين وحضوري', en: 'Hybrid' },
}

export default function GroupDetailPage() {
  const { groupId = '' } = useParams()
  const navigate = useNavigate()
  const { t, locale, pick, formatNumber, formatDate, formatTime } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { isAuthenticated, hasRole } = useAuth()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const group = useQuery({
    queryKey: qk.groups.details(groupId),
    queryFn: () => groupsApi.details(groupId),
    enabled: Boolean(groupId),
    retry: false,
  })

  const [curriculum, materials, reviews] = useQueries({
    queries: [
      {
        queryKey: qk.groups.curriculum(groupId),
        queryFn: () => groupsApi.curriculum(groupId),
        enabled: Boolean(groupId),
      },
      {
        queryKey: qk.groups.materials(groupId),
        queryFn: () => groupsApi.materials(groupId),
        enabled: Boolean(groupId),
      },
      {
        queryKey: qk.groups.reviews(groupId, { PageSize: 6 }),
        queryFn: () => groupsApi.reviews(groupId, { PageSize: 6 }),
        enabled: Boolean(groupId),
      },
    ],
  })

  const data = group.data
  usePageTitle(data?.name ?? t('common.group'), data?.description ?? undefined)

  const enroll = useMutation({
    mutationFn: () => enrollmentsApi.enrollInGroup({ groupId }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.groups.details(groupId) })
      queryClient.invalidateQueries({ queryKey: ['student'] })
      toast({ title: t('courses.enrolled'), variant: 'success' })
      navigate('/student/courses')
    },
    onError: (error) =>
      showApiError(error),
  })

  if (group.isPending) {
    return (
      <div className="container-page space-y-8 py-12">
        <div className="h-72 animate-pulse rounded-2xl bg-muted" />
        <CardGridSkeleton count={3} />
      </div>
    )
  }

  if (group.isError) {
    if (isApiError(group.error) && group.error.kind === 'notFound') return <NotFoundPage />
    return (
      <div className="container-page py-16">
        <QueryError error={group.error} onRetry={() => group.refetch()} />
      </div>
    )
  }

  const details = data!
  const price = pricingToBreakdown(details.pricing)
  const subjectKey = subjectKeyFor(details.subject)
  const schedules = details.schedules ?? []
  const capacity = details.capacity ?? 0
  const taken = details.enrolledStudentsCount ?? 0
  const full = Boolean(details.isFull) || (capacity > 0 && taken >= capacity)
  const canEnroll = isAuthenticated && hasRole('student') && !details.isEnrolled && !full
  const curriculumItems = curriculum.data ?? []
  const materialItems = materials.data ?? []
  const reviewItems = reviews.data?.items ?? []

  return (
    <>
      {/* ------------------------------- Hero ------------------------------- */}
      <section className="relative overflow-hidden border-b border-border">
        <div className="absolute inset-0">
          <CoverArt pattern={coverPatternFor(groupId)} subject={subjectKey} intensity="bold" />
          <div className="absolute inset-0 bg-gradient-to-t from-background via-background/92 to-background/70" />
        </div>

        <div className="container-page relative py-12 lg:py-16">
          <Breadcrumbs
            items={[
              { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
              { label: t('nav.groups'), href: '/groups' },
              { label: details.name ?? '' },
            ]}
            className="mb-5"
          />

          <div className="grid gap-10 lg:grid-cols-[1fr,22rem]">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <SubjectChip subject={subjectKey} />
                {details.type && (
                  <Badge variant="primary" size="md">
                    {TYPE_LABELS[details.type]?.[lang] ?? details.typeName}
                  </Badge>
                )}
                {details.grade && (
                  <Badge variant="outline" size="md">
                    {pick(details.grade.nameAr, details.grade.nameEn)}
                  </Badge>
                )}
                {price.percentOff > 0 && (
                  <Badge variant="gold" size="md" className="tnum">
                    <Sparkles className="size-3.5" />
                    {locale === 'ar-EG'
                      ? `خصم ${price.percentOff}%`
                      : `${price.percentOff}% off`}
                  </Badge>
                )}
              </div>

              <h1 className="mt-4 text-display-xl text-foreground">{details.name}</h1>

              {details.description && (
                <p className="mt-4 max-w-2xl text-body-lg leading-[1.9] text-muted-foreground">
                  {details.description}
                </p>
              )}

              <div className="mt-6 flex flex-wrap items-center gap-x-6 gap-y-3 text-body-sm text-muted-foreground">
                <Rating value={details.rating ?? 0} count={details.reviewCount} size="md" />
                <span className="inline-flex items-center gap-1.5">
                  <Users className="size-4" />
                  <span className="tnum">
                    {formatNumber(taken)} / {formatNumber(capacity)}
                  </span>
                </span>
                {details.startDate && (
                  <span className="tnum inline-flex items-center gap-1.5">
                    <CalendarDays className="size-4" />
                    {formatDate(details.startDate, 'd MMMM yyyy')}
                  </span>
                )}
                <span className="inline-flex items-center gap-1.5">
                  {details.teachingMode === 'Online' ? (
                    <Monitor className="size-4" />
                  ) : (
                    <MapPin className="size-4" />
                  )}
                  {details.location ||
                    MODE_LABELS[details.teachingMode ?? '']?.[lang] ||
                    details.teachingModeName}
                </span>
              </div>

              {details.teacher?.id && (
                <Link
                  to={`/teachers/${details.teacher.id}`}
                  className="mt-6 inline-flex items-center gap-3 rounded-xl border border-border bg-card p-3 pe-5 transition-colors hover:border-foreground/20"
                >
                  <Avatar
                    traits={avatarTraitsFor(details.teacher.id)}
                    seed={details.teacher.id}
                    name={details.teacher.displayName ?? ''}
                    src={imageUrl(details.teacher.profileImageUrl)}
                    size="md"
                  />
                  <span className="min-w-0">
                    <span className="flex items-center gap-1.5 font-display text-body-sm font-bold text-foreground">
                      {details.teacher.displayName}
                      {details.teacher.isVerified && (
                        <BadgeCheck className="size-4 text-primary" />
                      )}
                    </span>
                    <span className="block truncate text-caption text-muted-foreground">
                      {details.teacher.headline}
                    </span>
                  </span>
                </Link>
              )}
            </div>

            {/* ------------------------ Enrollment card ---------------------- */}
            <div className="lg:-mb-24">
              <Reveal>
                <Card className="space-y-5 p-5 shadow-lift">
                  <PriceTag
                    breakdown={price}
                    period={locale === 'ar-EG' ? '/ شهريًا' : '/ month'}
                    size="lg"
                    currency={details.pricing?.currency}
                  />

                  {capacity > 0 && <SeatsMeter taken={taken} total={capacity} />}

                  {details.isEnrolled ? (
                    <Button asChild size="lg" block>
                      <Link to="/student/courses">{t('courses.enrolled')}</Link>
                    </Button>
                  ) : canEnroll ? (
                    <Button
                      size="lg"
                      block
                      loading={enroll.isPending}
                      onClick={() => enroll.mutate()}
                    >
                      {enroll.isPending ? t('courses.enrolling') : t('courses.enroll')}
                    </Button>
                  ) : full ? (
                    <Button size="lg" block variant="outline" disabled>
                      {locale === 'ar-EG' ? 'المجموعة مكتملة' : 'Group is full'}
                    </Button>
                  ) : (
                    <Button asChild size="lg" block>
                      <Link to={isAuthenticated ? '/groups' : '/login'}>
                        {isAuthenticated ? t('common.details') : t('nav.login')}
                      </Link>
                    </Button>
                  )}

                  <div className="flex items-center gap-2">
                    {details.whatsAppLink && (
                      <WhatsAppButton href={details.whatsAppLink} block className="flex-1">
                        {locale === 'ar-EG' ? 'اسأل عنها' : 'Ask about it'}
                      </WhatsAppButton>
                    )}
                    <ShareButton title={details.name ?? ''} />
                  </div>

                  {/* Weekly times */}
                  {schedules.length > 0 && (
                    <div className="space-y-2.5 border-t border-border pt-4">
                      <p className="font-display text-caption font-bold text-foreground">
                        {t('nav.schedule')}
                      </p>
                      <ul className="space-y-1.5">
                        {WEEK_ORDER.flatMap((day) =>
                          schedules
                            .filter((slot) => slot.dayOfWeek === dayToApi(day))
                            .map((slot) => (
                              <li
                                key={slot.id}
                                className="flex items-center justify-between gap-2 text-body-sm text-muted-foreground"
                              >
                                <span className="inline-flex items-center gap-2">
                                  <Clock className="size-3.5 text-primary" />
                                  {apiDayLabel(slot.dayOfWeek, locale)}
                                </span>
                                <span className="tnum">
                                  {slot.startTime ? formatTime(slot.startTime) : ''} —{' '}
                                  {slot.endTime ? formatTime(slot.endTime) : ''}
                                </span>
                              </li>
                            )),
                        )}
                      </ul>
                    </div>
                  )}
                </Card>
              </Reveal>
            </div>
          </div>
        </div>
      </section>

      <section className="section-y !pt-12">
        <div className="container-page grid gap-10 lg:grid-cols-[1fr,22rem]">
          <div className="min-w-0 space-y-12">
            {/* ---------------------------- Curriculum -------------------- */}
            <div>
              <PanelHeading
                title={t('courses.curriculum')}
                description={
                  curriculumItems.length
                    ? `${formatNumber(curriculumItems.length)} ${locale === 'ar-EG' ? 'موضوع' : 'topics'}`
                    : undefined
                }
              />
              <div className="mt-5">
                {curriculum.isPending ? (
                  <RowSkeleton count={3} />
                ) : curriculumItems.length === 0 ? (
                  <EmptyState
                    spot="courses"
                    compact
                    title={locale === 'ar-EG' ? 'المنهج لسه متحطّش' : 'No curriculum published yet'}
                  />
                ) : (
                  <Card className="overflow-hidden p-2">
                    <Accordion type="multiple" defaultValue={[curriculumItems[0]?.id ?? '']}>
                      {curriculumItems.map((item, index) => (
                        <AccordionItem
                          key={item.id}
                          value={item.id ?? ''}
                          className="border-b border-border last:border-b-0"
                        >
                          <AccordionTrigger className="px-3 hover:no-underline">
                            <span className="tnum me-3 grid size-6 shrink-0 place-items-center rounded-md bg-muted text-caption font-bold text-muted-foreground">
                              {index + 1}
                            </span>
                            <span className="flex-1 text-start font-display text-body-sm font-bold text-foreground">
                              {item.title}
                            </span>
                          </AccordionTrigger>
                          {item.description && (
                            <AccordionContent className="px-3 pb-3 ps-12">
                              <p className="text-body-sm leading-relaxed text-muted-foreground">
                                {item.description}
                              </p>
                            </AccordionContent>
                          )}
                        </AccordionItem>
                      ))}
                    </Accordion>
                  </Card>
                )}
              </div>
            </div>

            {/* ---------------------------- Materials --------------------- */}
            {materialItems.length > 0 && (
              <div>
                <PanelHeading title={locale === 'ar-EG' ? 'المواد التعليمية' : 'Materials'} />
                <ul className="mt-5 space-y-2.5">
                  {materialItems.map((material) => {
                    const locked = material.isLocked && !details.isEnrolled
                    const body = (
                      <>
                        <span
                          className={cn(
                            'grid size-10 shrink-0 place-items-center rounded-xl',
                            locked
                              ? 'bg-muted text-muted-foreground'
                              : 'bg-primary/10 text-primary',
                          )}
                        >
                          {locked ? <Lock className="size-5" /> : <BookOpen className="size-5" />}
                        </span>
                        <span className="min-w-0 flex-1">
                          <span className="block truncate font-display text-body-sm font-semibold text-foreground">
                            {material.title}
                          </span>
                          {material.description && (
                            <span className="block truncate text-caption text-muted-foreground">
                              {material.description}
                            </span>
                          )}
                        </span>
                        {material.fileTypeName && (
                          <Badge variant="outline" size="sm" className="shrink-0">
                            {material.fileTypeName}
                          </Badge>
                        )}
                        {!locked && <Download className="size-4 shrink-0 text-muted-foreground" />}
                      </>
                    )

                    return (
                      <li key={material.id}>
                        {locked || !material.fileUrl ? (
                          <div className="flex items-center gap-3 rounded-xl border border-border bg-card p-4 opacity-70">
                            {body}
                          </div>
                        ) : (
                          <a
                            href={resolveMediaUrl(material.fileUrl)}
                            target="_blank"
                            rel="noopener noreferrer"
                            className="flex items-center gap-3 rounded-xl border border-border bg-card p-4 transition-colors hover:border-foreground/20 hover:bg-muted/40"
                          >
                            {body}
                          </a>
                        )}
                      </li>
                    )
                  })}
                </ul>
              </div>
            )}

            {/* ----------------------------- Reviews ---------------------- */}
            <div>
              <PanelHeading title={t('common.reviews')} />
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
            </div>
          </div>

          {/* ------------------------------ Sidebar --------------------------- */}
          <aside className="space-y-5">
            <Reveal>
              <Card className="p-5">
                <h3 className="font-display text-heading-sm text-foreground">
                  {locale === 'ar-EG' ? 'تفاصيل المجموعة' : 'Group details'}
                </h3>
                <GoldRule className="my-4 max-w-[6rem]" />
                <dl className="space-y-3">
                  <Fact
                    label={t('common.subject')}
                    value={pick(details.subject?.nameAr, details.subject?.nameEn)}
                  />
                  <Separator />
                  <Fact
                    label={t('common.grade')}
                    value={pick(details.grade?.nameAr, details.grade?.nameEn)}
                  />
                  <Separator />
                  <Fact
                    label={locale === 'ar-EG' ? 'المواعيد' : 'Days'}
                    value={formatScheduleDays(schedules, locale) || '—'}
                  />
                  <Separator />
                  <Fact
                    label={locale === 'ar-EG' ? 'طريقة الحضور' : 'Mode'}
                    value={
                      MODE_LABELS[details.teachingMode ?? '']?.[lang] ??
                      details.teachingModeName ??
                      '—'
                    }
                  />
                  {details.endDate && (
                    <>
                      <Separator />
                      <Fact
                        label={locale === 'ar-EG' ? 'تنتهي في' : 'Ends'}
                        value={formatDate(details.endDate, 'd MMMM yyyy')}
                      />
                    </>
                  )}
                </dl>
              </Card>
            </Reveal>

            {!details.isEnrolled && (
              <motion.div
                initial={{ opacity: 0, y: 10 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true }}
                transition={transitions.base}
              >
                <Card className="border-primary/25 bg-primary/5 p-5">
                  <h3 className="font-display text-heading-sm text-foreground">
                    {locale === 'ar-EG' ? 'إيه اللي هتاخده' : "What you'll get"}
                  </h3>
                  <ul className="mt-4 space-y-2.5">
                    {(locale === 'ar-EG'
                      ? [
                          'حصص مباشرة بمواعيد ثابتة',
                          'متابعة حضور وواجبات',
                          'امتحانات ودرجات على المنصة',
                          'تواصل مباشر مع المدرّس',
                        ]
                      : [
                          'Live sessions on a fixed schedule',
                          'Attendance and homework tracking',
                          'Exams and grades on the platform',
                          'Direct contact with your teacher',
                        ]
                    ).map((item) => (
                      <li key={item} className="flex items-start gap-2.5 text-body-sm text-muted-foreground">
                        <Check className="mt-0.5 size-4 shrink-0 text-success-strong" />
                        {item}
                      </li>
                    ))}
                  </ul>
                </Card>
              </motion.div>
            )}
          </aside>
        </div>
      </section>

      {details.whatsAppLink && (
        <FloatingWhatsApp
          phone=""
          contactName={details.teacher?.displayName}
          contactRole={details.name}
        />
      )}
    </>
  )
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <dt className="shrink-0 text-body-sm text-muted-foreground">{label}</dt>
      <dd className="min-w-0 text-end font-display text-body-sm font-bold text-foreground">
        {value || '—'}
      </dd>
    </div>
  )
}

/** Local helper — maps our week-order key onto the API's day name. */
function dayToApi(day: (typeof WEEK_ORDER)[number]) {
  const map = {
    sat: 'Saturday',
    sun: 'Sunday',
    mon: 'Monday',
    tue: 'Tuesday',
    wed: 'Wednesday',
    thu: 'Thursday',
    fri: 'Friday',
  } as const
  return map[day]
}
