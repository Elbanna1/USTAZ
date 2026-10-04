import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { ArrowUpRight, Check, CreditCard, Receipt, Sparkles } from 'lucide-react'
import { billingApi } from '@/api/endpoints/platform'
import type { SubscriptionPlanDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { featureLabel } from '@/lib/plans'
import { cn, percent } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { StatusBadge } from '@/components/common/StatusBadge'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Progress } from '@/components/ui/primitives'
import { GoldRule } from '@/components/illustrations/CoverArt'
import type { InvoiceDto, BillingTransactionDto } from '@/api/schema'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const QUOTA_LABELS: Record<string, { ar: string; en: string }> = {
  Students: { ar: 'الطلاب', en: 'Students' },
  Groups: { ar: 'المجموعات', en: 'Groups' },
  Courses: { ar: 'الكورسات', en: 'Courses' },
  Assistants: { ar: 'المساعدين', en: 'Assistants' },
  TeamMembers: { ar: 'أعضاء الفريق', en: 'Team members' },
  Storage: { ar: 'المساحة', en: 'Storage' },
}

export default function TeacherBillingPage() {
  const { t, locale, formatNumber, formatCurrency, formatDate } = useI18n()
  usePageTitle(t('billing.title'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()
  const [cycle, setCycle] = React.useState<'Monthly' | 'Yearly'>('Monthly')
  const [cancelOpen, setCancelOpen] = React.useState(false)

  const subscription = useQuery({
    queryKey: qk.billing.subscription,
    queryFn: billingApi.subscription,
    retry: false,
  })

  const plans = useQuery({
    queryKey: qk.billing.plans,
    queryFn: billingApi.plans,
  })

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['billing'] })

  const changePlan = useMutation({
    mutationFn: (planId: string) => billingApi.changePlan({ planId, billingCycle: cycle }),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const checkout = useMutation({
    mutationFn: (planId: string) =>
      billingApi.checkout({ planId, billingCycle: cycle, provider: 'manual' }),
    onSuccess: (session) => {
      invalidate()
      if (session.checkoutUrl) {
        window.open(session.checkoutUrl, '_blank', 'noopener,noreferrer')
      } else {
        toast({
          title: locale === 'ar-EG' ? 'الفاتورة اتعملت' : 'Invoice created',
          description: session.invoiceNumber ?? undefined,
          variant: 'success',
        })
      }
    },
    onError: (error) =>
      showApiError(error),
  })

  const cancel = useMutation({
    mutationFn: () => billingApi.cancel({ immediately: false }),
    onSuccess: () => {
      invalidate()
      setCancelOpen(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const reactivate = useMutation({
    mutationFn: () => billingApi.reactivate(),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const current = subscription.data
  const currentPlanId = current?.plan?.id

  return (
    <>
      <DashboardHeader
        title={t('billing.title')}
        description={
          locale === 'ar-EG'
            ? 'باقتك الحالية، حدود الاستخدام، والفواتير.'
            : 'Your plan, usage limits, and invoices.'
        }
      />

      {/* --------------------------- Current plan --------------------------- */}
      {subscription.isPending ? (
        <RowSkeleton count={2} />
      ) : current ? (
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={transitions.base}
        >
          <Card className="overflow-hidden">
            <div className="relative bg-gradient-to-bl from-primary via-primary-soft to-primary p-6 text-primary-foreground">
              <div className="absolute inset-0 bg-dot-grid opacity-[0.08]" aria-hidden />
              <div className="relative flex flex-wrap items-start justify-between gap-4">
                <div>
                  <p className="text-label uppercase tracking-wide text-primary-foreground/70">
                    {t('billing.currentPlan')}
                  </p>
                  <h2 className="mt-1 font-display text-display-lg text-primary-foreground">
                    {locale === 'ar-EG' ? current.plan?.nameAr : current.plan?.nameEn}
                  </h2>
                  <GoldRule className="mt-4 max-w-[8rem]" />
                </div>

                <div className="text-end">
                  <StatusBadge status={current.status} fallbackLabel={current.statusName} size="md" />
                  <p className="tnum mt-2 font-display text-heading-md font-extrabold text-primary-foreground">
                    {formatCurrency(current.amount ?? 0, current.currency)}
                  </p>
                  <p className="text-caption text-primary-foreground/70">
                    {current.billingCycle === 'Yearly' ? t('billing.yearly') : t('billing.monthly')}
                  </p>
                </div>
              </div>
            </div>

            <div className="space-y-5 p-6">
              <div className="flex flex-wrap items-center gap-4 text-body-sm text-muted-foreground">
                {current.isTrialing && current.trialEndsAt && (
                  <Badge variant="accent" size="md">
                    <Sparkles className="size-3.5" />
                    {t('billing.trial')} · {formatDate(current.trialEndsAt, 'd MMM yyyy')}
                  </Badge>
                )}
                {current.currentPeriodEnd && (
                  <span className="tnum">
                    {current.cancelAtPeriodEnd
                      ? t('billing.endsOn', {
                          date: formatDate(current.currentPeriodEnd, 'd MMM yyyy'),
                        })
                      : t('billing.renewsOn', {
                          date: formatDate(current.currentPeriodEnd, 'd MMM yyyy'),
                        })}
                  </span>
                )}
                {current.daysRemaining != null && (
                  <Badge variant="outline" size="sm" className="tnum">
                    {formatNumber(current.daysRemaining)}{' '}
                    {locale === 'ar-EG' ? 'يوم متبقي' : 'days left'}
                  </Badge>
                )}
              </div>

              {/* Usage quotas */}
              {(current.usage?.length ?? 0) > 0 && (
                <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                  {current.usage!.map((quota) => {
                    const used = quota.used ?? 0
                    const limit = quota.limit ?? 0
                    const pct = quota.isUnlimited ? 0 : percent(used, limit || 1)
                    return (
                      <div
                        key={quota.quota}
                        className="rounded-xl border border-border bg-surface p-4"
                      >
                        <div className="flex items-baseline justify-between gap-2">
                          <p className="font-display text-body-sm font-semibold text-foreground">
                            {QUOTA_LABELS[quota.quota ?? '']?.[
                              locale === 'ar-EG' ? 'ar' : 'en'
                            ] ?? quota.quota}
                          </p>
                          <p className="tnum text-caption text-muted-foreground">
                            {formatNumber(used)}
                            {quota.isUnlimited
                              ? ' / ∞'
                              : limit
                                ? ` / ${formatNumber(limit)}`
                                : ''}
                          </p>
                        </div>
                        <Progress
                          value={pct}
                          className="mt-2.5 h-1.5"
                          indicatorClassName={
                            pct >= 90 ? 'bg-destructive' : pct >= 70 ? 'bg-warning' : undefined
                          }
                        />
                      </div>
                    )
                  })}
                </div>
              )}

              <div className="flex flex-wrap gap-2 border-t border-border pt-5">
                {current.cancelAtPeriodEnd ? (
                  <Button loading={reactivate.isPending} onClick={() => reactivate.mutate()}>
                    {t('billing.reactivate')}
                  </Button>
                ) : (
                  <Button variant="outline" onClick={() => setCancelOpen(true)}>
                    {t('billing.cancel')}
                  </Button>
                )}
              </div>
            </div>
          </Card>
        </motion.div>
      ) : subscription.isError ? (
        <EmptyState
          spot="payments"
          title={locale === 'ar-EG' ? 'مفيش اشتراك نشط' : 'No active subscription'}
          description={
            locale === 'ar-EG'
              ? 'اختار باقة من تحت عشان تبدأ.'
              : 'Pick a plan below to get started.'
          }
        />
      ) : null}

      {/* ------------------------------- Plans ------------------------------ */}
      <DashboardSection
        title={t('billing.plans')}
        className="mt-10"
        action={
          <div className="flex items-center rounded-lg border border-border bg-card p-1">
            {(['Monthly', 'Yearly'] as const).map((value) => (
              <button
                key={value}
                type="button"
                onClick={() => setCycle(value)}
                aria-pressed={cycle === value}
                className={cn(
                  'rounded-md px-3 py-1.5 font-display text-caption font-semibold transition-colors',
                  cycle === value
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:text-foreground',
                )}
              >
                {value === 'Monthly' ? t('billing.monthly') : t('billing.yearly')}
              </button>
            ))}
          </div>
        }
      >
        {plans.isPending ? (
          <RowSkeleton count={3} />
        ) : plans.isError ? (
          <QueryError error={plans.error} onRetry={() => plans.refetch()} />
        ) : (
          <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
            {(plans.data ?? []).map((plan) => (
              <PlanCard
                key={plan.id}
                plan={plan}
                cycle={cycle}
                isCurrent={plan.id === currentPlanId}
                busy={changePlan.isPending || checkout.isPending}
                onSelect={() => {
                  if (!plan.id) return
                  if (current) changePlan.mutate(plan.id)
                  else checkout.mutate(plan.id)
                }}
              />
            ))}
          </div>
        )}
      </DashboardSection>

      {/* ---------------------- Invoices & transactions ---------------------- */}
      <DashboardSection title={t('billing.invoices')} className="mt-10">
        <Tabs defaultValue="invoices">
          <TabsList>
            <TabsTrigger value="invoices">
              <Receipt className="size-4" />
              {t('billing.invoices')}
            </TabsTrigger>
            <TabsTrigger value="transactions">
              <CreditCard className="size-4" />
              {t('billing.transactions')}
            </TabsTrigger>
          </TabsList>

          <TabsContent value="invoices" className="mt-6">
            <InvoicesTable />
          </TabsContent>
          <TabsContent value="transactions" className="mt-6">
            <TransactionsTable />
          </TabsContent>
        </Tabs>
      </DashboardSection>

      <ConfirmDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        title={t('billing.cancelConfirmTitle')}
        description={t('billing.cancelConfirmBody')}
        confirmLabel={t('billing.cancel')}
        loading={cancel.isPending}
        onConfirm={() => cancel.mutate()}
      />
    </>
  )
}

function PlanCard({
  plan,
  cycle,
  isCurrent,
  busy,
  onSelect,
}: {
  plan: SubscriptionPlanDto
  cycle: 'Monthly' | 'Yearly'
  isCurrent: boolean
  busy: boolean
  onSelect: () => void
}) {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  const price = cycle === 'Yearly' ? (plan.yearlyPrice ?? plan.monthlyPrice ?? 0) : (plan.monthlyPrice ?? 0)

  const limits = [
    { label: t('common.students'), value: plan.maxStudents },
    { label: t('common.groups'), value: plan.maxGroups },
    { label: t('common.courses'), value: plan.maxCourses },
    { label: t('assistants.title'), value: plan.maxAssistants },
  ]

  return (
    <Card
      className={cn(
        'relative flex h-full flex-col p-6 transition-all duration-200',
        isCurrent ? 'border-primary shadow-glow' : 'hover:-translate-y-0.5 hover:shadow-lift',
      )}
    >
      {isCurrent && (
        <Badge variant="solid" size="sm" className="absolute -top-2.5 end-5">
          {t('billing.currentPlan')}
        </Badge>
      )}

      <h3 className="font-display text-heading-sm font-bold text-foreground">
        {locale === 'ar-EG' ? plan.nameAr : plan.nameEn}
      </h3>
      {plan.description && (
        <p className="mt-1.5 text-body-sm text-muted-foreground">{plan.description}</p>
      )}

      <p className="mt-5 flex items-baseline gap-2">
        <span className="tnum font-display text-display-lg font-extrabold leading-none text-foreground">
          {price === 0 ? t('common.free') : formatCurrency(price, plan.currency)}
        </span>
        {price > 0 && (
          <span className="text-caption text-muted-foreground">
            / {cycle === 'Yearly' ? t('billing.yearly') : t('billing.monthly')}
          </span>
        )}
      </p>

      {(plan.trialDays ?? 0) > 0 && !isCurrent && (
        <Badge variant="accent" size="sm" className="mt-3 self-start">
          <Sparkles className="size-3.5" />
          {formatNumber(plan.trialDays ?? 0)} {locale === 'ar-EG' ? 'يوم تجربة' : 'day trial'}
        </Badge>
      )}

      <dl className="mt-5 space-y-2 border-t border-border pt-5">
        {limits.map((limit) => (
          <div key={limit.label} className="flex items-center justify-between gap-2 text-body-sm">
            <dt className="text-muted-foreground">{limit.label}</dt>
            <dd className="tnum font-semibold text-foreground">
              {limit.value == null ? '∞' : formatNumber(limit.value)}
            </dd>
          </div>
        ))}
      </dl>

      {(plan.features?.length ?? 0) > 0 && (
        <ul className="mt-4 space-y-1.5">
          {plan.features!.map((feature) => (
            <li key={feature} className="flex items-center gap-2 text-caption text-muted-foreground">
              <Check className="size-3.5 shrink-0 text-success-strong" />
              {featureLabel(feature)[locale === 'ar-EG' ? 'ar' : 'en']}
            </li>
          ))}
        </ul>
      )}

      <div className="mt-auto pt-6">
        <Button
          block
          variant={isCurrent ? 'outline' : 'primary'}
          disabled={isCurrent || busy}
          onClick={onSelect}
        >
          {isCurrent ? t('billing.currentPlan') : t('billing.changePlan')}
          {!isCurrent && <ArrowUpRight className="size-4 flip-rtl" />}
        </Button>
      </div>
    </Card>
  )
}

function InvoicesTable() {
  const { t, locale, formatCurrency, formatDate } = useI18n()
  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: 10 }), [page])

  const invoices = useQuery({
    queryKey: qk.billing.invoices(query),
    queryFn: () => billingApi.invoices(query),
    placeholderData: keepPreviousData,
  })

  const columns: Column<InvoiceDto>[] = [
    {
      id: 'number',
      header: locale === 'ar-EG' ? 'رقم الفاتورة' : 'Invoice',
      cell: (row) => (
        <span className="font-latin text-body-sm font-semibold text-foreground">{row.number}</span>
      ),
    },
    {
      id: 'issued',
      header: t('common.date'),
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {row.issuedAt ? formatDate(row.issuedAt, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.issuedAt ?? '',
    },
    {
      id: 'total',
      header: t('common.total'),
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {formatCurrency(row.total ?? 0, row.currency)}
        </span>
      ),
      sortValue: (row) => row.total ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
    },
  ]

  return (
    <DataTable
      rows={invoices.data?.items ?? []}
      columns={columns}
      rowKey={(row) => row.id ?? row.number ?? ''}
      loading={invoices.isPending}
      error={invoices.isError ? invoices.error : undefined}
      onRetry={() => invoices.refetch()}
      isFetching={invoices.isFetching}
      serverPage={invoices.data}
      onPageChange={setPage}
      empty={{
        spot: 'payments',
        title: locale === 'ar-EG' ? 'مفيش فواتير' : 'No invoices yet',
      }}
    />
  )
}

function TransactionsTable() {
  const { t, locale, formatCurrency, formatDate } = useI18n()
  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: 10 }), [page])

  const transactions = useQuery({
    queryKey: qk.billing.transactions(query),
    queryFn: () => billingApi.transactions(query),
    placeholderData: keepPreviousData,
  })

  const columns: Column<BillingTransactionDto>[] = [
    {
      id: 'provider',
      header: locale === 'ar-EG' ? 'وسيلة الدفع' : 'Provider',
      cell: (row) => (
        <span className="font-latin text-body-sm text-foreground">{row.provider}</span>
      ),
    },
    {
      id: 'date',
      header: t('common.date'),
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {row.paidAt
            ? formatDate(row.paidAt, 'd MMM yyyy')
            : row.createdAt
              ? formatDate(row.createdAt, 'd MMM yyyy')
              : '—'}
        </span>
      ),
      sortValue: (row) => row.paidAt ?? row.createdAt ?? '',
    },
    {
      id: 'amount',
      header: t('common.total'),
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {formatCurrency(row.amount ?? 0, row.currency)}
        </span>
      ),
      sortValue: (row) => row.amount ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
    },
  ]

  return (
    <DataTable
      rows={transactions.data?.items ?? []}
      columns={columns}
      rowKey={(row) => row.id ?? ''}
      loading={transactions.isPending}
      error={transactions.isError ? transactions.error : undefined}
      onRetry={() => transactions.refetch()}
      isFetching={transactions.isFetching}
      serverPage={transactions.data}
      onPageChange={setPage}
      empty={{
        spot: 'payments',
        title: locale === 'ar-EG' ? 'مفيش معاملات' : 'No transactions yet',
      }}
    />
  )
}
