import { Link } from 'react-router-dom'
import { CalendarDays, Clock, MapPin, Monitor, Video } from 'lucide-react'
import type { ScheduleSlotDto } from '@/api/schema'
import { Card } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { SubjectTile } from '@/components/illustrations/SubjectIcon'
import { subjectKeyFor } from '@/lib/identity'
import { apiDayLabel } from '@/lib/schedule'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'

/** Turns a slot's subject fields into the shape `subjectKeyFor` expects. */
function slotSubject(slot: ScheduleSlotDto) {
  return subjectKeyFor({ nameEn: slot.subjectNameEn, nameAr: slot.subjectNameAr })
}

function useSlotView(slot: ScheduleSlotDto) {
  const { locale, pick, formatTime, formatRelativeDay } = useI18n()
  const next = slot.nextOccurrenceUtc ? new Date(slot.nextOccurrenceUtc) : null
  const isLive =
    next !== null &&
    Math.abs(next.getTime() - Date.now()) < (slot.durationInMinutes ?? 60) * 60_000 &&
    next.getTime() <= Date.now()

  return {
    subjectKey: slotSubject(slot),
    subjectName: pick(slot.subjectNameAr, slot.subjectNameEn),
    dayName: apiDayLabel(slot.dayOfWeek, locale),
    timeRange:
      slot.startTime && slot.endTime
        ? `${formatTime(slot.startTime)} — ${formatTime(slot.endTime)}`
        : '',
    nextLabel: next ? formatRelativeDay(next) : null,
    isLive,
    isOnline: Boolean(slot.meetingUrl),
  }
}

interface SlotCardProps {
  slot: ScheduleSlotDto
  className?: string
  /** `compact` is the dense dashboard row; `full` is the standalone card. */
  variant?: 'full' | 'compact'
  showTeacher?: boolean
}

export function SlotCard({
  slot,
  className,
  variant = 'full',
  showTeacher = true,
}: SlotCardProps) {
  const { t, locale } = useI18n()
  const view = useSlotView(slot)

  if (variant === 'compact') {
    return (
      <div
        className={cn(
          'flex items-center gap-3.5 rounded-lg border border-border bg-card p-3.5 transition-colors',
          view.isLive && 'border-warning/45 bg-warning/6',
          className,
        )}
      >
        <SubjectTile subject={view.subjectKey} size="sm" />
        <div className="min-w-0 flex-1">
          <p className="truncate font-display text-body-sm font-semibold text-foreground">
            {slot.groupName}
          </p>
          <p className="tnum truncate text-caption text-muted-foreground">
            {[view.nextLabel ?? view.dayName, view.timeRange].filter(Boolean).join(' · ')}
          </p>
        </div>
        {view.isLive ? (
          <Badge variant="warning" size="sm" className="shrink-0">
            <span className="size-1.5 animate-pulse rounded-full bg-current" />
            {locale === 'ar-EG' ? 'دلوقتي' : 'Now'}
          </Badge>
        ) : (
          slot.meetingUrl && (
            <Button asChild size="sm" variant="outline" className="shrink-0">
              <a href={slot.meetingUrl} target="_blank" rel="noopener noreferrer">
                <Video className="size-4" />
              </a>
            </Button>
          )
        )}
      </div>
    )
  }

  return (
    <Card
      className={cn(
        'relative overflow-hidden transition-all duration-300',
        view.isLive && 'border-warning/45 shadow-glow',
        className,
      )}
    >
      {view.isLive && (
        <span className="absolute inset-x-0 top-0 h-0.5 bg-gradient-to-l from-warning to-accent" />
      )}

      <div className="flex gap-4 p-5">
        <SubjectTile subject={view.subjectKey} size="md" className="shrink-0" />

        <div className="min-w-0 flex-1 space-y-3">
          <div className="flex flex-wrap items-start justify-between gap-2">
            <div className="min-w-0">
              <h3 className="truncate font-display text-heading-sm font-bold text-foreground">
                {slot.groupId ? (
                  <Link to={`/groups/${slot.groupId}`} className="hover:text-primary">
                    {slot.groupName}
                  </Link>
                ) : (
                  slot.groupName
                )}
              </h3>
              {showTeacher && slot.teacherName && (
                <p className="mt-0.5 truncate text-body-sm text-muted-foreground">
                  {slot.teacherId ? (
                    <Link to={`/teachers/${slot.teacherId}`} className="hover:text-foreground">
                      {slot.teacherName}
                    </Link>
                  ) : (
                    slot.teacherName
                  )}
                </p>
              )}
            </div>
            {view.isLive ? (
              <Badge variant="warning" size="sm">
                <span className="size-1.5 animate-pulse rounded-full bg-current" />
                {locale === 'ar-EG' ? 'شغّالة دلوقتي' : 'Live now'}
              </Badge>
            ) : view.subjectName ? (
              <Badge variant="primary" size="sm">
                {view.subjectName}
              </Badge>
            ) : null}
          </div>

          <ul className="grid gap-2 text-body-sm text-muted-foreground sm:grid-cols-2">
            <li className="flex items-center gap-2">
              <CalendarDays className="size-4 shrink-0 text-primary" aria-hidden />
              {view.nextLabel ?? view.dayName}
            </li>
            {view.timeRange && (
              <li className="flex items-center gap-2">
                <Clock className="size-4 shrink-0 text-primary" aria-hidden />
                <span className="tnum">{view.timeRange}</span>
              </li>
            )}
            <li className="flex items-center gap-2 sm:col-span-2">
              {view.isOnline ? (
                <Monitor className="size-4 shrink-0 text-primary" aria-hidden />
              ) : (
                <MapPin className="size-4 shrink-0 text-primary" aria-hidden />
              )}
              <span className="truncate">
                {slot.location ||
                  (view.isOnline
                    ? locale === 'ar-EG'
                      ? 'حصة أونلاين'
                      : 'Online session'
                    : t('common.notProvided'))}
              </span>
            </li>
          </ul>

          {slot.meetingUrl && (
            <Button asChild size="sm" variant={view.isLive ? 'primary' : 'outline'}>
              <a href={slot.meetingUrl} target="_blank" rel="noopener noreferrer">
                <Video className="size-4" />
                {locale === 'ar-EG' ? 'ادخل الحصة' : 'Join session'}
              </a>
            </Button>
          )}
        </div>
      </div>
    </Card>
  )
}
