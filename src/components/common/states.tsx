import { Link } from 'react-router-dom'
import { Loader2, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Spot, type SpotName } from '@/components/illustrations/Spot'
import { cn } from '@/lib/utils'

interface EmptyStateProps {
  spot?: SpotName
  title: string
  description?: string
  action?: { label: string; href?: string; onClick?: () => void }
  secondaryAction?: { label: string; href?: string; onClick?: () => void }
  className?: string
  compact?: boolean
}

export function EmptyState({
  spot = 'search',
  title,
  description,
  action,
  secondaryAction,
  className,
  compact = false,
}: EmptyStateProps) {
  return (
    <div
      className={cn(
        'flex flex-col items-center justify-center rounded-2xl border border-dashed border-border bg-muted/25 text-center',
        compact ? 'gap-3 px-6 py-10' : 'gap-4 px-6 py-16',
        className,
      )}
    >
      <Spot name={spot} className={compact ? 'h-24' : 'h-32'} />
      <div className="max-w-md space-y-2">
        <h3 className="font-display text-heading-sm text-foreground">{title}</h3>
        {description && <p className="text-body-sm text-muted-foreground">{description}</p>}
      </div>

      {(action || secondaryAction) && (
        <div className="mt-1 flex flex-wrap items-center justify-center gap-2.5">
          {action &&
            (action.href ? (
              <Button asChild>
                <Link to={action.href}>{action.label}</Link>
              </Button>
            ) : (
              <Button onClick={action.onClick}>{action.label}</Button>
            ))}
          {secondaryAction &&
            (secondaryAction.href ? (
              <Button variant="outline" asChild>
                <Link to={secondaryAction.href}>{secondaryAction.label}</Link>
              </Button>
            ) : (
              <Button variant="outline" onClick={secondaryAction.onClick}>
                {secondaryAction.label}
              </Button>
            ))}
        </div>
      )}
    </div>
  )
}

export function ErrorState({
  title = 'حصل خطأ غير متوقع',
  description = 'حاول تاني، ولو المشكلة فضلت موجودة كلّمنا وهنساعدك.',
  onRetry,
  className,
}: {
  title?: string
  description?: string
  onRetry?: () => void
  className?: string
}) {
  return (
    <div
      role="alert"
      className={cn(
        'flex flex-col items-center justify-center gap-4 rounded-2xl border border-destructive/25 bg-destructive/5 px-6 py-14 text-center',
        className,
      )}
    >
      <Spot name="error" className="h-28" />
      <div className="max-w-md space-y-2">
        <h3 className="font-display text-heading-sm text-foreground">{title}</h3>
        <p className="text-body-sm text-muted-foreground">{description}</p>
      </div>
      {onRetry && (
        <Button variant="outline" onClick={onRetry}>
          <RotateCcw className="size-4" />
          حاول تاني
        </Button>
      )}
    </div>
  )
}

export function LoadingState({
  label = 'بنحمّل البيانات…',
  className,
}: {
  label?: string
  className?: string
}) {
  return (
    <div
      role="status"
      aria-live="polite"
      className={cn('flex flex-col items-center justify-center gap-3 py-16 text-center', className)}
    >
      <Loader2 className="size-7 animate-spin text-primary" aria-hidden />
      <p className="text-body-sm text-muted-foreground">{label}</p>
    </div>
  )
}

/** Inline banner used for non-blocking notices inside pages. */
export function NoticeBanner({
  tone = 'info',
  title,
  children,
  className,
  icon,
}: {
  tone?: 'info' | 'success' | 'warning' | 'danger'
  title?: string
  children: React.ReactNode
  className?: string
  icon?: React.ReactNode
}) {
  const tones = {
    info: 'border-primary/20 bg-primary/6 text-foreground [&_[data-i]]:text-primary',
    success: 'border-success/25 bg-success/8 text-foreground [&_[data-i]]:text-success-strong',
    warning: 'border-warning/30 bg-warning/8 text-foreground [&_[data-i]]:text-warning-strong',
    danger: 'border-destructive/25 bg-destructive/8 text-foreground [&_[data-i]]:text-destructive-strong',
  } as const

  return (
    <div className={cn('flex gap-3 rounded-xl border p-4', tones[tone], className)}>
      {icon && (
        <span data-i className="mt-0.5 shrink-0 [&_svg]:size-5">
          {icon}
        </span>
      )}
      <div className="min-w-0 flex-1 text-body-sm leading-relaxed">
        {title && <p className="font-display font-semibold text-foreground">{title}</p>}
        <div className={cn(title && 'mt-1', 'text-muted-foreground')}>{children}</div>
      </div>
    </div>
  )
}
