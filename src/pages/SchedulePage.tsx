import * as React from 'react'
import { useQuery } from '@tanstack/react-query'
import { CalendarDays, LayoutGrid, List } from 'lucide-react'
import { scheduleApi, type ScheduleQuery } from '@/api/endpoints/catalog'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useSubjects, useGrades } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { WEEK_ORDER, dayLabel, slotsByDay } from '@/lib/schedule'
import { PageHeader } from '@/components/common/PageHeader'
import { FilterChip } from '@/components/common/controls'
import { EmptyState } from '@/components/common/states'
import { QueryError } from '@/components/common/QueryBoundary'
import { SlotCard } from '@/components/schedule/SlotCard'
import { DayStrip, WeekGrid } from '@/components/schedule/WeekGrid'
import { RowSkeleton } from '@/components/ui/skeleton'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { RevealGroup, RevealItem } from '@/components/common/Reveal'
import type { DayKey } from '@/types'

const MODES = [
  { value: 'Online', ar: 'أونلاين', en: 'Online' },
  { value: 'Offline', ar: 'حضوري', en: 'In person' },
  { value: 'Hybrid', ar: 'الاتنين', en: 'Hybrid' },
] as const

export default function SchedulePage() {
  const { t, locale, formatNumber } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  usePageTitle(
    t('nav.schedule'),
    locale === 'ar-EG'
      ? 'كل حصص الأسبوع من كل المدرّسين في مكان واحد.'
      : 'Every class this week, from every teacher, in one place.',
  )

  const [view, setView] = React.useState<'week' | 'list'>('week')
  const [subjectId, setSubjectId] = React.useState<string>()
  const [gradeId, setGradeId] = React.useState<string>()
  const [mode, setMode] = React.useState<(typeof MODES)[number]['value']>()
  const [selectedDay, setSelectedDay] = React.useState<DayKey>(WEEK_ORDER[0])

  const subjects = useSubjects()
  const grades = useGrades()

  const query: ScheduleQuery = React.useMemo(
    () => ({ SubjectId: subjectId, GradeId: gradeId, TeachingMode: mode, Take: 300 }),
    [subjectId, gradeId, mode],
  )

  const slots = useQuery({
    queryKey: qk.schedule.public(query),
    queryFn: () => scheduleApi.list(query),
  })

  const all = slots.data ?? []
  const byDay = React.useMemo(() => slotsByDay(all), [all])
  const daySlots = byDay[selectedDay]

  const activeCount = (subjectId ? 1 : 0) + (gradeId ? 1 : 0) + (mode ? 1 : 0)

  return (
    <>
      <PageHeader
        eyebrow={locale === 'ar-EG' ? 'جدول الحصص' : 'Class schedule'}
        title={locale === 'ar-EG' ? 'كل حصص الأسبوع في مكان واحد' : 'Every class this week in one place'}
        description={
          locale === 'ar-EG'
            ? 'شوف مواعيد المجموعات المتاحة، وفلتر حسب المادة والصف وطريقة الحضور.'
            : 'Browse available group sessions and filter by subject, grade, and attendance mode.'
        }
        crumbs={[
          { label: locale === 'ar-EG' ? 'الرئيسية' : 'Home', href: '/' },
          { label: t('nav.schedule') },
        ]}
        aside={
          <div className="flex items-center gap-3 rounded-xl border border-border bg-card px-5 py-4">
            <CalendarDays className="size-8 text-primary" />
            <div>
              <p className="tnum font-display text-heading-md font-extrabold text-foreground">
                {slots.isPending ? '—' : formatNumber(all.length)}
              </p>
              <p className="text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'حصة أسبوعية' : 'weekly sessions'}
              </p>
            </div>
          </div>
        }
      />

      <section className="section-y !py-10">
        <div className="container-page space-y-6">
          {/* Toolbar */}
          <div className="flex flex-col gap-4 rounded-xl border border-border bg-card p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              <Select
                value={subjectId ?? 'all'}
                onValueChange={(v) => setSubjectId(v === 'all' ? undefined : v)}
              >
                <SelectTrigger className="w-44">
                  <SelectValue placeholder={t('common.subject')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('common.subject')}</SelectItem>
                  {subjects.subjects.map((s) => (
                    <SelectItem key={s.id} value={s.id!}>
                      {subjects.label(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              <Select
                value={gradeId ?? 'all'}
                onValueChange={(v) => setGradeId(v === 'all' ? undefined : v)}
              >
                <SelectTrigger className="w-40">
                  <SelectValue placeholder={t('common.grade')} />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">{t('common.grade')}</SelectItem>
                  {grades.grades.map((g) => (
                    <SelectItem key={g.id} value={g.id!}>
                      {grades.label(g)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>

              {MODES.map((m) => (
                <FilterChip
                  key={m.value}
                  label={m[lang]}
                  active={mode === m.value}
                  onClick={() => setMode(mode === m.value ? undefined : m.value)}
                />
              ))}

              {activeCount > 0 && (
                <button
                  type="button"
                  onClick={() => {
                    setSubjectId(undefined)
                    setGradeId(undefined)
                    setMode(undefined)
                  }}
                  className="text-caption font-semibold text-primary hover:underline"
                >
                  {t('common.clearFilters')}
                </button>
              )}
            </div>

            <div className="flex items-center rounded-lg border border-border bg-surface p-1">
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
          </div>

          {slots.isPending ? (
            <RowSkeleton count={6} />
          ) : slots.isError ? (
            <QueryError error={slots.error} onRetry={() => slots.refetch()} />
          ) : all.length === 0 ? (
            <EmptyState
              spot="calendar"
              title={locale === 'ar-EG' ? 'مفيش حصص مطابقة' : 'No sessions match'}
              description={
                locale === 'ar-EG'
                  ? 'جرّب تشيل الفلاتر أو تتصفّح المجموعات المتاحة.'
                  : 'Try clearing the filters or browsing the available groups.'
              }
              secondaryAction={{ label: t('nav.groups'), href: '/groups' }}
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
      </section>
    </>
  )
}


