import { clsx, type ClassValue } from 'clsx'
import { extendTailwindMerge } from 'tailwind-merge'
import { format, formatDistanceToNowStrict, isToday, isTomorrow, parseISO } from 'date-fns'
import { ar } from 'date-fns/locale'
import type { DayKey, TeachingMode } from '@/types'

/**
 * tailwind-merge only knows the stock scales. Our theme replaces the font-size
 * ramp with semantic names (`text-body`, `text-heading-md`, ...), and the stock
 * resolver cannot tell `text-body-sm` (a size) from `text-primary-foreground`
 * (a colour) — so it treated them as the same conflict group and dropped
 * whichever came first. That silently erased button and label colours all over
 * the app. Teaching it our scales keeps size and colour in separate groups.
 */
const twMerge = extendTailwindMerge({
  extend: {
    classGroups: {
      'font-size': [
        {
          text: [
            'display-2xl',
            'display-xl',
            'display-lg',
            'heading-lg',
            'heading-md',
            'heading-sm',
            'body-lg',
            'body',
            'body-sm',
            'caption',
            'label',
            'price',
          ],
        },
      ],
      'font-family': [{ font: ['display', 'sans', 'latin'] }],
      /* Our shadow ramp is named, not t-shirt sized, so the stock group misses it. */
      shadow: [{ shadow: ['xs', 'soft', 'card', 'lift', 'glow', 'insetTop'] }],
    },
    theme: {
      colors: [
        'surface',
        'surface-foreground',
        'primary-soft',
        'accent-soft',
        'accent-strong',
        'whatsapp',
        'whatsapp-dark',
        'whatsapp-ink',
        'success',
        'success-foreground',
        'success-strong',
        'warning',
        'warning-foreground',
        'warning-strong',
        'destructive-strong',
      ],
      borderRadius: ['xs'],
    },
  },
})

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

/* -------------------------------------------------------------------------- */
/*                                   Money                                     */
/* -------------------------------------------------------------------------- */

/** Egyptian Pound, Western numerals, no fraction digits — how prices are read locally. */
export function formatEGP(amount: number, opts: { withSymbol?: boolean } = {}): string {
  const { withSymbol = true } = opts
  const n = new Intl.NumberFormat('en-EG', {
    maximumFractionDigits: 0,
    minimumFractionDigits: 0,
  }).format(Math.round(amount))
  return withSymbol ? `${n} ج.م` : n
}

export function formatCompactNumber(n: number): string {
  if (n >= 1000) {
    const v = n / 1000
    return `${v % 1 === 0 ? v : v.toFixed(1)}K`
  }
  return String(n)
}

export function formatNumber(n: number): string {
  return new Intl.NumberFormat('en-EG').format(n)
}

/* -------------------------------------------------------------------------- */
/*                                Dates & times                                */
/* -------------------------------------------------------------------------- */

const DAY_LABELS: Record<DayKey, { long: string; short: string }> = {
  sat: { long: 'السبت', short: 'سبت' },
  sun: { long: 'الأحد', short: 'أحد' },
  mon: { long: 'الإثنين', short: 'إثنين' },
  tue: { long: 'الثلاثاء', short: 'ثلاثاء' },
  wed: { long: 'الأربعاء', short: 'أربعاء' },
  thu: { long: 'الخميس', short: 'خميس' },
  fri: { long: 'الجمعة', short: 'جمعة' },
}

export const WEEK_ORDER: DayKey[] = ['sat', 'sun', 'mon', 'tue', 'wed', 'thu', 'fri']

export function dayLabel(day: DayKey, variant: 'long' | 'short' = 'long'): string {
  return DAY_LABELS[day][variant]
}

export function formatDays(days: DayKey[]): string {
  const ordered = WEEK_ORDER.filter((d) => days.includes(d))
  return ordered.map((d) => dayLabel(d, 'short')).join(' · ')
}

/** "16:00" -> "٤:٠٠ م" style, but with Western numerals: "4:00 م". */
export function formatTime(time24: string): string {
  const [hStr, mStr] = time24.split(':')
  const h = Number(hStr)
  const suffix = h >= 12 ? 'م' : 'ص'
  const h12 = h % 12 === 0 ? 12 : h % 12
  return `${h12}:${mStr} ${suffix}`
}

export function formatTimeRange(start: string, end: string): string {
  return `${formatTime(start)} — ${formatTime(end)}`
}

