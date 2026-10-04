import { Badge } from '@/components/ui/badge'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'

type Variant = 'default' | 'primary' | 'accent' | 'success' | 'warning' | 'destructive' | 'outline' | 'gold'

interface StatusMeta {
  ar: string
  en: string
  variant: Variant
}

/**
 * Every status enum the API returns, mapped once to a label and a tone.
 * Keys are the raw API values, so a screen can pass `dto.status` straight in.
 */
const STATUS: Record<string, StatusMeta> = {
  /* Enrollment + subscription */
  Pending: { ar: 'قيد التأكيد', en: 'Pending', variant: 'warning' },
  Active: { ar: 'نشط', en: 'Active', variant: 'success' },
  Completed: { ar: 'مكتمل', en: 'Completed', variant: 'primary' },
  Cancelled: { ar: 'ملغي', en: 'Cancelled', variant: 'destructive' },
  Expired: { ar: 'منتهي', en: 'Expired', variant: 'outline' },
  Suspended: { ar: 'موقوف', en: 'Suspended', variant: 'destructive' },
  Trialing: { ar: 'فترة تجريبية', en: 'Trial', variant: 'accent' },
  PastDue: { ar: 'متأخر السداد', en: 'Past due', variant: 'destructive' },
  Inactive: { ar: 'غير نشط', en: 'Inactive', variant: 'outline' },

  /* Attendance */
  Present: { ar: 'حاضر', en: 'Present', variant: 'success' },
  Absent: { ar: 'غايب', en: 'Absent', variant: 'destructive' },
  Late: { ar: 'متأخر', en: 'Late', variant: 'warning' },
  Excused: { ar: 'بعذر', en: 'Excused', variant: 'outline' },

  /* Sessions */
  Draft: { ar: 'مسودة', en: 'Draft', variant: 'outline' },
  Open: { ar: 'مفتوحة', en: 'Open', variant: 'success' },
  Closed: { ar: 'مقفولة', en: 'Closed', variant: 'default' },

  /* Payments */
  Paid: { ar: 'مدفوع', en: 'Paid', variant: 'success' },
  Due: { ar: 'مستحق', en: 'Due', variant: 'warning' },
  Overdue: { ar: 'متأخر', en: 'Overdue', variant: 'destructive' },
  Refunded: { ar: 'مسترجع', en: 'Refunded', variant: 'outline' },
  Failed: { ar: 'فشل', en: 'Failed', variant: 'destructive' },
  Succeeded: { ar: 'تم', en: 'Succeeded', variant: 'success' },
  Processing: { ar: 'قيد التنفيذ', en: 'Processing', variant: 'warning' },

  /* Homework + exams */
  Submitted: { ar: 'اتسلّم', en: 'Submitted', variant: 'primary' },
  Graded: { ar: 'اتصحّح', en: 'Graded', variant: 'success' },
  NotSubmitted: { ar: 'لسه متسلّمش', en: 'Not submitted', variant: 'outline' },
  InProgress: { ar: 'جارية', en: 'In progress', variant: 'warning' },
  AwaitingGrading: { ar: 'مستني التصحيح', en: 'Awaiting grading', variant: 'accent' },
  Passed: { ar: 'ناجح', en: 'Passed', variant: 'success' },
  Abandoned: { ar: 'متسابة', en: 'Abandoned', variant: 'outline' },

  /* Inquiries */
  New: { ar: 'جديد', en: 'New', variant: 'primary' },
  Contacted: { ar: 'تم التواصل', en: 'Contacted', variant: 'warning' },
  Converted: { ar: 'تحوّل لاشتراك', en: 'Converted', variant: 'success' },

  /* Certificates */
  Issued: { ar: 'صادرة', en: 'Issued', variant: 'success' },
  Revoked: { ar: 'ملغاة', en: 'Revoked', variant: 'destructive' },

  /* Members */
  Invited: { ar: 'مدعو', en: 'Invited', variant: 'accent' },
  Declined: { ar: 'رفض', en: 'Declined', variant: 'outline' },
  Removed: { ar: 'اتشال', en: 'Removed', variant: 'outline' },

  /* Verification */
  Verified: { ar: 'موثّق', en: 'Verified', variant: 'success' },
  Unverified: { ar: 'غير موثّق', en: 'Unverified', variant: 'outline' },
  Rejected: { ar: 'مرفوض', en: 'Rejected', variant: 'destructive' },
  Approved: { ar: 'معتمد', en: 'Approved', variant: 'success' },
}

/**
 * Renders any API status value as a themed badge. Unknown values fall back to
 * the server's own display name so nothing renders blank.
 */
export function StatusBadge({
  status,
  fallbackLabel,
  size = 'sm',
  className,
}: {
  status?: string | null
  /** The DTO's `*Name` field, used when we have no mapping for the raw value. */
  fallbackLabel?: string | null
  size?: 'sm' | 'md'
  className?: string
}) {
  const { locale } = useI18n()
  if (!status && !fallbackLabel) return null

  const meta = status ? STATUS[status] : undefined
  const label = meta ? meta[locale === 'ar-EG' ? 'ar' : 'en'] : (fallbackLabel ?? status ?? '')

  return (
    <Badge variant={meta?.variant ?? 'outline'} size={size} className={cn('shrink-0', className)}>
      {label}
    </Badge>
  )
}

/** Same mapping, when a screen needs just the words. */
export function useStatusLabel() {
  const { locale } = useI18n()
  return (status?: string | null, fallback?: string | null) => {
    if (!status) return fallback ?? ''
    const meta = STATUS[status]
    return meta ? meta[locale === 'ar-EG' ? 'ar' : 'en'] : (fallback ?? status)
  }
}
