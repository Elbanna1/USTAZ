import * as React from 'react'
import { Link } from 'react-router-dom'
import { Check, ChevronLeft, Copy, Search, Share2, X } from 'lucide-react'
import { cn, copyToClipboard } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/* --------------------------------- Search --------------------------------- */

interface SearchBarProps {
  value: string
  onChange: (value: string) => void
  placeholder?: string
  className?: string
  /** Suggestions shown while typing; clicking one commits it. */
  suggestions?: string[]
  onSubmit?: () => void
  size?: 'md' | 'lg'
  id?: string
}

export function SearchBar({
  value,
  onChange,
  placeholder,
  className,
  suggestions = [],
  onSubmit,
  size = 'md',
  id = 'search',
}: SearchBarProps) {
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'
  const [focused, setFocused] = React.useState(false)
  const showSuggestions = focused && value.length > 0 && suggestions.length > 0

  return (
    <div className={cn('relative', className)}>
      <form
        role="search"
        onSubmit={(e) => {
          e.preventDefault()
          onSubmit?.()
        }}
      >
        <label htmlFor={id} className="sr-only">
          {isAr ? 'بحث' : 'Search'}
        </label>
        <Search
          className="pointer-events-none absolute start-4 top-1/2 size-[1.125rem] -translate-y-1/2 text-muted-foreground"
          aria-hidden
        />
        <input
          id={id}
          type="search"
          value={value}
          placeholder={placeholder ?? (isAr ? 'دوّر على مدرّس أو مادة…' : 'Search a teacher or subject…')}
          onChange={(e) => onChange(e.target.value)}
          onFocus={() => setFocused(true)}
          // Blur is delayed so a click on a suggestion registers first.
          onBlur={() => window.setTimeout(() => setFocused(false), 140)}
          autoComplete="off"
          className={cn(
            'w-full rounded-xl border border-input bg-surface ps-12 pe-11 text-body-sm text-foreground shadow-xs transition-all',
            'placeholder:text-muted-foreground/70',
            'focus-visible:outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/25',
            '[&::-webkit-search-cancel-button]:appearance-none',
            size === 'lg' ? 'h-14 text-body' : 'h-12',
          )}
        />
        {value && (
          <button
            type="button"
            onClick={() => onChange('')}
            className="absolute end-3 top-1/2 -translate-y-1/2 rounded-md p-1.5 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            aria-label={isAr ? 'امسح البحث' : 'Clear search'}
          >
            <X className="size-4" />
          </button>
        )}
      </form>

      {showSuggestions && (
        <ul
          role="listbox"
          className="absolute inset-x-0 top-full z-30 mt-2 overflow-hidden rounded-xl border border-border bg-popover p-1.5 shadow-lift"
        >
          {suggestions.slice(0, 6).map((s) => (
            <li key={s}>
              <button
                type="button"
                role="option"
                aria-selected={false}
                onMouseDown={(e) => e.preventDefault()}
                onClick={() => {
                  onChange(s)
                  setFocused(false)
                }}
                className="flex w-full items-center gap-2.5 rounded-md px-3 py-2 text-start text-body-sm text-foreground transition-colors hover:bg-muted"
              >
                <Search className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                <span className="truncate">{s}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

/* ------------------------------- Filter chip ------------------------------ */

export function FilterChip({
  label,
  active,
  onClick,
  count,
  icon,
  className,
}: {
  label: string
  active?: boolean
  onClick?: () => void
  count?: number
  icon?: React.ReactNode
  className?: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border px-3.5 py-1.5 font-display text-body-sm font-semibold transition-all duration-200 ease-premium',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
        '[&_svg]:size-4 [&_svg]:shrink-0',
        active
          ? 'border-primary bg-primary text-primary-foreground shadow-soft'
          : 'border-border bg-card text-muted-foreground hover:border-foreground/20 hover:text-foreground',
        className,
      )}
    >
      {icon}
      {label}
      {count !== undefined && (
        <span
          className={cn(
            'tnum rounded-full px-1.5 text-[0.6875rem]',
            active ? 'bg-primary-foreground/20' : 'bg-muted',
          )}
        >
          {count}
        </span>
      )}
    </button>
  )
}

/** Removable chip summarising one applied filter. */
export function ActiveFilterChip({ label, onRemove }: { label: string; onRemove: () => void }) {
  const { locale } = useI18n()
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full border border-primary/25 bg-primary/10 py-1 pe-1.5 ps-3 text-caption font-semibold text-primary">
      {label}
      <button
        type="button"
        onClick={onRemove}
        className="rounded-full p-0.5 transition-colors hover:bg-primary/20 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        aria-label={locale === 'ar-EG' ? `شيل فلتر ${label}` : `Remove the ${label} filter`}
      >
        <X className="size-3.5" />
      </button>
    </span>
  )
}

/* ------------------------------- Breadcrumbs ------------------------------ */

export interface Crumb {
  label: string
  href?: string
}

export function Breadcrumbs({ items, className }: { items: Crumb[]; className?: string }) {
  const { locale } = useI18n()
  return (
    <nav
      aria-label={locale === 'ar-EG' ? 'مسار التنقل' : 'Breadcrumb'}
      className={cn('min-w-0', className)}
    >
      <ol className="no-scrollbar flex items-center gap-1.5 overflow-x-auto text-caption">
        {items.map((item, i) => {
          const last = i === items.length - 1
          return (
            <li key={`${item.label}-${i}`} className="flex shrink-0 items-center gap-1.5">
              {item.href && !last ? (
                <Link
                  to={item.href}
                  className="text-muted-foreground transition-colors hover:text-foreground"
                >
                  {item.label}
                </Link>
              ) : (
                <span
                  className={cn(last ? 'font-semibold text-foreground' : 'text-muted-foreground')}
                  aria-current={last ? 'page' : undefined}
                >
                  {item.label}
                </span>
              )}
              {!last && (
                <ChevronLeft className="size-3.5 shrink-0 text-muted-foreground/60" aria-hidden />
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}

/* ---------------------------- Copy & share ------------------------------- */

export function CopyButton({
  value,
  label,
  copiedLabel,
  className,
  variant = 'outline',
  size = 'sm',
}: {
  value: string
  label?: string
  copiedLabel?: string
  className?: string
  variant?: React.ComponentProps<typeof Button>['variant']
  size?: React.ComponentProps<typeof Button>['size']
}) {
  const [copied, setCopied] = React.useState(false)
  const { toast } = useToast()
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'

  async function handleCopy() {
    const ok = await copyToClipboard(value)
    if (ok) {
      setCopied(true)
      window.setTimeout(() => setCopied(false), 1800)
      toast({
        title: isAr ? 'اتنسخ للحافظة' : 'Copied to your clipboard',
        description: value,
        variant: 'success',
      })
    } else {
      toast({
        title: isAr ? 'مقدرناش ننسخ' : "We couldn't copy that",
        description: isAr ? 'انسخ النص يدويًا من فضلك.' : 'Please copy the text by hand.',
        variant: 'error',
      })
    }
  }

  return (
    <Button type="button" variant={variant} size={size} onClick={handleCopy} className={className}>
      {copied ? <Check className="size-4 text-success-strong" /> : <Copy className="size-4" />}
      {copied
        ? (copiedLabel ?? (isAr ? 'اتنسخ!' : 'Copied'))
        : (label ?? (isAr ? 'انسخ' : 'Copy'))}
    </Button>
  )
}

export function ShareButton({
  title,
  text,
  url,
  className,
  variant = 'outline',
  size = 'icon',
  label,
}: {
  title: string
  text?: string
  url?: string
  className?: string
  variant?: React.ComponentProps<typeof Button>['variant']
  size?: React.ComponentProps<typeof Button>['size']
  label?: string
}) {
  const { toast } = useToast()
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'
  const shareUrl = url ?? (typeof window !== 'undefined' ? window.location.href : '')

  async function nativeShare() {
    if (navigator.share) {
      try {
        await navigator.share({ title, text, url: shareUrl })
        return true
      } catch {
        // User dismissed the sheet — not an error worth surfacing.
        return true
      }
    }
    return false
  }

  async function handleClick() {
    const shared = await nativeShare()
    if (shared) return
    const ok = await copyToClipboard(shareUrl)
    toast({
      title: ok
        ? isAr
          ? 'اتنسخ اللينك'
          : 'Link copied'
        : isAr
          ? 'مقدرناش ننسخ اللينك'
          : "We couldn't copy the link",
      description: ok ? (isAr ? 'ابعته لأي حد دلوقتي.' : 'Send it to anyone now.') : shareUrl,
      variant: ok ? 'success' : 'error',
    })
  }

  // Desktop browsers without navigator.share get an explicit channel menu.
  const encoded = encodeURIComponent(shareUrl)
  const encodedTitle = encodeURIComponent(title)

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant={variant}
          size={size}
          className={className}
          aria-label={isAr ? 'شارك' : 'Share'}
        >
          <Share2 className="size-4" />
          {label}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent>
        <DropdownMenuItem onSelect={() => void handleClick()}>
          <Copy className="size-4" /> {isAr ? 'انسخ اللينك' : 'Copy link'}
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <a
            href={`https://wa.me/?text=${encodedTitle}%20${encoded}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            {isAr ? 'شارك على واتساب' : 'Share on WhatsApp'}
          </a>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <a
            href={`https://www.facebook.com/sharer/sharer.php?u=${encoded}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            {isAr ? 'شارك على فيسبوك' : 'Share on Facebook'}
          </a>
        </DropdownMenuItem>
        <DropdownMenuItem asChild>
          <a
            href={`https://t.me/share/url?url=${encoded}&text=${encodedTitle}`}
            target="_blank"
            rel="noopener noreferrer"
          >
            {isAr ? 'شارك على تليجرام' : 'Share on Telegram'}
          </a>
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
