import * as React from 'react'
import { motion } from 'framer-motion'
import {
  Award,
  BookOpen,
  CalendarCheck,
  CheckCircle2,
  ClipboardCheck,
  FileText,
  GraduationCap,
  LogIn,
  LogOut,
  Megaphone,
  Star,
  Undo2,
  Upload,
  UserPlus,
  Wallet,
  type LucideIcon,
} from 'lucide-react'
import type { ActivityEventDto } from '@/api/schema'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { avatarTraitsFor } from '@/lib/identity'

/* -------------------------------------------------------------------------- */
/*                              Event vocabulary                              */
/* -------------------------------------------------------------------------- */

/*
 * The API stores a stable key per event (`homework.submitted`) and deliberately
 * no prose, so wording lives here and stays translatable. An unknown key still
 * renders — a new backend event should appear in the feed before anyone has
 * written copy for it, not vanish.
 */

type Tone = 'neutral' | 'success' | 'warning' | 'info' | 'accent'

interface EventStyle {
  icon: LucideIcon
  tone: Tone
  /** `{name}` is replaced with the student, `{actor}` with whoever acted. */
  ar: string
  en: string
}

const EVENTS: Record<string, EventStyle> = {
  'lesson.completed': {
    icon: CheckCircle2, tone: 'success',
    ar: 'خلّص درس', en: 'completed a lesson',
  },
  'course.joined': {
    icon: BookOpen, tone: 'info',
    ar: 'اشترك في كورس', en: 'joined a course',
  },
  'course.completed': {
    icon: GraduationCap, tone: 'success',
    ar: 'أتمّ الكورس', en: 'finished a course',
  },
  'group.joined': {
    icon: LogIn, tone: 'info',
    ar: 'انضم للمجموعة', en: 'joined the group',
  },
  'group.left': {
    icon: LogOut, tone: 'neutral',
    ar: 'ساب المجموعة', en: 'left the group',
  },
  'homework.submitted': {
    icon: Upload, tone: 'info',
    ar: 'سلّم واجب', en: 'submitted homework',
  },
  'homework.graded': {
    icon: ClipboardCheck, tone: 'success',
    ar: 'اتصحّح واجبه', en: 'had homework graded',
  },
  'homework.returned': {
    icon: Undo2, tone: 'warning',
    ar: 'اترجّعله الواجب للتعديل', en: 'had homework returned for changes',
  },
  'homework.published': {
    icon: FileText, tone: 'neutral',
    ar: 'اتنشر واجب جديد', en: 'homework published',
  },
  'exam.completed': {
    icon: FileText, tone: 'info',
    ar: 'خلّص امتحان', en: 'finished an exam',
  },
  'exam.graded': {
    icon: ClipboardCheck, tone: 'success',
    ar: 'اتصحّح امتحانه', en: 'had an exam graded',
  },
  'exam.published': {
    icon: FileText, tone: 'neutral',
    ar: 'اتنشر امتحان جديد', en: 'exam published',
  },
  'attendance.recorded': {
    icon: CalendarCheck, tone: 'neutral',
    ar: 'اتسجّل حضوره', en: 'attendance recorded',
  },
  'lesson.published': {
    icon: BookOpen, tone: 'neutral',
    ar: 'اتنشر درس جديد', en: 'lesson published',
  },
  'course.published': {
    icon: BookOpen, tone: 'neutral',
    ar: 'اتنشر كورس جديد', en: 'course published',
  },
  'group.published': {
    icon: Megaphone, tone: 'neutral',
    ar: 'اتنشرت مجموعة جديدة', en: 'group published',
  },
  'payment.received': {
    icon: Wallet, tone: 'success',
    ar: 'دفع اشتراكه', en: 'made a payment',
  },
  'review.received': {
    icon: Star, tone: 'accent',
    ar: 'ساب تقييم', en: 'left a review',
  },
  'student.joined_workspace': {
    icon: UserPlus, tone: 'info',
    ar: 'انضم لمساحة العمل', en: 'joined the workspace',
  },
  'teacher.verified': {
    icon: Award, tone: 'success',
    ar: 'اتوثّق الحساب', en: 'account verified',
  },
  'certificate.issued': {
    icon: Award, tone: 'accent',
    ar: 'استلم شهادة', en: 'earned a certificate',
  },
}

const TONE_STYLES: Record<Tone, string> = {
  neutral: 'bg-muted text-muted-foreground',
  success: 'bg-success/12 text-success-strong',
  warning: 'bg-warning/15 text-warning-strong',
  info: 'bg-info/12 text-info',
  accent: 'bg-accent/15 text-accent-strong',
}

/** Turns an unknown key into something readable rather than dropping the row. */
function prettify(key: string): string {
  return key.replace(/[._-]+/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase())
}

/** Metadata is a small JSON object of display values; never trust its shape. */
function readMetadata(raw: string | null | undefined): Record<string, unknown> {
  if (!raw) return {}
  try {
    const parsed = JSON.parse(raw)
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : {}
  } catch {
    return {}
  }
}

/*
 * The backend names the subject of an event after its entity — `homework`,
 * `exam`, `course`, `group`, `lesson` — rather than a uniform `title`, so the
 * first of those that is present is the thing the row is about.
 */
