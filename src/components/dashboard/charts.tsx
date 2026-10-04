import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { SeriesPoint } from '@/types'
import { useI18n } from '@/i18n'
import { cn, formatCompactNumber, formatEGP } from '@/lib/utils'

/**
 * Themed Recharts wrappers.
 *
 * Series colours come from the design tokens (primary / accent / success) rather
 * than a chart-library palette, so every chart matches the product in both
 * themes, and numerals stay tabular for scannability.
 *
 * Axis orientation follows the reading direction: in Arabic the time axis runs
 * right-to-left with the value axis on the right, in English the other way
 * round. `useAxisDirection` keeps that in one place.
 */

/** Axis props that depend on the reading direction. */
function useAxisDirection() {
  const { isRtl } = useI18n()
  return {
    /** Categories run with the text, so the oldest point sits at the start. */
    reversed: isRtl,
    /** The value axis sits at the end of the line, opposite the labels. */
    valueAxisSide: (isRtl ? 'right' : 'left') as 'right' | 'left',
    /** The legend starts where the reader does. */
    legendAlign: (isRtl ? 'right' : 'left') as 'right' | 'left',
  }
}

/** Fallback series names, so nothing ever falls through to a raw data key. */
function useSeriesNames() {
  const { locale } = useI18n()
  const ar = locale === 'ar-EG'
  return {
    value: ar ? 'القيمة' : 'Value',
    secondary: ar ? 'المقارنة' : 'Comparison',
    capacity: ar ? 'السعة' : 'Capacity',
    revenue: ar ? 'الإيراد' : 'Revenue',
    courseRevenue: ar ? 'إيراد الكورسات' : 'Course revenue',
    newSubscriptions: ar ? 'اشتراكات جديدة' : 'New subscriptions',
    cancellations: ar ? 'إلغاءات' : 'Cancellations',
  }
}

/** One legend treatment for every chart that plots more than one series. */
const LEGEND_PROPS = {
  verticalAlign: 'top',
  height: 32,
  iconType: 'circle',
  iconSize: 8,
  wrapperStyle: { fontSize: 12 },
} as const

const AXIS_PROPS = {
  stroke: 'hsl(var(--border))',
  tickLine: false,
  axisLine: false,
  tick: { fontSize: 12, fill: 'hsl(var(--muted-foreground))' },
} as const

