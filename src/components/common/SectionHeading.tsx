import { Link } from 'react-router-dom'
import { ArrowLeft } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Reveal } from '@/components/common/Reveal'

interface SectionHeadingProps {
  eyebrow?: string
  title: React.ReactNode
  description?: React.ReactNode
  align?: 'start' | 'center'
  /** Optional "see all" link rendered opposite the title on wide screens. */
  action?: { label: string; href: string }
  className?: string
  as?: 'h2' | 'h3'
}

export function SectionHeading({
  eyebrow,
  title,
  description,
  align = 'start',
  action,
  className,
  as: Heading = 'h2',
}: SectionHeadingProps) {
  return (
    <div
      className={cn(
        'flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between',
        align === 'center' && 'sm:flex-col sm:items-center',
        className,
      )}
    >
      <Reveal className={cn('max-w-2xl', align === 'center' && 'text-center')}>
        {eyebrow && <span className="eyebrow mb-4">{eyebrow}</span>}
        <Heading className="text-display-lg text-foreground">{title}</Heading>
        {description && (
          <p className="mt-4 text-body-lg text-muted-foreground">{description}</p>
        )}
      </Reveal>

      {action && (
        <Reveal delay={0.08} className="shrink-0">
          <Link
            to={action.href}
            className="group inline-flex items-center gap-2 rounded-lg border border-border bg-card px-4 py-2.5 font-display text-body-sm font-semibold text-foreground shadow-xs transition-all duration-200 ease-premium hover:border-foreground/20 hover:shadow-soft"
          >
            {action.label}
            <ArrowLeft className="size-4 transition-transform duration-200 group-hover:-translate-x-1" />
          </Link>
        </Reveal>
      )}
    </div>
  )
}

/** Compact heading used inside dashboards and detail-page sections. */
export function PanelHeading({
  title,
  description,
  action,
  className,
  id,
}: {
  title: React.ReactNode
  description?: React.ReactNode
  action?: React.ReactNode
  className?: string
  id?: string
}) {
  return (
    <div className={cn('flex flex-wrap items-start justify-between gap-4', className)}>
      <div className="min-w-0">
        <h3 id={id} className="font-display text-heading-md text-foreground">
          {title}
        </h3>
        {description && <p className="mt-1.5 text-body-sm text-muted-foreground">{description}</p>}
      </div>
      {action}
    </div>
  )
}
