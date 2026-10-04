import { useId, useState } from 'react'
import type { CoverPattern, SubjectKey } from '@/types'
import { subjectByKey } from '@/data/taxonomy'
import { cn } from '@/lib/utils'

/**
 * Generated cover artwork.
 *
 * Every teacher cover, group hero and course thumbnail in the product is drawn
 * from this one family of six geometric systems, tinted by the subject's hue.
 * No stock photography, no external image requests, no broken image boxes.
 */

interface CoverArtProps {
  pattern: CoverPattern
  subject?: SubjectKey
  className?: string
  /** Dials the artwork back behind text. */
  intensity?: 'soft' | 'normal' | 'bold'
}

export function CoverArt({ pattern, subject, className, intensity = 'normal' }: CoverArtProps) {
  const uid = useId().replace(/:/g, '')
  const hue = subject ? subjectByKey(subject).hue : 172

  const base = `hsl(${hue} 42% 13%)`
  const mid = `hsl(${hue} 38% 20%)`
  const tint = `hsl(${hue} 62% 62%)`
  const gold = '#D7A94A'

  const strokeOpacity = intensity === 'soft' ? 0.2 : intensity === 'bold' ? 0.55 : 0.36

  return (
    <svg
      viewBox="0 0 400 200"
      preserveAspectRatio="xMidYMid slice"
      className={cn('size-full', className)}
      aria-hidden
      focusable="false"
    >
      <defs>
        <linearGradient id={`bg-${uid}`} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0" stopColor={mid} />
          <stop offset="1" stopColor={base} />
        </linearGradient>
        <radialGradient id={`glow-${uid}`} cx="0.78" cy="0.15" r="0.75">
          <stop offset="0" stopColor={tint} stopOpacity="0.38" />
          <stop offset="1" stopColor={tint} stopOpacity="0" />
        </radialGradient>
        <linearGradient id={`fade-${uid}`} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0" stopColor={base} stopOpacity="0" />
          <stop offset="1" stopColor={base} stopOpacity="0.72" />
        </linearGradient>

        {pattern === 'arabesque' && (
          <pattern id={`pat-${uid}`} width="44" height="44" patternUnits="userSpaceOnUse">
            {/* eight-point star lattice */}
            <path
              d="M22 4 28 14 40 14 34 22 40 30 28 30 22 40 16 30 4 30 10 22 4 14 16 14z"
              fill="none"
              stroke={tint}
              strokeWidth="1"
              strokeOpacity={strokeOpacity}
            />
            <circle cx="22" cy="22" r="3" fill={gold} fillOpacity={strokeOpacity * 0.8} />
          </pattern>
        )}

        {pattern === 'grid' && (
          <pattern id={`pat-${uid}`} width="34" height="34" patternUnits="userSpaceOnUse">
            <path d="M34 0H0v34" fill="none" stroke={tint} strokeWidth="0.9" strokeOpacity={strokeOpacity} />
            <circle cx="0" cy="0" r="1.6" fill={gold} fillOpacity={strokeOpacity} />
          </pattern>
        )}
      </defs>

      <rect width="400" height="200" fill={`url(#bg-${uid})`} />
      <rect width="400" height="200" fill={`url(#glow-${uid})`} />

      {(pattern === 'arabesque' || pattern === 'grid') && (
        <rect width="400" height="200" fill={`url(#pat-${uid})`} />
      )}

      {pattern === 'waves' && (
        <g fill="none" strokeLinecap="round">
          {[0, 1, 2, 3, 4, 5].map((i) => (
            <path
              key={i}
              d={`M-20 ${60 + i * 26} C 60 ${30 + i * 26}, 140 ${94 + i * 26}, 220 ${62 + i * 26} S 380 ${
                34 + i * 26
              }, 420 ${68 + i * 26}`}
              stroke={i % 3 === 0 ? gold : tint}
              strokeWidth={i % 3 === 0 ? 1.6 : 1}
              strokeOpacity={strokeOpacity * (1 - i * 0.08)}
            />
          ))}
        </g>
      )}

      {pattern === 'orbit' && (
        <g fill="none">
          {[36, 62, 88, 116, 146].map((r, i) => (
            <ellipse
              key={r}
              cx="300"
              cy="72"
              rx={r}
              ry={r * 0.62}
              stroke={i === 1 ? gold : tint}
              strokeWidth={i === 1 ? 1.5 : 0.9}
              strokeOpacity={strokeOpacity * (1 - i * 0.1)}
              transform={`rotate(${-18 + i * 4} 300 72)`}
            />
          ))}
          <circle cx="300" cy="72" r="11" fill={gold} fillOpacity="0.85" />
          <circle cx="216" cy="46" r="4.5" fill={tint} fillOpacity="0.9" />
          <circle cx="368" cy="128" r="3.2" fill={tint} fillOpacity="0.7" />
          <circle cx="182" cy="126" r="2.6" fill={gold} fillOpacity="0.6" />
        </g>
      )}

      {pattern === 'strata' && (
        <g>
          {[0, 1, 2, 3, 4, 5, 6].map((i) => (
            <path
              key={i}
              d={`M-10 ${44 + i * 24} C 90 ${28 + i * 24}, 180 ${62 + i * 24}, 410 ${30 + i * 24} L410 ${
                70 + i * 24
              } C 180 ${100 + i * 24}, 90 ${66 + i * 24}, -10 ${82 + i * 24} Z`}
              fill={i % 2 === 0 ? tint : gold}
              fillOpacity={strokeOpacity * 0.24 * (1 - i * 0.06)}
            />
          ))}
        </g>
      )}

      {pattern === 'bloom' && (
        <g transform="translate(298 76)">
          {[0, 1, 2, 3, 4, 5, 6, 7].map((i) => (
            <ellipse
              key={i}
              rx="74"
              ry="21"
              fill="none"
              stroke={i % 4 === 0 ? gold : tint}
              strokeWidth={i % 4 === 0 ? 1.4 : 0.9}
              strokeOpacity={strokeOpacity * 0.85}
              transform={`rotate(${i * 22.5})`}
            />
          ))}
          <circle r="8" fill={gold} fillOpacity="0.8" />
        </g>
      )}

      <rect y="110" width="400" height="90" fill={`url(#fade-${uid})`} />
    </svg>
  )
}