function ChartTooltip({
  active,
  payload,
  label,
  formatter,
}: {
  active?: boolean
  payload?: { name?: string; value?: number; color?: string; dataKey?: string }[]
  label?: string
  formatter?: (value: number) => string
}) {
  if (!active || !payload?.length) return null
  return (
    <div className="rounded-lg border border-border bg-popover px-3 py-2.5 shadow-lift">
      {label && (
        <p className="font-display text-caption font-bold text-foreground">{label}</p>
      )}
      <ul className="mt-1.5 space-y-1">
        {payload.map((entry, i) => (
          <li key={i} className="flex items-center gap-2 text-caption">
            <span
              className="size-2 shrink-0 rounded-full"
              style={{ backgroundColor: entry.color }}
              aria-hidden
            />
            <span className="text-muted-foreground">{entry.name}</span>
            <span className="tnum ms-auto font-semibold text-foreground">
              {formatter ? formatter(entry.value ?? 0) : formatCompactNumber(entry.value ?? 0)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  )
}

interface ChartProps {
  data: SeriesPoint[]
  className?: string
  height?: number
  /** Names shown in the tooltip / legend. */
  seriesName?: string
  secondaryName?: string
  formatter?: (value: number) => string
}

/* ------------------------------- Area chart ------------------------------- */

export function TrendAreaChart({
  data,
  className,
  height = 260,
  seriesName,
  secondaryName,
  formatter,
}: ChartProps) {
  const hasSecondary = data.some((d) => d.secondary !== undefined)
  const axis = useAxisDirection()
  const names = useSeriesNames()

  return (
    <div className={cn('w-full', className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <AreaChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
          <defs>
            <linearGradient id="fillPrimary" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="hsl(var(--primary))" stopOpacity={0.32} />
              <stop offset="100%" stopColor="hsl(var(--primary))" stopOpacity={0.02} />
            </linearGradient>
            <linearGradient id="fillAccent" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="hsl(var(--accent))" stopOpacity={0.28} />
              <stop offset="100%" stopColor="hsl(var(--accent))" stopOpacity={0.02} />
            </linearGradient>
          </defs>

          <CartesianGrid vertical={false} strokeDasharray="4 4" />
          <XAxis dataKey="label" reversed={axis.reversed} {...AXIS_PROPS} />
          <YAxis
            orientation={axis.valueAxisSide}
            tickFormatter={(v) => formatCompactNumber(v)}
            {...AXIS_PROPS}
          />
          <Tooltip content={<ChartTooltip formatter={formatter} />} cursor={{ stroke: 'hsl(var(--border))' }} />
          {hasSecondary && <Legend align={axis.legendAlign} {...LEGEND_PROPS} />}

          {hasSecondary && (
            <Area
              type="monotone"
              dataKey="secondary"
              name={secondaryName ?? names.secondary}
              stroke="hsl(var(--accent))"
              strokeWidth={2}
              fill="url(#fillAccent)"
            />
          )}
          <Area
            type="monotone"
            dataKey="value"
            name={seriesName ?? names.value}
            stroke="hsl(var(--primary))"
            strokeWidth={2.4}
            fill="url(#fillPrimary)"
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  )
}

/* ------------------------------- Bar chart -------------------------------- */

export function CategoryBarChart({
  data,
  className,
  height = 260,
  seriesName,
  secondaryName,
  formatter,
}: ChartProps) {
  const hasSecondary = data.some((d) => d.secondary !== undefined)
  const axis = useAxisDirection()
  const names = useSeriesNames()

  return (
    <div className={cn('w-full', className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <BarChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }} barGap={4}>
          <CartesianGrid vertical={false} strokeDasharray="4 4" />
          <XAxis dataKey="label" reversed={axis.reversed} {...AXIS_PROPS} />
          <YAxis
            orientation={axis.valueAxisSide}
            tickFormatter={(v) => formatCompactNumber(v)}
            {...AXIS_PROPS}
          />
          <Tooltip
            content={<ChartTooltip formatter={formatter} />}
            cursor={{ fill: 'hsl(var(--muted))', opacity: 0.6 }}
          />
          {hasSecondary && <Legend align={axis.legendAlign} {...LEGEND_PROPS} />}
          {hasSecondary && (
            <Bar
              dataKey="secondary"
              name={secondaryName ?? names.secondary}
              fill="hsl(var(--muted-foreground))"
              fillOpacity={0.25}
              radius={[6, 6, 0, 0]}
              maxBarSize={38}
            />
          )}
          <Bar
            dataKey="value"
            name={seriesName ?? names.value}
            fill="hsl(var(--primary))"
            radius={[6, 6, 0, 0]}
            maxBarSize={38}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}

/* ------------------------------- Line chart ------------------------------- */

export function DualLineChart({
  data,
  className,
  height = 260,
  seriesName,
  secondaryName,
  formatter,
}: ChartProps) {
  const axis = useAxisDirection()
  const names = useSeriesNames()

  return (
    <div className={cn('w-full', className)} style={{ height }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
          <CartesianGrid vertical={false} strokeDasharray="4 4" />
          <XAxis dataKey="label" reversed={axis.reversed} {...AXIS_PROPS} />
          <YAxis orientation={axis.valueAxisSide} {...AXIS_PROPS} />
          <Tooltip content={<ChartTooltip formatter={formatter} />} />
          <Legend align={axis.legendAlign} {...LEGEND_PROPS} />
          <Line
            type="monotone"
            dataKey="value"
            name={seriesName ?? names.value}
            stroke="hsl(var(--primary))"
            strokeWidth={2.4}
            dot={{ r: 3, strokeWidth: 0, fill: 'hsl(var(--primary))' }}
            activeDot={{ r: 5 }}
          />
          <Line
            type="monotone"
            dataKey="secondary"
            name={secondaryName ?? names.secondary}
            stroke="hsl(var(--destructive))"
            strokeWidth={2}
            strokeDasharray="5 4"
            dot={{ r: 3, strokeWidth: 0, fill: 'hsl(var(--destructive))' }}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}

/* --------------------------------- Donut ---------------------------------- */

const DONUT_COLORS = [
  'hsl(var(--primary))',
  'hsl(var(--accent))',
  'hsl(var(--muted-foreground))',
  'hsl(var(--success))',
]

export function SplitDonutChart({
  data,
  className,
  height = 240,
  unit = '%',
}: {
  data: SeriesPoint[]
  className?: string
  height?: number
  unit?: string
}) {
  const total = data.reduce((sum, d) => sum + d.value, 0)

  return (
    <div className={cn('flex flex-col items-center gap-5 sm:flex-row', className)}>
      <div className="relative shrink-0" style={{ height, width: height }}>
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie
              data={data}
              dataKey="value"
              nameKey="label"
              innerRadius="64%"
              outerRadius="92%"
              paddingAngle={2}
              stroke="none"
            >
              {data.map((_, i) => (
                <Cell key={i} fill={DONUT_COLORS[i % DONUT_COLORS.length]} />
              ))}
            </Pie>
            <Tooltip content={<ChartTooltip formatter={(v) => `${v}${unit}`} />} />
          </PieChart>
        </ResponsiveContainer>

        <div className="pointer-events-none absolute inset-0 grid place-items-center text-center">
          <div>
            <p className="tnum font-display text-heading-md font-extrabold text-foreground">
              {data[0]?.value ?? 0}
              {unit}
            </p>
            <p className="text-caption text-muted-foreground">{data[0]?.label}</p>
          </div>
        </div>
      </div>

      <ul className="w-full space-y-2.5">
        {data.map((d, i) => (
          <li key={d.label} className="flex items-center gap-2.5 text-body-sm">
            <span
              className="size-2.5 shrink-0 rounded-full"
              style={{ backgroundColor: DONUT_COLORS[i % DONUT_COLORS.length] }}
              aria-hidden
            />
            <span className="flex-1 text-muted-foreground">{d.label}</span>
            <span className="tnum font-semibold text-foreground">
              {d.value}
              {unit}
            </span>
          </li>
        ))}
      </ul>

      <span className="sr-only">{total}</span>
    </div>
  )
}

/** Money-formatted variant used on the revenue panel. */
export function RevenueAreaChart({
  data,
  height = 260,
  seriesName,
  formatter,
}: {
  data: SeriesPoint[]
  height?: number
  seriesName?: string
  formatter?: (value: number) => string
}) {
  const names = useSeriesNames()
  return (
    <TrendAreaChart
      data={data}
      height={height}
      seriesName={seriesName ?? names.revenue}
      formatter={formatter ?? ((v) => formatEGP(v))}
    />
  )
}
