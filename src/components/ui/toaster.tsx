import { AnimatePresence, motion } from 'framer-motion'
import { AlertTriangle, CheckCircle2, Info, X, XCircle } from 'lucide-react'
import { useToast, type ToastVariant } from '@/hooks/use-toast'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

const ICONS: Record<ToastVariant, typeof Info> = {
  default: Info,
  success: CheckCircle2,
  error: XCircle,
  warning: AlertTriangle,
}

const STYLES: Record<ToastVariant, string> = {
  default: 'border-border [&_[data-icon]]:text-primary',
  success: 'border-success/30 [&_[data-icon]]:text-success-strong',
  error: 'border-destructive/30 [&_[data-icon]]:text-destructive-strong',
  warning: 'border-warning/35 [&_[data-icon]]:text-warning-strong',
}

export function Toaster() {
  const { toasts, dismiss } = useToast()

  return (
    <div
      role="region"
      aria-label="التنبيهات"
      className="pointer-events-none fixed bottom-4 start-4 z-[100] flex w-[calc(100%-2rem)] max-w-sm flex-col gap-2.5 sm:bottom-6 sm:start-6"
    >
      <AnimatePresence initial={false}>
        {toasts.map((toast) => {
          const Icon = ICONS[toast.variant]
          return (
            <motion.div
              key={toast.id}
              layout
              initial={{ opacity: 0, y: 16, scale: 0.96 }}
              animate={{ opacity: 1, y: 0, scale: 1 }}
              exit={{ opacity: 0, x: 24, scale: 0.96, transition: { duration: 0.16 } }}
              transition={transitions.spring}
              role="status"
              aria-live="polite"
              className={cn(
                'pointer-events-auto flex items-start gap-3 rounded-xl border bg-card/95 p-4 shadow-lift backdrop-blur-sm',
                STYLES[toast.variant],
              )}
            >
              <Icon data-icon className="mt-0.5 size-5 shrink-0" />
              <div className="min-w-0 flex-1">
                <p className="font-display text-body-sm font-semibold text-foreground">{toast.title}</p>
                {toast.description && (
                  <p className="mt-0.5 text-caption leading-relaxed text-muted-foreground">
                    {toast.description}
                  </p>
                )}
              </div>
              <button
                type="button"
                onClick={() => dismiss(toast.id)}
                className="shrink-0 rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
                aria-label="إغلاق التنبيه"
              >
                <X className="size-4" />
              </button>
            </motion.div>
          )
        })}
      </AnimatePresence>
    </div>
  )
}