export function toDate(value: string | Date): Date {
  return typeof value === 'string' ? parseISO(value) : value
}

export function formatDate(value: string | Date, pattern = 'd MMMM yyyy'): string {
  return format(toDate(value), pattern, { locale: ar })
}

export function formatShortDate(value: string | Date): string {
  return format(toDate(value), 'd MMM', { locale: ar })
}

/** "النهارده" / "بكرة" / "الخميس 12 ديسمبر" */
export function formatRelativeDay(value: string | Date): string {
  const d = toDate(value)
  if (isToday(d)) return 'النهارده'
  if (isTomorrow(d)) return 'بكرة'
  return format(d, 'EEEE d MMMM', { locale: ar })
}

export function formatTimeAgo(value: string | Date): string {
  return `من ${formatDistanceToNowStrict(toDate(value), { locale: ar })}`
}

export function formatDuration(minutes: number): string {
  const h = Math.floor(minutes / 60)
  const m = minutes % 60
  if (h && m) return `${h} س ${m} د`
  if (h) return `${h} ساعة`
  return `${m} دقيقة`
}

export function formatHours(hours: number): string {
  return hours % 1 === 0 ? `${hours} ساعة` : `${hours.toFixed(1)} ساعة`
}

/* -------------------------------------------------------------------------- */
/*                              Domain label maps                              */
/* -------------------------------------------------------------------------- */

export const MODE_LABELS: Record<TeachingMode, string> = {
  online: 'أونلاين',
  offline: 'حضوري',
  hybrid: 'أونلاين وحضوري',
}

export function modeLabel(mode: TeachingMode): string {
  return MODE_LABELS[mode]
}

/* -------------------------------------------------------------------------- */
/*                                   Misc                                      */
/* -------------------------------------------------------------------------- */

/** Stable 32-bit hash — used to make generated artwork deterministic per seed. */
export function hashSeed(seed: string): number {
  let h = 2166136261
  for (let i = 0; i < seed.length; i++) {
    h ^= seed.charCodeAt(i)
    h = Math.imul(h, 16777619)
  }
  return Math.abs(h)
}

/** Deterministic pseudo-random in [0,1) derived from a seed + index. */
export function seededRandom(seed: string, index = 0): number {
  const h = hashSeed(`${seed}:${index}`)
  return (h % 100000) / 100000
}

export function pickFromSeed<T>(items: readonly T[], seed: string, index = 0): T {
  return items[Math.floor(seededRandom(seed, index) * items.length)]
}

export function initials(name: string): string {
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((w) => w[0])
    .join('')
}

export function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max)
}

export function percent(part: number, whole: number): number {
  if (!whole) return 0
  return clamp(Math.round((part / whole) * 100), 0, 100)
}

export function slugify(value: string): string {
  return value
    .trim()
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, '-')
    .replace(/^-+|-+$/g, '')
}

/** Normalises Arabic text so search matches regardless of hamza/taa-marbuta form. */
export function normalizeArabic(value: string): string {
  return value
    .replace(/[ً-ٰٟ]/g, '')
    .replace(/[أإآٱ]/g, 'ا')
    .replace(/ى/g, 'ي')
    .replace(/ة/g, 'ه')
    .replace(/ؤ/g, 'و')
    .replace(/ئ/g, 'ي')
    .replace(/ـ/g, '')
    .toLowerCase()
    .trim()
}

export function matchesQuery(haystack: string[], query: string): boolean {
  const q = normalizeArabic(query)
  if (!q) return true
  return haystack.some((h) => normalizeArabic(h).includes(q))
}

export function unique<T>(items: T[]): T[] {
  return Array.from(new Set(items))
}

export function range(n: number): number[] {
  return Array.from({ length: n }, (_, i) => i)
}

export function toggleInArray<T>(arr: T[], value: T): T[] {
  return arr.includes(value) ? arr.filter((v) => v !== value) : [...arr, value]
}

export async function copyToClipboard(text: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(text)
    return true
  } catch {
    // Older browsers / insecure contexts — fall back to a hidden textarea.
    try {
      const el = document.createElement('textarea')
      el.value = text
      el.setAttribute('readonly', '')
      el.style.position = 'fixed'
      el.style.opacity = '0'
      document.body.appendChild(el)
      el.select()
      const ok = document.execCommand('copy')
      document.body.removeChild(el)
      return ok
    } catch {
      return false
    }
  }
}
