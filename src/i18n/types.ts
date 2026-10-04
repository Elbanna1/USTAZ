export type Locale = 'ar-EG' | 'en'

export type Direction = 'rtl' | 'ltr'

export const LOCALES: { value: Locale; label: string; nativeLabel: string; dir: Direction }[] = [
  { value: 'ar-EG', label: 'Arabic', nativeLabel: 'العربية', dir: 'rtl' },
  { value: 'en', label: 'English', nativeLabel: 'English', dir: 'ltr' },
]

export const DEFAULT_LOCALE: Locale = 'ar-EG'

export function directionFor(locale: Locale): Direction {
  return locale === 'ar-EG' ? 'rtl' : 'ltr'
}

/** Maps our locale onto the tag `Intl` and `date-fns` expect. */
export function intlTag(locale: Locale): string {
  return locale === 'ar-EG' ? 'ar-EG' : 'en-GB'
}
