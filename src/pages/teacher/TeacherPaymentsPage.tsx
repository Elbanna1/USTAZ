import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CheckCircle2, CircleDollarSign, MoreVertical, Plus, RotateCcw, Wallet } from 'lucide-react'
import {
  teacherEnrollmentsApi,
  teacherGroupsApi,
  teacherPaymentsApi,
} from '@/api/endpoints/teacher'
import type { EducationalPaymentDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatusBadge } from '@/components/common/StatusBadge'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { FormDialog } from '@/components/common/FormDialog'
import { StatSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import { Switch } from '@/components/ui/primitives'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 20

const STATUSES = [
  { value: undefined, ar: 'الكل', en: 'All' },
  { value: 'Pending', ar: 'مستحق', en: 'Pending' },
  { value: 'Paid', ar: 'مدفوع', en: 'Paid' },
  { value: 'Refunded', ar: 'مسترجع', en: 'Refunded' },
] as const

export default function TeacherPaymentsPage() {
  const { t, locale, formatCurrency, formatDate } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  usePageTitle(t('dashboard.payments'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [status, setStatus] = React.useState<string>()
  const [recordOpen, setRecordOpen] = React.useState(false)
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Search: debounced.trim() || undefined, Status: status }),
    [page, debounced, status],
  )

  const payments = useQuery({
    queryKey: qk.teacher.payments(query),
    queryFn: () => teacherPaymentsApi.list(query),
    placeholderData: keepPreviousData,
  })

  const all = useQuery({
    queryKey: qk.teacher.payments({ PageSize: 500 }),
    queryFn: () => teacherPaymentsApi.list({ PageSize: 500 }),
  })

  React.useEffect(() => setPage(1), [debounced, status])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher', 'payments'] })

  const markPaid = useMutation({
    mutationFn: (id: string) => teacherPaymentsApi.markPaid(id),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const refund = useMutation({
    mutationFn: (id: string) => teacherPaymentsApi.refund(id),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const summary = React.useMemo(() => {
    const items = all.data?.items ?? []
    const sum = (predicate: (p: EducationalPaymentDto) => boolean) =>
      items.filter(predicate).reduce((total, p) => total + (p.netAmount ?? 0), 0)
    return {
      collected: sum((p) => p.status === 'Paid'),
      outstanding: sum((p) => p.status === 'Pending'),
      refunded: sum((p) => p.status === 'Refunded'),
    }
  }, [all.data])

  const columns: Column<EducationalPaymentDto>[] = [
    {
      id: 'student',
      header: t('common.student'),
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-display text-body-sm font-semibold text-foreground">
            {row.studentName}
          </p>
          <p className="truncate text-caption text-muted-foreground">
            {row.groupName || row.courseTitle || row.typeName}
          </p>
        </div>
      ),
      sortValue: (row) => row.studentName ?? '',
    },
    {
      id: 'invoice',
      header: locale === 'ar-EG' ? 'الفاتورة' : 'Invoice',
      hideOnMobile: true,
      cell: (row) => (
        <span className="font-latin text-caption text-muted-foreground">{row.invoiceNumber}</span>
      ),
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
      id: 'amount',
      header: t('common.total'),
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {formatCurrency(row.netAmount ?? 0, row.currency)}
        </span>
      ),
      sortValue: (row) => row.netAmount ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
      sortValue: (row) => row.status ?? '',
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
            {row.status === 'Pending' && (
              <DropdownMenuItem onSelect={() => row.id && markPaid.mutate(row.id)}>
                <CheckCircle2 className="size-4" />
                {locale === 'ar-EG' ? 'علّم كمدفوع' : 'Mark as paid'}
              </DropdownMenuItem>
            )}
            {row.status === 'Paid' && (
              <DropdownMenuItem onSelect={() => row.id && refund.mutate(row.id)}>
                <RotateCcw className="size-4" />
                {locale === 'ar-EG' ? 'استرجاع' : 'Refund'}
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
        title={t('dashboard.payments')}
        description={
          locale === 'ar-EG'
            ? 'سجّل مدفوعات طلابك وتابع المستحق والمحصّل.'
            : 'Record student payments and track what is due versus collected.'
        }
        actions={
          <Button size="sm" onClick={() => setRecordOpen(true)}>
            <Plus className="size-4" />
            {locale === 'ar-EG' ? 'تسجيل دفعة' : 'Record payment'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-payments-search"
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

      {all.isPending ? (
        <StatSkeleton count={3} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-3">
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'محصّل' : 'Collected'}
              value={summary.collected}
              icon={<Wallet />}
              description={formatCurrency(summary.collected)}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'مستحق' : 'Outstanding'}
              value={summary.outstanding}
              icon={<CircleDollarSign />}
              description={formatCurrency(summary.outstanding)}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'مسترجع' : 'Refunded'}
              value={summary.refunded}
              icon={<RotateCcw />}
              description={formatCurrency(summary.refunded)}
              invertDelta
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
            title: locale === 'ar-EG' ? 'مفيش مدفوعات' : 'No payments recorded',
            description:
              locale === 'ar-EG'
                ? 'سجّل أول دفعة لطالب عشان تبدأ تتابع الإيرادات.'
                : 'Record a student payment to start tracking revenue.',
            action: {
              label: locale === 'ar-EG' ? 'تسجيل دفعة' : 'Record payment',
              onClick: () => setRecordOpen(true),
            },
          }}
          mobileCard={(row) => (
            <Card className="space-y-2.5 p-4">
              <div className="flex items-start justify-between gap-3">
                <div className="min-w-0">
                  <p className="truncate font-display text-body-sm font-bold text-foreground">
                    {row.studentName}
                  </p>
                  <p className="truncate text-caption text-muted-foreground">
                    {row.groupName || row.courseTitle}
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

      <RecordPaymentDialog open={recordOpen} onOpenChange={setRecordOpen} onSaved={invalidate} />
    </>
  )
}

function RecordPaymentDialog({
  open,
  onOpenChange,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()

  const [groupId, setGroupId] = React.useState('')
  const [studentId, setStudentId] = React.useState('')
  const [amount, setAmount] = React.useState('')
  const [periodMonth, setPeriodMonth] = React.useState(new Date().toISOString().slice(0, 7))
  const [markAsPaid, setMarkAsPaid] = React.useState(true)
  const [notes, setNotes] = React.useState('')
  const [formError, setFormError] = React.useState<string | null>(null)

  const groups = useQuery({
    queryKey: qk.teacher.groups({ PageSize: 100 }),
    queryFn: () => teacherGroupsApi.list({ PageSize: 100 }),
    enabled: open,
  })

  const roster = useQuery({
    queryKey: qk.teacher.enrollments('groups', { GroupId: groupId, PageSize: 200 }),
    queryFn: () => teacherEnrollmentsApi.groups({ GroupId: groupId, PageSize: 200 }),
    enabled: open && Boolean(groupId),
  })

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    setGroupId('')
    setStudentId('')
    setAmount('')
    setNotes('')
    setMarkAsPaid(true)
  }, [open])

  const save = useMutation({
    mutationFn: () =>
      teacherPaymentsApi.record({
        studentId,
        type: 'MonthlySubscription',
        groupId: groupId || null,
        amount: Number(amount) || 0,
        periodMonth: periodMonth ? `${periodMonth}-01` : null,
        markAsPaid,
        notes: notes.trim() || null,
      }),
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={locale === 'ar-EG' ? 'تسجيل دفعة' : 'Record payment'}
      error={formError}
      submitting={save.isPending}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        if (studentId && amount) save.mutate()
      }}
    >
      <Field label={t('common.group')} htmlFor="pay-group" required>
        <Select
          value={groupId}
          onValueChange={(v) => {
            setGroupId(v)
            setStudentId('')
          }}
        >
          <SelectTrigger id="pay-group">
            <SelectValue placeholder={t('common.group')} />
          </SelectTrigger>
          <SelectContent>
            {(groups.data?.items ?? []).map((g) => (
              <SelectItem key={g.id} value={g.id!}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field label={t('common.student')} htmlFor="pay-student" required>
        <Select value={studentId} onValueChange={setStudentId} disabled={!groupId}>
          <SelectTrigger id="pay-student">
            <SelectValue
              placeholder={
                groupId
                  ? t('common.student')
                  : locale === 'ar-EG'
                    ? 'اختار المجموعة الأول'
                    : 'Pick a group first'
              }
            />
          </SelectTrigger>
          <SelectContent>
            {(roster.data?.items ?? []).map((e) => (
              <SelectItem key={e.studentId} value={e.studentId!}>
                {e.studentName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={locale === 'ar-EG' ? 'المبلغ' : 'Amount'} htmlFor="pay-amount" required>
          <Input
            id="pay-amount"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
          />
        </Field>
        <Field label={locale === 'ar-EG' ? 'الشهر' : 'Month'} htmlFor="pay-period">
          <DateField
            kind="month"
            id="pay-period"
            value={periodMonth}
            onChange={(e) => setPeriodMonth(e.target.value)}
          />
        </Field>
      </div>

      <Field label={locale === 'ar-EG' ? 'ملاحظات' : 'Notes'} htmlFor="pay-notes">
        <Textarea id="pay-notes" rows={2} value={notes} onChange={(e) => setNotes(e.target.value)} />
      </Field>

      <label className="flex items-center justify-between gap-4 rounded-xl border border-border bg-muted/20 p-4">
        <span className="font-display text-body-sm font-semibold text-foreground">
          {locale === 'ar-EG' ? 'الدفعة اتحصّلت بالفعل' : 'Payment already collected'}
        </span>
        <Switch checked={markAsPaid} onCheckedChange={setMarkAsPaid} />
      </label>
    </FormDialog>
  )
}
