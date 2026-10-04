import type { SubscriptionPlanDto } from '@/api/schema'

/**
 * The API describes plans with machine feature codes (`educational_payments`).
 * This is the one place that turns them into something a teacher can read, and
 * that decides how a plan is presented. Unknown codes degrade to a tidied
 * version of the code rather than disappearing — a new backend feature should
 * still show up in the UI before anyone has translated it.
 */

export type Bilingual = { ar: string; en: string }

const FEATURE_LABELS: Record<string, Bilingual> = {
  attendance: { ar: 'تسجيل الحضور والغياب', en: 'Attendance tracking' },
  announcements: { ar: 'إعلانات للطلاب', en: 'Student announcements' },
  messaging: { ar: 'رسائل مباشرة', en: 'Direct messaging' },
  exams: { ar: 'امتحانات أونلاين بتصحيح تلقائي', en: 'Online exams with auto-marking' },
  homework: { ar: 'واجبات وتسليم وتصحيح', en: 'Homework, submissions, and marking' },
  educational_payments: { ar: 'إدارة مدفوعات الطلاب', en: 'Student payment tracking' },
  website_customization: { ar: 'تخصيص شكل صفحتك', en: 'Customise your public page' },
  advanced_analytics: { ar: 'تحليلات متقدّمة', en: 'Advanced analytics' },
  custom_domain: { ar: 'دومين خاص بيك', en: 'Your own domain' },
  certificates: { ar: 'شهادات للطلاب', en: 'Student certificates' },
  discounts: { ar: 'أكواد خصم وعروض', en: 'Discount codes and offers' },
}

/** Human label for one feature code, in both languages. */
export function featureLabel(code: string): Bilingual {
  const known = FEATURE_LABELS[code]
  if (known) return known
  const pretty = code.replace(/[_-]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
  return { ar: pretty, en: pretty }
}

/** Quota rows worth showing on a plan card, skipping the ones the plan omits. */
export interface PlanLimit {
  key: string
  label: Bilingual
  /** `null` means the API set no ceiling — present it as unlimited, not zero. */
  value: number | null
}

export function planLimits(plan: SubscriptionPlanDto): PlanLimit[] {
  const rows: PlanLimit[] = [
    { key: 'students', label: { ar: 'طلاب', en: 'Students' }, value: plan.maxStudents ?? null },
    { key: 'groups', label: { ar: 'مجموعات', en: 'Groups' }, value: plan.maxGroups ?? null },
    { key: 'courses', label: { ar: 'كورسات', en: 'Courses' }, value: plan.maxCourses ?? null },
    {
      key: 'assistants',
      label: { ar: 'مساعدين', en: 'Assistants' },
      value: plan.maxAssistants ?? null,
    },
  ]
  // A plan that never mentions a quota should not claim to have one.
  return rows.filter((row) => row.value !== null || plan.maxTeamMembers != null)
}

export function planName(plan: SubscriptionPlanDto, lang: 'ar' | 'en'): string {
  const name = lang === 'ar' ? plan.nameAr : plan.nameEn
  return name || plan.nameEn || plan.nameAr || plan.code || ''
}

/** Monthly-equivalent price for the chosen billing cycle. */
export function planPrice(plan: SubscriptionPlanDto, cycle: 'monthly' | 'yearly'): number {
  const monthly = plan.monthlyPrice ?? 0
  if (cycle === 'monthly') return monthly
  const yearly = plan.yearlyPrice ?? monthly * 12
  return Math.round(yearly / 12)
}

/** How much a year up front saves versus twelve monthly payments, in currency. */
export function yearlySaving(plan: SubscriptionPlanDto): number {
  const monthly = plan.monthlyPrice ?? 0
  const yearly = plan.yearlyPrice ?? monthly * 12
  return Math.max(0, Math.round(monthly * 12 - yearly))
}

export function isFreePlan(plan: SubscriptionPlanDto): boolean {
  return (plan.monthlyPrice ?? 0) <= 0
}

/**
 * The plan we point a new teacher at. The API marks no plan as recommended, so
 * we highlight the cheapest paid tier — the first real upgrade from free.
 */
export function recommendedPlanId(plans: SubscriptionPlanDto[]): string | undefined {
  const paid = plans
    .filter((p) => (p.monthlyPrice ?? 0) > 0)
    .sort((a, b) => (a.monthlyPrice ?? 0) - (b.monthlyPrice ?? 0))
  return paid[0]?.id
}

export function sortPlans(plans: SubscriptionPlanDto[]): SubscriptionPlanDto[] {
  return [...plans].sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
}

/**
 * The union of every feature any plan offers, ordered so the rows that
 * differentiate the plans surface first — that is what a comparison is for.
 */
export function comparisonFeatures(plans: SubscriptionPlanDto[]): string[] {
  const all = new Set<string>()
  for (const plan of plans) for (const f of plan.features ?? []) all.add(f)
  return [...all].sort((a, b) => {
    const countA = plans.filter((p) => p.features?.includes(a)).length
    const countB = plans.filter((p) => p.features?.includes(b)).length
    if (countA !== countB) return countB - countA
    return a.localeCompare(b)
  })
}
