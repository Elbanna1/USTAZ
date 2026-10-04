import { useId } from 'react'
import { cn } from '@/lib/utils'

/**
 * USTAZ — أستاذ
 *
 * The mark is an original construction, not an icon-library glyph:
 *   · a rounded ink tile — the "page"
 *   · two curved leaves forming an open book / lectern
 *   · the upright alef stroke of أستاذ rising from the spine
 *   · the hamza, set as a gold jewel — the brand's single warm accent
 *
 * It reads as the Arabic letter أ in an Arabic context and as a bookmark /
 * lectern in a Latin one, so the same mark carries both wordmarks.
 */

export type LogoTone = 'auto' | 'onLight' | 'onDark' | 'mono'

interface LogoMarkProps {
  className?: string
  tone?: LogoTone
  /** Renders a flat (unfilled) tile for use on already-dark surfaces. */
  plate?: boolean
}

export function LogoMark({ className, tone = 'auto', plate = true }: LogoMarkProps) {
  const uid = useId().replace(/:/g, '')
  const inkId = `ustaz-ink-${uid}`
  const goldId = `ustaz-gold-${uid}`

  const paper = tone === 'mono' ? 'currentColor' : '#F7F3EA'
  const gold = tone === 'mono' ? 'currentColor' : `url(#${goldId})`

  return (
    <svg
      viewBox="0 0 64 64"
      className={cn('size-10 shrink-0', className)}
      role="img"
      aria-label="أستاذ"
      fill="none"
    >
      <defs>
        <linearGradient id={inkId} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#1A5149" />
          <stop offset="0.55" stopColor="#103A34" />
          <stop offset="1" stopColor="#08221E" />
        </linearGradient>
        <linearGradient id={goldId} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor="#F2CC7C" />
          <stop offset="1" stopColor="#C9962F" />
        </linearGradient>
      </defs>

      {plate && (
        <rect
          width="64"
          height="64"
          rx="17"
          fill={tone === 'mono' ? 'none' : `url(#${inkId})`}
          stroke={tone === 'mono' ? 'currentColor' : 'none'}
          strokeWidth={tone === 'mono' ? 2.5 : 0}
        />
      )}

      {/* open book / lectern */}
      <path
        d="M15 40.6c5.6-4.6 11.4-4.6 17 0 5.6-4.6 11.4-4.6 17 0v6.8c-5.6-4.6-11.4-4.6-17 0-5.6-4.6-11.4-4.6-17 0z"
        fill={paper}
        opacity={tone === 'mono' ? 0.75 : 0.92}
      />

      {/* the alef */}
      <path d="M32 39.4V17.8" stroke={paper} strokeWidth="6.6" strokeLinecap="round" />

      {/* the hamza, as a gold jewel */}
      <rect
        x="39.2"
        y="15.6"
        width="9.2"
        height="9.2"
        rx="2.6"
        transform="rotate(45 43.8 20.2)"
        fill={gold}
      />
    </svg>
  )
}

interface LogoProps {
  /** `full` = mark + wordmark, `mark` = tile only, `wordmark` = type only. */
  variant?: 'full' | 'mark' | 'wordmark'
  /** Which script leads the wordmark. */
  script?: 'ar' | 'en' | 'both'
  size?: 'sm' | 'md' | 'lg' | 'xl'
  tone?: LogoTone
  className?: string
}

const SIZES = {
  sm: { mark: 'size-8', ar: 'text-[1.125rem]', en: 'text-[0.5625rem]', gap: 'gap-2' },
  md: { mark: 'size-10', ar: 'text-[1.4rem]', en: 'text-[0.625rem]', gap: 'gap-2.5' },
  lg: { mark: 'size-12', ar: 'text-[1.75rem]', en: 'text-[0.75rem]', gap: 'gap-3' },
  xl: { mark: 'size-16', ar: 'text-[2.25rem]', en: 'text-[0.875rem]', gap: 'gap-4' },
} as const

export function Logo({
  variant = 'full',
  script = 'both',
  size = 'md',
  tone = 'auto',
  className,
}: LogoProps) {
  const s = SIZES[size]

  const textColor =
    tone === 'onDark'
      ? 'text-white'
      : tone === 'onLight'
        ? 'text-primary'
        : tone === 'mono'
          ? 'text-current'
          : 'text-foreground'

  const subColor =
    tone === 'onDark'
      ? 'text-white/55'
      : tone === 'mono'
        ? 'text-current opacity-60'
        : 'text-muted-foreground'

  if (variant === 'mark') return <LogoMark className={cn(s.mark, className)} tone={tone} />

  const wordmark = (
    <span className="flex flex-col justify-center leading-none">
      {(script === 'ar' || script === 'both') && (
        <span
          className={cn('font-display font-extrabold tracking-[-0.02em]', s.ar, textColor)}
          style={{ lineHeight: 1.15 }}
        >
          أستاذ
        </span>
      )}
      {(script === 'en' || script === 'both') && (
        <span
          className={cn(
            'font-latin font-bold uppercase tracking-[0.34em]',
            s.en,
            script === 'both' ? subColor : textColor,
          )}
          dir="ltr"
        >
          Ustaz
        </span>
      )}
    </span>
  )

  if (variant === 'wordmark') return <span className={className}>{wordmark}</span>

  return (
    <span className={cn('inline-flex items-center', s.gap, className)}>
      <LogoMark className={s.mark} tone={tone} />
      {wordmark}
    </span>
  )
}

/**
 * Loading splash mark — the alef stroke draws itself once.
 * Used by the route-level Suspense fallback.
 */
export function LogoPulse({ className }: { className?: string }) {
  return (
    <div className={cn('relative grid place-items-center', className)}>
      <span className="absolute inset-0 animate-pulse-ring rounded-[1.4rem] bg-accent/30" aria-hidden />
      <LogoMark className="relative size-14" />
    </div>
  )
}
