import * as React from 'react'
import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { CalendarDays, ChevronLeft, ChevronRight, LayoutGrid, List } from 'lucide-react'
import { calendarApi, type CalendarEventKind, type CalendarQuery } from '@/api/endpoints/platform'
import { teacherGroupsApi } from '@/api/endpoints/teacher'
import { parentApi } from '@/api/endpoints/parent'
import { qk } from '@/lib/query-client'
import { dayKey, eventsByDay, shiftMonth, visibleRange } from '@/lib/calendar'
import { cn } from '@/lib/utils'
import { useAuth } from '@/hooks/use-auth'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { MonthGrid } from '@/components/calendar/MonthGrid'
import { DayAgenda } from '@/components/calendar/EventAgenda'
import { KINDS, KIND_ORDER } from '@/components/calendar/vocabulary'
import { QueryError } from '@/components/common/QueryBoundary'
import { EmptyState } from '@/components/common/states'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

type View = 'month' | 'list'

export default function CalendarPage() {
  const { locale, formatNumber, formatDate, formatRelativeDay } = useI18n()
  const { role } = useAuth()
  const isAr = locale === 'ar-EG'
  const isStaff = role === 'teacher' || role === 'assistant'
  const isParent = role === 'parent'

  usePageTitle(isAr ? 'التقويم' : 'Calendar')

  const [cursor, setCursor] = React.useState(() => {
    const now = new Date()
    return new Date(now.getFullYear(), now.getMonth(), 1)
  })
  const [selected, setSelected] = React.useState(() => dayKey(new Date()))
  const [kinds, setKinds] = React.useState<CalendarEventKind[]>([])
  const [groupId, setGroupId] = React.useState<string>('')
  const [childId, setChildId] = React.useState<string>('')
  const [view, setView] = React.useState<View>('month')

  /* ------------------------------ Scope pickers ----------------------------- */

  /*
   * A guardian's calendar is always one child's — the API refuses the request
   * without a student, because there is no sensible default child.
   */
  const children = useQuery({
    queryKey: qk.parent.children,
    queryFn: parentApi.children,
    enabled: isParent,
  })

  const confirmedChildren = React.useMemo(
    () => (children.data ?? []).filter((child) => child.isConfirmed && child.studentId),
    [children.data],
  )

  React.useEffect(() => {
    if (!isParent || childId) return
    const first = confirmedChildren[0]?.studentId
    if (first) setChildId(first)
  }, [isParent, childId, confirmedChildren])

  /*
   * The group filter is a convenience, not a requirement. An assistant without
   * `Groups.View` simply does not get the control rather than seeing the page
   * fail, so the query's own failure is left unhandled on purpose.
   */
  const groups = useQuery({
    queryKey: qk.teacher.groups({ calendarFilter: true }),
    queryFn: () => teacherGroupsApi.list({ PageSize: 100, Sort: 'name' }),
    enabled: isStaff,
  })

  const groupOptions = groups.data?.items ?? []

  /* -------------------------------- The feed -------------------------------- */

  const query = React.useMemo<CalendarQuery>(() => {
    const range = visibleRange(cursor)
    return {
      ...range,
      Types: kinds.length ? kinds : undefined,
      GroupId: groupId || undefined,
      StudentId: isParent ? childId || undefined : undefined,
    }
  }, [cursor, kinds, groupId, isParent, childId])

  const calendar = useQuery({
    queryKey: qk.calendar.range(query),
    queryFn: () => calendarApi.range(query),
    placeholderData: keepPreviousData,
    // Asking without a child would be refused, so a guardian waits for one.
    enabled: !isParent || Boolean(childId),
  })

  const events = calendar.data?.events ?? []
  const buckets = React.useMemo(() => eventsByDay(events), [events])
  const selectedEvents = buckets.get(selected) ?? []

  /* Keep the selection inside the month the user is looking at. */
  React.useEffect(() => {
    const monthPrefix = dayKey(cursor).slice(0, 7)
    if (selected.startsWith(monthPrefix)) return
    const today = dayKey(new Date())
    setSelected(today.startsWith(monthPrefix) ? today : dayKey(cursor))
  }, [cursor, selected])

  const monthLabel = formatDate(cursor, 'MMMM yyyy')
  const toggleKind = (kind: CalendarEventKind) =>
    setKinds((current) =>
      current.includes(kind) ? current.filter((k) => k !== kind) : [...current, kind],
    )

  const goToToday = () => {
    const now = new Date()
    setCursor(new Date(now.getFullYear(), now.getMonth(), 1))
    setSelected(dayKey(now))
  }

  /* A guardian with nobody linked has nothing to show and a clear next step. */
  if (isParent && children.isSuccess && confirmedChildren.length === 0) {
    return (
      <>
        <CalendarIntro isAr={isAr} />
        <EmptyState
          spot="calendar"
          title={isAr ? 'مفيش ابن مربوط بحسابك' : 'No child linked yet'}
          description={
            isAr
              ? 'اربط ابنك بحسابك الأول وهتشوف حصصه وامتحاناته ومواعيد تسليم واجباته هنا.'
              : 'Link your child to your account and their classes, exams and homework deadlines show up here.'
          }
          action={{ label: isAr ? 'اربط ابنك' : 'Link a child', href: '/parent/children' }}
        />
      </>
    )
  }

  return (
    <>
      <DashboardHeader
        title={isAr ? 'التقويم' : 'Calendar'}
        description={
          isAr
            ? 'الحصص والامتحانات ومواعيد تسليم الواجبات في مكان واحد.'
            : 'Classes, exams and homework deadlines in one place.'
        }
        actions={
          <div className="flex items-center rounded-lg border border-border bg-card p-1">
            {(
              [
                { key: 'month', icon: LayoutGrid, ar: 'شهر', en: 'Month' },
                { key: 'list', icon: List, ar: 'قائمة', en: 'List' },
              ] as const
            ).map(({ key, icon: Icon, ar, en }) => (
              <button
                key={key}
                type="button"
                onClick={() => setView(key)}
                aria-pressed={view === key}
                className={cn(
                  'flex items-center gap-1.5 rounded-md px-3 py-1.5 font-display text-caption font-semibold transition-colors',
                  view === key
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:text-foreground',
                )}
              >
                <Icon className="size-4" />
                {isAr ? ar : en}
              </button>
            ))}
          </div>
        }
      />

      {/* ------------------------------- Controls ------------------------------ */}

      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-1.5">
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => setCursor((c) => shiftMonth(c, -1))}
            aria-label={isAr ? 'الشهر اللي فات' : 'Previous month'}
          >
            <ChevronLeft className="flip-rtl" />
          </Button>
          <Button
            variant="outline"
            size="icon-sm"
            onClick={() => setCursor((c) => shiftMonth(c, 1))}
            aria-label={isAr ? 'الشهر الجاي' : 'Next month'}
          >
            <ChevronRight className="flip-rtl" />
          </Button>

          <h2 className="ms-2 min-w-[8.5rem] font-display text-heading-sm text-foreground">
            {monthLabel}
          </h2>

          <Button variant="ghost" size="sm" onClick={goToToday}>
            <CalendarDays className="size-4" />
            {isAr ? 'النهارده' : 'Today'}
          </Button>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {isParent && confirmedChildren.length > 0 && (
            <Select value={childId} onValueChange={setChildId}>
              <SelectTrigger className="h-9 w-[11rem] text-body-sm">
                <SelectValue placeholder={isAr ? 'اختار ابنك' : 'Choose a child'} />
              </SelectTrigger>
              <SelectContent>
                {confirmedChildren.map((child) => (
                  <SelectItem key={child.studentId} value={child.studentId!}>
                    {child.displayName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}

          {isStaff && groupOptions.length > 0 && (
            <Select value={groupId || 'all'} onValueChange={(v) => setGroupId(v === 'all' ? '' : v)}>
              <SelectTrigger className="h-9 w-[11rem] text-body-sm">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">{isAr ? 'كل المجموعات' : 'All groups'}</SelectItem>
                {groupOptions.map((group) => (
                  <SelectItem key={group.id} value={group.id!}>
                    {group.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        </div>
      </div>

      {/* Kind filters double as the legend — the colours mean the same thing. */}
      <div className="no-scrollbar mb-6 flex gap-2 overflow-x-auto pb-1">
        {KIND_ORDER.map((kind) => {
          const style = KINDS[kind]
          const active = kinds.includes(kind)
          const Icon = style.icon
          return (
            <button
              key={kind}
              type="button"
              onClick={() => toggleKind(kind)}
              aria-pressed={active}
              className={cn(
                'inline-flex shrink-0 items-center gap-1.5 rounded-full border px-3 py-1.5 font-display text-caption font-semibold transition-all duration-200 ease-premium',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
                active
                  ? 'border-foreground/20 bg-card text-foreground shadow-soft'
                  : 'border-border bg-transparent text-muted-foreground hover:text-foreground',
              )}
            >
              <span className={cn('size-2 rounded-full', style.dot)} aria-hidden />
              <Icon className="size-3.5" aria-hidden />
              {isAr ? style.ar : style.en}
            </button>
          )
        })}

        {kinds.length > 0 && (
          <button
            type="button"
            onClick={() => setKinds([])}
            className="shrink-0 px-2 text-caption font-semibold text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
          >
            {isAr ? 'شيل الفلاتر' : 'Clear filters'}
          </button>
        )}
      </div>

      {/* -------------------------------- Body -------------------------------- */}

      {calendar.isPending ? (
        <CalendarSkeleton view={view} />
      ) : calendar.isError ? (
        <QueryError error={calendar.error} onRetry={() => calendar.refetch()} />
      ) : view === 'month' ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
          <MonthGrid
            cursor={cursor}
            buckets={buckets}
            selected={selected}
            onSelect={setSelected}
          />

          <Card variant="flat" className="h-fit overflow-hidden">
            <div className="flex items-baseline justify-between gap-2 border-b border-border px-4 py-3 sm:px-5">
              <h3 className="font-display text-body font-bold text-foreground">
                {formatRelativeDay(selected)}
              </h3>
              <span className="tnum shrink-0 text-caption text-muted-foreground">
                {selectedEvents.length
                  ? isAr
                    ? `${formatNumber(selectedEvents.length)} مواعيد`
                    : `${formatNumber(selectedEvents.length)} events`
                  : isAr
                    ? 'فاضي'
                    : 'Clear'}
              </span>
            </div>

            {selectedEvents.length > 0 ? (
              <DayAgenda events={selectedEvents} />
            ) : (
              <p className="px-5 py-10 text-center text-body-sm text-muted-foreground">
                {isAr ? 'مفيش حاجة في اليوم ده.' : 'Nothing scheduled this day.'}
              </p>
            )}
          </Card>
        </div>
      ) : events.length === 0 ? (
        <EmptyMonth isAr={isAr} filtered={kinds.length > 0} onClear={() => setKinds([])} />
      ) : (
        <div className="space-y-6">
          {[...buckets.entries()].map(([day, dayEvents]) => (
            <Card key={day} variant="flat" className="overflow-hidden">
              <div className="flex items-baseline justify-between gap-2 border-b border-border px-4 py-3 sm:px-5">
                <h3 className="font-display text-body font-bold text-foreground">
                  {formatRelativeDay(day)}
                </h3>
                <span className="tnum shrink-0 text-caption text-muted-foreground">
                  {isAr
                    ? `${formatNumber(dayEvents.length)} مواعيد`
                    : `${formatNumber(dayEvents.length)} events`}
                </span>
              </div>
              <DayAgenda events={dayEvents} />
            </Card>
          ))}
        </div>
      )}

      {/* A month with nothing in it still needs saying, under the grid. */}
      {view === 'month' && calendar.isSuccess && events.length === 0 && (
        <EmptyMonth
          isAr={isAr}
          filtered={kinds.length > 0}
          onClear={() => setKinds([])}
          className="mt-6"
        />
      )}
    </>
  )
}

/* -------------------------------------------------------------------------- */
/*                                   Pieces                                   */
/* -------------------------------------------------------------------------- */

function CalendarIntro({ isAr }: { isAr: boolean }) {
  return (
    <DashboardHeader
      title={isAr ? 'التقويم' : 'Calendar'}
      description={
        isAr
          ? 'الحصص والامتحانات ومواعيد تسليم الواجبات في مكان واحد.'
          : 'Classes, exams and homework deadlines in one place.'
      }
    />
  )
}

function EmptyMonth({
  isAr,
  filtered,
  onClear,
  className,
}: {
  isAr: boolean
  filtered: boolean
  onClear: () => void
  className?: string
}) {
  return (
    <EmptyState
      spot="calendar"
      className={className}
      title={
        filtered
          ? isAr
            ? 'مفيش مواعيد من النوع ده الشهر ده'
            : 'Nothing of this kind this month'
          : isAr
            ? 'الشهر ده فاضي'
            : 'Nothing scheduled this month'
      }
      description={
        filtered
          ? isAr
            ? 'جرّب تشيل الفلاتر أو تتنقّل لشهر تاني.'
            : 'Try clearing the filters, or move to another month.'
          : isAr
            ? 'الحصص والامتحانات ومواعيد التسليم بتظهر هنا أول ما تتجدول.'
            : 'Classes, exams and deadlines appear here as soon as they are scheduled.'
      }
      action={filtered ? { label: isAr ? 'شيل الفلاتر' : 'Clear filters', onClick: onClear } : undefined}
    />
  )
}

function CalendarSkeleton({ view }: { view: View }) {
  if (view === 'list') {
    return (
      <div className="space-y-6">
        {[0, 1, 2].map((card) => (
          <Card key={card} variant="flat" className="overflow-hidden">
            <div className="border-b border-border px-5 py-3">
              <Skeleton className="h-4 w-36" />
            </div>
            <div className="divide-y divide-border">
              {[0, 1].map((row) => (
                <div key={row} className="flex gap-4 px-5 py-4">
                  <Skeleton className="h-4 w-14 shrink-0" />
                  <Skeleton className="size-8 shrink-0 rounded-lg" />
                  <div className="flex-1 space-y-2">
                    <Skeleton className="h-4 w-2/5" />
                    <Skeleton className="h-3 w-28" />
                  </div>
                </div>
              ))}
            </div>
          </Card>
        ))}
      </div>
    )
  }

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="overflow-hidden rounded-xl border border-border bg-card">
        <div className="grid grid-cols-7 border-b border-border bg-muted/40">
          {Array.from({ length: 7 }, (_, i) => (
            <div key={i} className="px-2 py-3">
              <Skeleton className="mx-auto h-3 w-8" />
            </div>
          ))}
        </div>
        {Array.from({ length: 5 }, (_, week) => (
          <div key={week} className="grid grid-cols-7 border-t border-border first:border-t-0">
            {Array.from({ length: 7 }, (_, day) => (
              <div
                key={day}
                className="h-20 space-y-1.5 border-s border-border p-2 first:border-s-0 sm:h-[6.75rem]"
              >
                <Skeleton className="size-6 rounded-full" />
                {(week + day) % 3 === 0 && <Skeleton className="hidden h-4 w-full sm:block" />}
              </div>
            ))}
          </div>
        ))}
      </div>

      <Card variant="flat" className="h-fit overflow-hidden">
        <div className="border-b border-border px-5 py-3">
          <Skeleton className="h-4 w-32" />
        </div>
        <div className="divide-y divide-border">
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex gap-3 px-4 py-4">
              <Skeleton className="h-4 w-12 shrink-0" />
              <Skeleton className="size-8 shrink-0 rounded-lg" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-3/5" />
                <Skeleton className="h-3 w-20" />
              </div>
            </div>
          ))}
        </div>
      </Card>
    </div>
  )
}
