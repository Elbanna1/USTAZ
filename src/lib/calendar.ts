import {
  addMonths,
  endOfMonth,
  endOfWeek,
  format,
  isSameMonth,
  isToday,
  parseISO,
  startOfMonth,
  startOfWeek,
} from 'date-fns'
import type { CalendarEventDto } from '@/api/schema'

/** Saturday. The Egyptian school week starts there, and so does the grid. */
const WEEK_STARTS_ON = 6 as const

/**
 * Reads one of the API's `*Utc` instants.
 *
 * Only some of them actually carry a `Z`. The ones the calendar handler builds
 * itself — class occurrences, held sessions, the range bounds — are stamped
 * UTC explicitly, but exam dates and homework deadlines are projected straight
 * off a SQL Server `datetime2` column, which EF materialises with
 * `DateTimeKind.Unspecified`, and System.Text.Json then writes with no
 * designator at all. `parseISO` reads those as *local* time, which in Egypt
 * drags every deadline three hours earlier and can move it to the wrong day.
 *
 * So a value with no designator is read as the UTC its field name promises.
 * See the note in the Calendar section of the API report.
 */
export function parseUtc(value: string): Date {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/.test(value)
  return parseISO(hasZone ? value : `${value}Z`)
}

/** `yyyy-MM-dd` in the viewer's own zone — the key a day cell is bucketed under. */
export function dayKey(date: Date): string {
  return format(date, 'yyyy-MM-dd')
}

/**
 * The 7-column weeks a month view renders, Saturday-first. Leading and
 * trailing days belong to the neighbouring months and are flagged so the grid
 * can mute them without dropping their events.
 */
export interface GridDay {
  date: Date
  key: string
  inMonth: boolean
  isToday: boolean
}

export function monthWeeks(cursor: Date): GridDay[][] {
  const first = startOfWeek(startOfMonth(cursor), { weekStartsOn: WEEK_STARTS_ON })
  const last = endOfWeek(endOfMonth(cursor), { weekStartsOn: WEEK_STARTS_ON })

  const weeks: GridDay[][] = []
  let row: GridDay[] = []

  for (const cell = new Date(first); cell <= last; cell.setDate(cell.getDate() + 1)) {
    const date = new Date(cell)
    row.push({
      date,
      key: dayKey(date),
      inMonth: isSameMonth(date, cursor),
      isToday: isToday(date),
    })
    if (row.length === 7) {
      weeks.push(row)
      row = []
    }
  }

  return weeks
}

/**
 * The date range to ask the API for. It covers the whole visible grid rather
 * than the calendar month, so the greyed-out neighbour days are not silently
 * empty. Both bounds are inclusive days, which is what the server expects.
 */
export function visibleRange(cursor: Date): { From: string; To: string } {
  return {
    From: dayKey(startOfWeek(startOfMonth(cursor), { weekStartsOn: WEEK_STARTS_ON })),
    To: dayKey(endOfWeek(endOfMonth(cursor), { weekStartsOn: WEEK_STARTS_ON })),
  }
}

export function shiftMonth(cursor: Date, by: number): Date {
  return startOfMonth(addMonths(cursor, by))
}

/** Buckets events under the local day they fall on, each bucket time-ordered. */
export function eventsByDay(events: CalendarEventDto[]): Map<string, CalendarEventDto[]> {
  const buckets = new Map<string, CalendarEventDto[]>()

  for (const event of events) {
    if (!event.startUtc) continue
    const key = dayKey(parseUtc(event.startUtc))
    const bucket = buckets.get(key)
    if (bucket) bucket.push(event)
    else buckets.set(key, [event])
  }

  for (const bucket of buckets.values()) {
    bucket.sort((a, b) => (a.startUtc ?? '').localeCompare(b.startUtc ?? ''))
  }

  return buckets
}

/** Weekday headers for the grid, Saturday-first, in the viewer's locale. */
export function weekdayHeadings(locale: 'ar-EG' | 'en'): string[] {
  const base = startOfWeek(new Date(), { weekStartsOn: WEEK_STARTS_ON })
  return Array.from({ length: 7 }, (_, i) => {
    const date = new Date(base)
    date.setDate(base.getDate() + i)
    return new Intl.DateTimeFormat(locale === 'ar-EG' ? 'ar-EG' : 'en-GB', {
      weekday: 'short',
    }).format(date)
  })
}
