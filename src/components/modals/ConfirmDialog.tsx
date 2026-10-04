import { AlertTriangle } from 'lucide-react'
import { useI18n } from '@/i18n'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'

interface ConfirmDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  confirmLabel?: string
  cancelLabel?: string
  onConfirm: () => void
  tone?: 'danger' | 'default'
  /** Keeps the dialog open and the button busy while the action runs. */
  loading?: boolean
  /** Extra context rendered between the description and the buttons. */
  children?: React.ReactNode
}

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel,
  cancelLabel,
  onConfirm,
  tone = 'danger',
  loading = false,
  children,
}: ConfirmDialogProps) {
  const { t } = useI18n()

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="sm">
        <div className="flex gap-4">
          <span
            className={cn(
              'grid size-11 shrink-0 place-items-center rounded-full',
              tone === 'danger' ? 'bg-destructive/12 text-destructive-strong' : 'bg-primary/12 text-primary',
            )}
          >
            <AlertTriangle className="size-5" />
          </span>
          <DialogHeader className="flex-1">
            <DialogTitle className="text-heading-sm">{title}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </DialogHeader>
        </div>

        {children}

        <DialogFooter>
          <Button
            variant={tone === 'danger' ? 'destructive' : 'primary'}
            loading={loading}
            onClick={() => {
              onConfirm()
              if (!loading) onOpenChange(false)
            }}
          >
            {confirmLabel ?? t('common.confirm')}
          </Button>
          <Button variant="outline" disabled={loading} onClick={() => onOpenChange(false)}>
            {cancelLabel ?? t('common.cancel')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
