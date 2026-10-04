import * as React from 'react'
import { useQuery } from '@tanstack/react-query'
import { CalendarDays, LayoutGrid, List } from 'lucide-react'
import { teacherScheduleApi } from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { WEEK_ORDER, dayLabel, slotsByDay } from '@/lib/schedule'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { SlotCard } from '@/components/schedule/SlotCard'
import { DayStrip, WeekGrid } from '@/components/schedule/WeekGrid'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Card } from '@/components/ui/card'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import type { DayKey } from '@/types'

export default function TeacherSchedulePage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('nav.schedule'))

  const [view, setView] = React.useState<'week' | 'list'>('week')
  const [selectedDay, setSelectedDay] = React.useState<DayKey>(WEEK_ORDER[0])

  const schedule = useQuery({
    queryKey: qk.teacher.schedule,
    queryFn: teacherScheduleApi.list,
  })

  const all = schedule.data ?? []
  const byDay = React.useMemo(() => slotsByDay(all), [all])
  const daySlots = byDay[selectedDay]
  const weeklyMinutes = all.reduce((sum, slot) => sum + (slot.durationInMinutes ?? 0), 0)

  return (
    <>
      <DashboardHeader
        title={t('nav.schedule')}
        description={
          locale === 'ar-EG'
            ? 'كل حصص مجموعاتك في أسبوع واحد. المواعيد بتتعدّل من صفحة المجموعة.'
            : 'Every group session in one week. Times are edited from the group page.'
        }
        actions={
          <div className="flex items-center rounded-lg border border-border bg-card p-1">
            {(['week', 'list'] as const).map((v) => (
              <button
                key={v}
                type="button"
                onClick={() => setView(v)}
                aria-pressed={view === v}
                className={cn(
                  'flex items-center gap-1.5 rounded-md px-3 py-1.5 font-display text-caption font-semibold transition-colors',
                  view === v
                    ? 'bg-primary text-primary-foreground'
                    : 'text-muted-foreground hover:text-foreground',
                )}
              >
                {v === 'week' ? <LayoutGrid className="size-4" /> : <List className="size-4" />}
                {v === 'week'
                  ? locale === 'ar-EG'
                    ? 'أسبوعي'
                    : 'Week'
                  : locale === 'ar-EG'
                    ? 'قائمة'
                    : 'List'}
              </button>
            ))}
          </div>
        }
      >
        <Card className="flex flex-wrap items-center gap-6 p-4">
          <span className="flex items-center gap-2.5">
            <CalendarDays className="size-5 text-primary" />
            <span>
              <span className="tnum block font-display text-heading-sm font-extrabold text-foreground">
                {formatNumber(all.length)}
              </span>
              <span className="block text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'حصة أسبوعية' : 'weekly sessions'}
              </span>
            </span>
          </span>
          <span className="h-10 w-px bg-border" />
          <span>
            <span className="tnum block font-display text-heading-sm font-extrabold text-foreground">
              {formatNumber(Math.round(weeklyMinutes / 60))}
            </span>
            <span className="block text-caption text-muted-foreground">
              {locale === 'ar-EG' ? 'ساعة في الأسبوع' : 'hours per week'}
            </span>
          </span>
        </Card>
      </DashboardHeader>

      {schedule.isPending ? (
        <RowSkeleton count={5} />
      ) : schedule.isError ? (
        <QueryError error={schedule.error} onRetry={() => schedule.refetch()} />
      ) : all.length === 0 ? (
        <EmptyState
          spot="calendar"
          title={locale === 'ar-EG' ? 'جدولك فاضي' : 'Your schedule is empty'}
          description={
            locale === 'ar-EG'
              ? 'ضيف مواعيد لمجموعاتك عشان تظهر هنا وفي جدول الطلاب.'
              : 'Add weekly times to your groups and they appear here and in student schedules.'
          }
          action={{ label: t('common.groups'), href: '/teacher/groups' }}
        />
      ) : view === 'week' ? (
        <>
          <WeekGrid slots={all} className="hidden lg:block" />
          <div className="space-y-5 lg:hidden">
            <DayStrip slots={all} selected={selectedDay} onSelect={setSelectedDay} />
            {daySlots.length === 0 ? (
              <EmptyState
                spot="calendar"
                compact
                title={
                  locale === 'ar-EG'
                    ? `مفيش حصص يوم ${dayLabel(selectedDay, locale)}`
                    : `No sessions on ${dayLabel(selectedDay, locale)}`
                }
              />
            ) : (
              <RevealGroup className="space-y-3">
                {daySlots.map((slot) => (
                  <RevealItem key={slot.id}>
                    <SlotCard slot={slot} showTeacher={false} />
                  </RevealItem>
                ))}
              </RevealGroup>
            )}
          </div>
        </>
      ) : (
        <div className="space-y-8">
          {WEEK_ORDER.filter((day) => byDay[day].length > 0).map((day) => (
            <section key={day} className="space-y-3">
              <h2 className="flex items-center gap-2.5 font-display text-heading-sm text-foreground">
                {dayLabel(day, locale)}
                <span className="tnum rounded-full bg-muted px-2 py-0.5 text-caption font-semibold text-muted-foreground">
                  {formatNumber(byDay[day].length)}
                </span>
              </h2>
              <RevealGroup className="grid gap-4 lg:grid-cols-2">
                {byDay[day].map((slot) => (
                  <RevealItem key={slot.id ?? `${day}-${slot.startTime}`}>
                    <SlotCard slot={slot} showTeacher={false} />
                  </RevealItem>
                ))}
              </RevealGroup>
            </section>
          ))}
        </div>
      )}
    </>
  )
}
