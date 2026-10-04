import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { BookOpen, Users } from 'lucide-react'
import { adminApi } from '@/api/endpoints/admin'
import type {
  CourseListItemDto,
  GroupEnrollmentDto,
  GroupListItemDto,
  InquiryDto,
  StudentProfileDto,
} from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useDebounced } from '@/hooks/use-ui'
import { useI18n } from '@/i18n'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { pricingToBreakdown } from '@/lib/pricing'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { DataTable, type Column } from '@/components/dashboard/DataTable'
import { SearchBar } from '@/components/common/controls'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Rating } from '@/components/common/Rating'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { PriceTag } from '@/components/common/pricing-bits'

const PAGE_SIZE = 20

/** Shared search + pagination state for the plain admin listings. */
function useAdminList() {
  const [page, setPage] = React.useState(1)
  const [search, setSearch] = React.useState('')
  const debounced = useDebounced(search, 300)

  React.useEffect(() => setPage(1), [debounced])

  const query = React.useMemo(
    () => ({ Page: page, PageSize: PAGE_SIZE, Search: debounced.trim() || undefined }),
    [page, debounced],
  )

  return { page, setPage, search, setSearch, query }
}

/* -------------------------------------------------------------------------- */
/*                                  Students                                  */
/* -------------------------------------------------------------------------- */

