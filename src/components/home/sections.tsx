import * as React from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  ArrowRight,
  BookOpen,
  CalendarClock,
  GraduationCap,
  Sparkles,
  Star,
  Users,
} from 'lucide-react'
import { coursesApi, groupsApi, reviewsApi, scheduleApi, teachersApi } from '@/api/endpoints/catalog'
import { billingApi } from '@/api/endpoints/platform'
import { recommendedPlanId, sortPlans, yearlySaving } from '@/lib/plans'
import { qk } from '@/lib/query-client'
import { useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Switch } from '@/components/ui/primitives'
import { SectionHeading } from '@/components/common/SectionHeading'
import { Reveal, RevealGroup, RevealItem } from '@/components/common/Reveal'
import { StatBlock } from '@/components/common/StatCard'
import { FaqAccordion } from '@/components/common/FaqAccordion'
import { PlanCard } from '@/components/common/PlanCard'
import { ReviewCard } from '@/components/common/ReviewCard'
import { TeacherCard } from '@/components/teachers/TeacherCard'
import { GroupCard } from '@/components/groups/GroupCard'
import { CourseCard } from '@/components/courses/CourseCard'
import { SlotCard } from '@/components/schedule/SlotCard'
import { SubjectIcon } from '@/components/illustrations/SubjectIcon'
import { AmbientGlow, GoldRule, PaperTexture } from '@/components/illustrations/CoverArt'
import { WhatsAppButton } from '@/components/whatsapp/WhatsAppButton'
import { Logo } from '@/components/brand/Logo'
import { CardGridSkeleton, RowSkeleton, Skeleton } from '@/components/ui/skeleton'
import { subjectByKey } from '@/data/taxonomy'
import { subjectKeyFor } from '@/lib/identity'
import { useHomeFaq, useHowItWorksStudent, useHowItWorksTeacher } from '@/data/content'
import { SITE } from '@/lib/constants'
import { cn } from '@/lib/utils'

/* -------------------------------------------------------------------------- */
/*                                 Stats band                                 */
/* -------------------------------------------------------------------------- */

