import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Award, Ban, Copy, MoreVertical } from 'lucide-react'
import { teacherCertificatesApi } from '@/api/endpoints/teacher'
import type { CertificateDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { copyToClipboard } from '@/lib/utils'
import { avatarTraitsFor } from '@/lib/identity'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { StatusBadge } from '@/components/common/StatusBadge'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 20

export default function TeacherCertificatesPage() {
  const { t, locale, formatNumber, formatDate } = useI18n()
  usePageTitle(t('certificates.title'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [status, setStatus] = React.useState<string>()
  const [revoking, setRevoking] = React.useState<CertificateDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Search: debounced.trim() || undefined, Status: status }),
    [page, debounced, status],
  )

  const certificates = useQuery({
    queryKey: qk.teacher.certificates(query),
    queryFn: () => teacherCertificatesApi.list(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, status])

  const revoke = useMutation({
    mutationFn: (id: string) => teacherCertificatesApi.revoke(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher', 'certificates'] })
      setRevoking(null)
      toast({ title: t('certificates.revoked'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const columns: Column<CertificateDto>[] = [
    {
      id: 'student',
      header: t('common.student'),
      cell: (row) => (
        <div className="flex min-w-0 items-center gap-3">
          <Avatar
            traits={avatarTraitsFor(row.studentId ?? '')}
            seed={row.studentId ?? ''}
            name={row.studentName ?? ''}
            size="sm"
          />
          <div className="min-w-0">
            <p className="truncate font-display text-body-sm font-semibold text-foreground">
              {row.studentName}
            </p>
            <p className="truncate text-caption text-muted-foreground">{row.courseTitle}</p>
          </div>
        </div>
      ),
      sortValue: (row) => row.studentName ?? '',
    },
    {
      id: 'number',
      header: locale === 'ar-EG' ? 'رقم الشهادة' : 'Certificate no.',
      hideOnMobile: true,
      cell: (row) => (
        <span className="font-latin text-caption text-muted-foreground">
          {row.certificateNumber}
        </span>
      ),
    },
    {
      id: 'lessons',
      header: t('courses.lessons'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {formatNumber(row.lessonsCompleted ?? 0)}
        </span>
      ),
      sortValue: (row) => row.lessonsCompleted ?? 0,
    },
    {
      id: 'issued',
      header: t('certificates.issuedOn'),
      cell: (row) => (
        <span className="tnum text-body-sm text-muted-foreground">
          {row.issuedAt ? formatDate(row.issuedAt, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.issuedAt ?? '',
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
            <DropdownMenuItem
              onSelect={async () => {
                const ok = await copyToClipboard(row.verificationCode ?? '')
                toast({ title: ok ? t('common.copied') : t('states.errorTitle') })
              }}
            >
              <Copy className="size-4" />
              {t('certificates.code')}
            </DropdownMenuItem>
            {row.status !== 'Revoked' && (
              <DropdownMenuItem className="text-destructive-strong" onSelect={() => setRevoking(row)}>
                <Ban className="size-4" />
                {t('certificates.revoke')}
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
        title={t('certificates.title')}
        description={
          locale === 'ar-EG'
            ? 'الشهادات اللي صدرت لطلابك بعد ما خلّصوا كورساتهم.'
            : 'Certificates issued to your students on course completion.'
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="teacher-certificates-search"
          />
          <div className="flex gap-2">
            <FilterChip
              label={t('common.all')}
              active={status === undefined}
              onClick={() => setStatus(undefined)}
            />
            <FilterChip
              label={locale === 'ar-EG' ? 'صادرة' : 'Issued'}
              active={status === 'Issued'}
              onClick={() => setStatus('Issued')}
            />
            <FilterChip
              label={locale === 'ar-EG' ? 'ملغاة' : 'Revoked'}
              active={status === 'Revoked'}
              onClick={() => setStatus('Revoked')}
            />
          </div>
        </div>
      </DashboardHeader>

      <DataTable
        rows={certificates.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? row.certificateNumber ?? ''}
        loading={certificates.isPending}
        error={certificates.isError ? certificates.error : undefined}
        onRetry={() => certificates.refetch()}
        isFetching={certificates.isFetching}
        serverPage={certificates.data}
        onPageChange={setPage}
        empty={{
          spot: 'certificate',
          title: locale === 'ar-EG' ? 'مفيش شهادات' : 'No certificates yet',
          description:
            locale === 'ar-EG'
              ? 'الشهادة بتصدر تلقائي لما الطالب يخلّص الكورس بالكامل.'
              : 'Certificates are issued automatically when a student completes a course.',
        }}
        mobileCard={(row) => (
          <Card className="flex items-center gap-3 p-4">
            <span className="grid size-10 shrink-0 place-items-center rounded-xl bg-accent/12 text-accent-strong">
              <Award className="size-5" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {row.studentName}
              </p>
              <p className="truncate text-caption text-muted-foreground">{row.courseTitle}</p>
            </div>
            <StatusBadge status={row.status} fallbackLabel={row.statusName} />
          </Card>
        )}
      />

      <ConfirmDialog
        open={Boolean(revoking)}
        onOpenChange={(open) => !open && setRevoking(null)}
        title={t('certificates.revoke')}
        description={
          locale === 'ar-EG'
            ? `هتلغي شهادة ${revoking?.studentName ?? ''}. التحقّق العام هيرجع إنها ملغاة.`
            : `This revokes the certificate for ${revoking?.studentName ?? ''}. Public verification will show it as revoked.`
        }
        confirmLabel={t('certificates.revoke')}
        loading={revoke.isPending}
        onConfirm={() => revoking?.id && revoke.mutate(revoking.id)}
      />
    </>
  )
}
