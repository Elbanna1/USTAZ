import { useEffect, useId, useState } from 'react'
import type { AvatarTraits } from '@/types'
import { cn, hashSeed } from '@/lib/utils'

/**
 * Generated portraits.
 *
 * Every teacher and student "photo" in this product is drawn here as an SVG
 * illustration derived from the person's trait record. That keeps the art
 * direction consistent, keeps the bundle free of stock photography, and — since
 * all the people in this build are fictional — avoids depicting anyone real.
 *
 * Deterministic: the same seed always produces the same portrait.
 */

interface PersonAvatarProps {
  traits: AvatarTraits
  seed: string
  className?: string
  /** Subtle background wash behind the figure. */
  background?: boolean
}

function shade(hex: string, amount: number): string {
  const clean = hex.replace('#', '')
  const num = parseInt(clean.length === 3 ? clean.replace(/./g, '$&$&') : clean, 16)
  const r = Math.max(0, Math.min(255, ((num >> 16) & 255) + amount))
  const g = Math.max(0, Math.min(255, ((num >> 8) & 255) + amount))
  const b = Math.max(0, Math.min(255, (num & 255) + amount))
  return `rgb(${r}, ${g}, ${b})`
}

export function PersonAvatar({ traits, seed, className, background = true }: PersonAvatarProps) {
  const uid = useId().replace(/:/g, '')
  const clipId = `av-clip-${uid}`
  const bgId = `av-bg-${uid}`

  const { skin, hair, garment, style, glasses } = traits
  const isHijab = style === 'hijab'

  // Faces vary slightly so a grid of portraits never looks stamped.
  const jitter = (hashSeed(seed) % 5) - 2
  const faceCx = 50 + jitter * 0.3
  const faceCy = isHijab ? 43 : 41
  const faceRx = isHijab ? 12.6 : 16.6
  const faceRy = isHijab ? 15.6 : 19.4

  const eyeY = faceCy + 1
  const eyeDx = isHijab ? 5.4 : 6.8
  const skinShadow = shade(skin, -26)
  const hairHighlight = shade(hair, 26)
  const garmentDark = shade(garment, -28)

  return (
    <svg
      viewBox="0 0 100 100"
      className={cn('size-full', className)}
      role="img"
      aria-label="صورة رمزية"
      fill="none"
    >
      <defs>
        <clipPath id={clipId}>
          <circle cx="50" cy="50" r="50" />
        </clipPath>
        <linearGradient id={bgId} x1="0" y1="0" x2="0.6" y2="1">
          <stop offset="0" stopColor={shade(garment, 96)} />
          <stop offset="1" stopColor={shade(garment, 58)} />
        </linearGradient>
      </defs>

      <g clipPath={`url(#${clipId})`}>
        {background && <rect width="100" height="100" fill={`url(#${bgId})`} />}

        {/* shoulders */}
        <path
          d="M50 63c-19 0-33 13-35 37h70c-2-24-16-37-35-37z"
          fill={garment}
        />
        <path
          d="M50 63c-4 0-8 .6-11.6 1.8L50 76l11.6-11.2A40 40 0 0 0 50 63z"
          fill={garmentDark}
          opacity="0.55"
        />

        {/* neck */}
        <path d="M43 52h14v13a7 7 0 0 1-14 0z" fill={skinShadow} />

        {/* hijab wrap sits behind the face */}
        {isHijab && (
          <>
            <path
              d="M50 17c-16 0-24 12-24 27 0 9 3 16 7 21l-6 35h46l-6-35c4-5 7-12 7-21 0-15-8-27-24-27z"
              fill={hair}
            />
            <path
              d="M50 17c-16 0-24 12-24 27 0 4 .6 7.6 1.7 10.8C30 44 37 36 50 36s20 8 22.3 18.8c1.1-3.2 1.7-6.8 1.7-10.8 0-15-8-27-24-27z"
              fill={hairHighlight}
              opacity="0.35"
            />
          </>
        )}

        {/* ears */}
        {!isHijab && (
          <>
            <ellipse cx={faceCx - faceRx} cy={faceCy + 3} rx="3" ry="4.4" fill={skinShadow} />
            <ellipse cx={faceCx + faceRx} cy={faceCy + 3} rx="3" ry="4.4" fill={skinShadow} />
          </>
        )}

        {/* face */}
        <ellipse cx={faceCx} cy={faceCy} rx={faceRx} ry={faceRy} fill={skin} />

        {/* hair silhouettes */}
        {style === 'short' && (
          <path
            d={`M${faceCx - 17} ${faceCy + 1}c0-16 7-23 17-23s17 7 17 23c0-7-5-12-17-12s-17 5-17 12z`}
            fill={hair}
          />
        )}

        {style === 'wavy' && (
          <path
            d={`M${faceCx - 18} ${faceCy + 6}c0-20 8-27 18-27s18 7 18 27c0-7-3-13-7-14-2 5-6 7-11 7s-9-2-11-7c-4 1-7 7-7 14z`}
            fill={hair}
          />
        )}

        {style === 'bun' && (
          <>
            <circle cx={faceCx} cy={faceCy - 24} r="7.5" fill={hair} />
            <path
              d={`M${faceCx - 17} ${faceCy + 1}c0-16 7-23 17-23s17 7 17 23c0-7-5-12-17-12s-17 5-17 12z`}
              fill={hair}
            />
          </>
        )}

        {style === 'beard' && (
          <>
            <path
              d={`M${faceCx - 17} ${faceCy}c0-16 7-22 17-22s17 6 17 22c0-7-5-11-17-11s-17 4-17 11z`}
              fill={hair}
            />
            <path
              d={`M${faceCx - 16.6} ${faceCy + 3}c0 12 6.6 19 16.6 19s16.6-7 16.6-19c0 7-5.6 10-16.6 10s-16.6-3-16.6-10z`}
              fill={hair}
            />
            <path
              d={`M${faceCx - 5} ${faceCy + 8.5}h10c0 2-2.2 3.2-5 3.2s-5-1.2-5-3.2z`}
              fill={hair}
            />
          </>
        )}

        {style === 'bald' && (
          <path
            d={`M${faceCx - 17} ${faceCy + 4}c0-4 1-7 3-9 1 4 5 6 14 6s13-2 14-6c2 2 3 5 3 9 0-14-7-21-17-21s-17 7-17 21z`}
            fill={hair}
            opacity="0.85"
          />
        )}

        {/* brows */}
        <path
          d={`M${faceCx - eyeDx - 3.4} ${eyeY - 5.6}q3.4-2 6.8 0`}
          stroke={hair}
          strokeWidth="1.5"
          strokeLinecap="round"
        />
        <path
          d={`M${faceCx + eyeDx - 3.4} ${eyeY - 5.6}q3.4-2 6.8 0`}
          stroke={hair}
          strokeWidth="1.5"
          strokeLinecap="round"
        />

        {/* eyes */}
        <circle cx={faceCx - eyeDx} cy={eyeY} r="1.85" fill="#221C18" />
        <circle cx={faceCx + eyeDx} cy={eyeY} r="1.85" fill="#221C18" />

        {/* nose + mouth */}
        <path
          d={`M${faceCx} ${eyeY + 3}v3.4`}
          stroke={skinShadow}
          strokeWidth="1.3"
          strokeLinecap="round"
        />
        <path
          d={`M${faceCx - 3.6} ${eyeY + 9.4}q3.6 3 7.2 0`}
          stroke={shade(skin, -70)}
          strokeWidth="1.6"
          strokeLinecap="round"
          fill="none"
        />

        {/* glasses */}
        {glasses && (
          <g stroke="#2B2B2B" strokeWidth="1.5" fill="none" opacity="0.85">
            <rect
              x={faceCx - eyeDx - 4.4}
              y={eyeY - 4}
              width="8.8"
              height="8"
              rx="3.2"
              fill="#FFFFFF"
              fillOpacity="0.14"
            />
            <rect
              x={faceCx + eyeDx - 4.4}
              y={eyeY - 4}
              width="8.8"
              height="8"
              rx="3.2"
              fill="#FFFFFF"
              fillOpacity="0.14"
            />
            <path d={`M${faceCx - 4.4} ${eyeY}h8.8`} />
          </g>
        )}
      </g>
    </svg>
  )
}

