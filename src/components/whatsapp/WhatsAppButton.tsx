import * as React from 'react'
import { AnimatePresence, motion } from 'framer-motion'
import { MessageCircle, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { buildWhatsAppUrl, type WhatsAppOptions } from '@/lib/whatsapp'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

/** WhatsApp glyph — drawn here because the icon set doesn't carry brand marks. */
export function WhatsAppIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" className={cn('size-5', className)} fill="currentColor" aria-hidden focusable="false">
      <path d="M12.04 2C6.58 2 2.13 6.45 2.13 11.91c0 1.75.46 3.45 1.32 4.95L2 22l5.25-1.38a9.87 9.87 0 0 0 4.79 1.22h.01c5.46 0 9.91-4.45 9.91-9.91S17.5 2 12.04 2zm0 18.16h-.01a8.2 8.2 0 0 1-4.18-1.15l-.3-.18-3.12.82.83-3.04-.2-.31a8.19 8.19 0 0 1-1.26-4.39c0-4.54 3.7-8.24 8.25-8.24 2.2 0 4.27.86 5.83 2.42a8.19 8.19 0 0 1 2.41 5.83c0 4.54-3.7 8.24-8.25 8.24zm4.52-6.17c-.25-.13-1.47-.72-1.69-.81-.23-.08-.39-.12-.56.13-.16.24-.64.8-.78.97-.15.16-.29.18-.54.06-.25-.13-1.05-.39-1.99-1.23-.74-.66-1.23-1.47-1.38-1.72-.14-.25-.01-.38.11-.5.11-.11.25-.29.37-.43.13-.15.17-.25.25-.41.08-.17.04-.31-.02-.43-.06-.13-.56-1.35-.77-1.84-.2-.49-.4-.42-.56-.43h-.47c-.17 0-.43.06-.66.31-.22.25-.87.85-.87 2.07s.89 2.4 1.02 2.57c.13.16 1.76 2.68 4.26 3.76.59.26 1.06.41 1.42.52.6.19 1.14.16 1.57.1.48-.07 1.47-.6 1.68-1.18.21-.58.21-1.07.15-1.18-.06-.11-.23-.17-.48-.29z" />
    </svg>
  )
}

interface WhatsAppButtonProps extends Partial<WhatsAppOptions> {
  children?: React.ReactNode
  className?: string
  variant?: 'whatsapp' | 'whatsapp-outline' | 'outline' | 'ghost' | 'secondary'
  size?: React.ComponentProps<typeof Button>['size']
  block?: boolean
  /** A ready-made link from the API — used instead of building one from a phone. */
  href?: string
}

/**
 * The single WhatsApp entry point used everywhere in the product.
 * Builds a safe wa.me deep link with a pre-written Arabic message for the intent.
 */
export function WhatsAppButton({
  children,
  className,
  variant = 'whatsapp',
  size = 'md',
  block,
  href: explicitHref,
  ...options
}: WhatsAppButtonProps) {
  const href =
    explicitHref ??
    buildWhatsAppUrl({ phone: options.phone ?? '', intent: options.intent ?? 'send-inquiry', ...options })

  return (
    <Button asChild variant={variant} size={size} block={block} className={className}>
      <a href={href} target="_blank" rel="noopener noreferrer">
        <WhatsAppIcon className={size === 'sm' ? 'size-4' : 'size-5'} />
        {children ?? 'كلّمنا على واتساب'}
      </a>
    </Button>
  )
}

/** Icon-only variant for dense card footers. */
export function WhatsAppIconButton({
  className,
  label = 'كلّمه على واتساب',
  href: explicitHref,
  ...options
}: Partial<WhatsAppOptions> & { className?: string; label?: string; href?: string }) {
  const href =
    explicitHref ??
    buildWhatsAppUrl({ phone: options.phone ?? '', intent: options.intent ?? 'send-inquiry', ...options })
  return (
    <a
      href={href}
      target="_blank"
      rel="noopener noreferrer"
      aria-label={label}
      title={label}
      className={cn(
        'grid size-11 shrink-0 place-items-center rounded-lg border border-whatsapp/35 bg-whatsapp/10 text-whatsapp-ink dark:text-whatsapp transition-all duration-200 ease-premium',
        'hover:bg-whatsapp hover:text-white hover:shadow-soft',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-whatsapp focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        'dark:text-whatsapp dark:hover:text-white',
        className,
      )}
    >
      <WhatsAppIcon />
    </a>
  )
}

