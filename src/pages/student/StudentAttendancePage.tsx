import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { CalendarCheck, CalendarX, Clock, ShieldCheck } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import type { AttendanceRecordDto } from '@/api/schema'
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

export default function StudentAttendancePage() {
  const { t, locale, formatDate } = useI18n()
  usePageTitle(t('dashboard.attendance'))

  const [page, setPage] = React.useState(1)

  const query = React.useMemo(() => ({ Page: page, PageSize: PAGE_SIZE }), [page])
  const records = useQuery({
    queryKey: qk.student.attendance(query),
    queryFn: () => studentApi.attendance(query),
    placeholderData: keepPreviousData,
  })

  /* The API paginates records; the summary is computed from the whole first page. */
  const all = useQuery({
    queryKey: qk.student.attendance({ PageSize: 500 }),
    queryFn: () => studentApi.attendance({ PageSize: 500 }),
  })

  const summary = React.useMemo(() => {
    const items = all.data?.items ?? []
    const count = (status: string) => items.filter((r) => r.status === status).length
    const total = items.length
    return {
      total,
      present: count('Present'),
      absent: count('Absent'),
      late: count('Late'),
      excused: count('Excused'),
      rate: total ? Math.round(((count('Present') + count('Late')) / total) * 100) : 0,
    }
  }, [all.data])

  const columns: Column<AttendanceRecordDto>[] = [
    {
      id: 'date',
      header: t('common.date'),
      cell: (row) => (
        <span className="tnum font-display text-body-sm font-semibold text-foreground">
          {row.sessionDate ? formatDate(row.sessionDate, 'd MMM yyyy') : '—'}
        </span>
      ),
      sortValue: (row) => row.sessionDate ?? '',
    },
    {
      id: 'group',
      header: t('common.group'),
      cell: (row) => (
        <span className="truncate text-body-sm text-muted-foreground">{row.groupName ?? '—'}</span>
      ),
      sortValue: (row) => row.groupName ?? '',
    },
    {
      id: 'status',
      header: t('common.status'),
      cell: (row) => <StatusBadge status={row.status} fallbackLabel={row.statusName} />,
      sortValue: (row) => row.status ?? '',
    },
    {
      id: 'note',
      header: locale === 'ar-EG' ? 'ملاحظة' : 'Note',
      hideOnMobile: true,
      cell: (row) => (
        <span className="text-caption text-muted-foreground">
          {row.minutesLate
            ? locale === 'ar-EG'
              ? `تأخير ${row.minutesLate} د`
              : `${row.minutesLate} min late`
            : (row.note ?? '—')}
        </span>
      ),
    },
  ]

  return (
    <>
      <DashboardHeader
        title={t('dashboard.attendance')}
        description={
          locale === 'ar-EG'
            ? 'سجل حضورك في كل المجموعات.'
            : 'Your attendance record across every group.'
        }
      />

      {all.isPending ? (
        <StatSkeleton count={4} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('dashboard.attendanceRate')}
              value={summary.rate}
              suffix="%"
              icon={<ShieldCheck />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'حاضر' : 'Present'}
              value={summary.present}
              icon={<CalendarCheck />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'غايب' : 'Absent'}
              value={summary.absent}
              icon={<CalendarX />}
              invertDelta
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'متأخر' : 'Late'}
              value={summary.late}
              icon={<Clock />}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8">
        <DataTable
          rows={records.data?.items ?? []}
          columns={columns}
          rowKey={(row) => row.id ?? `${row.attendanceSessionId}-${row.studentId}`}
          loading={records.isPending}
          error={records.isError ? records.error : undefined}
          onRetry={() => records.refetch()}
          isFetching={records.isFetching}
          serverPage={records.data}
          onPageChange={setPage}
          empty={{
            spot: 'calendar',
            title: locale === 'ar-EG' ? 'مفيش سجل حضور' : 'No attendance records',
            description:
              locale === 'ar-EG'
                ? 'الحضور هيتسجّل أول ما تبدأ حصصك.'
                : 'Attendance is recorded once your sessions begin.',
          }}
          mobileCard={(row) => (
            <Card className="flex items-center gap-3 p-4">
              <div className="min-w-0 flex-1">
                <p className="tnum font-display text-body-sm font-bold text-foreground">
                  {row.sessionDate ? formatDate(row.sessionDate, 'd MMM yyyy') : '—'}
                </p>
                <p className="truncate text-caption text-muted-foreground">{row.groupName}</p>
              </div>
              <StatusBadge status={row.status} fallbackLabel={row.statusName} />
            </Card>
          )}
        />
      </div>
    </>
  )
}
