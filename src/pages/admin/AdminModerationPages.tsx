import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { Check, CreditCard, Sparkles, X } from 'lucide-react'
import { adminApi, type AuditLogQuery } from '@/api/endpoints/admin'
import type { AuditLogDto, ReviewDto, SubscriptionPlanDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { Pagination } from '@/components/common/Pagination'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Rating } from '@/components/common/Rating'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { RevenueAreaChart } from '@/components/dashboard/charts'
import { useApiErrorToast } from '@/hooks/use-api-toast'

/* -------------------------------------------------------------------------- */
/*                              Review moderation                             */
/* -------------------------------------------------------------------------- */

export function AdminReviewsPage() {
  const { t, locale, formatDate } = useI18n()
  usePageTitle(t('admin.moderation'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [pendingOnly, setPendingOnly] = React.useState(true)
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({
      Page: page,
      PageSize: 15,
      Search: debounced.trim() || undefined,
      IncludeUnapproved: true,
    }),
    [page, debounced],
  )

  const reviews = useQuery({
    queryKey: qk.admin.reviews(query),
    queryFn: () => adminApi.reviews(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced])

  const moderate = useMutation({
    mutationFn: ({ id, approved }: { id: string; approved: boolean }) =>
      adminApi.moderateReview(id, { isApproved: approved }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'reviews'] })
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const all = reviews.data?.items ?? []
  const items = pendingOnly ? all.filter((r) => !r.isApproved) : all

  return (
    <>
      <DashboardHeader
        title={t('admin.moderation')}
        description={
          locale === 'ar-EG'
            ? 'راجع تقييمات الطلاب قبل ما تظهر على البروفايلات.'
            : 'Review student ratings before they appear on public profiles.'
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="admin-reviews-search"
          />
          <div className="flex gap-2">
            <FilterChip
              label={locale === 'ar-EG' ? 'تحت المراجعة' : 'Pending'}
              active={pendingOnly}
              onClick={() => setPendingOnly(true)}
            />
            <FilterChip
              label={t('common.all')}
              active={!pendingOnly}
              onClick={() => setPendingOnly(false)}
            />
          </div>
        </div>
      </DashboardHeader>

      <div className={cn('transition-opacity', reviews.isFetching && 'opacity-60')}>
        {reviews.isPending ? (
          <RowSkeleton count={4} />
        ) : reviews.isError ? (
          <QueryError error={reviews.error} onRetry={() => reviews.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="success"
            title={
              pendingOnly
                ? locale === 'ar-EG'
                  ? 'مفيش تقييمات مستنية مراجعة'
                  : 'Nothing waiting for review'
                : locale === 'ar-EG'
                  ? 'مفيش تقييمات'
                  : 'No reviews'
            }
          />
        ) : (
          <div className="space-y-4">
            {items.map((review, i) => (
              <ReviewModerationCard
                key={review.id}
                review={review}
                index={i}
                busy={moderate.isPending}
                onModerate={(approved) =>
                  review.id && moderate.mutate({ id: review.id, approved })
                }
                formatDate={formatDate}
              />
            ))}
          </div>
        )}
      </div>

      {reviews.data && all.length > 0 && (
        <Pagination page={reviews.data} onChange={setPage} className="mt-8" />
      )}
    </>
  )
}

function ReviewModerationCard({
  review,
  index,
  busy,
  onModerate,
  formatDate,
}: {
  review: ReviewDto
  index: number
  busy: boolean
  onModerate: (approved: boolean) => void
  formatDate: (value: string, pattern?: string) => string
}) {
  const { t, locale } = useI18n()

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ ...transitions.base, delay: Math.min(index * 0.04, 0.3) }}
    >
      <Card className={cn('p-5', !review.isApproved && 'border-warning/30 bg-warning/[0.03]')}>
        <div className="flex flex-wrap items-start gap-4">
          <Avatar
            traits={avatarTraitsFor(review.studentId ?? '')}
            seed={review.studentId ?? ''}
            name={review.studentName ?? ''}
            src={imageUrl(review.studentImageUrl)}
            size="sm"
          />

          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p className="font-display text-body-sm font-bold text-foreground">
                {review.studentName}
              </p>
              <Rating value={review.rating ?? 0} size="sm" />
              <Badge variant="outline" size="sm">
                {review.targetTypeName ?? review.targetType}
              </Badge>
              {!review.isApproved && (
                <Badge variant="warning" size="sm">
                  {locale === 'ar-EG' ? 'تحت المراجعة' : 'Pending'}
                </Badge>
              )}
            </div>

            <p className="mt-1 text-caption text-muted-foreground">
              {review.teacherName}
              {review.groupName ? ` · ${review.groupName}` : ''}
              {review.courseTitle ? ` · ${review.courseTitle}` : ''}
            </p>

            {review.comment && (
              <p className="mt-2.5 text-body-sm leading-relaxed text-foreground">
                {review.comment}
              </p>
            )}

            {review.createdAt && (
              <p className="tnum mt-2 text-caption text-muted-foreground/70">
                {formatDate(review.createdAt, 'd MMM yyyy')}
              </p>
            )}
          </div>

          <div className="flex shrink-0 gap-2">
            {!review.isApproved && (
              <Button size="sm" disabled={busy} onClick={() => onModerate(true)}>
                <Check className="size-4" />
                {t('admin.approve')}
              </Button>
            )}
            {review.isApproved && (
              <Button
                size="sm"
                variant="outline"
                disabled={busy}
                onClick={() => onModerate(false)}
                className="text-destructive-strong"
              >
                <X className="size-4" />
                {t('admin.reject')}
              </Button>
            )}
          </div>
        </div>
      </Card>
    </motion.div>
  )
}

/* -------------------------------------------------------------------------- */
/*                                 Audit logs                                 */
/* -------------------------------------------------------------------------- */

export function AdminAuditLogsPage() {
  const { t, locale, formatDateTime } = useI18n()
  usePageTitle(t('admin.auditLogs'))

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const debounced = useDebounced(search, 300)

  const query: AuditLogQuery = React.useMemo(
    () => ({ Page: page, PageSize: 25, Search: debounced.trim() || undefined }),
    [page, debounced],
  )

  const logs = useQuery({
    queryKey: qk.admin.auditLogs(query),
    queryFn: () => adminApi.auditLogs(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced])

  const columns: Column<AuditLogDto>[] = [
    {
      id: 'when',
      header: t('common.date'),
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {row.createdAt ? formatDateTime(row.createdAt) : '—'}
        </span>
      ),
      sortValue: (row) => row.createdAt ?? '',
    },
    {
      id: 'action',
      header: locale === 'ar-EG' ? 'العملية' : 'Action',
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-latin text-body-sm font-semibold text-foreground">
            {row.action}
          </p>
          {row.entityName && (
            <p className="truncate font-latin text-caption text-muted-foreground">
              {row.entityName}
            </p>
          )}
        </div>
      ),
      sortValue: (row) => row.action ?? '',
    },
    {
      id: 'user',
      header: t('admin.users'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="truncate font-latin text-caption text-muted-foreground">
          {row.userEmail ?? '—'}
        </span>
      ),
    },
    {
      id: 'ip',
      header: 'IP',
      hideOnMobile: true,
      cell: (row) => (
        <span className="font-latin text-caption text-muted-foreground">
          {row.ipAddress ?? '—'}
        </span>
      ),
    },
    {
      id: 'result',
      header: t('common.status'),
      cell: (row) => (
        <Badge variant={row.succeeded ? 'success' : 'destructive'} size="sm">
          {row.succeeded
            ? locale === 'ar-EG'
              ? 'نجحت'
              : 'Succeeded'
            : locale === 'ar-EG'
              ? 'فشلت'
              : 'Failed'}
        </Badge>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('admin.auditLogs')}
        description={
          locale === 'ar-EG'
            ? 'سجل كل العمليات الحساسة على المنصة.'
            : 'A record of every sensitive action on the platform.'
        }
      >
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="admin-audit-search"
        />
      </DashboardHeader>

      <DataTable
        rows={logs.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={logs.isPending}
        error={logs.isError ? logs.error : undefined}
        onRetry={() => logs.refetch()}
        isFetching={logs.isFetching}
        serverPage={logs.data}
        onPageChange={setPage}
        empty={{ spot: 'search', title: locale === 'ar-EG' ? 'السجل فاضي' : 'The log is empty' }}
        mobileCard={(row) => (
          <Card className="space-y-2 p-4">
            <div className="flex items-start justify-between gap-3">
              <p className="truncate font-latin text-body-sm font-bold text-foreground">
                {row.action}
              </p>
              <Badge variant={row.succeeded ? 'success' : 'destructive'} size="sm">
                {row.succeeded ? 'OK' : '!'}
              </Badge>
            </div>
            <p className="tnum text-caption text-muted-foreground">
              {row.createdAt ? formatDateTime(row.createdAt) : '—'}
            </p>
          </Card>
        )}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Plans                                    */
/* -------------------------------------------------------------------------- */

export function AdminPlansPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  usePageTitle(t('admin.plans'))

  const plans = useQuery({ queryKey: qk.admin.plans, queryFn: adminApi.plans })

  return (
    <>
      <DashboardHeader
        title={t('admin.plans')}
        description={
          locale === 'ar-EG'
            ? 'باقات الاشتراك المتاحة للمدرّسين والسناتر.'
            : 'Subscription plans available to teachers and centres.'
        }
      />

      {plans.isPending ? (
        <RowSkeleton count={3} />
      ) : plans.isError ? (
        <QueryError error={plans.error} onRetry={() => plans.refetch()} />
      ) : (plans.data ?? []).length === 0 ? (
        <EmptyState spot="payments" title={locale === 'ar-EG' ? 'مفيش باقات' : 'No plans'} />
      ) : (
        <div className="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
          {(plans.data ?? []).map((plan: SubscriptionPlanDto) => (
            <Card key={plan.id} className="flex h-full flex-col p-6">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <h3 className="font-display text-heading-sm font-bold text-foreground">
                    {locale === 'ar-EG' ? plan.nameAr : plan.nameEn}
                  </h3>
                  <Badge variant="outline" size="sm" className="mt-1.5 font-latin">
                    {plan.code}
                  </Badge>
                </div>
                <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-primary/10 text-primary">
                  <CreditCard className="size-5" />
                </span>
              </div>

              {plan.description && (
                <p className="mt-3 text-body-sm text-muted-foreground">{plan.description}</p>
              )}

              <p className="mt-4 flex items-baseline gap-2">
                <span className="tnum font-display text-display-lg font-extrabold leading-none text-foreground">
                  {(plan.monthlyPrice ?? 0) === 0
                    ? t('common.free')
                    : formatCurrency(plan.monthlyPrice ?? 0, plan.currency)}
                </span>
                {(plan.monthlyPrice ?? 0) > 0 && (
                  <span className="text-caption text-muted-foreground">/ {t('billing.monthly')}</span>
                )}
              </p>

              {(plan.trialDays ?? 0) > 0 && (
                <Badge variant="accent" size="sm" className="mt-3 self-start">
                  <Sparkles className="size-3.5" />
                  {formatNumber(plan.trialDays ?? 0)} {locale === 'ar-EG' ? 'يوم تجربة' : 'day trial'}
                </Badge>
              )}

              <dl className="mt-5 space-y-2 border-t border-border pt-5">
                {[
                  { label: t('common.students'), value: plan.maxStudents },
                  { label: t('common.groups'), value: plan.maxGroups },
                  { label: t('common.courses'), value: plan.maxCourses },
                  { label: t('assistants.title'), value: plan.maxAssistants },
                ].map((limit) => (
                  <div
                    key={limit.label}
                    className="flex items-center justify-between gap-2 text-body-sm"
                  >
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
                    <li
                      key={feature}
                      className="flex items-center gap-2 text-caption text-muted-foreground"
                    >
                      <Check className="size-3.5 shrink-0 text-success-strong" />
                      {feature}
                    </li>
                  ))}
                </ul>
              )}

              <Badge
                variant={plan.isActive ? 'success' : 'outline'}
                size="sm"
                className="mt-auto self-start pt-0"
              >
                {plan.isActive ? t('common.active') : t('common.inactive')}
              </Badge>
            </Card>
          ))}
        </div>
      )}
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Reports                                   */
/* -------------------------------------------------------------------------- */

export function AdminReportsPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  usePageTitle(t('admin.reports'))

  const [months, setMonths] = React.useState(12)

  const reports = useQuery({
    queryKey: qk.admin.revenue(months),
    queryFn: () => adminApi.reports(months),
  })

  const series = (reports.data ?? []).map((p) => ({
    label: p.label ?? '',
    value: p.totalRevenue ?? 0,
    secondary: p.courseRevenue ?? 0,
  }))
  const total = series.reduce((sum, p) => sum + p.value, 0)
  const enrollments = (reports.data ?? []).reduce((sum, p) => sum + (p.enrollmentsCount ?? 0), 0)

  return (
    <>
      <DashboardHeader
        title={t('admin.reports')}
        description={
          locale === 'ar-EG'
            ? 'تقرير الإيرادات والاشتراكات على مستوى المنصة.'
            : 'Platform-wide revenue and enrollment reporting.'
        }
        actions={
          <div className="flex gap-2">
            {[6, 12, 24].map((value) => (
              <FilterChip
                key={value}
                label={
                  locale === 'ar-EG' ? `${formatNumber(value)} شهر` : `${formatNumber(value)} months`
                }
                active={months === value}
                onClick={() => setMonths(value)}
              />
            ))}
          </div>
        }
      />

      <div className="mb-6 grid gap-4 sm:grid-cols-2">
        <Card className="p-5">
          <p className="text-body-sm text-muted-foreground">{t('dashboard.revenue')}</p>
          <p className="tnum mt-2 font-display text-display-lg font-extrabold text-foreground">
            {formatCurrency(total)}
          </p>
        </Card>
        <Card className="p-5">
          <p className="text-body-sm text-muted-foreground">
            {locale === 'ar-EG' ? 'الاشتراكات' : 'Enrollments'}
          </p>
          <p className="tnum mt-2 font-display text-display-lg font-extrabold text-foreground">
            {formatNumber(enrollments)}
          </p>
        </Card>
      </div>

      <Card className="p-5">
        {reports.isPending ? (
          <div className="h-[300px] animate-pulse rounded-lg bg-muted" />
        ) : reports.isError ? (
          <QueryError error={reports.error} onRetry={() => reports.refetch()} compact />
        ) : total === 0 ? (
          <EmptyState
            spot="payments"
            compact
            className="border-0 bg-transparent"
            title={locale === 'ar-EG' ? 'لسه مفيش إيرادات' : 'No revenue yet'}
          />
        ) : (
          <RevenueAreaChart data={series} height={320} />
        )}
      </Card>
    </>
  )
}
