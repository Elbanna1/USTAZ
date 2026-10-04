import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BadgePercent, Copy, MoreVertical, Plus, Power, Trash2 } from 'lucide-react'
import { teacherDiscountsApi } from '@/api/endpoints/teacher'
import type { DiscountDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { copyToClipboard, percent } from '@/lib/utils'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { FormDialog } from '@/components/common/FormDialog'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import { Progress } from '@/components/ui/primitives'
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
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 15

export default function TeacherDiscountsPage() {
  const { t, locale, formatNumber, formatCurrency, formatDate } = useI18n()
  usePageTitle(locale === 'ar-EG' ? 'الخصومات' : 'Discounts')

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [active, setActive] = React.useState<boolean | undefined>()
  const [formOpen, setFormOpen] = React.useState(false)
  const [editing, setEditing] = React.useState<DiscountDto | null>(null)
  const [deleting, setDeleting] = React.useState<DiscountDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      IsActive: active,
    }),
    [page, debounced, active],
  )

  const discounts = useQuery({
    queryKey: qk.teacher.discounts(query),
    queryFn: () => teacherDiscountsApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, active])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['teacher', 'discounts'] })

  const toggle = useMutation({
    mutationFn: ({ id, next }: { id: string; next: boolean }) =>
      next ? teacherDiscountsApi.activate(id) : teacherDiscountsApi.deactivate(id),
    onSuccess: (_r, { next }) => {
      invalidate()
      toast({ title: next ? t('common.active') : t('common.inactive'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const remove = useMutation({
    mutationFn: (id: string) => teacherDiscountsApi.remove(id),
    onSuccess: () => {
      invalidate()
      setDeleting(null)
      toast({ title: t('common.delete'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const columns: Column<DiscountDto>[] = [
    {
      id: 'code',
      header: locale === 'ar-EG' ? 'الكود' : 'Code',
      cell: (row) => (
        <div className="flex min-w-0 items-center gap-2">
          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-accent/12 text-accent-strong">
            <BadgePercent className="size-4" />
          </span>
          <div className="min-w-0">
            <p className="truncate font-latin text-body-sm font-bold text-foreground">{row.code}</p>
            {row.description && (
              <p className="truncate text-caption text-muted-foreground">{row.description}</p>
            )}
          </div>
        </div>
      ),
      sortValue: (row) => row.code ?? '',
    },
    {
      id: 'value',
      header: locale === 'ar-EG' ? 'الخصم' : 'Discount',
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {row.type === 'Percentage'
            ? `${formatNumber(row.value ?? 0)}%`
            : formatCurrency(row.value ?? 0)}
        </span>
      ),
      sortValue: (row) => row.value ?? 0,
    },
    {
      id: 'usage',
      header: locale === 'ar-EG' ? 'الاستخدام' : 'Usage',
      hideOnMobile: true,
      cell: (row) => {
        const used = row.usedCount ?? 0
        const limit = row.usageLimit ?? 0
        return (
          <div className="min-w-[7rem]">
            <p className="tnum text-caption text-muted-foreground">
              {formatNumber(used)}
              {limit ? ` / ${formatNumber(limit)}` : ''}
            </p>
            {limit > 0 && <Progress value={percent(used, limit)} className="mt-1.5 h-1" />}
          </div>
        )
      },
      sortValue: (row) => row.usedCount ?? 0,
    },
    {
      id: 'window',
      header: locale === 'ar-EG' ? 'الصلاحية' : 'Valid',
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {row.startDate ? formatDate(row.startDate, 'd MMM') : '—'}
          {row.endDate ? ` — ${formatDate(row.endDate, 'd MMM yyyy')}` : ''}
        </span>
      ),
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => (
        <Badge
          variant={row.isCurrentlyValid ? 'success' : row.isActive ? 'warning' : 'outline'}
          size="sm"
        >
          {row.isCurrentlyValid
            ? t('common.active')
            : row.isActive
              ? locale === 'ar-EG'
                ? 'خارج الفترة'
                : 'Out of window'
              : t('common.inactive')}
        </Badge>
      ),
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
            <DropdownMenuItem
              onSelect={async () => {
                const ok = await copyToClipboard(row.code ?? '')
                toast({ title: ok ? t('common.copied') : t('states.errorTitle') })
              }}
            >
              <Copy className="size-4" />
              {t('common.copy')}
            </DropdownMenuItem>
            <DropdownMenuItem
              onSelect={() => row.id && toggle.mutate({ id: row.id, next: !row.isActive })}
            >
              <Power className="size-4" />
              {row.isActive ? t('common.inactive') : t('common.active')}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem className="text-destructive-strong" onSelect={() => setDeleting(row)}>
              <Trash2 className="size-4" />
              {t('common.delete')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={locale === 'ar-EG' ? 'الخصومات' : 'Discounts'}
        description={
          locale === 'ar-EG'
            ? 'أكواد خصم تبعتها لطلابك عشان يستخدموها وقت الاشتراك.'
            : 'Discount codes your students can apply when they enroll.'
        }
        actions={
          <Button
            size="sm"
            onClick={() => {
              setEditing(null)
              setFormOpen(true)
            }}
          >
            <Plus className="size-4" />
            {locale === 'ar-EG' ? 'كود جديد' : 'New code'}
          </Button>
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-discounts-search"
          />
          <div className="flex gap-2">
            <FilterChip
              label={t('common.all')}
              active={active === undefined}
              onClick={() => setActive(undefined)}
            />
            <FilterChip
              label={t('common.active')}
              active={active === true}
              onClick={() => setActive(true)}
            />
            <FilterChip
              label={t('common.inactive')}
              active={active === false}
              onClick={() => setActive(false)}
            />
          </div>
        </div>
      </DashboardHeader>

      <DataTable
        rows={discounts.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? row.code ?? ''}
        loading={discounts.isPending}
        error={discounts.isError ? discounts.error : undefined}
        onRetry={() => discounts.refetch()}
        isFetching={discounts.isFetching}
        serverPage={discounts.data}
        onPageChange={setPage}
        empty={{
          spot: 'payments',
          title: locale === 'ar-EG' ? 'مفيش أكواد خصم' : 'No discount codes',
          description:
            locale === 'ar-EG'
              ? 'اعمل كود خصم عشان تشجّع الطلاب على الاشتراك.'
              : 'Create a code to encourage students to enroll.',
          action: {
            label: locale === 'ar-EG' ? 'كود جديد' : 'New code',
            onClick: () => {
              setEditing(null)
              setFormOpen(true)
            },
          },
        }}
        mobileCard={(row) => (
          <Card className="space-y-3 p-4">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                <p className="truncate font-latin text-body-sm font-bold text-foreground">
                  {row.code}
                </p>
                <p className="tnum text-caption text-muted-foreground">
                  {row.type === 'Percentage'
                    ? `${formatNumber(row.value ?? 0)}%`
                    : formatCurrency(row.value ?? 0)}
                </p>
              </div>
              <Badge variant={row.isCurrentlyValid ? 'success' : 'outline'} size="sm">
                {row.isCurrentlyValid ? t('common.active') : t('common.inactive')}
              </Badge>
            </div>
          </Card>
        )}
      />

      <DiscountFormDialog
        open={formOpen}
        onOpenChange={setFormOpen}
        discount={editing}
        onSaved={invalidate}
      />

      <ConfirmDialog
        open={Boolean(deleting)}
        onOpenChange={(open) => !open && setDeleting(null)}
        title={locale === 'ar-EG' ? 'حذف الكود؟' : 'Delete this code?'}
        description={deleting?.code ?? ''}
        confirmLabel={t('common.delete')}
        loading={remove.isPending}
        onConfirm={() => deleting?.id && remove.mutate(deleting.id)}
      />
    </>
  )
}

function DiscountFormDialog({
  open,
  onOpenChange,
  discount,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  discount: DiscountDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const isEdit = Boolean(discount?.id)

  const [code, setCode] = React.useState('')
  const [description, setDescription] = React.useState('')
  const [type, setType] = React.useState<'Percentage' | 'FixedAmount'>('Percentage')
  const [value, setValue] = React.useState('10')
  const [minimumAmount, setMinimumAmount] = React.useState('')
  const [maximumDiscount, setMaximumDiscount] = React.useState('')
  const [startDate, setStartDate] = React.useState(new Date().toISOString().slice(0, 10))
  const [endDate, setEndDate] = React.useState('')
  const [usageLimit, setUsageLimit] = React.useState('')
  const [formError, setFormError] = React.useState<string | null>(null)

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    setCode(discount?.code ?? '')
    setDescription(discount?.description ?? '')
    setType(discount?.type ?? 'Percentage')
    setValue(String(discount?.value ?? 10))
    setMinimumAmount(discount?.minimumAmount ? String(discount.minimumAmount) : '')
    setMaximumDiscount(discount?.maximumDiscount ? String(discount.maximumDiscount) : '')
    setStartDate(discount?.startDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10))
    setEndDate(discount?.endDate?.slice(0, 10) ?? '')
    setUsageLimit(discount?.usageLimit ? String(discount.usageLimit) : '')
  }, [open, discount])

  const save = useMutation({
    mutationFn: () => {
      const shared = {
        description: description.trim() || null,
        value: Number(value) || 0,
        minimumAmount: minimumAmount ? Number(minimumAmount) : null,
        maximumDiscount: maximumDiscount ? Number(maximumDiscount) : null,
        startDate,
        endDate: endDate || null,
        usageLimit: usageLimit ? Number(usageLimit) : null,
      }
      return isEdit && discount?.id
        ? teacherDiscountsApi.update(discount.id, shared)
        : teacherDiscountsApi.create({ ...shared, code: code.trim().toUpperCase(), type })
    },
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
      title={
        isEdit
          ? locale === 'ar-EG'
            ? 'تعديل الكود'
            : 'Edit code'
          : locale === 'ar-EG'
            ? 'كود خصم جديد'
            : 'New discount code'
      }
      error={formError}
      submitting={save.isPending}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        if (code.trim()) save.mutate()
      }}
    >
      <Field label={locale === 'ar-EG' ? 'الكود' : 'Code'} htmlFor="d-code" required>
        <Input
          id="d-code"
          dir="ltr"
          className="text-start font-latin uppercase"
          readOnly={isEdit}
          value={code}
          onChange={(e) => setCode(e.target.value.toUpperCase())}
          placeholder="SUMMER25"
        />
      </Field>

      <Field label={locale === 'ar-EG' ? 'الوصف' : 'Description'} htmlFor="d-description">
        <Textarea
          id="d-description"
          rows={2}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
        />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={locale === 'ar-EG' ? 'نوع الخصم' : 'Discount type'} htmlFor="d-type">
          <Select
            value={type}
            disabled={isEdit}
            onValueChange={(v) => setType(v as 'Percentage' | 'FixedAmount')}
          >
            <SelectTrigger id="d-type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="Percentage">
                {locale === 'ar-EG' ? 'نسبة مئوية' : 'Percentage'}
              </SelectItem>
              <SelectItem value="FixedAmount">
                {locale === 'ar-EG' ? 'مبلغ ثابت' : 'Fixed amount'}
              </SelectItem>
            </SelectContent>
          </Select>
        </Field>

        <Field
          label={type === 'Percentage' ? '%' : locale === 'ar-EG' ? 'المبلغ' : 'Amount'}
          htmlFor="d-value"
          required
        >
          <Input
            id="d-value"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            value={value}
            onChange={(e) => setValue(e.target.value)}
          />
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={locale === 'ar-EG' ? 'أقل مبلغ' : 'Minimum amount'} htmlFor="d-min">
          <Input
            id="d-min"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            value={minimumAmount}
            onChange={(e) => setMinimumAmount(e.target.value)}
          />
        </Field>
        <Field label={locale === 'ar-EG' ? 'أقصى خصم' : 'Maximum discount'} htmlFor="d-max">
          <Input
            id="d-max"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            value={maximumDiscount}
            onChange={(e) => setMaximumDiscount(e.target.value)}
          />
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-3">
        <Field label={t('common.from')} htmlFor="d-start" required>
          <DateField
            id="d-start"
            value={startDate}
            onChange={(e) => setStartDate(e.target.value)}
          />
        </Field>
        <Field label={t('common.to')} htmlFor="d-end">
          <DateField
            id="d-end"
            value={endDate}
            onChange={(e) => setEndDate(e.target.value)}
          />
        </Field>
        <Field label={locale === 'ar-EG' ? 'حد الاستخدام' : 'Usage limit'} htmlFor="d-limit">
          <Input
            id="d-limit"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            value={usageLimit}
            onChange={(e) => setUsageLimit(e.target.value)}
          />
        </Field>
      </div>
    </FormDialog>
  )
}
