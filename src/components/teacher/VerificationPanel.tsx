import * as React from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AnimatePresence, motion } from 'framer-motion'
import { BadgeCheck, Check, Clock, Eye, FileWarning, Send } from 'lucide-react'
import { Link } from 'react-router-dom'
import {
  teacherVerificationApi,
  type TeacherVerification,
  type TeacherVerificationStatus,
} from '@/api/endpoints/teacher'
import { qk } from '@/lib/query-client'
import { useApiErrorToast } from '@/hooks/use-api-toast'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { QueryError } from '@/components/common/QueryBoundary'
import { transitions } from '@/lib/motion'
import { cn } from '@/lib/utils'

/* -------------------------------------------------------------------------- */
/*                                   Copy                                     */
/* -------------------------------------------------------------------------- */

type Bi = { ar: string; en: string }

/** What each requirement key means, and where the teacher fixes it. */
const REQUIREMENTS: Record<string, { label: Bi; href: string }> = {
  headline: {
    label: { ar: 'سطر تعريفي يوضّح مادتك', en: 'A headline naming what you teach' },
    href: '/teacher/settings',
  },
  bio: {
    label: { ar: 'نبذة عن خبرتك وطريقتك', en: 'A bio covering your experience and approach' },
    href: '/teacher/settings',
  },
  whatsAppNumber: {
    label: { ar: 'رقم واتساب للتواصل', en: 'A WhatsApp number students can reach' },
    href: '/teacher/settings',
  },
  subjects: {
    label: { ar: 'مادة واحدة على الأقل', en: 'At least one subject' },
    href: '/teacher/settings',
  },
  grades: {
    label: { ar: 'صف دراسي واحد على الأقل', en: 'At least one grade' },
    href: '/teacher/settings',
  },
}

const STATUS_COPY: Record<
  TeacherVerificationStatus,
  { title: Bi; body: Bi; tone: 'neutral' | 'info' | 'success' | 'warning'; icon: React.ElementType }
> = {
  NotSubmitted: {
    title: { ar: 'وثّق حسابك عشان تنشر', en: 'Get verified to publish' },
    body: {
      ar: 'التوثيق بيخلي بروفايلك يظهر في دليل المدرّسين، وبيسمحلك تنشر مجموعاتك وكورساتك.',
      en: 'Verification puts your profile in the teacher directory and lets you publish groups and courses.',
    },
    tone: 'neutral',
    icon: BadgeCheck,
  },
  Pending: {
    title: { ar: 'طلبك في الطابور', en: 'Your request is queued' },
    body: {
      ar: 'استلمنا طلبك. هنراجع بياناتك ونبعتلك إشعار أول ما يتقرر.',
      en: 'We have your request. We will review your details and notify you as soon as it is decided.',
    },
    tone: 'info',
    icon: Clock,
  },
  UnderReview: {
    title: { ar: 'فريق المراجعة بيبص على بياناتك', en: 'A reviewer is looking at your profile' },
    body: {
      ar: 'حد من الفريق فاتح بياناتك دلوقتي. عادةً بنخلص في يوم عمل.',
      en: 'Someone on the team has your details open. This usually takes a working day.',
    },
    tone: 'info',
    icon: Eye,
  },
  Verified: {
    title: { ar: 'حسابك موثّق', en: 'Your account is verified' },
    body: {
      ar: 'تقدر تنشر مجموعاتك وكورساتك، وبروفايلك ظاهر في دليل المدرّسين.',
      en: 'You can publish groups and courses, and your profile appears in the teacher directory.',
    },
    tone: 'success',
    icon: BadgeCheck,
  },
  Rejected: {
    title: { ar: 'الطلب محتاج تعديل', en: 'Your request needs changes' },
    body: {
      ar: 'راجعنا البيانات وفيه حاجة محتاجة تتظبط. عدّلها وابعت الطلب تاني.',
      en: 'We reviewed your details and something needs fixing. Update it and submit again.',
    },
    tone: 'warning',
    icon: FileWarning,
  },
}

const TONES = {
  neutral: 'border-border bg-card',
  info: 'border-info/25 bg-info/[0.06]',
  success: 'border-success/25 bg-success/[0.06]',
  warning: 'border-warning/30 bg-warning/[0.07]',
} as const

const ICON_TONES = {
  neutral: 'bg-primary/12 text-primary',
  info: 'bg-info/15 text-info',
  success: 'bg-success/15 text-success-strong',
  warning: 'bg-warning/15 text-warning-strong',
} as const

/* -------------------------------------------------------------------------- */
/*                                  Panel                                     */
/* -------------------------------------------------------------------------- */

/**
 * The teacher's own view of verification: where the request stands, what is still
 * missing, and the one action that moves it forward. Every state comes from the
 * backend — nothing here decides whether a teacher is verified.
 */
