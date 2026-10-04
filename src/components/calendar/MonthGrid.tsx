import * as React from 'react'
import type { CalendarEventDto } from '@/api/schema'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { dayKey, monthWeeks, parseUtc, weekdayHeadings, type GridDay } from '@/lib/calendar'
import { kindStyle } from '@/components/calendar/vocabulary'

/** Chips beyond this collapse into a "+N" row, so cell heights stay even. */
const CHIPS_PER_CELL = 3

export function MonthGrid({
  cursor,
  buckets,
  selected,
  onSelect,
  className,
}: {
  /** Any date inside the month being shown. */
  cursor: Date
  buckets: Map<string, CalendarEventDto[]>
  selected: string | null
  onSelect: (key: string) => void
  className?: string
}) {
  const { locale, formatNumber } = useI18n()
  const isAr = locale === 'ar-EG'

  const weeks = React.useMemo(() => monthWeeks(cursor), [cursor])
  const headings = React.useMemo(() => weekdayHeadings(locale), [locale])
  const todayKey = React.useMemo(() => dayKey(new Date()), [])

  return (
    <div className={cn('overflow-hidden rounded-xl border border-border bg-card', className)}>
      <div
        className="grid grid-cols-7 border-b border-border bg-muted/40"
        role="presentation"
        aria-hidden
      >
        {headings.map((label) => (
          <div
            key={label}
            className="truncate px-1 py-2.5 text-center font-display text-caption font-bold text-muted-foreground sm:px-2 sm:py-3"
          >
            {label}
          </div>
        ))}
      </div>

      <div role="grid" aria-label={isAr ? 'شهر' : 'Month'}>
        {weeks.map((week, weekIndex) => (
          <div
            role="row"
            key={week[0].key}
            className={cn('grid grid-cols-7', weekIndex > 0 && 'border-t border-border')}
          >
            {week.map((day) => (
              <DayCell
                key={day.key}
                day={day}
                events={buckets.get(day.key) ?? []}
                isSelected={selected === day.key}
                isToday={day.key === todayKey}
                onSelect={onSelect}
                formatNumber={formatNumber}
                isAr={isAr}
              />
            ))}
          </div>
        ))}
      </div>
    </div>
  )
}

function DayCell({
  day,
  events,
  isSelected,
  isToday,
  onSelect,
  formatNumber,
  isAr,
}: {
  day: GridDay
  events: CalendarEventDto[]
  isSelected: boolean
  isToday: boolean
  onSelect: (key: string) => void
  formatNumber: (n: number) => string
  isAr: boolean
}) {
  const { formatTime } = useI18n()
  const shown = events.slice(0, CHIPS_PER_CELL)
  const hidden = events.length - shown.length

  const dayNumber = formatNumber(day.date.getDate())
  const label = `${dayNumber}${
    events.length ? ` — ${formatNumber(events.length)} ${isAr ? 'موعد' : 'events'}` : ''
  }`

  return (
    <div role="gridcell" className="border-s border-border first:border-s-0">
      <button
        type="button"
        onClick={() => onSelect(day.key)}
        aria-label={label}
        aria-current={isToday ? 'date' : undefined}
        aria-pressed={isSelected}
        className={cn(
          'flex h-20 w-full flex-col gap-1 p-1.5 text-start transition-colors sm:h-[6.75rem] sm:gap-1.5 sm:p-2',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
          !day.inMonth && 'bg-muted/25',
          isSelected ? 'bg-primary/[0.07]' : 'hover:bg-muted/50',
        )}
      >
        <span className="flex items-center justify-between gap-1">
          <span
            className={cn(
              'tnum grid size-6 shrink-0 place-items-center rounded-full text-caption font-semibold transition-colors',
              isToday && 'bg-primary text-primary-foreground',
              !isToday && isSelected && 'bg-primary/15 text-primary',
              !isToday && !isSelected && day.inMonth && 'text-foreground',
              !isToday && !isSelected && !day.inMonth && 'text-muted-foreground/60',
            )}
          >
            {dayNumber}
          </span>

          {/* Below sm there is no room for chips, so the day reads as dots. */}
          {events.length > 0 && (
            <span className="flex gap-0.5 sm:hidden" aria-hidden>
              {events.slice(0, 3).map((event, i) => (
                <span
                  key={event.id ?? i}
                  className={cn('size-1.5 rounded-full', kindStyle(event).dot)}
                />
              ))}
            </span>
          )}
        </span>

        <span className="hidden min-w-0 flex-1 flex-col gap-1 sm:flex">
          {shown.map((event, i) => {
            const style = kindStyle(event)
            return (
              <span
                key={event.id ?? i}
                className={cn(
                  'flex items-center gap-1 truncate rounded px-1.5 py-0.5 text-[0.6875rem] font-medium leading-tight',
                  style.chip,
                )}
              >
                {event.startUtc && !event.isDeadline && (
                  <span className="tnum shrink-0 opacity-70">
                    {formatTime(timeOf(event.startUtc))}
                  </span>
                )}
                <span className="truncate">{event.title}</span>
              </span>
            )
          })}

          {hidden > 0 && (
            <span className="tnum px-1.5 text-[0.6875rem] font-semibold text-muted-foreground">
              {isAr ? `+${formatNumber(hidden)} كمان` : `+${formatNumber(hidden)} more`}
            </span>
          )}
        </span>
      </button>
    </div>
  )
}

/** `formatTime` takes a wall-clock `HH:mm`, so hand it the local one. */
function timeOf(startUtc: string): string {
  const date = parseUtc(startUtc)
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}