export function AdminStudentsPage() {
  const { t, locale, formatNumber, formatDate } = useI18n()
  usePageTitle(t('common.students'))
  const list = useAdminList()

  const students = useQuery({
    queryKey: qk.admin.students(list.query),
    queryFn: () => adminApi.students(list.query),
    placeholderData: keepPreviousData,
  })

  const columns: Column<StudentProfileDto>[] = [
    {
      id: 'student',
      header: t('common.student'),
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
            <p className="truncate font-display text-body-sm font-semibold text-foreground">
              {row.displayName}
            </p>
            <p className="truncate font-latin text-caption text-muted-foreground">{row.email}</p>
          </div>
        </div>
      ),
      sortValue: (row) => row.displayName ?? '',
    },
    {
      id: 'grade',
      header: t('common.grade'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="text-body-sm text-muted-foreground">
          {locale === 'ar-EG' ? row.gradeNameAr : row.gradeNameEn}
        </span>
      ),
    },
    {
      id: 'enrollments',
      header: locale === 'ar-EG' ? 'الاشتراكات' : 'Enrollments',
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {formatNumber(row.activeGroupsCount ?? 0)} {t('common.groups')} ·{' '}
          {formatNumber(row.activeCoursesCount ?? 0)} {t('common.courses')}
        </span>
      ),
      sortValue: (row) => (row.activeGroupsCount ?? 0) + (row.activeCoursesCount ?? 0),
    },
    {
      id: 'joined',
      header: locale === 'ar-EG' ? 'الانضمام' : 'Joined',
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {row.createdAt ? formatDate(row.createdAt, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.createdAt ?? '',
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => (
        <Badge variant={row.isActive ? 'success' : 'outline'} size="sm">
          {row.isActive ? t('common.active') : t('common.inactive')}
        </Badge>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('common.students')}
        description={
          locale === 'ar-EG' ? 'كل طلاب المنصة.' : 'Every student registered on the platform.'
        }
      >
        <SearchBar
          value={list.search}
          onChange={list.setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="admin-students-search"
        />
      </DashboardHeader>

      <DataTable
        rows={students.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={students.isPending}
        error={students.isError ? students.error : undefined}
        onRetry={() => students.refetch()}
        isFetching={students.isFetching}
        serverPage={students.data}
        onPageChange={list.setPage}
        empty={{ spot: 'teachers', title: locale === 'ar-EG' ? 'مفيش طلاب' : 'No students' }}
        mobileCard={(row) => (
          <Card className="flex items-center gap-3 p-4">
            <Avatar
              traits={avatarTraitsFor(row.id ?? '')}
              seed={row.id ?? ''}
              name={row.displayName ?? ''}
              size="sm"
            />
            <div className="min-w-0 flex-1">
              <p className="truncate font-display text-body-sm font-bold text-foreground">
                {row.displayName}
              </p>
              <p className="truncate font-latin text-caption text-muted-foreground">{row.email}</p>
            </div>
          </Card>
        )}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Courses                                  */
/* -------------------------------------------------------------------------- */

export function AdminCoursesPage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('common.courses'))
  const list = useAdminList()

  const courses = useQuery({
    queryKey: qk.admin.courses({ ...list.query, IncludeUnpublished: true }),
    queryFn: () => adminApi.courses({ ...list.query, IncludeUnpublished: true }),
    placeholderData: keepPreviousData,
  })

  const columns: Column<CourseListItemDto>[] = [
    {
      id: 'course',
      header: t('common.course'),
      cell: (row) => (
        <div className="flex min-w-0 items-center gap-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
            <BookOpen className="size-4" />
          </span>
          <div className="min-w-0">
            <p className="truncate font-display text-body-sm font-semibold text-foreground">
              {row.title}
            </p>
            <p className="truncate text-caption text-muted-foreground">
              {row.teacher?.displayName}
            </p>
          </div>
        </div>
      ),
      sortValue: (row) => row.title ?? '',
    },
    {
      id: 'subject',
      header: t('common.subject'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="text-caption text-muted-foreground">
          {locale === 'ar-EG' ? row.subject?.nameAr : row.subject?.nameEn}
        </span>
      ),
    },
    {
      id: 'students',
      header: t('common.students'),
      cell: (row) => (
        <span className="tnum text-body-sm text-foreground">
          {formatNumber(row.studentCount ?? 0)}
        </span>
      ),
      sortValue: (row) => row.studentCount ?? 0,
    },
    {
      id: 'rating',
      header: t('common.rating'),
      hideOnMobile: true,
      cell: (row) => <Rating value={row.rating ?? 0} count={row.reviewCount} size="sm" />,
      sortValue: (row) => row.rating ?? 0,
    },
    {
      id: 'price',
      header: t('common.price'),
      cell: (row) => (
        <PriceTag
          breakdown={pricingToBreakdown(row.pricing)}
          size="sm"
          showBadge={false}
          currency={row.pricing?.currency}
        />
      ),
      sortValue: (row) => row.pricing?.price ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => (
        <Badge variant={row.isPublished ? 'success' : 'outline'} size="sm">
          {row.isPublished ? t('common.published') : t('common.draft')}
        </Badge>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('common.courses')}
        description={
          locale === 'ar-EG' ? 'كل الكورسات على المنصة.' : 'Every course across the platform.'
        }
      >
        <SearchBar
          value={list.search}
          onChange={list.setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="admin-courses-search"
        />
      </DashboardHeader>

      <DataTable
        rows={courses.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={courses.isPending}
        error={courses.isError ? courses.error : undefined}
        onRetry={() => courses.refetch()}
        isFetching={courses.isFetching}
        serverPage={courses.data}
        onPageChange={list.setPage}
        empty={{ spot: 'courses', title: locale === 'ar-EG' ? 'مفيش كورسات' : 'No courses' }}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Groups                                   */
/* -------------------------------------------------------------------------- */

export function AdminGroupsPage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('common.groups'))
  const list = useAdminList()

  const groups = useQuery({
    queryKey: qk.admin.groups({ ...list.query, IncludeUnpublished: true }),
    queryFn: () => adminApi.groups({ ...list.query, IncludeUnpublished: true }),
    placeholderData: keepPreviousData,
  })

  const columns: Column<GroupListItemDto>[] = [
    {
      id: 'group',
      header: t('common.group'),
      cell: (row) => (
        <div className="flex min-w-0 items-center gap-3">
          <span className="grid size-9 shrink-0 place-items-center rounded-lg bg-primary/10 text-primary">
            <Users className="size-4" />
          </span>
          <div className="min-w-0">
            <p className="truncate font-display text-body-sm font-semibold text-foreground">
              {row.name}
            </p>
            <p className="truncate text-caption text-muted-foreground">
              {row.teacher?.displayName}
            </p>
          </div>
        </div>
      ),
      sortValue: (row) => row.name ?? '',
    },
    {
      id: 'subject',
      header: t('common.subject'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="text-caption text-muted-foreground">
          {locale === 'ar-EG' ? row.subject?.nameAr : row.subject?.nameEn}
        </span>
      ),
    },
    {
      id: 'seats',
      header: locale === 'ar-EG' ? 'الأماكن' : 'Seats',
      cell: (row) => (
        <span className="tnum text-body-sm text-foreground">
          {formatNumber(row.enrolledStudentsCount ?? 0)} / {formatNumber(row.capacity ?? 0)}
        </span>
      ),
      sortValue: (row) => row.enrolledStudentsCount ?? 0,
    },
    {
      id: 'price',
      header: t('common.price'),
      hideOnMobile: true,
      cell: (row) => (
        <PriceTag
          breakdown={pricingToBreakdown(row.pricing)}
          size="sm"
          showBadge={false}
          currency={row.pricing?.currency}
        />
      ),
      sortValue: (row) => row.pricing?.price ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => (
        <Badge variant={row.isPublished ? 'success' : 'outline'} size="sm">
          {row.isPublished ? t('common.published') : t('common.draft')}
        </Badge>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('common.groups')}
        description={
          locale === 'ar-EG' ? 'كل المجموعات على المنصة.' : 'Every group across the platform.'
        }
      >
        <SearchBar
          value={list.search}
          onChange={list.setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="admin-groups-search"
        />
      </DashboardHeader>

      <DataTable
        rows={groups.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={groups.isPending}
        error={groups.isError ? groups.error : undefined}
        onRetry={() => groups.refetch()}
        isFetching={groups.isFetching}
        serverPage={groups.data}
        onPageChange={list.setPage}
        empty={{ spot: 'groups', title: locale === 'ar-EG' ? 'مفيش مجموعات' : 'No groups' }}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                Enrollments                                 */
/* -------------------------------------------------------------------------- */

export function AdminEnrollmentsPage() {
  const { t, locale, formatCurrency, formatDate } = useI18n()
  usePageTitle(locale === 'ar-EG' ? 'الاشتراكات' : 'Enrollments')
  const list = useAdminList()

  const enrollments = useQuery({
    queryKey: qk.admin.enrollments(list.query),
    queryFn: () => adminApi.enrollments(list.query),
    placeholderData: keepPreviousData,
  })

  const columns: Column<GroupEnrollmentDto>[] = [
    {
      id: 'student',
      header: t('common.student'),
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-display text-body-sm font-semibold text-foreground">
            {row.studentName}
          </p>
          <p className="truncate text-caption text-muted-foreground">{row.groupName}</p>
        </div>
      ),
      sortValue: (row) => row.studentName ?? '',
    },
    {
      id: 'teacher',
      header: t('common.teacher'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="text-body-sm text-muted-foreground">{row.teacher?.displayName}</span>
      ),
    },
    {
      id: 'date',
      header: t('common.date'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {row.enrolledAt ? formatDate(row.enrolledAt, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.enrolledAt ?? '',
    },
    {
      id: 'price',
      header: t('common.total'),
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {formatCurrency(row.finalPrice ?? 0)}
        </span>
      ),
      sortValue: (row) => row.finalPrice ?? 0,
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
    },
  ]

  return (
    <>
      <DashboardHeader
        title={locale === 'ar-EG' ? 'الاشتراكات' : 'Enrollments'}
        description={
          locale === 'ar-EG'
            ? 'كل اشتراكات الطلاب في المجموعات.'
            : 'Every student enrollment across all groups.'
        }
      >
        <SearchBar
          value={list.search}
          onChange={list.setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="admin-enrollments-search"
        />
      </DashboardHeader>

      <DataTable
        rows={enrollments.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={enrollments.isPending}
        error={enrollments.isError ? enrollments.error : undefined}
        onRetry={() => enrollments.refetch()}
        isFetching={enrollments.isFetching}
        serverPage={enrollments.data}
        onPageChange={list.setPage}
        empty={{
          spot: 'payments',
          title: locale === 'ar-EG' ? 'مفيش اشتراكات' : 'No enrollments',
        }}
      />
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                 Inquiries                                  */
/* -------------------------------------------------------------------------- */

export function AdminInquiriesPage() {
  const { t, locale, formatDate } = useI18n()
  usePageTitle(locale === 'ar-EG' ? 'الاستفسارات' : 'Inquiries')
  const list = useAdminList()

  const inquiries = useQuery({
    queryKey: qk.admin.inquiries(list.query),
    queryFn: () => adminApi.inquiries(list.query),
    placeholderData: keepPreviousData,
  })

  const columns: Column<InquiryDto>[] = [
    {
      id: 'from',
      header: locale === 'ar-EG' ? 'من' : 'From',
      cell: (row) => (
        <div className="min-w-0">
          <p className="truncate font-display text-body-sm font-semibold text-foreground">
            {row.name}
          </p>
          <p className="truncate font-latin text-caption text-muted-foreground" dir="ltr">
            {row.phoneNumber}
          </p>
        </div>
      ),
      sortValue: (row) => row.name ?? '',
    },
    {
      id: 'teacher',
      header: t('common.teacher'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="text-body-sm text-muted-foreground">{row.teacherName}</span>
      ),
    },
    {
      id: 'message',
      header: locale === 'ar-EG' ? 'الرسالة' : 'Message',
      cell: (row) => (
        <p className="line-clamp-2-rtl max-w-md text-caption text-muted-foreground">
          {row.message}
        </p>
      ),
    },
    {
      id: 'date',
      header: t('common.date'),
      hideOnMobile: true,
      cell: (row) => (
        <span className="tnum text-caption text-muted-foreground">
          {row.createdAt ? formatDate(row.createdAt, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.createdAt ?? '',
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
    },
  ]

  return (
    <>
      <DashboardHeader
        title={locale === 'ar-EG' ? 'الاستفسارات' : 'Inquiries'}
        description={
          locale === 'ar-EG'
            ? 'طلبات التواصل اللي وصلت للمدرّسين.'
            : 'Contact requests sent to teachers.'
        }
      >
        <SearchBar
          value={list.search}
          onChange={list.setSearch}
          placeholder={t('common.search')}
          className="max-w-md"
          id="admin-inquiries-search"
        />
      </DashboardHeader>

      <DataTable
        rows={inquiries.data?.items ?? []}
        columns={columns}
        rowKey={(row) => row.id ?? ''}
        loading={inquiries.isPending}
        error={inquiries.isError ? inquiries.error : undefined}
        onRetry={() => inquiries.refetch()}
        isFetching={inquiries.isFetching}
        serverPage={inquiries.data}
        onPageChange={list.setPage}
        empty={{ spot: 'inbox', title: locale === 'ar-EG' ? 'مفيش استفسارات' : 'No inquiries' }}
      />
    </>
  )
}
