import * as React from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { BadgeCheck, Sparkles } from 'lucide-react'
import { isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

/**
 * The backend refuses some actions for reasons that are not the user's mistake:
 * the plan does not include a feature, or the teacher is not verified yet.
 * Those arrive as 422 + a business code, and deserve an explanation with a way
 * forward rather than a red toast.
 */
export type GateReason = 'plan' | 'verification'

const GATE_CODES: Record<string, GateReason> = {
  FEATURE_NOT_AVAILABLE: 'plan',
  PLAN_LIMIT_REACHED: 'plan',
  SUBSCRIPTION_REQUIRED: 'plan',
  TEACHER_NOT_VERIFIED: 'verification',
  TEACHER_VERIFICATION_REQUIRED: 'verification',
}

/** Reads the gate out of a thrown error, or null when it is an ordinary failure. */
export function gateReasonOf(error: unknown): GateReason | null {
  if (!isApiError(error)) return null
  const code = error.code?.toUpperCase()
  if (code && GATE_CODES[code]) return GATE_CODES[code]
  if (!error.isBusinessRule) return null

  const haystack = `${error.detail ?? ''} ${error.message}`.toLowerCase()
  if (haystack.includes('verif') || haystack.includes('توثيق') || haystack.includes('موثّق')) {
    return 'verification'
  }
  if (haystack.includes('plan') || haystack.includes('subscription') || haystack.includes('باقة')) {
    return 'plan'
  }
  return null
}

export function FeatureGateNotice({
  reason,
  /** The server's own sentence, shown under the explanation when it adds detail. */
  detail,
  upgradeHref = '/teacher/billing',
  className,
  compact = false,
}: {
  reason: GateReason
  detail?: string
  upgradeHref?: string
  className?: string
  compact?: boolean
}) {
  const { t } = useI18n()
  const isPlan = reason === 'plan'
  const Icon = isPlan ? Sparkles : BadgeCheck

  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={transitions.base}
      className={cn(
        'relative overflow-hidden rounded-2xl border border-primary/25 bg-gradient-to-br from-primary/[0.07] via-card to-card',
        compact ? 'p-5' : 'p-6 sm:p-8',
        className,
      )}
    >
      {/* Soft corner glow — the same decorative language the dashboards use. */}
      <div
        aria-hidden
        className="pointer-events-none absolute -top-16 end-[-3rem] h-48 w-48 rounded-full bg-primary/15 blur-3xl"
      />

      <div className="relative flex flex-col gap-4 sm:flex-row sm:items-start sm:gap-5">
        <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-primary/12 text-primary">
          <Icon className="h-6 w-6" />
        </span>

        <div className="min-w-0 flex-1 space-y-2">
          <h3 className="font-display text-heading-sm text-foreground">
            {isPlan ? t('gate.planTitle') : t('gate.verifyTitle')}
          </h3>
          <p className="text-body-sm text-muted-foreground">
            {isPlan ? t('gate.planDescription') : t('gate.verifyDescription')}
          </p>
          {detail && (
            <p className="rounded-lg bg-muted/50 px-3 py-2 text-caption text-muted-foreground">
              {detail}
            </p>
          )}

          {isPlan && (
            <div className="pt-1">
              <Button asChild size="sm">
                <Link to={upgradeHref}>{t('gate.planAction')}</Link>
              </Button>
            </div>
          )}
        </div>
      </div>
    </motion.div>
  )
}

/**
 * Renders the gate when the query failed because of one, and otherwise hands the
 * error back so the caller can fall through to its normal error UI.
 */
export function useFeatureGate(error: unknown): {
  gate: React.ReactNode | null
  isGated: boolean
} {
  const reason = gateReasonOf(error)
  return {
    isGated: reason !== null,
    gate: reason ? (
      <FeatureGateNotice
        reason={reason}
        detail={isApiError(error) ? error.detail : undefined}
      />
    ) : null,
  }
}
