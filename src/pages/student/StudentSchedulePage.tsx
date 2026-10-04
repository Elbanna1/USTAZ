import * as React from 'react'
import { useQuery } from '@tanstack/react-query'
import { CalendarDays, LayoutGrid, List } from 'lucide-react'
import { studentApi } from '@/api/endpoints/student'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { WEEK_ORDER, dayLabel, slotsByDay } from '@/lib/schedule'
import { DashboardHeader, DashboardSection } from '@/components/dashboard/DashboardHeader'
import { SlotCard } from '@/components/schedule/SlotCard'
import { DayStrip, WeekGrid } from '@/components/schedule/WeekGrid'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import type { DayKey } from '@/types'

export default function StudentSchedulePage() {
  const { t, locale, formatNumber } = useI18n()
  usePageTitle(t('nav.schedule'))

  const [view, setView] = React.useState<'week' | 'list'>('week')
  const [selectedDay, setSelectedDay] = React.useState<DayKey>(WEEK_ORDER[0])

  const schedule = useQuery({
    queryKey: qk.student.schedule,
    queryFn: studentApi.schedule,
  })

  const upcoming = useQuery({
    queryKey: qk.student.upcoming,
    queryFn: () => studentApi.upcomingClasses(6),
  })

  const all = schedule.data ?? []
  const byDay = React.useMemo(() => slotsByDay(all), [all])
  const daySlots = byDay[selectedDay]

  return (
    <>
      <DashboardHeader
        title={t('nav.schedule')}
        description={
          locale === 'ar-EG'
            ? 'كل حصصك من كل مدرّسينك في جدول واحد.'
            : 'Every class from every teacher, in one weekly view.'
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
      />

      <div className="grid gap-8 xl:grid-cols-[1.7fr,1fr]">
        <div>
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
                  ? 'اشترك في مجموعة عشان تلاقي حصصك هنا.'
                  : 'Join a group and your sessions will show up here.'
              }
              action={{ label: t('nav.groups'), href: '/groups' }}
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
                        <SlotCard slot={slot} />
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
                        <SlotCard slot={slot} />
                      </RevealItem>
                    ))}
                  </RevealGroup>
                </section>
              ))}
            </div>
          )}
        </div>

        <DashboardSection title={t('dashboard.upcomingClasses')}>
          {upcoming.isPending ? (
            <RowSkeleton count={4} />
          ) : (upcoming.data ?? []).length === 0 ? (
            <EmptyState
              spot="calendar"
              compact
              title={t('dashboard.noUpcomingClasses')}
            />
          ) : (
            <div className="space-y-2.5">
              {(upcoming.data ?? []).map((slot) => (
                <SlotCard key={slot.id} slot={slot} variant="compact" />
              ))}
            </div>
          )}

          <div className="rounded-xl border border-dashed border-border bg-muted/20 p-4 text-center">
            <CalendarDays className="mx-auto size-6 text-muted-foreground" />
            <p className="mt-2 text-caption text-muted-foreground">
              {locale === 'ar-EG'
                ? 'المواعيد بتوقيت القاهرة.'
                : 'All times are shown in Cairo time.'}
            </p>
          </div>
        </DashboardSection>
      </div>
    </>
  )
}
