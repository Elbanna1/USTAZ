import { motion } from 'framer-motion'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

interface DashboardHeaderProps {
  title: string
  description?: string
  actions?: React.ReactNode
  className?: string
  children?: React.ReactNode
}

export function DashboardHeader({
  title,
  description,
  actions,
  className,
  children,
}: DashboardHeaderProps) {
  return (
    <motion.header
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={transitions.base}
      className={cn('mb-7', className)}
    >
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-display-lg text-foreground">{title}</h1>
          {description && (
            <p className="mt-2 max-w-2xl text-body text-muted-foreground">{description}</p>
          )}
        </div>
        {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
      </div>
      {children && <div className="mt-6">{children}</div>}
    </motion.header>
  )
}

/** Section wrapper inside a dashboard page. */
export function DashboardSection({
  title,
  description,
  action,
  children,
  className,
}: {
  title: string
  description?: string
  action?: React.ReactNode
  children: React.ReactNode
  className?: string
}) {
  return (
    <section className={cn('space-y-5', className)}>
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <h2 className="font-display text-heading-md text-foreground">{title}</h2>
          {description && <p className="mt-1 text-body-sm text-muted-foreground">{description}</p>}
        </div>
        {action}
      </div>
      {children}
    </section>
  )
}
