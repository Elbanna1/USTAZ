import * as React from 'react'
import { CalendarDays, Clock } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'

/**
 * A date, time, or datetime input that reads correctly in an Arabic product.
 *
 * Native pickers are kept on purpose — they are accessible, keyboard-friendly,
 * and give mobile users the platform date wheel, which no hand-rolled popover
 * matches. What they do not do is follow the page language: the widget's own
 * placeholder comes from the *browser's* locale, so an Egyptian teacher sees
 * `mm/dd/yyyy` inside an otherwise Arabic form and cannot tell whether they are
 * typing a day or a month.
 *
 * So rather than fight the widget, this echoes the chosen value back underneath
 * in the reader's own language and calendar. The user confirms what they picked
 * instead of decoding the field.
 *
 * The control itself is always LTR: a date is a left-to-right sequence in every
 * locale, and mirroring it puts the year where the day belongs.
 */

export type DateFieldKind = 'date' | 'datetime-local' | 'time' | 'month'

export interface DateFieldProps
  extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> {
  kind?: DateFieldKind
  invalid?: boolean
  /**
   * Compact mode for dense rows: drops the echo and the leading icon so the
   * control can sit inline at a narrow width.
   */
  hideEcho?: boolean
}

export const DateField = React.forwardRef<HTMLInputElement, DateFieldProps>(
  ({ kind = 'date', invalid, hideEcho = false, className, value, onChange, ...props }, ref) => {
    const { locale, formatDate, formatTime, formatDateTime } = useI18n()
    const isAr = locale === 'ar-EG'

    // Track the value for the echo whether the field is controlled or not.
    const [internal, setInternal] = React.useState<string>(
      typeof value === 'string' ? value : '',
    )
    const current = typeof value === 'string' ? value : internal

    const handleChange = (event: React.ChangeEvent<HTMLInputElement>) => {
      setInternal(event.target.value)
      onChange?.(event)
    }

    const echo = React.useMemo(() => {
      if (!current) return null
      try {
        // `formatTime` takes a bare "HH:mm" from the API, which is exactly what
        // a native time input produces.
        if (kind === 'time') return formatTime(current)
        if (kind === 'month') {
          const [year, month] = current.split('-')
          if (!year || !month) return null
          return formatDate(`${year}-${month}-01`)
        }
        if (kind === 'datetime-local') return formatDateTime(current)
        return formatDate(current)
      } catch {
        // A half-typed value is not an error worth surfacing.
        return null
      }
    }, [current, kind, formatDate, formatTime, formatDateTime])

    // A clock belongs on a time field; a calendar on everything else.
    const Icon = kind === 'time' ? Clock : CalendarDays

    const control = (
      <Input
        ref={ref}
        type={kind}
        // A date reads left-to-right in every locale, including Arabic.
        dir="ltr"
        invalid={invalid}
        value={value}
        onChange={handleChange}
        className={cn('text-start', !hideEcho && 'ps-10', className)}
        {...props}
      />
    )

    // Compact rows keep the bare control so they can sit inline.
    if (hideEcho) return control

    return (
      <div className="space-y-1.5">
        <div className="relative">
          {control}
          <Icon
            className="pointer-events-none absolute start-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground"
            aria-hidden
          />
        </div>

        {!hideEcho && echo && (
          <p className="text-caption text-muted-foreground">
            {isAr ? 'اخترت: ' : 'Selected: '}
            <span className="font-medium text-foreground">{echo}</span>
          </p>
        )}
      </div>
    )
  },
)

DateField.displayName = 'DateField'
