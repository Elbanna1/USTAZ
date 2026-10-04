import * as React from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { AlertTriangle, Lock, SearchX, ServerCrash, WifiOff } from 'lucide-react'
import type { UseQueryResult } from '@tanstack/react-query'
import { ApiError, isApiError } from '@/api/errors'
import { Button } from '@/components/ui/button'
import { EmptyState } from '@/components/common/states'
import { FeatureGateNotice, gateReasonOf } from '@/components/common/FeatureGate'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

/* -------------------------------------------------------------------------- */
/*                              Error presentation                            */
/* -------------------------------------------------------------------------- */

const ERROR_ICONS = {
  network: WifiOff,
  timeout: WifiOff,
  forbidden: Lock,
  unauthorized: Lock,
  notFound: SearchX,
  server: ServerCrash,
  default: AlertTriangle,
} as const

/** Turns any thrown value into a title/description pair the user can act on. */
export function useErrorCopy() {
  const { t } = useI18n()
  return React.useCallback(
    (error: unknown): { title: string; description: string; icon: React.ElementType } => {
      if (isApiError(error)) {
        switch (error.kind) {
          case 'network':
          case 'timeout':
            return {
              title: t('states.networkTitle'),
              description: t('states.networkDescription'),
              icon: ERROR_ICONS.network,
            }
          case 'forbidden':
          case 'unauthorized':
            return {
              title: t('states.forbiddenTitle'),
              description: t('states.forbiddenDescription'),
              icon: ERROR_ICONS.forbidden,
            }
          case 'notFound':
            return {
              title: t('states.notFoundTitle'),
              description: t('states.notFoundDescription'),
              icon: ERROR_ICONS.notFound,
            }
          default:
            return {
              title: t('states.errorTitle'),
              description: error.message,
              icon: ERROR_ICONS.default,
            }
        }
      }
      return {
        title: t('states.errorTitle'),
        description: t('states.errorDescription'),
        icon: ERROR_ICONS.default,
      }
    },
    [t],
  )
}

export function QueryError({
  error,
  onRetry,
  className,
  compact = false,
}: {
  error: unknown
  onRetry?: () => void
  className?: string
  compact?: boolean
}) {
  const { t } = useI18n()
  const copy = useErrorCopy()(error)
  const Icon = copy.icon
  const retryable = !isApiError(error) || error.isRetryable

  // A plan limit or a pending verification is not a failure — explain it instead.
  const gate = gateReasonOf(error)
  if (gate) {
    return (
      <FeatureGateNotice
        reason={gate}
        detail={isApiError(error) ? error.detail : undefined}
        className={className}
        compact={compact}
      />
    )
  }

  return (
    <motion.div
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={transitions.base}
      role="alert"
      className={cn(
        'flex flex-col items-center justify-center gap-4 rounded-2xl border border-destructive/25 bg-destructive/5 text-center',
        compact ? 'px-5 py-8' : 'px-6 py-14',
        className,
      )}
    >
      <span className="grid size-12 place-items-center rounded-2xl bg-destructive/12 text-destructive-strong">
        <Icon className="size-6" />
      </span>
      <div className="max-w-md space-y-1.5">
        <h3 className="font-display text-heading-sm text-foreground">{copy.title}</h3>
        <p className="text-body-sm text-muted-foreground">{copy.description}</p>
        {isApiError(error) && error.traceId && (
          <p className="pt-1 font-latin text-[0.6875rem] text-muted-foreground/70">
            ref: {error.traceId}
          </p>
        )}
      </div>
      {onRetry && retryable && (
        <Button variant="outline" size="sm" onClick={onRetry}>
          {t('common.retry')}
        </Button>
      )}
    </motion.div>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Boundary                                  */
/* -------------------------------------------------------------------------- */

interface QueryBoundaryProps<T> {
  query: UseQueryResult<T>
  /** Skeleton shown while the first page of data loads. */
  skeleton: React.ReactNode
  /** Rendered when the query resolves to nothing worth showing. */
  empty?: React.ReactNode
  /** Decides whether the resolved data counts as empty. */
  isEmpty?: (data: T) => boolean
  children: (data: T) => React.ReactNode
  className?: string
}

/**
 * The four states — loading, error, empty, success — in one place, so no screen
 * ever shows a blank panel while data is in flight.
 */
export function QueryBoundary<T>({
  query,
  skeleton,
  empty,
  isEmpty,
  children,
  className,
}: QueryBoundaryProps<T>) {
  const phase = query.isPending
    ? 'loading'
    : query.isError
      ? 'error'
      : isEmpty && query.data !== undefined && isEmpty(query.data)
        ? 'empty'
        : 'ready'

  return (
    <div className={className}>
      <AnimatePresence mode="wait" initial={false}>
        <motion.div
          key={phase}
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: 0.2 }}
        >
          {phase === 'loading' && skeleton}
          {phase === 'error' && <QueryError error={query.error} onRetry={() => query.refetch()} />}
          {phase === 'empty' && (empty ?? <DefaultEmpty />)}
          {phase === 'ready' && children(query.data as T)}
        </motion.div>
      </AnimatePresence>
    </div>
  )
}

function DefaultEmpty() {
  const { t } = useI18n()
  return <EmptyState title={t('states.emptyTitle')} description={t('states.emptyDescription')} />
}

/** Re-exported so screens can type-narrow on the error without importing from `@/api`. */
export { ApiError }
