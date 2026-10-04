import { motion } from 'framer-motion'
import { CalendarDays, Check, MessageCircle } from 'lucide-react'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Spot } from '@/components/illustrations/Spot'
import { GoldRule } from '@/components/illustrations/CoverArt'
import { WhatsAppButton } from '@/components/whatsapp/WhatsAppButton'
import type { WhatsAppOptions } from '@/lib/whatsapp'
import { formatEGP } from '@/lib/utils'

export interface SubscriptionSummary {
  groupName: string
  teacherName: string
  studentName: string
  schedule: string
  original: number
  final: number
  discountLabel?: string
  reference: string
}

interface SuccessModalProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  summary: SubscriptionSummary
  whatsapp: WhatsAppOptions
}

/**
 * Shown after a successful (frontend-only) subscription submit. It repeats the
 * exact numbers the student just agreed to, then hands them to WhatsApp — which
 * is where the real conversation continues.
 */
export function SuccessModal({ open, onOpenChange, summary, whatsapp }: SuccessModalProps) {
  const saved = summary.original - summary.final

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="md" className="gap-0 p-0">
        <div className="relative overflow-hidden rounded-t-2xl bg-primary px-6 pb-8 pt-10 text-center">
          <div className="pointer-events-none absolute inset-0 bg-dot-grid opacity-15" aria-hidden />

          <motion.div
            initial={{ scale: 0.7, opacity: 0 }}
            animate={{ scale: 1, opacity: 1 }}
            transition={{ type: 'spring', stiffness: 320, damping: 20, delay: 0.06 }}
            className="relative mx-auto grid size-16 place-items-center rounded-full bg-primary-foreground/15 ring-4 ring-primary-foreground/10"
          >
            <Check className="size-8 text-primary-foreground" strokeWidth={3} />
          </motion.div>

          <DialogHeader className="relative mt-5 items-center text-center">
            <DialogTitle className="text-primary-foreground">تم تسجيل طلبك ✅</DialogTitle>
            <DialogDescription className="text-primary-foreground/75">
              حجزنا مكانك مبدئيًا في «{summary.groupName}». باقي خطوة واحدة بس.
            </DialogDescription>
          </DialogHeader>
        </div>

        <div className="space-y-5 p-6">
          <dl className="space-y-3 rounded-xl border border-border bg-muted/40 p-4 text-body-sm">
            <div className="flex items-start justify-between gap-4">
              <dt className="text-muted-foreground">الطالب</dt>
              <dd className="font-semibold text-foreground">{summary.studentName}</dd>
            </div>
            <div className="flex items-start justify-between gap-4">
              <dt className="text-muted-foreground">المدرّس</dt>
              <dd className="font-semibold text-foreground">أ. {summary.teacherName}</dd>
            </div>
            <div className="flex items-start justify-between gap-4">
              <dt className="text-muted-foreground">الميعاد</dt>
              <dd className="tnum text-end font-semibold text-foreground">{summary.schedule}</dd>
            </div>

            <GoldRule />

            {saved > 0 && (
              <>
                <div className="flex items-center justify-between gap-4">
                  <dt className="text-muted-foreground">السعر الأصلي</dt>
                  <dd className="tnum text-muted-foreground line-through">
                    {formatEGP(summary.original)}
                  </dd>
                </div>
                <div className="flex items-center justify-between gap-4">
                  <dt className="text-muted-foreground">الخصم</dt>
                  <dd className="flex items-center gap-2">
                    {summary.discountLabel && (
                      <Badge variant="accent" size="sm">
                        {summary.discountLabel}
                      </Badge>
                    )}
                    <span className="tnum font-semibold text-success-strong">−{formatEGP(saved)}</span>
                  </dd>
                </div>
              </>
            )}

            <div className="flex items-center justify-between gap-4 border-t border-border pt-3">
              <dt className="font-display font-bold text-foreground">الإجمالي شهريًا</dt>
              <dd className="tnum font-display text-heading-sm font-extrabold text-foreground">
                {formatEGP(summary.final)}
              </dd>
            </div>
          </dl>

          <div className="rounded-xl border border-whatsapp/25 bg-whatsapp/8 p-4">
            <p className="flex items-center gap-2 font-display text-body-sm font-bold text-foreground">
              <MessageCircle className="size-4 text-whatsapp" aria-hidden />
              الخطوة الأخيرة
            </p>
            <p className="mt-1.5 text-caption leading-relaxed text-muted-foreground">
              ابعت رسالة للمدرّس على واتساب عشان يأكّد مكانك ويتفق معاك على طريقة الدفع. الرسالة
              جاهزة بكل التفاصيل.
            </p>
          </div>

          <div className="flex flex-col gap-2.5 sm:flex-row">
            <WhatsAppButton {...whatsapp} size="lg" className="flex-1">
              كمّل على واتساب
            </WhatsAppButton>
            <Button variant="outline" size="lg" onClick={() => onOpenChange(false)}>
              <CalendarDays className="size-4" />
              تمام
            </Button>
          </div>

          <p className="tnum text-center text-caption text-muted-foreground">
            رقم الطلب: <span className="font-semibold text-foreground">{summary.reference}</span>
          </p>
        </div>
      </DialogContent>
    </Dialog>
  )
}

/** Generic success dialog used by auth and contact flows. */
export function SimpleSuccessModal({
  open,
  onOpenChange,
  title,
  description,
  actionLabel = 'تمام',
  onAction,
  children,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  title: string
  description: string
  actionLabel?: string
  onAction?: () => void
  children?: React.ReactNode
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent size="sm" className="text-center">
        <Spot name="success" className="mx-auto h-28" />
        <DialogHeader className="items-center text-center">
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        {children}
        <Button
          block
          size="lg"
          onClick={() => {
            onAction?.()
            onOpenChange(false)
          }}
        >
          {actionLabel}
        </Button>
      </DialogContent>
    </Dialog>
  )
}
