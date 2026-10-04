import * as React from 'react'
import { Link } from 'react-router-dom'
import type { ScheduleSlotDto } from '@/api/schema'
import { subjectByKey } from '@/data/taxonomy'
import { subjectKeyFor } from '@/lib/identity'
import { WEEK_ORDER, dayLabel, slotsByDay } from '@/lib/schedule'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { EmptyState } from '@/components/common/states'
import { useI18n } from '@/i18n'
import type { DayKey } from '@/types'

/** Earliest and latest hours we render rows for. */
const HOUR_START = 8
const HOUR_END = 23

function minutesFrom(time?: string): number | null {
  if (!time) return null
  const [h, m] = time.split(':').map(Number)
  if (Number.isNaN(h)) return null
  return h * 60 + (m || 0)
}

/**
 * Weekly grid of recurring slots. Columns are Saturday-first; in an RTL document
 * the grid flows right-to-left, so Saturday sits where a reader expects it.
 */
export function WeekGrid({ slots, className }: { slots: ScheduleSlotDto[]; className?: string }) {
  const { locale, formatTime } = useI18n()
  const byDay = React.useMemo(() => slotsByDay(slots), [slots])
  const hours = React.useMemo(
    () => Array.from({ length: HOUR_END - HOUR_START }, (_, i) => HOUR_START + i),
    [],
  )

  if (slots.length === 0) {
    return (
      <EmptyState
        spot="calendar"
        title={locale === 'ar-EG' ? 'مفيش حصص في الجدول' : 'No sessions scheduled'}
        description={
          locale === 'ar-EG'
            ? 'أول ما تتضاف مواعيد للمجموعات هتلاقيها هنا.'
            : 'Group sessions will appear here once they are scheduled.'
        }
        className={className}
      />
    )
  }

  return (
    <div className={cn('overflow-x-auto rounded-xl border border-border bg-card', className)}>
      <div className="min-w-[56rem]">
        {/* Header */}
        <div className="grid grid-cols-[4rem_repeat(7,1fr)] border-b border-border bg-muted/40">
          <div aria-hidden />
          {WEEK_ORDER.map((day) => (
            <div key={day} className="border-s border-border px-2 py-3 text-center">
              <p className="font-display text-caption font-bold text-foreground">
                {dayLabel(day, locale, 'short')}
              </p>
              <p className="tnum text-caption text-muted-foreground">
                {byDay[day].length || '—'}
              </p>
            </div>
          ))}
        </div>

        {/* Body */}
        <div className="relative grid grid-cols-[4rem_repeat(7,1fr)]">
          <div className="border-e border-border">
            {hours.map((h) => (
              <div
                key={h}
                className="tnum flex h-16 items-start justify-center border-b border-border pt-1 text-[0.6875rem] text-muted-foreground"
              >
                {formatTime(`${String(h).padStart(2, '0')}:00`)}
              </div>
            ))}
          </div>

          {WEEK_ORDER.map((day) => (
            <div key={day} className="relative border-s border-border">
              {hours.map((h) => (
                <div key={h} className="h-16 border-b border-border" />
              ))}

              {byDay[day].map((slot) => {
                const startMin = minutesFrom(slot.startTime)
                if (startMin === null) return null
                const startHour = startMin / 60
                if (startHour < HOUR_START || startHour >= HOUR_END) return null

                const duration =
                  slot.durationInMinutes ??
                  Math.max(30, (minutesFrom(slot.endTime) ?? startMin + 60) - startMin)
                const topRem = (startHour - HOUR_START) * 4
                const heightRem = Math.max(2.25, (duration / 60) * 4 - 0.25)
                const hue = subjectByKey(
                  subjectKeyFor({ nameEn: slot.subjectNameEn, nameAr: slot.subjectNameAr }),
                ).hue

                const body = (
                  <>
                    <span className="block truncate font-display text-[0.6875rem] font-bold leading-tight">
                      {slot.groupName}
                    </span>
                    <span className="tnum block truncate text-[0.625rem] opacity-80">
                      {slot.startTime ? formatTime(slot.startTime) : ''}
                    </span>
                  </>
                )

                const style = {
                  backgroundColor: `hsl(${hue} 60% 50% / 0.14)`,
                  borderInlineStartColor: `hsl(${hue} 58% 44%)`,
                  color: `hsl(${hue} 62% 26%)`,
                }

                return (
                  <div
                    key={slot.id ?? `${day}-${slot.startTime}`}
                    className="absolute inset-x-1"
                    style={{ top: `${topRem}rem`, height: `${heightRem}rem` }}
                  >
                    {slot.groupId ? (
                      <Link
                        to={`/groups/${slot.groupId}`}
                        title={`${slot.groupName ?? ''} — ${slot.startTime ?? ''}`}
                        className="flex h-full flex-col justify-center overflow-hidden rounded-md border-s-[3px] px-2 py-1 transition-all duration-200 hover:z-10 hover:shadow-soft"
                        style={style}
                      >
                        {body}
                      </Link>
                    ) : (
                      <div
                        className="flex h-full flex-col justify-center overflow-hidden rounded-md border-s-[3px] px-2 py-1"
                        style={style}
                      >
                        {body}
                      </div>
                    )}
                  </div>
                )
              })}
            </div>
          ))}
        </div>
      </div>
    </div>
  )
}

/** Mobile-first day strip: pick a day, read its sessions underneath. */
export function DayStrip({
  slots,
  selected,
  onSelect,
  className,
}: {
  slots: ScheduleSlotDto[]
  selected: DayKey
  onSelect: (day: DayKey) => void
  className?: string
}) {
  const { locale } = useI18n()
  const byDay = React.useMemo(() => slotsByDay(slots), [slots])

  return (
    <div className={cn('no-scrollbar flex gap-2 overflow-x-auto pb-1', className)} role="tablist">
      {WEEK_ORDER.map((day) => {
        const active = day === selected
        const count = byDay[day].length
        return (
          <button
            key={day}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onSelect(day)}
            className={cn(
              'flex min-w-[4.5rem] shrink-0 flex-col items-center gap-1.5 rounded-xl border px-3 py-2.5 transition-all duration-200 ease-premium',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              active
                ? 'border-primary bg-primary text-primary-foreground shadow-soft'
                : 'border-border bg-card text-muted-foreground hover:border-foreground/20 hover:text-foreground',
            )}
          >
            <span className="font-display text-caption font-bold">
              {dayLabel(day, locale, 'short')}
            </span>
            {count > 0 ? (
              <Badge
                variant={active ? 'outline' : 'primary'}
                size="sm"
                className={cn(
                  'tnum px-1.5 py-0',
                  active && 'border-primary-foreground/30 text-primary-foreground',
                )}
              >
                {count}
              </Badge>
            ) : (
              <span className="text-[0.625rem] opacity-60">—</span>
            )}
          </button>
        )
      })}
    </div>
  )
}
