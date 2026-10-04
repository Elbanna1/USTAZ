import { CalendarClock, ClipboardCheck, FileText, Users, type LucideIcon } from 'lucide-react'
import type { CalendarEventDto } from '@/api/schema'
import type { CalendarEventKind } from '@/api/endpoints/platform'
import type { AppRole } from '@/hooks/use-auth'

/* -------------------------------------------------------------------------- */
/*                                   Kinds                                    */
/* -------------------------------------------------------------------------- */

/*
 * Every calendar entry is a projection of something that already exists — a
 * weekly slot, an exam, a deadline, a register — so the four kinds are fixed by
 * the API rather than configurable. Each one gets a colour of its own, because
 * the whole point of merging them into one grid is telling them apart at a
 * glance.
 */

export interface KindStyle {
  icon: LucideIcon
  ar: string
  en: string
  /** Soft fill + text, for chips inside a day cell. */
  chip: string
  /** The 3px leading rule on an agenda row. */
  rule: string
  /** Solid dot, for the compact mobile grid and the legend. */
  dot: string
}

export const KINDS: Record<CalendarEventKind, KindStyle> = {
  Class: {
    icon: Users,
    ar: 'حصة',
    en: 'Class',
    chip: 'bg-primary/10 text-primary dark:bg-primary/15',
    rule: 'border-s-primary',
    dot: 'bg-primary',
  },
  Exam: {
    icon: FileText,
    ar: 'امتحان',
    en: 'Exam',
    chip: 'bg-accent/15 text-accent-strong',
    rule: 'border-s-accent',
    dot: 'bg-accent',
  },
  HomeworkDeadline: {
    icon: CalendarClock,
    ar: 'تسليم واجب',
    en: 'Homework due',
    chip: 'bg-warning/15 text-warning-strong',
    rule: 'border-s-warning',
    dot: 'bg-warning',
  },
  AttendanceSession: {
    icon: ClipboardCheck,
    ar: 'حصة اتحضرت',
    en: 'Session held',
    chip: 'bg-success/12 text-success-strong',
    rule: 'border-s-success',
    dot: 'bg-success',
  },
}

/** The order the filters and legend read in — roughly how often each matters. */
export const KIND_ORDER: CalendarEventKind[] = [
  'Class',
  'Exam',
  'HomeworkDeadline',
  'AttendanceSession',
]

const FALLBACK: KindStyle = {
  icon: CalendarClock,
  ar: 'موعد',
  en: 'Entry',
  chip: 'bg-muted text-muted-foreground',
  rule: 'border-s-border',
  dot: 'bg-muted-foreground',
}

/** A kind the client does not know about still renders rather than vanishing. */
export function kindStyle(event: CalendarEventDto): KindStyle {
  const key = event.type as CalendarEventKind | undefined
  return (key && KINDS[key]) || FALLBACK
}

/* -------------------------------------------------------------------------- */
/*                                  Statuses                                  */
/* -------------------------------------------------------------------------- */

/*
 * The server sends one of a small set of status words. On a student's own
 * calendar a homework status is their own standing, which is the single most
 * useful thing on the row — so it is worth naming properly in both languages
 * rather than echoing the raw token.
 */

type StatusTone = 'neutral' | 'success' | 'warning' | 'danger' | 'info'

const STATUSES: Record<string, { ar: string; en: string; tone: StatusTone }> = {
  Scheduled: { ar: 'مجدول', en: 'Scheduled', tone: 'neutral' },
  Draft: { ar: 'مسودة', en: 'Draft', tone: 'neutral' },
  Open: { ar: 'مفتوح للتسليم', en: 'Open', tone: 'info' },
  Closed: { ar: 'اتقفل', en: 'Closed', tone: 'neutral' },
  Overdue: { ar: 'فات موعده', en: 'Overdue', tone: 'danger' },
  Submitted: { ar: 'سلّمته', en: 'Submitted', tone: 'info' },
  Graded: { ar: 'اتصحّح', en: 'Graded', tone: 'success' },
  Returned: { ar: 'اترجّع للتعديل', en: 'Returned', tone: 'warning' },
  Late: { ar: 'سلّمته متأخر', en: 'Submitted late', tone: 'warning' },
  NotSubmitted: { ar: 'لسه مسلّمتهوش', en: 'Not submitted', tone: 'warning' },
  Completed: { ar: 'خلصت', en: 'Completed', tone: 'success' },
  Held: { ar: 'اتحضرت', en: 'Held', tone: 'success' },
  Cancelled: { ar: 'اتلغت', en: 'Cancelled', tone: 'danger' },
  Postponed: { ar: 'اتأجلت', en: 'Postponed', tone: 'warning' },
}

const STATUS_CLASSES: Record<StatusTone, string> = {
  neutral: 'border-border bg-muted text-muted-foreground',
  info: 'border-primary/20 bg-primary/10 text-primary',
  success: 'border-success/25 bg-success/12 text-success-strong',
  warning: 'border-warning/30 bg-warning/12 text-warning-strong',
  danger: 'border-destructive/25 bg-destructive/12 text-destructive-strong',
}

/**
 * `Scheduled` is the default state of almost everything on the calendar, so
 * labelling it would put a grey pill on nearly every row and say nothing.
 */
export function statusLabel(
  status: string | undefined,
  locale: 'ar-EG' | 'en',
): { label: string; className: string } | null {
  if (!status || status === 'Scheduled') return null
  const entry = STATUSES[status]
  if (!entry) {
    return { label: status, className: STATUS_CLASSES.neutral }
  }
  return {
    label: locale === 'ar-EG' ? entry.ar : entry.en,
    className: STATUS_CLASSES[entry.tone],
  }
}

/* -------------------------------------------------------------------------- */
/*                                Deep links                                  */
/* -------------------------------------------------------------------------- */

/*
 * `relatedEntityType` + `relatedEntityId` point back at the thing the entry
 * projects. Only some of those have a screen in every console, so this returns
 * a link exclusively where one genuinely exists — a calendar full of rows that
 * navigate nowhere is worse than one where only the real ones are clickable.
 */
export function eventHref(event: CalendarEventDto, role: AppRole | null): string | null {
  const id = event.relatedEntityId
  if (!id) return null

  switch (event.relatedEntityType) {
    case 'Exam':
      if (role === 'teacher' || role === 'assistant') return `/teacher/exams/${id}`
      if (role === 'student') return `/student/exams/${id}`
      return null

    case 'Homework':
      if (role === 'teacher' || role === 'assistant') return `/teacher/homework/${id}`
      if (role === 'student') return `/student/homework/${id}`
      return null

    // A class occurrence and a held register both belong to a group, and the
    // group page is the only screen that describes either. It is public, so it
    // reads the same for a student, a guardian and the teacher.
    case 'GroupSchedule':
    case 'AttendanceSession':
      return event.groupId ? `/groups/${event.groupId}` : null

    default:
      return null
  }
}
