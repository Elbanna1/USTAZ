import * as React from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { Check, Minus, Sparkles } from 'lucide-react'
import { billingApi } from '@/api/endpoints/platform'
import { qk } from '@/lib/query-client'
import { usePricingFaq } from '@/data/content'
import {
  comparisonFeatures,
  featureLabel,
  planName,
  recommendedPlanId,
  sortPlans,
  yearlySaving,
} from '@/lib/plans'
import { cn } from '@/lib/utils'
import { SITE } from '@/lib/constants'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { PageHeader } from '@/components/common/PageHeader'
import { PanelHeading } from '@/components/common/SectionHeading'
import { Reveal, RevealGroup, RevealItem } from '@/components/common/Reveal'
import { FaqAccordion } from '@/components/common/FaqAccordion'
import { QueryError } from '@/components/common/QueryBoundary'
import { PlanCard, type Cycle } from '@/components/common/PlanCard'
import { Switch } from '@/components/ui/primitives'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { WhatsAppButton } from '@/components/whatsapp/WhatsAppButton'

export default function PricingPage() {
  const { locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const isAr = lang === 'ar'

  usePageTitle(
    isAr ? 'الأسعار' : 'Pricing',
    isAr
      ? 'باقات أستاذ للمدرّسين والسناتر — من غير أي نسبة من اشتراكات الطلبة.'
      : 'USTAZ plans for teachers and centres — we never take a cut of your students’ fees.',
  )

  const [cycle, setCycle] = React.useState<Cycle>('monthly')
  const pricingFaq = usePricingFaq()

  // The plans, their prices and their limits are all the backend's to decide.
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

  return (
    <>
      <PageHeader
        eyebrow={isAr ? 'الأسعار' : 'Pricing'}
        title={isAr ? 'اشتراك ثابت، ومفيش نسبة من طلابك' : 'A flat subscription, never a cut of your students'}
        description={
          isAr
            ? 'إنت بتدفع للأداة بس. كل جنيه بيدفعه الطالب بيروحلك إنت مباشرة.'
            : 'You pay for the tool, nothing else. Every pound a student pays goes straight to you.'
        }
        crumbs={[
          { label: isAr ? 'الرئيسية' : 'Home', href: '/' },
          { label: isAr ? 'الأسعار' : 'Pricing' },
        ]}
      >
        {anyYearly && (
          <div className="flex flex-wrap items-center gap-3">
            <span
              className={cn('text-body-sm font-medium', cycle === 'monthly' && 'font-bold text-foreground')}
            >
              {isAr ? 'فوترة شهرية' : 'Billed monthly'}
            </span>
            <Switch
              checked={cycle === 'yearly'}
              onCheckedChange={(next) => setCycle(next ? 'yearly' : 'monthly')}
              aria-label={isAr ? 'تبديل الفوترة السنوية' : 'Toggle yearly billing'}
            />
            <span
              className={cn('text-body-sm font-medium', cycle === 'yearly' && 'font-bold text-foreground')}
            >
              {isAr ? 'فوترة سنوية' : 'Billed yearly'}
            </span>
            <Badge variant="success" size="sm">
              {isAr ? 'أوفر' : 'Cheaper'}
            </Badge>
          </div>
        )}
      </PageHeader>

      {/* Plans */}
      <section className="section-y !pt-12">
        <div className="container-page">
          {plansQuery.isPending ? (
            <div className="grid gap-6 lg:grid-cols-3">
              {[0, 1, 2].map((i) => (
                <Card key={i} className="flex h-full flex-col gap-4 p-6">
                  <Skeleton className="h-5 w-28" />
                  <Skeleton className="h-10 w-36" />
                  <Skeleton className="h-4 w-full" />
                  <div className="mt-2 space-y-2">
                    {[0, 1, 2, 3, 4].map((r) => (
                      <Skeleton key={r} className="h-4 w-11/12" />
                    ))}
                  </div>
                  <Skeleton className="mt-auto h-11 w-full" />
                </Card>
              ))}
            </div>
          ) : plansQuery.isError ? (
            <QueryError error={plansQuery.error} onRetry={() => plansQuery.refetch()} />
          ) : (
            <RevealGroup className="grid gap-6 lg:grid-cols-3">
              {plans.map((plan) => (
                <RevealItem key={plan.id} className="h-full">
                  <PlanCard
                    plan={plan}
                    cycle={cycle}
                    lang={lang}
                    popular={plan.id === highlightId}
                  />
                </RevealItem>
              ))}
            </RevealGroup>
          )}

          <Reveal className="mt-8">
            <Card className="flex flex-col items-center gap-4 border-dashed bg-muted/30 p-6 text-center sm:flex-row sm:text-start">
              <span className="grid size-11 shrink-0 place-items-center rounded-xl bg-accent/12 text-accent-strong">
                <Sparkles className="size-5" />
              </span>
              <div className="flex-1">
                <h3 className="font-display text-body font-bold text-foreground">
                  {isAr ? 'سنتر بفريق كبير؟' : 'A centre with a large team?'}
                </h3>
                <p className="mt-1 text-body-sm text-muted-foreground">
                  {isAr
                    ? 'نعملك عرض مخصص حسب عدد المدرّسين والقاعات والفروع.'
                    : 'We will quote for your number of teachers, rooms, and branches.'}
                </p>
              </div>
              <div className="flex gap-2">
                <Button asChild variant="outline">
                  <Link to="/contact">{isAr ? 'اطلب عرض' : 'Request a quote'}</Link>
                </Button>
                <WhatsAppButton
                  phone={SITE.phone}
                  intent="send-inquiry"
                  context={{
                    note: isAr
                      ? 'عندي سنتر وعايز عرض مخصص.'
                      : 'I run a centre and would like a custom quote.',
                  }}
                  variant="whatsapp-outline"
                >
                  {isAr ? 'واتساب' : 'WhatsApp'}
                </WhatsAppButton>
              </div>
            </Card>
          </Reveal>
        </div>
      </section>

      {/* Comparison — built from the same plan payload, never a second source. */}
      {plans.length > 1 && (
        <section className="section-y bg-surface">
          <div className="container-page">
            <PanelHeading
              title={isAr ? 'مقارنة تفصيلية' : 'Full comparison'}
              description={
                isAr ? 'كل اللي في كل باقة، من غير أي مفاجآت.' : 'Everything in each plan, no surprises.'
              }
            />

            <Reveal className="mt-8">
              <div className="overflow-x-auto rounded-xl border border-border bg-card">
                <table className="w-full min-w-[42rem] text-body-sm">
                  <caption className="sr-only">
                    {isAr ? 'مقارنة مميزات الباقات' : 'Plan feature comparison'}
                  </caption>
                  <thead>
                    <tr className="border-b border-border bg-muted/40">
                      <th
                        scope="col"
                        className="p-4 text-start font-display text-body-sm font-bold text-foreground"
                      >
                        {isAr ? 'المميزات' : 'Features'}
                      </th>
                      {plans.map((p) => (
                        <th
                          key={p.id}
                          scope="col"
                          className={cn(
                            'p-4 text-center font-display text-body-sm font-bold text-foreground',
                            p.id === highlightId && 'bg-accent/8',
                          )}
                        >
                          {planName(p, lang)}
                          {p.id === highlightId && (
                            <Badge variant="gold" size="sm" className="ms-2">
                              {isAr ? 'الأنسب' : 'Best fit'}
                            </Badge>
                          )}
                        </th>
                      ))}
                    </tr>
                  </thead>

                  <tbody>
                    {/* Quotas */}
                    <tr className="border-b border-border bg-muted/20">
                      <th
                        scope="colgroup"
                        colSpan={plans.length + 1}
                        className="p-3 text-start text-label uppercase tracking-wide text-muted-foreground"
                      >
                        {isAr ? 'الحدود' : 'Limits'}
                      </th>
                    </tr>
                    {(
                      [
                        ['maxStudents', { ar: 'الطلاب', en: 'Students' }],
                        ['maxGroups', { ar: 'المجموعات', en: 'Groups' }],
                        ['maxCourses', { ar: 'الكورسات', en: 'Courses' }],
                        ['maxAssistants', { ar: 'المساعدين', en: 'Assistants' }],
                      ] as const
                    ).map(([key, label]) => (
                      <tr key={key} className="border-b border-border">
                        <th
                          scope="row"
                          className="p-4 text-start font-normal text-muted-foreground"
                        >
                          {label[lang]}
                        </th>
                        {plans.map((p) => {
                          const value = p[key] as number | null | undefined
                          return (
                            <td
                              key={p.id}
                              className={cn('p-4 text-center', p.id === highlightId && 'bg-accent/5')}
                            >
                              <span className="tnum font-semibold text-foreground">
                                {value == null ? (isAr ? 'بلا حدود' : 'Unlimited') : formatNumber(value)}
                              </span>
                            </td>
                          )
                        })}
                      </tr>
                    ))}

                    {/* Features */}
                    <tr className="border-b border-border bg-muted/20">
                      <th
                        scope="colgroup"
                        colSpan={plans.length + 1}
                        className="p-3 text-start text-label uppercase tracking-wide text-muted-foreground"
                      >
                        {isAr ? 'المميزات' : 'Features'}
                      </th>
                    </tr>
                    {comparisonFeatures(plans).map((code) => (
                      <tr key={code} className="border-b border-border last:border-b-0">
                        <th
                          scope="row"
                          className="p-4 text-start font-normal text-muted-foreground"
                        >
                          {featureLabel(code)[lang]}
                        </th>
                        {plans.map((p) => {
                          const has = p.features?.includes(code) ?? false
                          return (
                            <td
                              key={p.id}
                              className={cn('p-4 text-center', p.id === highlightId && 'bg-accent/5')}
                            >
                              {has ? (
                                <span className="mx-auto grid size-6 place-items-center rounded-full bg-success/15 text-success-strong">
                                  <Check className="size-3.5" strokeWidth={3} />
                                  <span className="sr-only">{isAr ? 'متاح' : 'Included'}</span>
                                </span>
                              ) : (
                                <span className="mx-auto grid size-6 place-items-center rounded-full bg-muted text-muted-foreground">
                                  <Minus className="size-3.5" strokeWidth={3} />
                                  <span className="sr-only">
                                    {isAr ? 'غير متاح' : 'Not included'}
                                  </span>
                                </span>
                              )}
                            </td>
                          )
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </Reveal>
          </div>
        </section>
      )}

      {/* FAQ */}
      <section className="section-y bg-surface">
        <div className="container-page">
          <div className="grid gap-10 lg:grid-cols-[0.8fr,1.2fr] lg:gap-16">
            <Reveal>
              <span className="eyebrow">{isAr ? 'أسئلة الأسعار' : 'Pricing questions'}</span>
              <h2 className="mt-5 text-display-lg text-foreground">
                {isAr ? 'أسئلة قبل ما تشترك' : 'Before you subscribe'}
              </h2>
              <p className="mt-4 text-body-lg text-muted-foreground">
                {isAr
                  ? 'لو عندك سؤال مش موجود هنا، كلّمنا وهنجاوبك بصراحة.'
                  : 'If your question is not here, ask us and we will answer straight.'}
              </p>
              <WhatsAppButton
                phone={SITE.phone}
                intent="support"
                variant="whatsapp-outline"
                className="mt-6"
              >
                {isAr ? 'اسأل على واتساب' : 'Ask on WhatsApp'}
              </WhatsAppButton>
            </Reveal>

            <Reveal delay={0.08}>
              <FaqAccordion items={pricingFaq} defaultOpen={pricingFaq[0]?.id} />
            </Reveal>
          </div>
        </div>
      </section>
    </>
  )

}
