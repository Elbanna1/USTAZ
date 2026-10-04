import * as React from 'react'
import { ChevronDown, SlidersHorizontal, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/primitives'
import { Sheet, SheetContent, SheetFooter, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet'
import { Badge } from '@/components/ui/badge'
import { useI18n } from '@/i18n'

/** Collapsible group of checkboxes inside a filter panel. */
export function FilterGroup({
  title,
  children,
  defaultOpen = true,
  count,
}: {
  title: string
  children: React.ReactNode
  defaultOpen?: boolean
  count?: number
}) {
  const [open, setOpen] = React.useState(defaultOpen)
  const id = React.useId()

  return (
    <div className="border-b border-border py-4 first:pt-0 last:border-b-0">
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
        aria-controls={id}
        className="flex w-full items-center justify-between gap-2 text-start focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <span className="flex items-center gap-2 font-display text-body-sm font-bold text-foreground">
          {title}
          {count ? (
            <Badge variant="primary" size="sm" className="tnum">
              {count}
            </Badge>
          ) : null}
        </span>
        <ChevronDown
          className={cn(
            'size-4 shrink-0 text-muted-foreground transition-transform duration-200',
            open && 'rotate-180',
          )}
        />
      </button>

      {open && (
        <div id={id} className="mt-3.5 space-y-2.5">
          {children}
        </div>
      )}
    </div>
  )
}

/** One checkbox row with an optional trailing count. */
export function FilterCheckbox({
  id,
  label,
  checked,
  onChange,
  count,
  icon,
}: {
  id: string
  label: string
  checked: boolean
  onChange: (checked: boolean) => void
  count?: number
  icon?: React.ReactNode
}) {
  return (
    <div className="flex items-center gap-2.5">
      <Checkbox id={id} checked={checked} onCheckedChange={(v) => onChange(v === true)} />
      <label
        htmlFor={id}
        className="flex flex-1 cursor-pointer items-center gap-2 text-body-sm text-foreground"
      >
        {icon}
        <span className="flex-1">{label}</span>
        {count !== undefined && (
          <span className="tnum text-caption text-muted-foreground">{count}</span>
        )}
      </label>
    </div>
  )
}

interface FilterPanelProps {
  children: React.ReactNode
  activeCount: number
  onClear: () => void
  className?: string
  /** Result count shown in the mobile sheet footer. */
  resultCount?: number
}

/**
 * Responsive filter shell: a sticky rail on desktop, a bottom-anchored sheet on
 * mobile. The same children render in both, so the filter set never diverges.
 */
export function FilterPanel({
  children,
  activeCount,
  onClear,
  className,
  resultCount,
}: FilterPanelProps) {
  const { locale, formatNumber } = useI18n()
  const isAr = locale === 'ar-EG'
  const [open, setOpen] = React.useState(false)

  const filtersLabel = isAr ? 'الفلاتر' : 'Filters'
  const clearLabel = isAr ? 'مسح الكل' : 'Clear all'

  const panel = (
    <>
      <div className="flex items-center justify-between gap-2 pb-4">
        <h2 className="flex items-center gap-2 font-display text-body font-bold text-foreground">
          <SlidersHorizontal className="size-4" aria-hidden />
          {filtersLabel}
        </h2>
        {activeCount > 0 && (
          <Button variant="ghost" size="sm" onClick={onClear}>
            <X className="size-4" />
            {clearLabel}
          </Button>
        )}
      </div>
      {children}
    </>
  )

  return (
    <>
      {/* Desktop rail */}
      <aside className={cn('hidden lg:block', className)}>
        <div className="sticky top-24 max-h-[calc(100dvh-8rem)] overflow-y-auto rounded-xl border border-border bg-card p-5">
          {panel}
        </div>
      </aside>

      {/* Mobile trigger + sheet */}
      <Sheet open={open} onOpenChange={setOpen}>
        <SheetTrigger asChild>
          <Button variant="outline" className="lg:hidden">
            <SlidersHorizontal className="size-4" />
            {filtersLabel}
            {activeCount > 0 && (
              <Badge variant="primary" size="sm" className="tnum">
                {activeCount}
              </Badge>
            )}
          </Button>
        </SheetTrigger>
        <SheetContent side="bottom" className="max-h-[85dvh] rounded-t-3xl">
          <SheetHeader>
            <SheetTitle>{filtersLabel}</SheetTitle>
          </SheetHeader>
          <div className="min-h-0 flex-1 overflow-y-auto px-5">{children}</div>
          <SheetFooter>
            {activeCount > 0 && (
              <Button variant="outline" onClick={onClear} className="flex-1">
                {clearLabel}
              </Button>
            )}
            <Button onClick={() => setOpen(false)} className="flex-1">
              {resultCount === undefined
                ? isAr
                  ? 'تطبيق'
                  : 'Apply'
                : isAr
                  ? `اعرض ${formatNumber(resultCount)} نتيجة`
                  : `Show ${formatNumber(resultCount)} results`}
            </Button>
          </SheetFooter>
        </SheetContent>
      </Sheet>
    </>
  )
}
