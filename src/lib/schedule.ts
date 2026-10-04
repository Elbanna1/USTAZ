import type { GroupScheduleDto, ScheduleSlotDto } from '@/api/schema'
import type { DayKey } from '@/types'

/** The API speaks English weekday names; the product thinks in day keys. */
export type ApiDay =
  | 'Sunday'
  | 'Monday'
  | 'Tuesday'
  | 'Wednesday'
  | 'Thursday'
  | 'Friday'
  | 'Saturday'

export const API_DAY_TO_KEY: Record<ApiDay, DayKey> = {
  Saturday: 'sat',
  Sunday: 'sun',
  Monday: 'mon',
  Tuesday: 'tue',
  Wednesday: 'wed',
  Thursday: 'thu',
  Friday: 'fri',
}

export const KEY_TO_API_DAY: Record<DayKey, ApiDay> = {
  sat: 'Saturday',
  sun: 'Sunday',
  mon: 'Monday',
  tue: 'Tuesday',
  wed: 'Wednesday',
  thu: 'Thursday',
  fri: 'Friday',
}

/** Saturday-first — the Egyptian school week. */
export const WEEK_ORDER: DayKey[] = ['sat', 'sun', 'mon', 'tue', 'wed', 'thu', 'fri']

const DAY_LABELS: Record<DayKey, { ar: string; arShort: string; en: string; enShort: string }> = {
  sat: { ar: 'السبت', arShort: 'سبت', en: 'Saturday', enShort: 'Sat' },
  sun: { ar: 'الأحد', arShort: 'أحد', en: 'Sunday', enShort: 'Sun' },
  mon: { ar: 'الإثنين', arShort: 'إثنين', en: 'Monday', enShort: 'Mon' },
  tue: { ar: 'الثلاثاء', arShort: 'ثلاثاء', en: 'Tuesday', enShort: 'Tue' },
  wed: { ar: 'الأربعاء', arShort: 'أربعاء', en: 'Wednesday', enShort: 'Wed' },
  thu: { ar: 'الخميس', arShort: 'خميس', en: 'Thursday', enShort: 'Thu' },
  fri: { ar: 'الجمعة', arShort: 'جمعة', en: 'Friday', enShort: 'Fri' },
}

export function dayLabel(
  day: DayKey,
  locale: 'ar-EG' | 'en',
  variant: 'long' | 'short' = 'long',
): string {
  const entry = DAY_LABELS[day]
  if (locale === 'ar-EG') return variant === 'short' ? entry.arShort : entry.ar
  return variant === 'short' ? entry.enShort : entry.en
}

export function apiDayLabel(
  day: ApiDay | undefined,
  locale: 'ar-EG' | 'en',
  variant: 'long' | 'short' = 'long',
): string {
  if (!day) return ''
  return dayLabel(API_DAY_TO_KEY[day], locale, variant)
}

/** "سبت · إثنين · خميس" in week order, whatever order the API returned. */
export function formatScheduleDays(
  schedules: (GroupScheduleDto | ScheduleSlotDto)[] | undefined,
  locale: 'ar-EG' | 'en',
): string {
  if (!schedules?.length) return ''
  const keys = new Set(
    schedules
      .map((s) => s.dayOfWeek)
      .filter((d): d is ApiDay => Boolean(d))
      .map((d) => API_DAY_TO_KEY[d]),
  )
  return WEEK_ORDER.filter((key) => keys.has(key))
    .map((key) => dayLabel(key, locale, 'short'))
    .join(' · ')
}

/** Takes the earliest start and latest end across the week's sessions. */
export function scheduleTimeRange(
  schedules: (GroupScheduleDto | ScheduleSlotDto)[] | undefined,
): { start: string; end: string } | null {
  const valid = (schedules ?? []).filter((s) => s.startTime && s.endTime)
  if (!valid.length) return null
  const start = valid.map((s) => s.startTime!).sort()[0]
  const end = valid.map((s) => s.endTime!).sort().at(-1)!
  return { start, end }
}

/** Groups slots into the seven weekday buckets a week grid renders. */
export function slotsByDay<T extends { dayOfWeek?: ApiDay }>(slots: T[]): Record<DayKey, T[]> {
  const buckets = Object.fromEntries(WEEK_ORDER.map((d) => [d, [] as T[]])) as Record<DayKey, T[]>
  for (const slot of slots) {
    if (!slot.dayOfWeek) continue
    buckets[API_DAY_TO_KEY[slot.dayOfWeek]].push(slot)
  }
  for (const day of WEEK_ORDER) {
    buckets[day].sort((a, b) =>
      String((a as { startTime?: string }).startTime ?? '').localeCompare(
        String((b as { startTime?: string }).startTime ?? ''),
      ),
    )
  }
  return buckets
}
