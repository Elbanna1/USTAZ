import * as React from 'react'
import { Search } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useI18n } from '@/i18n'
import { GlobalSearch } from '@/components/search/GlobalSearch'

/** Mac gets ⌘, everyone else Ctrl. Read once — the platform does not change. */
const IS_APPLE =
  typeof navigator !== 'undefined' &&
  /mac|iphone|ipad|ipod/i.test(navigator.platform || navigator.userAgent)

/**
 * Opens the global search palette, and owns the ⌘K / Ctrl-K shortcut.
 *
 * `field` is the dashboard topbar's resting search box, `icon` the compact
 * button the marketing navbar has room for, and `responsive` shows the field on
 * a wide header and the icon on a narrow one. One trigger covers both widths on
 * purpose: mounting two would mean two palettes and two shortcut listeners, so
 * ⌘K would open the dialog twice.
 */
export function SearchTrigger({
  variant = 'icon',
  className,
}: {
  variant?: 'icon' | 'field' | 'responsive'
  className?: string
}) {
  const { t } = useI18n()
  const [open, setOpen] = React.useState(false)

  React.useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key.toLowerCase() !== 'k' || !(event.metaKey || event.ctrlKey)) return
      // Chrome's own ⌘K focuses the address bar; the app's search wins here.
      event.preventDefault()
      setOpen((current) => !current)
    }

    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const openPalette = () => setOpen(true)
  const label = t('search.open')
  const hint = `${label} · ${IS_APPLE ? '⌘K' : 'Ctrl K'}`

  return (
    <>
      {variant !== 'icon' && (
        <button
          type="button"
          onClick={openPalette}
          title={hint}
          className={cn(
            'group h-10 w-full max-w-[20rem] items-center gap-2.5 rounded-lg border border-border bg-card px-3 text-start transition-colors',
            'hover:border-foreground/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            variant === 'responsive' ? 'hidden md:flex' : 'flex',
            className,
          )}
        >
          <Search
            className="size-[1.125rem] shrink-0 text-muted-foreground transition-colors group-hover:text-foreground"
            aria-hidden
          />
          <span className="flex-1 truncate text-body-sm text-muted-foreground">
            {t('common.search')}
          </span>
          <Shortcut />
        </button>
      )}

      {variant !== 'field' && (
        <button
          type="button"
          onClick={openPalette}
          aria-label={label}
          title={hint}
          className={cn(
            'size-10 place-items-center rounded-lg border border-border bg-card text-muted-foreground transition-colors',
            'hover:border-foreground/20 hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            variant === 'responsive' ? 'grid md:hidden' : 'grid',
            className,
          )}
        >
          <Search className="size-[1.125rem]" aria-hidden />
        </button>
      )}

      <GlobalSearch open={open} onOpenChange={setOpen} />
    </>
  )
}

function Shortcut() {
  return (
    <kbd
      aria-hidden
      className={cn(
        'hidden shrink-0 items-center gap-0.5 rounded border border-border bg-muted px-1.5 py-0.5 sm:flex',
        'font-sans text-[0.625rem] font-semibold text-muted-foreground',
      )}
    >
      {IS_APPLE ? '⌘' : 'Ctrl'}
      <span>K</span>
    </kbd>
  )
}
