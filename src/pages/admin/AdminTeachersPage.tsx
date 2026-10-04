import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BadgeCheck, MoreVertical, Pause, Play, ShieldCheck, Sparkles, XCircle } from 'lucide-react'
import { adminApi, type AdminTeacherQuery } from '@/api/endpoints/admin'
import type { TeacherListItemDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { FilterChip, SearchBar } from '@/components/common/controls'
import { ConfirmDialog } from '@/components/modals/ConfirmDialog'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Rating } from '@/components/common/Rating'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { useApiErrorToast } from '@/hooks/use-api-toast'

const PAGE_SIZE = 20

const VERIFICATION = [
  { value: undefined, ar: 'الكل', en: 'All' },
  { value: 'Pending', ar: 'مستني', en: 'Pending' },
  { value: 'Verified', ar: 'موثّق', en: 'Verified' },
  { value: 'Rejected', ar: 'مرفوض', en: 'Rejected' },
] as const

export default function AdminTeachersPage() {
  const { t, locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  usePageTitle(t('common.teachers'))

  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()

  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const [verification, setVerification] = React.useState<string>()
  const [suspending, setSuspending] = React.useState<TeacherListItemDto | null>(null)
  const debounced = useDebounced(search, 300)

  const query: AdminTeacherQuery = React.useMemo(
    () => ({
      Page: page,
      PageSize: PAGE_SIZE,
      Search: debounced.trim() || undefined,
      VerificationStatus: verification,
    }),
    [page, debounced, verification],
  )

  const teachers = useQuery({
    queryKey: qk.admin.teachers(query),
    queryFn: () => adminApi.teachers(query),
    placeholderData: keepPreviousData,
  })

  React.useEffect(() => setPage(1), [debounced, verification])

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ['admin'] })

  const onError = (error: unknown) =>
    showApiError(error)

  const verify = useMutation({
    mutationFn: ({ id, approve }: { id: string; approve: boolean }) =>
      adminApi.verifyTeacher(id, approve),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError,
  })

  const feature = useMutation({
    mutationFn: ({ id, featured }: { id: string; featured: boolean }) =>
      adminApi.featureTeacher(id, featured),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError,
  })

  const activate = useMutation({
    mutationFn: (id: string) => adminApi.activateTeacher(id),
    onSuccess: () => {
      invalidate()
      toast({ title: t('common.active'), variant: 'success' })
    },
    onError,
  })

  const suspend = useMutation({
    mutationFn: (id: string) =>
      adminApi.suspendTeacher(
        id,
        locale === 'ar-EG' ? 'إيقاف إداري' : 'Suspended by platform admin',
      ),
    onSuccess: () => {
      invalidate()
      setSuspending(null)
      toast({ title: t('common.inactive'), variant: 'success' })
    },
    onError,
  })

  const columns: Column<TeacherListItemDto>[] = [
    {
      id: 'teacher',
      header: t('common.teacher'),
      cell: (row) => (
        <div className="flex min-w-0 items-center gap-3">
          <Avatar
            traits={avatarTraitsFor(row.id ?? '')}
            seed={row.id ?? ''}
            name={row.displayName ?? ''}
            src={imageUrl(row.profileImageUrl)}
            size="sm"
          />
          <div className="min-w-0">
            <p className="flex items-center gap-1.5 truncate font-display text-body-sm font-semibold text-foreground">
              {row.displayName}
              {row.isVerified && <BadgeCheck className="size-3.5 shrink-0 text-primary" />}
            </p>
            <p className="truncate text-caption text-muted-foreground">{row.headline}</p>
          </div>
        </div>
      ),
      sortValue: (row) => row.displayName ?? '',
    },
    {
      id: 'subjects',
      header: t('common.subject'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="truncate text-caption text-muted-foreground">
          {(row.subjects ?? [])
            .map((s) => (locale === 'ar-EG' ? s.nameAr : s.nameEn))
            .filter(Boolean)
            .join('، ')}
        </span>
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
      id: 'rating',
      header: t('common.rating'),
      cell: (row) => <Rating value={row.rating ?? 0} count={row.reviewCount} size="sm" />,
      sortValue: (row) => row.rating ?? 0,
    },
    {
      id: 'flags',
      header: t('common.status'),
      cell: (row) => (
        <div className="flex flex-wrap gap-1.5">
          <Badge variant={row.isVerified ? 'success' : 'outline'} size="sm">
            {row.isVerified
              ? locale === 'ar-EG'
                ? 'موثّق'
                : 'Verified'
              : locale === 'ar-EG'
                ? 'غير موثّق'
                : 'Unverified'}
          </Badge>
          {row.isFeatured && (
            <Badge variant="gold" size="sm">
              <Sparkles className="size-3" />
              {locale === 'ar-EG' ? 'مميّز' : 'Featured'}
            </Badge>
          )}
        </div>
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
            {!row.isVerified && (
              <DropdownMenuItem
                onSelect={() => row.id && verify.mutate({ id: row.id, approve: true })}
              >
                <ShieldCheck className="size-4" />
                {t('admin.verify')}
              </DropdownMenuItem>
            )}
            {row.isVerified && (
              <DropdownMenuItem
                onSelect={() => row.id && verify.mutate({ id: row.id, approve: false })}
              >
                <XCircle className="size-4" />
                {t('admin.reject')}
              </DropdownMenuItem>
            )}
            <DropdownMenuItem
              onSelect={() => row.id && feature.mutate({ id: row.id, featured: !row.isFeatured })}
            >
              <Sparkles className="size-4" />
              {row.isFeatured ? t('admin.unfeature') : t('admin.feature')}
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onSelect={() => row.id && activate.mutate(row.id)}>
              <Play className="size-4 flip-rtl" />
              {t('admin.activate')}
            </DropdownMenuItem>
            <DropdownMenuItem className="text-destructive-strong" onSelect={() => setSuspending(row)}>
              <Pause className="size-4" />
              {t('admin.suspend')}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('common.teachers')}
        description={
          locale === 'ar-EG'
            ? 'وثّق المدرّسين، ميّز اللي يستاهل، وأوقف المخالف.'
            : 'Verify teachers, feature the best, and suspend rule-breakers.'
        }
      >
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
          <SearchBar
            value={search}
            onChange={setSearch}
            placeholder={t('common.search')}
            className="sm:max-w-md"
            id="admin-teachers-search"
          />
          <div className="no-scrollbar flex gap-2 overflow-x-auto">
            {VERIFICATION.map((v) => (
              <FilterChip
                key={v.value ?? 'all'}
                label={v[lang]}
                active={verification === v.value}
                onClick={() => setVerification(v.value)}
              />
            ))}
          </div>
        </div>
      </DashboardHeader>

      <DataTable
        rows={teachers.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={teachers.isPending}
        error={teachers.isError ? teachers.error : undefined}
        onRetry={() => teachers.refetch()}
        isFetching={teachers.isFetching}
        serverPage={teachers.data}
        onPageChange={setPage}
        empty={{
          spot: 'teachers',
          title: locale === 'ar-EG' ? 'مفيش مدرّسين' : 'No teachers',
        }}
        mobileCard={(row) => (
          <Card className="flex items-center gap-3 p-4">
            <Avatar
              traits={avatarTraitsFor(row.id ?? '')}
              seed={row.id ?? ''}
              name={row.displayName ?? ''}
              src={imageUrl(row.profileImageUrl)}
              size="sm"
            />
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {row.displayName}
              </p>
              <p className="truncate text-caption text-muted-foreground">{row.headline}</p>
            </div>
            <Badge variant={row.isVerified ? 'success' : 'outline'} size="sm">
              {row.isVerified ? 'OK' : '—'}
            </Badge>
          </Card>
        )}
      />

      <ConfirmDialog
        open={Boolean(suspending)}
        onOpenChange={(open) => !open && setSuspending(null)}
        title={locale === 'ar-EG' ? 'إيقاف المدرّس؟' : 'Suspend this teacher?'}
        description={
          locale === 'ar-EG'
            ? `هيتم إيقاف حساب ${suspending?.displayName ?? ''}.`
            : `This suspends ${suspending?.displayName ?? ''}'s account.`
        }
        confirmLabel={t('admin.suspend')}
        loading={suspend.isPending}
        onConfirm={() => suspending?.id && suspend.mutate(suspending.id)}
      />
    </>
  )
}