/* -------------------------------------------------------------------------- */
/*                                 Avatar frame                               */
/* -------------------------------------------------------------------------- */

const FRAME_SIZES = {
  xs: 'size-8',
  sm: 'size-10',
  md: 'size-12',
  lg: 'size-16',
  xl: 'size-24',
  '2xl': 'size-32',
  '3xl': 'size-40',
} as const

export type AvatarSize = keyof typeof FRAME_SIZES

interface AvatarProps {
  traits?: AvatarTraits
  seed: string
  name: string
  size?: AvatarSize
  className?: string
  ring?: boolean
  /** A real uploaded photo. Falls back to the drawn portrait if it fails to load. */
  src?: string
  /** Small badge anchored to the bottom-start corner. */
  badge?: React.ReactNode
}

/**
 * Circular portrait. Prefers a real uploaded photo, then the generated portrait,
 * then initials — so a face is always rendered, never a broken image box.
 */
export function Avatar({
  traits,
  seed,
  name,
  size = 'md',
  className,
  ring = false,
  src,
  badge,
}: AvatarProps) {
  const [imageFailed, setImageFailed] = useState(false)
  const showImage = Boolean(src) && !imageFailed

  useEffect(() => setImageFailed(false), [src])

  return (
    /* rounded-full on the frame too, so a ring or shadow passed in by a caller
       follows the circle instead of boxing it. */
    <span className={cn('relative inline-block shrink-0 rounded-full', FRAME_SIZES[size], className)}>
      <span
        className={cn(
          'block size-full overflow-hidden rounded-full bg-muted',
          ring && 'ring-2 ring-card ring-offset-2 ring-offset-background',
        )}
      >
        {showImage ? (
          <img
            src={src}
            alt={name}
            loading="lazy"
            decoding="async"
            onError={() => setImageFailed(true)}
            className="size-full object-cover"
          />
        ) : traits ? (
          <PersonAvatar traits={traits} seed={seed} />
        ) : (
          <span className="grid size-full place-items-center bg-primary/12 font-display text-body-sm font-bold text-primary">
            {name.trim().split(/\s+/).slice(0, 2).map((w) => w[0]).join('')}
          </span>
        )}
      </span>
      {badge && <span className="absolute -bottom-0.5 -start-0.5">{badge}</span>}
    </span>
  )
}