/* -------------------------------------------------------------------------- */
/*                            Floating action button                          */
/* -------------------------------------------------------------------------- */

interface FloatingWhatsAppProps {
  phone: string
  /** Name shown in the expanded card, e.g. the teacher being viewed. */
  contactName?: string
  contactRole?: string
  options?: { label: string; intent: WhatsAppOptions['intent']; note?: string }[]
  context?: WhatsAppOptions['context']
}

/**
 * Persistent WhatsApp affordance. Collapsed it is a single circular button;
 * expanded it offers the two or three intents that make sense on the page —
 * deliberately restrained so it reads as helpful, not as a spam widget.
 */
export function FloatingWhatsApp({
  phone,
  contactName,
  contactRole,
  options,
  context,
}: FloatingWhatsAppProps) {
  const [open, setOpen] = React.useState(false)

  const intents = options ?? [
    { label: 'عندي استفسار', intent: 'send-inquiry' as const },
    { label: 'عايز أحجز حصة تجريبية', intent: 'book-class' as const },
  ]

  return (
    <div className="pointer-events-none fixed bottom-4 end-4 z-40 flex flex-col items-end gap-3 sm:bottom-6 sm:end-6">
      <AnimatePresence>
        {open && (
          <motion.div
            initial={{ opacity: 0, y: 12, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: 8, scale: 0.97, transition: { duration: 0.15 } }}
            transition={transitions.spring}
            className="pointer-events-auto w-[min(20rem,calc(100vw-2rem))] overflow-hidden rounded-2xl border border-border bg-card shadow-lift"
            role="dialog"
            aria-label="تواصل على واتساب"
          >
            <div className="flex items-start gap-3 bg-whatsapp-ink p-4 text-white">
              <span className="grid size-10 shrink-0 place-items-center rounded-full bg-white/15">
                <WhatsAppIcon />
              </span>
              <div className="min-w-0 flex-1">
                <p className="font-display text-body-sm font-bold">
                  {contactName ?? 'فريق أستاذ'}
                </p>
                <p className="text-caption text-white/70">
                  {contactRole ?? 'بنرد عادةً خلال ساعة'}
                </p>
              </div>
              <button
                type="button"
                onClick={() => setOpen(false)}
                className="rounded-md p-1 text-white/70 transition-colors hover:bg-white/15 hover:text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white/60"
                aria-label="إغلاق"
              >
                <X className="size-4" />
              </button>
            </div>

            <div className="space-y-2 p-4">
              <p className="text-caption text-muted-foreground">اختار اللي يناسبك وهنكمّل على واتساب:</p>
              {intents.map((o) => (
                <a
                  key={o.label}
                  href={buildWhatsAppUrl({
                    phone,
                    intent: o.intent,
                    context: { ...context, note: o.note },
                  })}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="flex items-center justify-between gap-3 rounded-lg border border-border bg-surface px-3.5 py-2.5 text-body-sm font-medium text-foreground transition-colors hover:border-whatsapp/40 hover:bg-whatsapp/8"
                >
                  {o.label}
                  <MessageCircle className="size-4 shrink-0 text-whatsapp" aria-hidden />
                </a>
              ))}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-label={open ? 'إغلاق قائمة واتساب' : 'تواصل على واتساب'}
        className={cn(
          'pointer-events-auto relative grid size-14 place-items-center rounded-full bg-whatsapp text-white shadow-lift transition-transform duration-200 ease-premium',
          'hover:scale-105 active:scale-95',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-whatsapp focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        )}
      >
        {!open && (
          <span
            className="absolute inset-0 animate-pulse-ring rounded-full bg-whatsapp/45"
            aria-hidden
          />
        )}
        <span className="relative">
          {open ? <X className="size-6" /> : <WhatsAppIcon className="size-7" />}
        </span>
      </button>
    </div>
  )
}
