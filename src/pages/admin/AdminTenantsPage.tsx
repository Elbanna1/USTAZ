import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Building2, MoreVertical, Play, Pause } from 'lucide-react'
import { adminApi, type TenantQuery } from '@/api/endpoints/admin'
import type { AdminTenantDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { StatusBadge } from '@/components/common/StatusBadge'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 20

const STATUSES = [
  { value: undefined, ar: 'الكل', en: 'All' },
  { value: 'Active', ar: 'نشطة', en: 'Active' },
  { value: 'Suspended', ar: 'موقوفة', en: 'Suspended' },
  { value: 'Archived', ar: 'مؤرشفة', en: 'Archived' },
] as const

export default function AdminTenantsPage() {
  const { t, locale, formatNumber, formatCurrency } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  usePageTitle(t('admin.tenants'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [status, setStatus] = React.useState<string>()
  const [suspending, setSuspending] = React.useState<AdminTenantDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query: TenantQuery = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Search: debounced.trim() || undefined, Status: status }),
    [page, debounced, status],
  )

  const tenants = useQuery({
    queryKey: qk.admin.tenants(query),
    queryFn: () => adminApi.tenants(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, status])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin'] })

  const activate = useMutation({
    mutationFn: (id: string) => adminApi.activateTenant(id),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.active'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const suspend = useMutation({
    mutationFn: (id: string) =>
      adminApi.suspendTenant(
        id,
        locale === 'ar-EG' ? 'إيقاف إداري' : 'Suspended by platform admin',
      ),
    onSuccess: () => {
      invalidate()
      setSuspending(null)
      toast({ title: t('common.inactive'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const columns: Column<AdminTenantDto>[] = [
    {
      id: 'tenant',
      header: t('admin.tenants'),
      cell: (row) => (
        <div className="flex min-w-0 items-center gap-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
            <Building2 className="size-4" />
          </span>
          <div className="min-w-0">
            <p className="truncate font-display text-body-sm font-semibold text-foreground">
              {row.name}
            </p>
            <p className="truncate font-latin text-caption text-muted-foreground">{row.slug}</p>
          </div>
        </div>
      ),
      sortValue: (row) => row.name ?? '',
    },
    {
      id: 'owner',
      header: locale === 'ar-EG' ? 'المالك' : 'Owner',
      hideOnMobile: true,
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate text-body-sm text-foreground">{row.ownerDisplayName}</p>
          <p className="truncate font-latin text-caption text-muted-foreground">{row.ownerEmail}</p>
        </div>
      ),
    },
    {
      id: 'plan',
      header: t('billing.plans'),
      cell: (row) => (
        <div className="flex flex-wrap items-center gap-1.5">
          {row.planCode && (
            <Badge variant="primary" size="sm" className="font-latin">
              {row.planCode}
            </Badge>
          )}
          <StatusBadge status={row.subscriptionStatus} />
        </div>
      ),
    },
    {
      id: 'scale',
      header: locale === 'ar-EG' ? 'الحجم' : 'Scale',
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {formatNumber(row.studentsCount ?? 0)} {t('common.students')} ·{' '}
          {formatNumber(row.groupsCount ?? 0)} {t('common.groups')}
        </span>
      ),
      sortValue: (row) => row.studentsCount ?? 0,
    },
    {
      id: 'revenue',
      header: t('dashboard.revenue'),
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {formatCurrency(row.lifetimeRevenue ?? 0)}
        </span>
      ),
      sortValue: (row) => row.lifetimeRevenue ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
    },
    {
      id: 'actions',
      header: '',
      cell: (row) => (
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon-sm" aria-label={t('common.actions')}>
              <MoreVertical className="size-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            {row.status === 'Active' ? (
              <DropdownMenuItem className="text-destructive-strong" onSelect={() => setSuspending(row)}>
                <Pause className="size-4" />
                {t('admin.suspend')}
              </DropdownMenuItem>
            ) : (
              <DropdownMenuItem onSelect={() => row.id && activate.mutate(row.id)}>
                <Play className="size-4 flip-rtl" />
                {t('admin.activate')}
              </DropdownMenuItem>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('admin.tenants')}
        description={
          locale === 'ar-EG'
            ? 'كل مساحات العمل على المنصة، اشتراكاتها وحجمها.'
            : 'Every workspace on the platform — its subscription and its scale.'
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="admin-tenants-search"
          />
          <div className="no-scrollbar flex gap-2 overflow-x-auto">
            {STATUSES.map((s) => (
              <FilterChip
                key={s.value ?? 'all'}
                label={s[lang]}
                active={status === s.value}
                onClick={() => setStatus(s.value)}
              />
            ))}
          </div>
        </div>
      </DashboardHeader>

      <DataTable
        rows={tenants.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? row.slug ?? ''}
        loading={tenants.isPending}
        error={tenants.isError ? tenants.error : undefined}
        onRetry={() => tenants.refetch()}
        isFetching={tenants.isFetching}
        serverPage={tenants.data}
        onPageChange={setPage}
        empty={{
          spot: 'search',
          title: locale === 'ar-EG' ? 'مفيش مساحات عمل' : 'No workspaces',
        }}
        mobileCard={(row) => (
          <Card className="space-y-2.5 p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate font-display text-body-sm font-bold text-foreground">
                  {row.name}
                </p>
                <p className="truncate font-latin text-caption text-muted-foreground">{row.slug}</p>
              </div>
              <StatusBadge status={row.status} fallbackLabel={row.statusName} />
            </div>
            <p className="tnum text-caption text-muted-foreground">
              {formatNumber(row.studentsCount ?? 0)} {t('common.students')} ·{' '}
              {formatCurrency(row.lifetimeRevenue ?? 0)}
            </p>
          </Card>
        )}
      />

      <ConfirmDialog
        open={Boolean(suspending)}
        onOpenChange={(open) => !open && setSuspending(null)}
        title={locale === 'ar-EG' ? 'إيقاف مساحة العمل؟' : 'Suspend this workspace?'}
        description={
          locale === 'ar-EG'
            ? `هيتم إيقاف «${suspending?.name}» وكل المستخدمين فيها.`
            : `This suspends "${suspending?.name}" and everyone inside it.`
        }
        confirmLabel={t('admin.suspend')}
        loading={suspend.isPending}
        onConfirm={() => suspending?.id && suspend.mutate(suspending.id)}
      />
    </>
  )
}
