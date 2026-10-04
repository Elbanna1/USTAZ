import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { ChevronRight, MapPin, Users, Video } from 'lucide-react'
import type { CalendarEventDto } from '@/api/schema'
import { useAuth } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { parseUtc } from '@/lib/calendar'
import { eventHref, kindStyle, statusLabel } from '@/components/calendar/vocabulary'

/* -------------------------------------------------------------------------- */
/*                                    Day                                     */
/* -------------------------------------------------------------------------- */

/**
 * The day's entries in time order. The month grid is for scanning; this is
 * where a row carries enough to act on — who, where, and what state it is in.
 */
export function DayAgenda({
  events,
  className,
}: {
  events: CalendarEventDto[]
  className?: string
}) {
  return (
    <ul className={cn('divide-y divide-border', className)}>
      {events.map((event, index) => (
        <EventRow key={event.id ?? index} event={event} index={index} />
      ))}
    </ul>
  )
}

/* -------------------------------------------------------------------------- */
/*                                    Row                                     */
/* -------------------------------------------------------------------------- */

function EventRow({ event, index }: { event: CalendarEventDto; index: number }) {
  const { locale, formatTime } = useI18n()
  const { role } = useAuth()
  const isAr = locale === 'ar-EG'

  const style = kindStyle(event)
  const Icon = style.icon
  const status = statusLabel(event.status, locale)
  const href = eventHref(event, role)

  const body = (
    <>
      {/* Time column — a deadline is a moment, so it shows one time, not a span. */}
      <span className="flex w-16 shrink-0 flex-col items-start pt-0.5 sm:w-20">
        <span className="tnum font-display text-body-sm font-bold text-foreground">
          {event.startUtc ? formatTime(localTime(event.startUtc)) : '—'}
        </span>
        {!event.isDeadline && event.endUtc && event.endUtc !== event.startUtc && (
          <span className="tnum text-caption text-muted-foreground">
            {formatTime(localTime(event.endUtc))}
          </span>
        )}
        {event.isDeadline && (
          <span className="text-caption text-muted-foreground">
            {isAr ? 'آخر ميعاد' : 'deadline'}
          </span>
        )}
      </span>

      <span
        className={cn('mt-1 grid size-8 shrink-0 place-items-center rounded-lg', style.chip)}
        aria-hidden
      >
        <Icon className="size-4" />
      </span>

      <span className="min-w-0 flex-1">
        <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="font-display text-body-sm font-bold text-foreground">
            {event.title}
          </span>
          <span className="text-caption text-muted-foreground">
            {isAr ? style.ar : style.en}
          </span>
          {status && (
            <span
              className={cn(
                'rounded-full border px-2 py-0.5 text-[0.6875rem] font-semibold',
                status.className,
              )}
            >
              {status.label}
            </span>
          )}
        </span>

        {event.description && (
          <span className="mt-1 block truncate text-body-sm text-muted-foreground">
            {event.description}
          </span>
        )}

        <span className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-caption text-muted-foreground">
          {event.groupName && (
            <span className="inline-flex items-center gap-1">
              <Users className="size-3.5" aria-hidden />
              {event.groupName}
            </span>
          )}
          {event.location && (
            <span className="inline-flex items-center gap-1">
              <MapPin className="size-3.5" aria-hidden />
              {event.location}
            </span>
          )}
          {event.subjectNameAr && isAr && <span>{event.subjectNameAr}</span>}
        </span>
      </span>

      {href && (
        <ChevronRight
          className="mt-2 size-4 shrink-0 text-muted-foreground/60 flip-rtl"
          aria-hidden
        />
      )}
    </>
  )

  const shell = 'flex gap-3 px-4 py-3.5 sm:gap-4 sm:px-5'

  return (
    <motion.li
      initial={{ opacity: 0, y: 4 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ ...transitions.fast, delay: Math.min(index, 10) * 0.025 }}
    >
      {href ? (
        <Link
          to={href}
          className={cn(
            shell,
            'transition-colors hover:bg-muted/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring',
          )}
        >
          {body}
        </Link>
      ) : (
        <div className={shell}>{body}</div>
      )}

      {/* A live class is the one thing worth a button rather than a line of text. */}
      {event.meetingUrl && (
        <div className="px-4 pb-3.5 ps-[4.75rem] sm:px-5 sm:ps-[6.5rem]">
          <a
            href={event.meetingUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 rounded-lg border border-border bg-surface px-2.5 py-1.5 text-caption font-semibold text-foreground transition-colors hover:border-foreground/20 hover:bg-muted focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            <Video className="size-3.5" aria-hidden />
            {isAr ? 'ادخل الحصة' : 'Join class'}
          </a>
        </div>
      )}
    </motion.li>
  )
}

/** `formatTime` wants a wall-clock `HH:mm`; the API sends a UTC instant. */
function localTime(utc: string): string {
  const date = parseUtc(utc)
  return `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`
}