export function StatsBand() {
  const { t, locale } = useI18n()
  const subjects = useSubjects()

  const teachers = useQuery({
    queryKey: qk.teachers.list({ PageSize: 1 }),
    queryFn: () => teachersApi.list({ PageSize: 1 }),
  })
  const groups = useQuery({
    queryKey: qk.groups.list({ PageSize: 1 }),
    queryFn: () => groupsApi.list({ PageSize: 1 }),
  })
  const courses = useQuery({
    queryKey: qk.courses.list({ PageSize: 1 }),
    queryFn: () => coursesApi.list({ PageSize: 1 }),
  })

  const stats = [
    {
      id: 'teachers',
      value: teachers.data?.totalCount ?? 0,
      label: t('common.teachers'),
      description: locale === 'ar-EG' ? 'على المنصة' : 'on the platform',
      icon: GraduationCap,
    },
    {
      id: 'groups',
      value: groups.data?.totalCount ?? 0,
      label: t('common.groups'),
      description: locale === 'ar-EG' ? 'بمواعيد ثابتة' : 'with fixed schedules',
      icon: Users,
    },
    {
      id: 'courses',
      value: courses.data?.totalCount ?? 0,
      label: t('common.courses'),
      description: locale === 'ar-EG' ? 'محتوى مسجّل' : 'recorded content',
      icon: BookOpen,
    },
    {
      id: 'subjects',
      value: subjects.subjects.length,
      label: t('common.subject'),
      description: locale === 'ar-EG' ? 'في كل المراحل' : 'across every stage',
      icon: Star,
    },
  ]

  return (
    <section className="relative border-y border-border bg-surface py-14">
      <PaperTexture className="opacity-30" />
      <div className="container-page relative">
        <RevealGroup className="grid grid-cols-2 gap-x-6 gap-y-10 lg:grid-cols-4">
          {stats.map((stat) => (
            <RevealItem key={stat.id}>
              <StatBlock
                value={stat.value}
                label={stat.label}
                description={stat.description}
                icon={<stat.icon />}
              />
            </RevealItem>
          ))}
        </RevealGroup>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Subjects                                  */
/* -------------------------------------------------------------------------- */

export function SubjectsSection() {
  const { t, locale, formatNumber } = useI18n()
  const subjects = useSubjects()

  return (
    <section className="section-y">
      <div className="container-page">
        <SectionHeading
          eyebrow={t('common.subject')}
          title={
            locale === 'ar-EG' ? 'ابدأ من المادة اللي محتاجها' : 'Start from the subject you need'
          }
          description={
            locale === 'ar-EG'
              ? 'كل مادة فيها مدرّسين متخصصين في كل مرحلة، بمواعيد وأسعار واضحة من أول لحظة.'
              : 'Every subject has specialist teachers for each stage, with clear times and prices upfront.'
          }
          action={{ label: t('nav.teachers'), href: '/teachers' }}
        />

        {subjects.isPending ? (
          <div className="mt-10 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            {Array.from({ length: 5 }, (_, i) => (
              <div key={i} className="h-36 animate-pulse rounded-xl bg-muted" />
            ))}
          </div>
        ) : (
          <RevealGroup className="mt-10 grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
            {subjects.subjects.map((subject) => {
              const key = subjectKeyFor(subject)
              const hue = subjectByKey(key).hue

              return (
                <RevealItem key={subject.id}>
                  <Link
                    to={`/teachers?subject=${subject.slug ?? subject.id}`}
                    className="group relative flex h-full flex-col gap-3 overflow-hidden rounded-xl border border-border bg-card p-4 transition-all duration-300 ease-premium hover:-translate-y-1 hover:border-foreground/15 hover:shadow-lift focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  >
                    <span
                      className="pointer-events-none absolute -end-6 -top-6 size-20 rounded-full opacity-0 blur-2xl transition-opacity duration-300 group-hover:opacity-100"
                      style={{ backgroundColor: `hsl(${hue} 70% 55% / 0.28)` }}
                      aria-hidden
                    />
                    <span
                      className="relative grid size-11 place-items-center rounded-lg border transition-colors border-[hsl(var(--sh)_55%_45%/0.25)] bg-[hsl(var(--sh)_60%_50%/0.12)] text-[hsl(var(--sh)_62%_32%)] dark:text-[hsl(var(--sh)_70%_68%)]"
                      style={{ '--sh': hue } as React.CSSProperties}
                    >
                      <SubjectIcon subject={key} className="size-5" />
                    </span>

                    <span className="relative">
                      <span className="block font-display text-body font-bold text-foreground">
                        {subjects.label(subject)}
                      </span>
                      {subject.description && (
                        <span className="mt-0.5 line-clamp-2-rtl block text-caption leading-snug text-muted-foreground">
                          {subject.description}
                        </span>
                      )}
                    </span>

                    <span className="tnum relative mt-auto flex items-center gap-1.5 text-caption text-muted-foreground">
                      <Users className="size-3.5" aria-hidden />
                      {formatNumber(subject.teachersCount ?? 0)} {t('common.teacher')}
                      <ArrowRight className="ms-auto size-4 flip-rtl -translate-x-1 opacity-0 transition-all duration-200 group-hover:translate-x-0 group-hover:opacity-100" />
                    </span>
                  </Link>
                </RevealItem>
              )
            })}
          </RevealGroup>
        )}
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                             Featured teachers                              */
/* -------------------------------------------------------------------------- */

export function FeaturedTeachers() {
  const { t, locale } = useI18n()

  const teachers = useQuery({
    queryKey: qk.teachers.list({ PageSize: 6, Sort: 'featured' }),
    queryFn: () => teachersApi.list({ PageSize: 6, Sort: 'featured' }),
  })

  const items = teachers.data?.items ?? []
  if (!teachers.isPending && items.length === 0) return null

  return (
    <section className="section-y bg-surface">
      <div className="container-page">
        <SectionHeading
          eyebrow={locale === 'ar-EG' ? 'مدرّسين مميّزين' : 'Featured teachers'}
          title={
            locale === 'ar-EG'
              ? 'مدرّسين بنى كل واحد فيهم اسمه بجد'
              : 'Teachers who built their name for real'
          }
          description={
            locale === 'ar-EG'
              ? 'بروفايلات كاملة بخبرة موثّقة، تقييمات من طلاب حقيقيين، ومواعيد واضحة تقدر تحجز فيها فورًا.'
              : 'Full profiles with verified experience, real student ratings, and times you can book right away.'
          }
          action={{ label: t('common.viewAll'), href: '/teachers' }}
        />

        {teachers.isPending ? (
          <CardGridSkeleton count={3} className="mt-10" />
        ) : (
          <RevealGroup className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {items.slice(0, 6).map((teacher) => (
              <RevealItem key={teacher.id} className="h-full">
                <TeacherCard teacher={teacher} />
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Groups                                   */
/* -------------------------------------------------------------------------- */

export function GroupsSection() {
  const { t, locale } = useI18n()

  const groups = useQuery({
    queryKey: qk.groups.list({ PageSize: 6, Sort: 'featured' }),
    queryFn: () => groupsApi.list({ PageSize: 6, Sort: 'featured' }),
  })

  const items = groups.data?.items ?? []
  if (!groups.isPending && items.length === 0) return null

  return (
    <section className="section-y">
      <div className="container-page">
        <SectionHeading
          eyebrow={t('nav.groups')}
          title={
            locale === 'ar-EG'
              ? 'مجموعات بمواعيد ثابتة وأماكن محدودة'
              : 'Groups with fixed times and limited seats'
          }
          description={
            locale === 'ar-EG'
              ? 'اعرف المنهج والمواعيد والسعر والأماكن المتاحة قبل ما تحجز — من غير ما تسأل حد.'
              : 'See the curriculum, schedule, price, and seats left before you book — without asking anyone.'
          }
          action={{ label: t('common.viewAll'), href: '/groups' }}
        />

        {groups.isPending ? (
          <CardGridSkeleton count={3} className="mt-10" />
        ) : (
          <RevealGroup className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {items.map((group) => (
              <RevealItem key={group.id} className="h-full">
                <GroupCard group={group} />
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                              Upcoming classes                              */
/* -------------------------------------------------------------------------- */

export function UpcomingClasses() {
  const { locale } = useI18n()

  const slots = useQuery({
    queryKey: qk.schedule.upcoming,
    queryFn: () => scheduleApi.upcoming(4),
  })

  const items = slots.data ?? []
  if (!slots.isPending && items.length === 0) return null

  const points =
    locale === 'ar-EG'
      ? [
          'عرض أسبوعي وعرض قائمة، وفلترة بالمادة والمدرّس',
          'لينك الحصة الأونلاين جاهز في ميعادها',
          'تنبيه قبل كل حصة بوقت كافي',
        ]
      : [
          'Week view and list view, filtered by subject and teacher',
          'The online session link is ready at its time',
          'A reminder before every class',
        ]

  return (
    <section className="section-y bg-surface">
      <div className="container-page">
        <div className="grid gap-10 lg:grid-cols-[0.85fr,1.15fr] lg:gap-14">
          <Reveal>
            <span className="eyebrow">
              <CalendarClock className="size-3.5" />
              {locale === 'ar-EG' ? 'الجدول' : 'Schedule'}
            </span>
            <h2 className="mt-5 text-display-lg text-foreground">
              {locale === 'ar-EG'
                ? 'كل حصصك من كل المدرّسين في جدول واحد.'
                : 'Every class, from every teacher, in one schedule.'}
            </h2>
            <p className="mt-4 text-body-lg text-muted-foreground">
              {locale === 'ar-EG'
                ? 'مش محتاج تفتح ٣ جروبات واتساب عشان تعرف عندك إيه بكرة. الجدول بيجمّع مجموعاتك كلها وبينبّهك قبل كل حصة.'
                : 'No more digging through three WhatsApp groups to find out what you have tomorrow.'}
            </p>

            <ul className="mt-7 space-y-3.5">
              {points.map((line) => (
                <li
                  key={line}
                  className="flex items-start gap-2.5 text-body-sm text-muted-foreground"
                >
                  <span
                    className="mt-1.5 size-1.5 shrink-0 rotate-45 rounded-[1px] bg-accent"
                    aria-hidden
                  />
                  {line}
                </li>
              ))}
            </ul>

            <Button asChild size="lg" className="mt-8">
              <Link to="/schedule">
                {locale === 'ar-EG' ? 'افتح الجدول' : 'Open the schedule'}
                <ArrowRight className="size-4 flip-rtl" />
              </Link>
            </Button>
          </Reveal>

          {slots.isPending ? (
            <RowSkeleton count={4} />
          ) : (
            <RevealGroup className="space-y-3.5">
              {items.map((slot) => (
                <RevealItem key={slot.id}>
                  <SlotCard slot={slot} />
                </RevealItem>
              ))}
            </RevealGroup>
          )}
        </div>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Courses                                   */
/* -------------------------------------------------------------------------- */

export function CoursesSection() {
  const { t, locale } = useI18n()

  const courses = useQuery({
    queryKey: qk.courses.list({ PageSize: 6, Sort: 'featured' }),
    queryFn: () => coursesApi.list({ PageSize: 6, Sort: 'featured' }),
  })

  const items = courses.data?.items ?? []
  if (!courses.isPending && items.length === 0) return null

  return (
    <section className="section-y">
      <div className="container-page">
        <SectionHeading
          eyebrow={t('nav.courses')}
          title={
            locale === 'ar-EG'
              ? 'كورسات مسجّلة تذاكر منها في أي وقت'
              : 'Recorded courses you can study any time'
          }
          description={
            locale === 'ar-EG'
              ? 'مراجعات نهائية، تأسيس، وحل مسائل — بمحتوى منظّم وشهادة إتمام.'
              : 'Revision, foundations, and problem solving — structured content with a completion certificate.'
          }
          action={{ label: t('common.viewAll'), href: '/courses' }}
        />

        {courses.isPending ? (
          <CardGridSkeleton count={3} className="mt-10" />
        ) : (
          <RevealGroup className="mt-10 grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {items.map((course) => (
              <RevealItem key={course.id} className="h-full">
                <CourseCard course={course} />
              </RevealItem>
            ))}
          </RevealGroup>
        )}
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                             Student reviews                                */
/* -------------------------------------------------------------------------- */

export function StudentReviewsSection() {
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'

  /* Real, approved reviews written by real students — never invented quotes.
     When the platform has none yet the section simply does not render. */
  const query = useQuery({
    queryKey: qk.reviews.list({ PageSize: 6 }),
    queryFn: () => reviewsApi.list({ PageSize: 6 }),
    staleTime: 5 * 60_000,
  })

  const reviews = (query.data?.items ?? []).filter((r) => r.comment?.trim())

  if (query.isPending) {
    return (
      <section className="section-y bg-surface">
        <div className="container-page">
          <CardGridSkeleton count={3} />
        </div>
      </section>
    )
  }

  if (!reviews.length) return null

  return (
    <section className="section-y bg-surface">
      <div className="container-page">
        <SectionHeading
          eyebrow={isAr ? 'آراء الطلاب' : 'Student reviews'}
          title={isAr ? 'تقييمات من طلاب فعليين' : 'Reviews from real students'}
          description={
            isAr
              ? 'كل تقييم هنا كتبه طالب مشترك فعلًا مع المدرّس، وبيتراجع قبل ما يتنشر.'
              : 'Every review here was written by a student actually enrolled with the teacher, and is moderated before it appears.'
          }
          align="center"
        />

        <RevealGroup className="mt-12 grid gap-5 md:grid-cols-2 lg:grid-cols-3">
          {reviews.slice(0, 6).map((review) => (
            <RevealItem key={review.id} className="h-full">
              <ReviewCard review={review} showTeacher />
            </RevealItem>
          ))}
        </RevealGroup>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                                How it works                                */
/* -------------------------------------------------------------------------- */

export function HowItWorks() {
  const { locale } = useI18n()
  const teacherSteps = useHowItWorksTeacher()
  const studentSteps = useHowItWorksStudent()

  return (
    <section className="section-y">
      <div className="container-page">
        <SectionHeading
          eyebrow={locale === 'ar-EG' ? 'إزاي بتشتغل' : 'How it works'}
          title={locale === 'ar-EG' ? 'أربع خطوات وخلاص' : 'Four steps, that is it'}
          description={
            locale === 'ar-EG'
              ? 'سواء إنت مدرّس بتبني اسمك، أو طالب بتدوّر على المدرّس المناسب.'
              : 'Whether you are a teacher building your name or a student finding the right one.'
          }
          align="center"
        />

        <Tabs defaultValue="teacher" className="mt-10">
          <div className="flex justify-center">
            <TabsList>
              <TabsTrigger value="teacher">
                <GraduationCap /> {locale === 'ar-EG' ? 'للمدرّسين' : 'For teachers'}
              </TabsTrigger>
              <TabsTrigger value="student">
                <Users /> {locale === 'ar-EG' ? 'للطلاب' : 'For students'}
              </TabsTrigger>
            </TabsList>
          </div>

          {(
            [
              ['teacher', teacherSteps],
              ['student', studentSteps],
            ] as const
          ).map(([value, steps]) => (
            <TabsContent key={value} value={value}>
              <RevealGroup className="relative grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                <span
                  className="pointer-events-none absolute inset-x-12 top-11 hidden h-px bg-gradient-to-l from-transparent via-border to-transparent lg:block"
                  aria-hidden
                />
                {steps.map((step) => (
                  <RevealItem key={step.id}>
                    <Card className="relative h-full p-6">
                      <span className="tnum grid size-11 place-items-center rounded-xl border border-accent/30 bg-accent/10 font-display text-body font-extrabold text-accent-strong">
                        {step.number}
                      </span>
                      <h3 className="mt-5 font-display text-heading-sm text-foreground">
                        {step.title}
                      </h3>
                      <p className="mt-2.5 text-body-sm leading-relaxed text-muted-foreground">
                        {step.description}
                      </p>
                    </Card>
                  </RevealItem>
                ))}
              </RevealGroup>
            </TabsContent>
          ))}
        </Tabs>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                               Pricing preview                              */
/* -------------------------------------------------------------------------- */

export function PricingPreview() {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const [yearly, setYearly] = React.useState(false)

  // Same source as the pricing page: the live plan catalogue.
  const plansQuery = useQuery({
    queryKey: qk.billing.plans,
    queryFn: billingApi.plans,
    staleTime: 10 * 60 * 1000,
  })
  const plans = React.useMemo(
    () => sortPlans((plansQuery.data ?? []).filter((p) => p.isActive !== false)),
    [plansQuery.data],
  )
  const highlightId = React.useMemo(() => recommendedPlanId(plans), [plans])
  const anyYearly = plans.some((p) => yearlySaving(p) > 0)

  // Nothing to show and nothing to promise — skip the whole band.
  if (!plansQuery.isPending && plans.length === 0) return null

  return (
    <section className="section-y bg-surface">
      <div className="container-page">
        <SectionHeading
          eyebrow={t('nav.pricing')}
          title={
            locale === 'ar-EG' ? 'باقة واحدة تكفي شغلك كله' : 'One plan that covers your whole practice'
          }
          description={
            locale === 'ar-EG'
              ? 'مفيش نسبة من اشتراكات طلابك. اشتراك شهري ثابت وبس، وتقدر توقفه أي وقت.'
              : 'No commission on your students. A flat monthly subscription you can cancel any time.'
          }
          align="center"
        />

        {anyYearly && (
          <Reveal className="mt-8 flex items-center justify-center gap-3">
            <span className={cn('text-body-sm font-medium', !yearly && 'font-bold text-foreground')}>
              {t('billing.monthly')}
            </span>
            <Switch checked={yearly} onCheckedChange={setYearly} aria-label={t('billing.yearly')} />
            <span className={cn('text-body-sm font-medium', yearly && 'font-bold text-foreground')}>
              {t('billing.yearly')}
            </span>
          </Reveal>
        )}

        {plansQuery.isPending ? (
          <div className="mt-10 grid gap-6 lg:grid-cols-3">
            {[0, 1, 2].map((i) => (
              <Card key={i} className="flex h-full flex-col gap-4 p-6">
                <Skeleton className="h-5 w-28" />
                <Skeleton className="h-10 w-36" />
                <div className="mt-2 space-y-2">
                  {[0, 1, 2, 3].map((r) => (
                    <Skeleton key={r} className="h-4 w-11/12" />
                  ))}
                </div>
                <Skeleton className="mt-auto h-11 w-full" />
              </Card>
            ))}
          </div>
        ) : (
          <RevealGroup className="mt-10 grid gap-6 lg:grid-cols-3">
            {plans.map((plan) => (
              <RevealItem key={plan.id} className="h-full">
                <PlanCard
                  plan={plan}
                  cycle={yearly ? 'yearly' : 'monthly'}
                  lang={lang}
                  popular={plan.id === highlightId}
                />
              </RevealItem>
            ))}
          </RevealGroup>
        )}

        <Reveal className="mt-8 text-center">
          <Link to="/pricing" className="link-underline text-body-sm font-semibold text-primary">
            {locale === 'ar-EG' ? 'شوف مقارنة الباقات بالتفصيل' : 'Compare plans in detail'}
          </Link>
        </Reveal>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                                    FAQ                                     */
/* -------------------------------------------------------------------------- */

export function HomeFaq() {
  const { t, locale } = useI18n()
  const homeFaq = useHomeFaq()

  return (
    <section className="section-y">
      <div className="container-page">
        <div className="grid gap-10 lg:grid-cols-[0.8fr,1.2fr] lg:gap-16">
          <Reveal>
            <span className="eyebrow">{t('nav.faq')}</span>
            <h2 className="mt-5 text-display-lg text-foreground">
              {locale === 'ar-EG' ? 'أسئلة بتتسأل كتير' : 'Questions we hear a lot'}
            </h2>
            <p className="mt-4 text-body-lg text-muted-foreground">
              {locale === 'ar-EG'
                ? 'لو ملقتش إجابة سؤالك هنا، كلّمنا على واتساب وهنرد عليك بنفسنا.'
                : 'If your question is not here, message us on WhatsApp and we will answer personally.'}
            </p>

            <div className="mt-7 flex flex-wrap gap-3">
              <Button asChild variant="outline">
                <Link to="/faq">{t('common.viewAll')}</Link>
              </Button>
              <WhatsAppButton phone={SITE.phone} intent="support" variant="whatsapp-outline">
                {locale === 'ar-EG' ? 'اسأل على واتساب' : 'Ask on WhatsApp'}
              </WhatsAppButton>
            </div>
          </Reveal>

          <Reveal delay={0.08}>
            <FaqAccordion items={homeFaq} defaultOpen={homeFaq[0]?.id} />
          </Reveal>
        </div>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                                 Final CTA                                  */
/* -------------------------------------------------------------------------- */

export function FinalCta() {
  const { locale } = useI18n()

  return (
    <section className="relative overflow-hidden border-t border-border bg-primary py-section text-primary-foreground">
      <AmbientGlow className="opacity-60" />
      <div className="pointer-events-none absolute inset-0 bg-dot-grid opacity-[0.07]" aria-hidden />

      <div className="container-page relative">
        <Reveal className="mx-auto max-w-2xl text-center">
          <Logo variant="mark" size="xl" className="mx-auto" />

          <h2 className="mt-8 text-display-lg text-primary-foreground">
            {locale === 'ar-EG'
              ? 'مساحة عملك جاهزة في أقل من نص ساعة.'
              : 'Your workspace is live in under half an hour.'}
          </h2>

          <p className="mt-5 text-body-lg text-primary-foreground/75">
            {locale === 'ar-EG'
              ? 'ابدأ مجانًا، ضيف مجموعاتك، وابعت لينك بروفايلك لطلابك. من غير كارت ائتمان ومن غير أي التزام.'
              : 'Start free, add your groups, and share your profile link with your students. No card, no commitment.'}
          </p>

          <GoldRule className="mx-auto mt-8 max-w-xs" />

          <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
            <Button asChild size="xl" variant="accent">
              <Link to="/register">
                {locale === 'ar-EG' ? 'ابدأ مجانًا' : 'Start free'}
                <ArrowRight className="size-5 flip-rtl" />
              </Link>
            </Button>
            <Button
              asChild
              size="xl"
              variant="outline"
              className="border-primary-foreground/25 text-primary-foreground hover:bg-primary-foreground/10 hover:text-primary-foreground"
            >
              <Link to="/teachers">
                {locale === 'ar-EG' ? 'اتفرّج على المدرّسين' : 'Browse teachers'}
              </Link>
            </Button>
          </div>
        </Reveal>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------- */
/*                               Offers strip                                 */
/* -------------------------------------------------------------------------- */

/**
 * Discount codes are teacher-scoped in the API, so the marketing site has no
 * public feed to read. The strip stays out of the homepage until one exists.
 */
export function OffersStrip() {
  const { locale } = useI18n()

  return (
    <section className="border-y border-border bg-primary py-12 text-primary-foreground">
      <div className="container-page">
        <div className="flex flex-col items-start gap-6 lg:flex-row lg:items-center lg:justify-between">
          <Reveal>
            <span className="inline-flex items-center gap-2 rounded-full border border-primary-foreground/20 bg-primary-foreground/10 px-3 py-1 text-label uppercase tracking-[0.12em]">
              <Sparkles className="size-3.5" />
              {locale === 'ar-EG' ? 'أكواد الخصم' : 'Discount codes'}
            </span>
            <h2 className="mt-4 text-heading-lg text-primary-foreground">
              {locale === 'ar-EG'
                ? 'مدرّسك ممكن يبعتلك كود خصم'
                : 'Your teacher can send you a discount code'}
            </h2>
            <p className="mt-2 max-w-md text-body-sm text-primary-foreground/70">
              {locale === 'ar-EG'
                ? 'اكتب الكود وقت الاشتراك وهتشوف السعر بيتغيّر قدامك على طول.'
                : 'Enter it when you enroll and watch the price update instantly.'}
            </p>
          </Reveal>

          <Button asChild size="lg" variant="accent">
            <Link to="/groups">
              {locale === 'ar-EG' ? 'تصفّح المجموعات' : 'Browse groups'}
              <ArrowRight className="size-4 flip-rtl" />
            </Link>
          </Button>
        </div>
      </div>
    </section>
  )
}
