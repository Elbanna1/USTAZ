import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { CircleDollarSign, Receipt, Wallet } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import type { EducationalPaymentDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatusBadge } from '@/components/common/StatusBadge'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { StatSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const PAGE_SIZE = 20

export default function StudentPaymentsPage() {
  const { t, locale, formatCurrency, formatDate } = useI18n()
  usePageTitle(t('dashboard.payments'))

  const [page, setPage] = React.useState(1)
  const query = React.useMemo(() => ({ Page: page, PageSize: PAGE_SIZE }), [page])

  const payments = useQuery({
    queryKey: qk.student.payments(query),
    queryFn: () => studentApi.payments(query),
    placeholderData: keepPreviousData,
  })

  const all = useQuery({
    queryKey: qk.student.payments({ PageSize: 500 }),
    queryFn: () => studentApi.payments({ PageSize: 500 }),
  })

  const summary = React.useMemo(() => {
    const items = all.data?.items ?? []
    const sum = (predicate: (p: EducationalPaymentDto) => boolean) =>
      items.filter(predicate).reduce((total, p) => total + (p.netAmount ?? 0), 0)
    return {
      paid: sum((p) => p.status === 'Paid'),
      pending: sum((p) => p.status === 'Pending'),
      count: items.length,
    }
  }, [all.data])

  const columns: Column<EducationalPaymentDto>[] = [
    {
      id: 'invoice',
      header: locale === 'ar-EG' ? 'الفاتورة' : 'Invoice',
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-display text-body-sm font-semibold text-foreground">
            {row.groupName || row.courseTitle || row.typeName || '—'}
          </p>
          <p className="truncate font-latin text-caption text-muted-foreground">
            {row.invoiceNumber}
          </p>
        </div>
      ),
      sortValue: (row) => row.invoiceNumber ?? '',
    },
    {
      id: 'period',
      header: locale === 'ar-EG' ? 'الفترة' : 'Period',
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {row.periodMonth ? formatDate(row.periodMonth, 'MMMM yyyy') : '—'}
        </span>
      ),
    },
    {
      id: 'due',
      header: t('common.date'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {row.paidAt
            ? formatDate(row.paidAt, 'd MMM yyyy')
            : row.dueAt
              ? formatDate(row.dueAt, 'd MMM yyyy')
              : '—'}
        </span>
      ),
      sortValue: (row) => row.paidAt ?? row.dueAt ?? '',
    },
    {
      id: 'amount',
      header: t('common.total'),
      cell: (row) => (
        <div className="text-end">
          <p className="tnum font-display text-body-sm font-bold text-foreground">
            {formatCurrency(row.netAmount ?? 0, row.currency)}
          </p>
          {(row.discountAmount ?? 0) > 0 && (
            <p className="tnum text-caption text-success-strong">
              −{formatCurrency(row.discountAmount ?? 0, row.currency)}
            </p>
          )}
        </div>
      ),
      sortValue: (row) => row.netAmount ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
      sortValue: (row) => row.status ?? '',
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('dashboard.payments')}
        description={
          locale === 'ar-EG'
            ? 'كل مدفوعاتك للمجموعات والكورسات.'
            : 'Every payment you have made for groups and courses.'
        }
      />

      {all.isPending ? (
        <StatSkeleton count={3} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-3">
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'مدفوع' : 'Paid'}
              value={summary.paid}
              icon={<Wallet />}
              description={formatCurrency(summary.paid)}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'مستحق' : 'Outstanding'}
              value={summary.pending}
              icon={<CircleDollarSign />}
              description={formatCurrency(summary.pending)}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'عدد الفواتير' : 'Invoices'}
              value={summary.count}
              icon={<Receipt />}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8">
        <DataTable
          rows={payments.data?.items ?? []}
          columns={columns}
          rowKey={(row) => row.id ?? row.invoiceNumber ?? ''}
          loading={payments.isPending}
          error={payments.isError ? payments.error : undefined}
          onRetry={() => payments.refetch()}
          isFetching={payments.isFetching}
          serverPage={payments.data}
          onPageChange={setPage}
          empty={{
            spot: 'payments',
            title: locale === 'ar-EG' ? 'مفيش مدفوعات' : 'No payments yet',
            description:
              locale === 'ar-EG'
                ? 'فواتيرك هتظهر هنا أول ما تشترك في مجموعة أو كورس.'
                : 'Your invoices appear here once you enroll in a group or course.',
            action: { label: t('nav.groups'), href: '/groups' },
          }}
          mobileCard={(row) => (
            <Card className="space-y-2.5 p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="truncate font-display text-body-sm font-bold text-foreground">
                    {row.groupName || row.courseTitle || row.typeName}
                  </p>
                  <p className="truncate font-latin text-caption text-muted-foreground">
                    {row.invoiceNumber}
                  </p>
                </div>
                <StatusBadge status={row.status} fallbackLabel={row.statusName} />
              </div>
              <p className="tnum font-display text-heading-sm font-extrabold text-foreground">
                {formatCurrency(row.netAmount ?? 0, row.currency)}
              </p>
            </Card>
          )}
        />
      </div>
    </>
  )
}
