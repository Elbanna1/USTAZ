import type { SubjectKey } from '@/types'
import { subjectByKey } from '@/data/taxonomy'
import { cn } from '@/lib/utils'

/**
 * A purpose-drawn icon per subject. Built as one stroke system — 24px grid,
 * 1.6 stroke, round caps — so the ten marks read as a family rather than ten
 * icons borrowed from a library.
 */

const PATHS: Record<SubjectKey, React.ReactNode> = {
  physics: (
    <>
      <ellipse cx="12" cy="12" rx="9.5" ry="4" />
      <ellipse cx="12" cy="12" rx="9.5" ry="4" transform="rotate(60 12 12)" />
      <ellipse cx="12" cy="12" rx="9.5" ry="4" transform="rotate(-60 12 12)" />
      <circle cx="12" cy="12" r="2.1" fill="currentColor" stroke="none" />
    </>
  ),
  math: (
    <>
      <path d="M16.5 5h-9l5.5 7-5.5 7h9" />
      <path d="M4 5v14" />
    </>
  ),
  chemistry: (
    <>
      <path d="M9 3h6" />
      <path d="M10 3v6.2L4.9 17.6A2 2 0 0 0 6.6 20.7h10.8a2 2 0 0 0 1.7-3.1L14 9.2V3" />
      <path d="M7 15.2h10" />
      <circle cx="10.4" cy="17.6" r="1.05" fill="currentColor" stroke="none" />
      <circle cx="14.2" cy="18.4" r="0.8" fill="currentColor" stroke="none" />
    </>
  ),
  biology: (
    <>
      <path d="M8 3c0 4.5 8 4.5 8 9s-8 4.5-8 9" />
      <path d="M16 3c0 4.5-8 4.5-8 9s8 4.5 8 9" />
      <path d="M9.3 6.6h5.4M8 12h8M9.3 17.4h5.4" />
    </>
  ),
  arabic: (
    <>
      <path d="M4 20.2c5.2-1 8.4-4.2 12.4-9.2l3.2-4a1.7 1.7 0 0 0-2.4-2.4l-4 3.2C8.2 11.8 5 15 4 20.2z" />
      <path d="M4 20.2l3.2-3.2" />
      <path d="M14.6 5.8l3.6 3.6" />
    </>
  ),
  english: (
    <>
      <path d="M21 11.6a8 8 0 0 1-8 8H8.4L4 22.6v-4.4a8 8 0 0 1 9-14 8 8 0 0 1 8 7.4z" />
      <path d="M9.8 14.8L13 7.6l3.2 7.2" />
      <path d="M10.8 12.8h4.4" />
    </>
  ),
  french: (
    <>
      <path d="M21 11.6a8 8 0 0 1-8 8H8.4L4 22.6v-4.4a8 8 0 0 1 9-14 8 8 0 0 1 8 7.4z" />
      <path d="M13.4 7.4l1.9-2.1" />
      <path d="M9.9 13.2h5.6a2.8 2.8 0 1 0-5.6 0 2.8 2.8 0 0 0 4.8 2" />
    </>
  ),
  history: (
    <>
      <path d="M12 2.8l9 4.2H3z" />
      <path d="M5.4 8.2v9.4M9.8 8.2v9.4M14.2 8.2v9.4M18.6 8.2v9.4" />
      <path d="M3 20.6h18" />
    </>
  ),
  geology: (
    <>
      <path d="M12 2.6l4.2 5.2-4.2 4.2-4.2-4.2z" />
      <path d="M3 16.2l9-4.4 9 4.4" />
      <path d="M3 20.4l9-4.4 9 4.4" />
    </>
  ),
  science: (
    <>
      <path d="M9 3h6" />
      <path d="M10 3v5.9L5.1 17.5a2 2 0 0 0 1.7 3.1h10.4a2 2 0 0 0 1.7-3.1L14 8.9V3" />
      <path d="M7.4 15h9.2" />
      <path d="M19.4 2.6l.75 1.9 1.9.75-1.9.75-.75 1.9-.75-1.9-1.9-.75 1.9-.75z" />
    </>
  ),
}

interface SubjectIconProps {
  subject: SubjectKey
  className?: string
  strokeWidth?: number
}

export function SubjectIcon({ subject, className, strokeWidth = 1.6 }: SubjectIconProps) {
  return (
    <svg
      viewBox="0 0 24 24"
      className={cn('size-6', className)}
      fill="none"
      stroke="currentColor"
      strokeWidth={strokeWidth}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
      focusable="false"
    >
      {PATHS[subject]}
    </svg>
  )
}

/** Tinted square that houses a subject icon — used across cards and tiles. */
export function SubjectTile({
  subject,
  size = 'md',
  className,
}: {
  subject: SubjectKey
  size?: 'sm' | 'md' | 'lg' | 'xl'
  className?: string
}) {
  const { hue } = subjectByKey(subject)
  const sizes = {
    sm: { box: 'size-9 rounded-md', icon: 'size-[1.125rem]' },
    md: { box: 'size-11 rounded-lg', icon: 'size-5' },
    lg: { box: 'size-14 rounded-xl', icon: 'size-7' },
    xl: { box: 'size-16 rounded-xl', icon: 'size-8' },
  } as const

  return (
    <span
      className={cn(
        'grid shrink-0 place-items-center border transition-colors',
        // Hue comes from the subject; lightness flips with the theme so the
        // icon keeps its contrast on both the ivory and the ink ground.
        'border-[hsl(var(--subject-h)_55%_45%/0.25)] bg-[hsl(var(--subject-h)_60%_50%/0.12)]',
        'text-[hsl(var(--subject-h)_62%_32%)] dark:text-[hsl(var(--subject-h)_70%_68%)]',
        sizes[size].box,
        className,
      )}
      style={{ '--subject-h': hue } as React.CSSProperties}
    >
      <SubjectIcon subject={subject} className={sizes[size].icon} />
    </span>
  )
}

/** Dark-surface variant used on cover overlays where the tinted tile would mud. */
export function SubjectChip({ subject, className }: { subject: SubjectKey; className?: string }) {
  const s = subjectByKey(subject)
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-full border border-white/20 bg-white/12 px-2.5 py-1 text-caption font-semibold text-white backdrop-blur-sm',
        className,
      )}
    >
      <SubjectIcon subject={subject} className="size-3.5" strokeWidth={1.8} />
      {s.name}
    </span>
  )
}