/* -------------------------------------------------------------------------- */
/*                          Decorative page backdrops                         */
/* -------------------------------------------------------------------------- */

/** Soft ambient blobs used behind hero and CTA sections. */
export function AmbientGlow({ className }: { className?: string }) {
  return (
    <div className={cn('pointer-events-none absolute inset-0 overflow-hidden', className)} aria-hidden>
      <div className="absolute -top-24 -start-20 size-[30rem] rounded-full bg-primary/12 blur-[100px] dark:bg-primary/15" />
      <div className="absolute -bottom-32 -end-24 size-[26rem] rounded-full bg-accent/14 blur-[110px]" />
      <div className="absolute end-1/3 top-1/4 size-64 rounded-full bg-accent/8 blur-[80px]" />
    </div>
  )
}

/** Fine paper grid that fades out — the shared page texture. */
export function PaperTexture({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'pointer-events-none absolute inset-0 bg-paper-grid opacity-[0.55] mask-fade-b dark:opacity-30',
        className,
      )}
      aria-hidden
    />
  )
}

/** Thin gold rule with a diamond at its centre — a recurring brand device. */
export function GoldRule({ className }: { className?: string }) {
  return (
    <div className={cn('flex items-center gap-3', className)} aria-hidden>
      <span className="h-px flex-1 bg-gradient-to-l from-transparent via-accent/45 to-transparent" />
      <span className="size-1.5 rotate-45 rounded-[2px] bg-accent" />
      <span className="h-px flex-1 bg-gradient-to-l from-transparent via-accent/45 to-transparent" />
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*                          Real image with art fallback                      */
/* -------------------------------------------------------------------------- */

/**
 * Shows an uploaded cover when the API has one and falls back to the generated
 * artwork otherwise — so a missing upload is never an empty grey band.
 */
export function CoverImage({
  src,
  pattern,
  subject,
  intensity = 'normal',
  className,
  alt = '',
}: {
  src?: string
  pattern: CoverPattern
  subject?: SubjectKey
  intensity?: 'soft' | 'normal' | 'bold'
  className?: string
  alt?: string
}) {
  const [failed, setFailed] = useState(false)

  if (src && !failed) {
    return (
      <img
        src={src}
        alt={alt}
        loading="lazy"
        decoding="async"
        onError={() => setFailed(true)}
        className={cn('size-full object-cover', className)}
      />
    )
  }

  return <CoverArt pattern={pattern} subject={subject} intensity={intensity} className={className} />
}