const SUBJECT_KEYS = ['homework', 'exam', 'course', 'group', 'lesson', 'title'] as const

function readSubject(meta: Record<string, unknown>): string | null {
  for (const key of SUBJECT_KEYS) {
    const value = meta[key]
    if (typeof value === 'string' && value.trim()) return value
  }
  return null
}

/** The one extra number worth showing inline, when the event carries one. */
function readDetail(
  meta: Record<string, unknown>,
  isAr: boolean,
  formatNumber: (n: number) => string,
): string | null {
  if (typeof meta.percentage === 'number') return `${formatNumber(Math.round(meta.percentage))}%`
  if (typeof meta.score === 'number') return formatNumber(meta.score)
  if (typeof meta.rating === 'number') return `${formatNumber(meta.rating)}★`
  if (typeof meta.amount === 'number') return formatNumber(meta.amount)
  if (meta.isLate === true) return isAr ? 'متأخر' : 'late'
  return null
}

/* -------------------------------------------------------------------------- */
/*                                  Timeline                                  */
/* -------------------------------------------------------------------------- */

export function ActivityTimeline({
  events,
  className,
  /** Hide the per-row person when the whole feed is already about one student. */
  hideActor = false,
}: {
  events: ActivityEventDto[]
  className?: string
  hideActor?: boolean
}) {
  const { locale, formatTimeAgo, formatDateTime, formatNumber } = useI18n()
  const isAr = locale === 'ar-EG'

  /* Group by day so a long feed reads as a diary rather than a flat list. */
  const days = React.useMemo(() => {
    const buckets = new Map<string, ActivityEventDto[]>()
    for (const event of events) {
      const key = (event.occurredAt ?? '').slice(0, 10)
      const bucket = buckets.get(key)
      if (bucket) bucket.push(event)
      else buckets.set(key, [event])
    }
    return [...buckets.entries()]
  }, [events])

  return (
    <div className={cn('space-y-8', className)}>
      {days.map(([day, dayEvents]) => (
        <section key={day || 'undated'}>
          {day && (
            <h3 className="mb-4 text-caption font-semibold uppercase tracking-wide text-muted-foreground">
              <DayLabel iso={day} />
            </h3>
          )}

          <ol className="relative space-y-1">
            {/* The spine sits on the start edge so it mirrors under RTL. */}
            <span
              aria-hidden
              className="pointer-events-none absolute inset-y-2 start-[1.125rem] w-px bg-border"
            />

            {dayEvents.map((event, index) => {
              const key = event.type ?? ''
              const style = EVENTS[key]
              const Icon = style?.icon ?? FileText
              const tone = style?.tone ?? 'neutral'
              const meta = readMetadata(event.metadata)
              const subject = readSubject(meta)
              const detail = readDetail(meta, isAr, formatNumber)
              const who = event.studentName || event.actorName || ''
              const phrase = style ? (isAr ? style.ar : style.en) : prettify(key)

              return (
                <motion.li
                  key={event.id ?? `${key}-${index}`}
                  initial={{ opacity: 0, y: 6 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ ...transitions.fast, delay: Math.min(index, 8) * 0.03 }}
                  className="relative flex gap-3.5 rounded-xl p-2 transition-colors hover:bg-muted/40"
                >
                  <span
                    className={cn(
                      'relative z-10 mt-0.5 grid size-9 shrink-0 place-items-center rounded-full ring-4 ring-background',
                      TONE_STYLES[tone],
                    )}
                  >
                    <Icon className="size-4" />
                  </span>

                  <div className="min-w-0 flex-1 pt-1">
                    <p className="text-body-sm text-foreground">
                      {!hideActor && who && (
                        <span className="font-display font-bold">{who} </span>
                      )}
                      <span className={cn(!hideActor && who ? 'text-muted-foreground' : '')}>
                        {phrase}
                      </span>
                      {subject && <span className="font-medium"> — {subject}</span>}
                      {detail && (
                        <span className="tnum text-muted-foreground"> · {detail}</span>
                      )}
                    </p>

                    {typeof meta.reason === 'string' && meta.reason.trim() && (
                      <p className="mt-1 text-caption text-muted-foreground">{meta.reason}</p>
                    )}

                    {event.occurredAt && (
                      <time
                        dateTime={event.occurredAt}
                        title={formatDateTime(event.occurredAt)}
                        className="mt-0.5 block text-caption text-muted-foreground"
                      >
                        {formatTimeAgo(event.occurredAt)}
                      </time>
                    )}
                  </div>

                  {!hideActor && event.studentId && (
                    <Avatar
                      traits={avatarTraitsFor(event.studentId)}
                      seed={event.studentId}
                      name={event.studentName ?? ''}
                      size="xs"
                      className="mt-1 shrink-0"
                    />
                  )}
                </motion.li>
              )
            })}
          </ol>
        </section>
      ))}
    </div>
  )
}

function DayLabel({ iso }: { iso: string }) {
  const { formatRelativeDay } = useI18n()
  return <>{formatRelativeDay(iso)}</>
}
