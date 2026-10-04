import { useCallback } from 'react'
import { isApiError } from '@/api/errors'
import { gateReasonOf } from '@/components/common/FeatureGate'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'

/**
 * One place that turns a failed mutation into a toast, so no screen has to
 * decide how to phrase an API failure. Plan and verification gates get their
 * own wording — they are not errors the user made.
 */
export function useApiErrorToast() {
  const { toast } = useToast()
  const { t } = useI18n()

  return useCallback(
    (error: unknown) => {
      const gate = gateReasonOf(error)
      if (gate) {
        toast({
          title: gate === 'plan' ? t('gate.planTitle') : t('gate.verifyTitle'),
          description: gate === 'plan' ? t('gate.planDescription') : t('gate.verifyDescription'),
          variant: 'warning',
          duration: 6500,
        })
        return
      }

      toast({
        title: isApiError(error) && error.isBusinessRule ? t('gate.blockedTitle') : t('states.errorTitle'),
        description: isApiError(error) ? error.message : t('states.errorDescription'),
        variant: 'error',
      })
    },
    [toast, t],
  )
}
