import * as React from 'react'
import { AlertCircle } from 'lucide-react'
import { motion } from 'framer-motion'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { useI18n } from '@/i18n'
import { transitions } from '@/lib/motion'

interface FormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description?: string
  /** Top-of-form error that could not be attached to a field. */
  error?: string | null
  submitting?: boolean
  submitLabel?: string
  /** Blocks submission while the form is incomplete, without hiding the button. */
  submitDisabled?: boolean
  onSubmit: (event: React.FormEvent<HTMLFormElement>) => void
  size?: 'sm' | 'md' | 'lg' | 'xl'
  children: React.ReactNode
}

/**
 * The shape every create/edit dialog in the product shares: a scrolling body,
 * a sticky footer, and one place for the server error the fields could not take.
 */
export function FormDialog({
  open,
  onOpenChange,
  title,
  description,
  error,
  submitting = false,
  submitLabel,
  submitDisabled = false,
  onSubmit,
  size = 'lg',
  children,
}: FormDialogProps) {
  const { t } = useI18n()

  return (
    <Dialog open={open} onOpenChange={(next) => !submitting && onOpenChange(next)}>
      <DialogContent size={size} className="max-h-[92dvh] overflow-hidden p-0">
        <form onSubmit={onSubmit} className="flex max-h-[92dvh] flex-col" noValidate>
          <DialogHeader className="border-b border-border p-6 pb-4">
            <DialogTitle>{title}</DialogTitle>
            {description && <DialogDescription>{description}</DialogDescription>}
          </DialogHeader>

          <div className="flex-1 space-y-5 overflow-y-auto p-6">
            {error && (
              <motion.div
                initial={{ opacity: 0, y: -6 }}
                animate={{ opacity: 1, y: 0 }}
                transition={transitions.fast}
                role="alert"
                className="flex items-start gap-2.5 rounded-lg border border-destructive/30 bg-destructive/8 p-3.5 text-body-sm text-destructive-strong"
              >
                <AlertCircle className="mt-0.5 size-4 shrink-0" />
                {error}
              </motion.div>
            )}
            {children}
          </div>

          <DialogFooter className="border-t border-border p-4">
            <Button type="submit" loading={submitting} disabled={submitDisabled || submitting}>
              {submitLabel ?? t('common.save')}
            </Button>
            <Button
              type="button"
              variant="outline"
              disabled={submitting}
              onClick={() => onOpenChange(false)}
            >
              {t('common.cancel')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
