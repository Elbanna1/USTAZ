import * as React from 'react'
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { motion } from 'framer-motion'
import { CalendarCheck, CalendarPlus, CalendarX, Check, Clock, ShieldCheck } from 'lucide-react'
import {
  teacherAttendanceApi,
  teacherEnrollmentsApi,
  teacherGroupsApi,
} from '@/api/endpoints/teacher'
import type { AttendanceEntryInput, AttendanceSessionDto } from '@/api/schema'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { isApiError } from '@/api/errors'
import { cn } from '@/lib/utils'
import { avatarTraitsFor } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { StatCard } from '@/components/common/StatCard'
import { StatusBadge } from '@/components/common/StatusBadge'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { Pagination } from '@/components/common/Pagination'
import { FormDialog } from '@/components/common/FormDialog'
import { RowSkeleton, StatSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'

const STATUS_OPTIONS = [
  { value: 'Present', ar: 'حاضر', en: 'Present', tone: 'success' },
  { value: 'Late', ar: 'متأخر', en: 'Late', tone: 'warning' },
  { value: 'Absent', ar: 'غايب', en: 'Absent', tone: 'destructive' },
  { value: 'Excused', ar: 'بعذر', en: 'Excused', tone: 'outline' },
] as const

export default function TeacherAttendancePage() {
  const { t, locale, formatDate } = useI18n()
  usePageTitle(t('dashboard.attendance'))

  const [page, setPage] = React.useState(1)
  const [groupId, setGroupId] = React.useState<string>()
  const [createOpen, setCreateOpen] = React.useState(false)
  const [recording, setRecording] = React.useState<AttendanceSessionDto | null>(null)

  const groups = useQuery({
    queryKey: qk.teacher.groups({ PageSize: 100 }),
    queryFn: () => teacherGroupsApi.list({ PageSize: 100 }),
  })

  const query = React.useMemo(
    () => ({ Page: page, PageSize: 15, GroupId: groupId }),
    [page, groupId],
  )

  const sessions = useQuery({
    queryKey: qk.teacher.attendanceSessions(query),
    queryFn: () => teacherAttendanceApi.sessions(query),
    placeholderData: keepPreviousData,
  })

  const stats = useQuery({
    queryKey: qk.teacher.attendanceStats({ GroupId: groupId }),
    queryFn: () => teacherAttendanceApi.statistics({ GroupId: groupId }),
  })

  React.useEffect(() => setPage(1), [groupId])

  const items = sessions.data?.items ?? []
  const summary = stats.data

  return (
    <>
      <DashboardHeader
        title={t('dashboard.attendance')}
        description={
          locale === 'ar-EG'
            ? 'افتح حصة، سجّل الحضور، وتابع النسب لكل مجموعة.'
            : 'Open a session, record attendance, and track rates per group.'
        }
        actions={
          <Button size="sm" onClick={() => setCreateOpen(true)}>
            <CalendarPlus className="size-4" />
            {locale === 'ar-EG' ? 'حصة جديدة' : 'New session'}
          </Button>
        }
      >
        <Select
          value={groupId ?? 'all'}
          onValueChange={(v) => setGroupId(v === 'all' ? undefined : v)}
        >
          <SelectTrigger className="w-full sm:w-64">
            <SelectValue placeholder={t('common.group')} />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">{t('common.all')}</SelectItem>
            {(groups.data?.items ?? []).map((g) => (
              <SelectItem key={g.id} value={g.id!}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </DashboardHeader>

      {stats.isPending ? (
        <StatSkeleton count={4} />
      ) : (
        <RevealGroup className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <RevealItem>
            <StatCard
              label={t('dashboard.attendanceRate')}
              value={Math.round(summary?.attendanceRate ?? 0)}
              suffix="%"
              icon={<ShieldCheck />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'حاضر' : 'Present'}
              value={summary?.present ?? 0}
              icon={<CalendarCheck />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'غايب' : 'Absent'}
              value={summary?.absent ?? 0}
              icon={<CalendarX />}
            />
          </RevealItem>
          <RevealItem>
            <StatCard
              label={locale === 'ar-EG' ? 'متأخر' : 'Late'}
              value={summary?.late ?? 0}
              icon={<Clock />}
            />
          </RevealItem>
        </RevealGroup>
      )}

      <div className="mt-8">
        {sessions.isPending ? (
          <RowSkeleton count={5} />
        ) : sessions.isError ? (
          <QueryError error={sessions.error} onRetry={() => sessions.refetch()} />
        ) : items.length === 0 ? (
          <EmptyState
            spot="calendar"
            title={locale === 'ar-EG' ? 'مفيش حصص مسجّلة' : 'No sessions recorded'}
            description={
              locale === 'ar-EG'
                ? 'افتح حصة جديدة عشان تسجّل حضور طلابك.'
                : 'Open a session to start recording attendance.'
            }
            action={{
              label: locale === 'ar-EG' ? 'حصة جديدة' : 'New session',
              onClick: () => setCreateOpen(true),
            }}
          />
        ) : (
          <div className="space-y-3">
            {items.map((session) => (
              <Card key={session.id} className="p-5">
                <div className="flex flex-wrap items-center gap-4">
                  <div className="min-w-0 flex-1">
                    <p className="font-display text-body font-bold text-foreground">
                      {session.title || session.groupName}
                    </p>
                    <p className="tnum mt-0.5 text-caption text-muted-foreground">
                      {session.sessionDate ? formatDate(session.sessionDate, 'd MMM yyyy') : '—'}
                      {session.groupName ? ` · ${session.groupName}` : ''}
                    </p>
                  </div>

                  <div className="flex flex-wrap items-center gap-2">
                    <Badge variant="success" size="sm" className="tnum">
                      {session.presentCount ?? 0}
                    </Badge>
                    <Badge variant="destructive" size="sm" className="tnum">
                      {session.absentCount ?? 0}
                    </Badge>
                    <Badge variant="warning" size="sm" className="tnum">
                      {session.lateCount ?? 0}
                    </Badge>
                    <StatusBadge status={session.status} fallbackLabel={session.statusName} />
                    <Button size="sm" variant="outline" onClick={() => setRecording(session)}>
                      {locale === 'ar-EG' ? 'سجّل الحضور' : 'Record attendance'}
                    </Button>
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}

        {sessions.data && items.length > 0 && (
          <Pagination page={sessions.data} onChange={setPage} className="mt-6" />
        )}
      </div>

      <CreateSessionDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        groups={groups.data?.items ?? []}
      />

      {recording && (
        <RecordAttendanceDialog session={recording} onClose={() => setRecording(null)} />
      )}
    </>
  )
}

/* -------------------------------------------------------------------------- */

function CreateSessionDialog({
  open,
  onOpenChange,
  groups,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  groups: { id?: string; name?: string }[]
}) {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [groupId, setGroupId] = React.useState('')
  const [sessionDate, setSessionDate] = React.useState(new Date().toISOString().slice(0, 10))
  const [title, setTitle] = React.useState('')
  const [formError, setFormError] = React.useState<string | null>(null)

  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    setGroupId('')
    setTitle('')
    setSessionDate(new Date().toISOString().slice(0, 10))
  }, [open])

  const create = useMutation({
    mutationFn: () =>
      teacherAttendanceApi.createSession({
        groupId,
        sessionDate,
        title: title.trim() || null,
        prefillPresent: true,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher', 'attendance'] })
      toast({ title: t('common.saved'), variant: 'success' })
      onOpenChange(false)
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={locale === 'ar-EG' ? 'حصة جديدة' : 'New session'}
      description={
        locale === 'ar-EG'
          ? 'هنسجّل كل الطلاب حاضرين ثم تعدّل اللي غاب.'
          : 'Everyone starts marked present — change the ones who missed it.'
      }
      error={formError}
      submitting={create.isPending}
      size="md"
      onSubmit={(e) => {
        e.preventDefault()
        if (groupId) create.mutate()
      }}
    >
      <Field label={t('common.group')} htmlFor="session-group" required>
        <Select value={groupId} onValueChange={setGroupId}>
          <SelectTrigger id="session-group">
            <SelectValue placeholder={t('common.group')} />
          </SelectTrigger>
          <SelectContent>
            {groups.map((g) => (
              <SelectItem key={g.id} value={g.id!}>
                {g.name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>

      <Field label={t('common.date')} htmlFor="session-date" required>
        <DateField
          id="session-date"
          value={sessionDate}
          onChange={(e) => setSessionDate(e.target.value)}
        />
      </Field>

      <Field label={locale === 'ar-EG' ? 'عنوان الحصة' : 'Session title'} htmlFor="session-title">
        <Input id="session-title" value={title} onChange={(e) => setTitle(e.target.value)} />
      </Field>
    </FormDialog>
  )
}

/* -------------------------------------------------------------------------- */

function RecordAttendanceDialog({
  session,
  onClose,
}: {
  session: AttendanceSessionDto
  onClose: () => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [formError, setFormError] = React.useState<string | null>(null)
  const [entries, setEntries] = React.useState<Record<string, AttendanceEntryInput['status']>>({})

  const detail = useQuery({
    queryKey: qk.teacher.attendanceSession(session.id ?? ''),
    queryFn: () => teacherAttendanceApi.session(session.id!),
    enabled: Boolean(session.id),
  })

  /* When a session has no records yet, seed the roster from group enrollments. */
  const roster = useQuery({
    queryKey: qk.teacher.enrollments('groups', { GroupId: session.groupId, PageSize: 200 }),
    queryFn: () => teacherEnrollmentsApi.groups({ GroupId: session.groupId, PageSize: 200 }),
    enabled: Boolean(session.groupId),
  })

  const students = React.useMemo(() => {
    const records = detail.data?.records ?? []
    if (records.length) {
      return records.map((r) => ({
        studentId: r.studentId ?? '',
        studentName: r.studentName ?? '',
        status: r.status,
      }))
    }
    return (roster.data?.items ?? []).map((e) => ({
      studentId: e.studentId ?? '',
      studentName: e.studentName ?? '',
      status: 'Present' as const,
    }))
  }, [detail.data, roster.data])

  React.useEffect(() => {
    if (!students.length) return
    setEntries((prev) =>
      Object.keys(prev).length
        ? prev
        : Object.fromEntries(students.map((s) => [s.studentId, s.status ?? 'Present'])),
    )
  }, [students])

  const save = useMutation({
    mutationFn: () =>
      teacherAttendanceApi.recordBulk(session.id!, {
        entries: Object.entries(entries).map(([studentId, status]) => ({ studentId, status })),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['teacher'] })
      toast({ title: t('common.saved'), variant: 'success' })
      onClose()
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  const counts = STATUS_OPTIONS.map((option) => ({
    ...option,
    count: Object.values(entries).filter((s) => s === option.value).length,
  }))

  return (
    <FormDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title={`${locale === 'ar-EG' ? 'تسجيل الحضور' : 'Record attendance'} — ${session.groupName ?? ''}`}
      error={formError}
      submitting={save.isPending}
      onSubmit={(e) => {
        e.preventDefault()
        save.mutate()
      }}
    >
      {detail.isPending || roster.isPending ? (
        <RowSkeleton count={5} />
      ) : students.length === 0 ? (
        <EmptyState
          spot="teachers"
          compact
          title={locale === 'ar-EG' ? 'مفيش طلاب في المجموعة' : 'No students in this group'}
        />
      ) : (
        <>
          <div className="flex flex-wrap gap-2">
            {counts.map((option) => (
              <Badge key={option.value} variant={option.tone} size="md" className="tnum">
                {option[lang]}: {option.count}
              </Badge>
            ))}
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="ms-auto"
              onClick={() =>
                setEntries(Object.fromEntries(students.map((s) => [s.studentId, 'Present'])))
              }
            >
              <Check className="size-4" />
              {locale === 'ar-EG' ? 'الكل حاضر' : 'All present'}
            </Button>
          </div>

          <div className="space-y-2">
            {students.map((student, i) => (
              <motion.div
                key={student.studentId}
                initial={{ opacity: 0, y: 6 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ ...transitions.fast, delay: Math.min(i * 0.02, 0.2) }}
                className="flex flex-wrap items-center gap-3 rounded-xl border border-border bg-card p-3"
              >
                <Avatar
                  traits={avatarTraitsFor(student.studentId)}
                  seed={student.studentId}
                  name={student.studentName}
                  size="xs"
                />
                <span className="min-w-0 flex-1 truncate font-display text-body-sm font-semibold text-foreground">
                  {student.studentName}
                </span>

                <div className="flex gap-1">
                  {STATUS_OPTIONS.map((option) => {
                    const active = entries[student.studentId] === option.value
                    return (
                      <button
                        key={option.value}
                        type="button"
                        onClick={() =>
                          setEntries((prev) => ({ ...prev, [student.studentId]: option.value }))
                        }
                        aria-pressed={active}
                        className={cn(
                          'rounded-lg border px-2.5 py-1.5 text-caption font-semibold transition-colors',
                          active
                            ? option.tone === 'success'
                              ? 'border-success bg-success/12 text-success-strong'
                              : option.tone === 'warning'
                                ? 'border-warning bg-warning/12 text-warning-strong'
                                : option.tone === 'destructive'
                                  ? 'border-destructive bg-destructive/12 text-destructive-strong'
                                  : 'border-foreground/30 bg-muted text-foreground'
                            : 'border-border text-muted-foreground hover:border-foreground/25',
                        )}
                      >
                        {option[lang]}
                      </button>
                    )
                  })}
                </div>
              </motion.div>
            ))}
          </div>
        </>
      )}
    </FormDialog>
  )
}
