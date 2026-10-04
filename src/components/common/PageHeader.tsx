import { motion } from 'framer-motion'
import { AmbientGlow, PaperTexture } from '@/components/illustrations/CoverArt'
import { Breadcrumbs, type Crumb } from '@/components/common/controls'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

interface PageHeaderProps {
  eyebrow?: string
  title: React.ReactNode
  description?: React.ReactNode
  crumbs?: Crumb[]
  /** Rendered under the description — search bars, chips, counters. */
  children?: React.ReactNode
  /** Rendered opposite the title on wide screens. */
  aside?: React.ReactNode
  className?: string
  compact?: boolean
}

/** Shared header band for every directory and content page. */
export function PageHeader({
  eyebrow,
  title,
  description,
  crumbs,
  children,
  aside,
  className,
  compact = false,
}: PageHeaderProps) {
  return (
    <section
      className={cn(
        'relative overflow-hidden border-b border-border bg-surface',
        compact ? 'py-8' : 'pb-12 pt-8 sm:pb-14 sm:pt-10',
        className,
      )}
    >
      <AmbientGlow className="opacity-70" />
      <PaperTexture className="opacity-30 dark:opacity-15" />

      <div className="container-page relative">
        {crumbs && <Breadcrumbs items={crumbs} className="mb-5" />}

        <div className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
          <motion.div
            initial={{ opacity: 0, y: 14 }}
            animate={{ opacity: 1, y: 0 }}
            transition={transitions.base}
            className="max-w-2xl"
          >
            {eyebrow && <span className="eyebrow mb-4">{eyebrow}</span>}
            <h1 className={cn(compact ? 'text-display-lg' : 'text-display-xl', 'text-foreground')}>
              {title}
            </h1>
            {description && (
              <p className="mt-4 text-body-lg text-muted-foreground">{description}</p>
            )}
          </motion.div>

          {aside && (
            <motion.div
              initial={{ opacity: 0, y: 14 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ ...transitions.base, delay: 0.08 }}
              className="shrink-0"
            >
              {aside}
            </motion.div>
          )}
        </div>

        {children && (
          <motion.div
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ ...transitions.base, delay: 0.12 }}
            className="mt-8"
          >
            {children}
          </motion.div>
        )}
      </div>
    </section>
  )
}
