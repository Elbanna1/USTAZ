import { Link } from 'react-router-dom'
import { Check } from 'lucide-react'
import type { SubscriptionPlanDto } from '@/api/schema'
import { featureLabel, isFreePlan, planLimits, planName, planPrice, yearlySaving } from '@/lib/plans'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { cn } from '@/lib/utils'

export type Cycle = 'monthly' | 'yearly'

/**
 * One subscription plan, rendered from exactly what the API returned — the home
 * page and the pricing page show the same prices because they share this.
 */
export function PlanCard({
  plan,
  cycle: activeCycle,
  lang: cardLang,
  popular,
}: {
  plan: SubscriptionPlanDto
  cycle: Cycle
  lang: 'ar' | 'en'
  popular: boolean
}) {
  const { isAuthenticated } = useAuth()
  const { t, formatCurrency, formatNumber } = useI18n()
  const cardIsAr = cardLang === 'ar'
  const price = planPrice(plan, activeCycle)
  const free = isFreePlan(plan)
  const saving = yearlySaving(plan)
  const limits = planLimits(plan)

  return (
    <Card
      variant={popular ? 'elevated' : 'default'}
      className={cn(
        'relative flex h-full flex-col p-6 transition-all duration-300',
        popular && 'border-accent/45 ring-1 ring-accent/25',
      )}
    >
      {popular && (
        <Badge variant="gold" size="sm" className="absolute -top-3 start-6">
          {cardIsAr ? 'الأنسب للبداية' : 'Best first upgrade'}
        </Badge>
      )}

      <h3 className="font-display text-heading-sm text-foreground">{planName(plan, cardLang)}</h3>
      {plan.description && (
        <p className="mt-1.5 text-body-sm text-muted-foreground">{plan.description}</p>
      )}

      <div className="mt-5 flex flex-wrap items-baseline gap-2">
        <span className="tnum font-display text-[2rem] font-extrabold leading-none text-foreground">
          {free ? t('common.free') : formatCurrency(price, plan.currency ?? 'EGP')}
        </span>
        {!free && (
          <span className="text-caption text-muted-foreground">
            {cardIsAr ? '/ شهريًا' : '/ month'}
          </span>
        )}
      </div>
      {!free && activeCycle === 'yearly' && saving > 0 && (
        <p className="mt-1.5 text-caption text-success-strong">
          {cardIsAr
            ? `توفّر ${formatCurrency(saving, plan.currency ?? 'EGP')} في السنة`
            : `Saves ${formatCurrency(saving, plan.currency ?? 'EGP')} a year`}
        </p>
      )}
      {(plan.trialDays ?? 0) > 0 && !free && (
        <p className="mt-1 text-caption text-muted-foreground">
          {cardIsAr
            ? `${formatNumber(plan.trialDays!)} يوم تجربة مجانية`
            : `${formatNumber(plan.trialDays!)}-day free trial`}
        </p>
      )}

      {limits.length > 0 && (
        <dl className="mt-5 grid grid-cols-2 gap-x-4 gap-y-2 rounded-xl bg-muted/40 p-4">
          {limits.map((limit) => (
            <div key={limit.key} className="flex items-center justify-between gap-2">
              <dt className="text-caption text-muted-foreground">{limit.label[cardLang]}</dt>
              <dd className="tnum text-caption font-bold text-foreground">
                {limit.value == null ? '∞' : formatNumber(limit.value)}
              </dd>
            </div>
          ))}
        </dl>
      )}

      <ul className="mt-5 space-y-2.5">
        {(plan.features ?? []).map((code) => (
          <li key={code} className="flex items-start gap-2.5 text-body-sm text-muted-foreground">
            <span className="mt-0.5 grid size-4 shrink-0 place-items-center rounded-full bg-success/15 text-success-strong">
              <Check className="size-2.5" strokeWidth={3.5} />
            </span>
            {featureLabel(code)[cardLang]}
          </li>
        ))}
      </ul>

      <div className="mt-auto pt-6">
        <Button asChild block variant={popular ? 'primary' : 'outline'}>
          <Link to={isAuthenticated ? '/teacher/billing' : '/register'}>
            {isAuthenticated
              ? cardIsAr
                ? 'اختار الباقة'
                : 'Choose this plan'
              : free
                ? cardIsAr
                  ? 'ابدأ مجانًا'
                  : 'Start free'
                : cardIsAr
                  ? 'ابدأ التجربة'
                  : 'Start the trial'}
          </Link>
        </Button>
      </div>
    </Card>
  )
}