export function VerificationPanel({
  className,
  /** Hides itself entirely once verified, for placements like the dashboard. */
  hideWhenVerified = false,
}: {
  className?: string
  hideWhenVerified?: boolean
}) {
  const { locale, formatDate } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const isAr = lang === 'ar'

  const queryClient = useQueryClient()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()

  const verification = useQuery({
    queryKey: qk.teacher.verification,
    queryFn: teacherVerificationApi.get,
  })

  const submit = useMutation({
    mutationFn: teacherVerificationApi.submit,
    onSuccess: (next: TeacherVerification) => {
      queryClient.setQueryData(qk.teacher.verification, next)
      void queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      toast({
        title: isAr ? 'بعتنا طلب التوثيق' : 'Verification request sent',
        description: isAr
          ? 'هنراجع بياناتك ونبعتلك إشعار أول ما يتقرر.'
          : 'We will review your details and notify you once it is decided.',
        variant: 'success',
      })
    },
    onError: showApiError,
  })

  if (verification.isPending) {
    return (
      <Card className={cn('space-y-4 p-6', className)}>
        <div className="flex items-center gap-3">
          <Skeleton className="size-11 rounded-xl" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-4 w-44" />
            <Skeleton className="h-3 w-64" />
          </div>
        </div>
        <Skeleton className="h-9 w-40" />
      </Card>
    )
  }

  if (verification.isError) {
    return (
      <QueryError
        error={verification.error}
        onRetry={() => verification.refetch()}
        className={className}
        compact
      />
    )
  }

  const data = verification.data
  if (!data) return null
  if (hideWhenVerified && data.isVerified) return null

  const copy = STATUS_COPY[data.statusName] ?? STATUS_COPY.NotSubmitted
  const Icon = copy.icon
  const outstanding = data.requirements.filter((requirement) => !requirement.isMet)
  const met = data.requirements.length - outstanding.length

  return (
    <Card className={cn('overflow-hidden p-0', TONES[copy.tone], className)}>
      <div className="flex flex-col gap-4 p-6 sm:flex-row sm:items-start sm:gap-5">
        <span
          className={cn(
            'grid size-11 shrink-0 place-items-center rounded-xl',
            ICON_TONES[copy.tone],
          )}
        >
          <Icon className="size-5" />
        </span>

        <div className="min-w-0 flex-1 space-y-2">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="font-display text-heading-sm text-foreground">{copy.title[lang]}</h3>
            {data.statusName === 'Verified' && (
              <Badge variant="success" size="sm">
                {isAr ? 'موثّق' : 'Verified'}
              </Badge>
            )}
          </div>

          <p className="text-body-sm leading-relaxed text-muted-foreground">{copy.body[lang]}</p>

          {/* The reviewer's own words, when they rejected the request. */}
          {data.statusName === 'Rejected' && data.notes && (
            <p className="rounded-lg border border-warning/25 bg-warning/[0.08] px-3 py-2 text-body-sm text-foreground">
              <span className="font-semibold">{isAr ? 'سبب الرفض: ' : 'Reason: '}</span>
              {data.notes}
            </p>
          )}

          {data.submittedAt && data.statusName !== 'Verified' && (
            <p className="text-caption text-muted-foreground">
              {isAr ? 'اتبعت في ' : 'Submitted '}
              {formatDate(data.submittedAt)}
            </p>
          )}

          {/* The checklist only matters while the teacher can still act on it. */}
          <AnimatePresence initial={false}>
            {!data.isVerified && !data.isUnderReview && (
              <motion.div
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto' }}
                exit={{ opacity: 0, height: 0 }}
                transition={transitions.base}
                className="overflow-hidden"
              >
                <div className="pt-2">
                  <p className="text-caption font-semibold text-muted-foreground">
                    {isAr
                      ? `اكتملت ${met} من ${data.requirements.length}`
                      : `${met} of ${data.requirements.length} complete`}
                  </p>

                  <ul className="mt-3 space-y-2">
                    {data.requirements.map((requirement) => {
                      const detail = REQUIREMENTS[requirement.key]
                      const label = detail?.label[lang] ?? requirement.key
                      return (
                        <li
                          key={requirement.key}
                          className="flex items-center gap-2.5 text-body-sm"
                        >
                          <span
                            className={cn(
                              'grid size-4 shrink-0 place-items-center rounded-full',
                              requirement.isMet
                                ? 'bg-success/15 text-success-strong'
                                : 'border border-dashed border-muted-foreground/40',
                            )}
                          >
                            {requirement.isMet && <Check className="size-2.5" strokeWidth={3.5} />}
                          </span>
                          <span
                            className={cn(
                              requirement.isMet
                                ? 'text-muted-foreground line-through decoration-muted-foreground/40'
                                : 'text-foreground',
                            )}
                          >
                            {label}
                          </span>
                        </li>
                      )
                    })}
                  </ul>
                </div>
              </motion.div>
            )}
          </AnimatePresence>

          {!data.isVerified && (
            <div className="flex flex-wrap items-center gap-2.5 pt-2">
              <Button
                size="sm"
                loading={submit.isPending}
                disabled={!data.canSubmit || submit.isPending}
                onClick={() => submit.mutate()}
              >
                <Send className="size-4" />
                {data.statusName === 'Rejected'
                  ? isAr
                    ? 'ابعت الطلب تاني'
                    : 'Submit again'
                  : isAr
                    ? 'اطلب التوثيق'
                    : 'Request verification'}
              </Button>

              {!data.isProfileComplete && (
                <Button asChild size="sm" variant="outline">
                  <Link to="/teacher/settings">{isAr ? 'كمّل بياناتك' : 'Complete your profile'}</Link>
                </Button>
              )}
            </div>
          )}
        </div>
      </div>
    </Card>
  )
}
