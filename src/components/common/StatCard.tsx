import { ArrowDownRight, ArrowUpRight } from 'lucide-react'
import { cn, formatNumber } from '@/lib/utils'
import { useCountUp } from '@/hooks/use-ui'
import { Card } from '@/components/ui/card'

/** Number that counts up once it scrolls into view. */
export function Counter({
  value,
  decimals = 0,
  prefix,
  suffix,
  className,
  format = true,
}: {
  value: number
  decimals?: number
  prefix?: string
  suffix?: string
  className?: string
  format?: boolean
}) {
  const { ref, value: current } = useCountUp(value, { decimals })
  const shown = decimals > 0 ? current.toFixed(decimals) : formatNumber(Math.round(current))

  return (
    <span ref={ref} className={cn('tnum', className)}>
      {prefix}
      {format ? shown : current}
      {suffix}
    </span>
  )
}

interface StatCardProps {
  label: string
  value: number
  /** e.g. "+12.4" — rendered as a delta pill. */
  delta?: number
  deltaSuffix?: string
  prefix?: string
  suffix?: string
  decimals?: number
  description?: string
  icon?: React.ReactNode
  className?: string
  /** Inverts the delta colouring for metrics where "down" is good. */
  invertDelta?: boolean
}

export function StatCard({
  label,
  value,
  delta,
  deltaSuffix = '%',
  prefix,
  suffix,
  decimals = 0,
  description,
  icon,
  className,
  invertDelta = false,
}: StatCardProps) {
  const positive = delta !== undefined && delta >= 0
  const good = invertDelta ? !positive : positive

  return (
    <Card className={cn('relative overflow-hidden p-5', className)}>
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className="text-body-sm font-medium text-muted-foreground">{label}</p>
          <p className="mt-2 font-display text-[1.75rem] font-extrabold leading-none text-foreground">
            <Counter value={value} decimals={decimals} prefix={prefix} suffix={suffix} />
          </p>
        </div>

        {icon && (
          <span className="grid size-10 shrink-0 place-items-center rounded-lg border border-border bg-muted/60 text-primary [&_svg]:size-5">
            {icon}
          </span>
        )}
      </div>

      {/* Only reserve the footer row when there is something to put in it —
          otherwise every card carried 12px of empty margin. */}
      {(delta !== undefined || description) && (
      <div className="mt-3 flex flex-wrap items-center gap-2">
        {delta !== undefined && (
          <span
            className={cn(
              'inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-caption font-bold',
              good ? 'bg-success/12 text-success-strong' : 'bg-destructive/12 text-destructive-strong',
            )}
          >
            {positive ? <ArrowUpRight className="size-3.5" /> : <ArrowDownRight className="size-3.5" />}
            <span className="tnum">
              {positive ? '+' : ''}
              {delta}
              {deltaSuffix}
            </span>
          </span>
        )}
        {description && <span className="text-caption text-muted-foreground">{description}</span>}
      </div>
      )}
    </Card>
  )
}

/** Big centred stat used in the homepage band — no card chrome. */
export function StatBlock({
  value,
  label,
  description,
  suffix,
  decimals = 0,
  icon,
}: {
  value: number
  label: string
  description?: string
  suffix?: string
  decimals?: number
  icon?: React.ReactNode
}) {
  return (
    <div className="text-center">
      {icon && (
        <span className="mx-auto mb-4 grid size-12 place-items-center rounded-xl border border-accent/25 bg-accent/10 text-accent-strong [&_svg]:size-6">
          {icon}
        </span>
      )}
      <p className="font-display text-[clamp(2rem,4vw,2.75rem)] font-extrabold leading-none text-foreground">
        <Counter value={value} decimals={decimals} suffix={suffix} />
      </p>
      <p className="mt-2.5 font-display text-body font-semibold text-foreground">{label}</p>
      {description && <p className="mt-1 text-caption text-muted-foreground">{description}</p>}
    </div>
  )
}
