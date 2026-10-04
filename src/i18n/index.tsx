import * as React from 'react'
import { format, formatDistanceToNowStrict, isToday, isTomorrow, parseISO } from 'date-fns'
import { ar as arLocale, enGB } from 'date-fns/locale'
import { STORAGE } from '@/api/config'
import { ar, type Dictionary } from '@/i18n/locales/ar'
import { en } from '@/i18n/locales/en'
import { DEFAULT_LOCALE, directionFor, type Direction, type Locale } from '@/i18n/types'

type Leaves<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string
    ? `${Prefix}${K}`
    : Leaves<T[K], `${Prefix}${K}.`>
}[keyof T & string]

export type TranslationKey = Leaves<Dictionary>

const DICTIONARIES: Record<Locale, Dictionary> = { 'ar-EG': ar, en }

interface I18nValue {
  locale: Locale
  dir: Direction
  isRtl: boolean
  setLocale: (locale: Locale) => void
  /** Look up a key; `{placeholders}` in the string are replaced from `vars`. */
  t: (key: TranslationKey, vars?: Record<string, string | number>) => string
  /** Picks the Arabic or English field off an API DTO. */
  pick: (arValue?: string | null, enValue?: string | null) => string
  formatNumber: (value: number, options?: Intl.NumberFormatOptions) => string
  formatCurrency: (value: number, currency?: string) => string
  formatDate: (value: string | Date, pattern?: string) => string
  formatDateTime: (value: string | Date) => string
  formatRelativeDay: (value: string | Date) => string
  formatTimeAgo: (value: string | Date) => string
  formatTime: (value: string) => string
}

const I18nContext = React.createContext<I18nValue | null>(null)

function readInitialLocale(): Locale {
  if (typeof window === 'undefined') return DEFAULT_LOCALE
  try {
    const stored = localStorage.getItem(STORAGE.locale)
    if (stored === 'en' || stored === 'ar-EG') return stored
  } catch {
    /* storage blocked */
  }
  return DEFAULT_LOCALE
}

function lookup(dict: Dictionary, key: string): string | undefined {
  let node: unknown = dict
  for (const part of key.split('.')) {
    if (node === null || typeof node !== 'object') return undefined
    node = (node as Record<string, unknown>)[part]
  }
  return typeof node === 'string' ? node : undefined
}

function interpolate(template: string, vars?: Record<string, string | number>): string {
  if (!vars) return template
  return template.replace(/\{(\w+)\}/g, (match, name: string) =>
    name in vars ? String(vars[name]) : match,
  )
}

/** Arabic label if we have one, English as the fallback — never an empty cell. */
function pickLocalized(locale: Locale, arValue?: string | null, enValue?: string | null): string {
  const primary = locale === 'ar-EG' ? arValue : enValue
  return (primary || arValue || enValue || '').trim()
}

export function I18nProvider({ children }: { children: React.ReactNode }) {
  const [locale, setLocaleState] = React.useState<Locale>(readInitialLocale)
  const dir = directionFor(locale)

  React.useEffect(() => {
    const root = document.documentElement
    root.lang = locale === 'ar-EG' ? 'ar' : 'en'
    root.dir = dir
    try {
      localStorage.setItem(STORAGE.locale, locale)
    } catch {
      /* storage blocked — direction is still applied for this session */
    }
  }, [locale, dir])

  const value = React.useMemo<I18nValue>(() => {
    const dict = DICTIONARIES[locale]

    const dateLocale = locale === 'ar-EG' ? arLocale : enGB

    const toDate = (input: string | Date) =>
      typeof input === 'string' ? parseISO(input) : input

    return {
      locale,
      dir,
      isRtl: dir === 'rtl',
      setLocale: setLocaleState,

      t: (key, vars) => {
        const raw = lookup(dict, key) ?? lookup(ar, key)
        if (raw === undefined) {
          if (import.meta.env.DEV) console.warn(`[i18n] missing key: ${key}`)
          return key
        }
        return interpolate(raw, vars)
      },

      pick: (arValue, enValue) => pickLocalized(locale, arValue, enValue),

      // Western-Arabic digits throughout — how prices and dates are read in Egypt.
      formatNumber: (n, options) =>
        new Intl.NumberFormat(locale === 'ar-EG' ? 'en-EG' : 'en-GB', options).format(n),

      formatCurrency: (n, currency = 'EGP') => {
        const amount = new Intl.NumberFormat(locale === 'ar-EG' ? 'en-EG' : 'en-GB', {
          maximumFractionDigits: 0,
        }).format(Math.round(n))
        if (currency !== 'EGP') return `${amount} ${currency}`
        return locale === 'ar-EG' ? `${amount} ج.م` : `EGP ${amount}`
      },

      formatDate: (input, pattern = 'd MMMM yyyy') =>
        format(toDate(input), pattern, { locale: dateLocale }),

      formatDateTime: (input) =>
        format(toDate(input), 'd MMM yyyy · h:mm a', { locale: dateLocale }),

      formatRelativeDay: (input) => {
        const d = toDate(input)
        if (isToday(d)) return locale === 'ar-EG' ? 'النهارده' : 'Today'
        if (isTomorrow(d)) return locale === 'ar-EG' ? 'بكرة' : 'Tomorrow'
        return format(d, 'EEEE d MMMM', { locale: dateLocale })
      },

      formatTimeAgo: (input) => {
        const distance = formatDistanceToNowStrict(toDate(input), { locale: dateLocale })
        return locale === 'ar-EG' ? `من ${distance}` : `${distance} ago`
      },

      /** Accepts "16:00" or "16:00:00" from the API. */
      formatTime: (time24) => {
        const [hStr, mStr = '00'] = time24.split(':')
        const h = Number(hStr)
        if (Number.isNaN(h)) return time24
        const h12 = h % 12 === 0 ? 12 : h % 12
        const suffix =
          locale === 'ar-EG' ? (h >= 12 ? 'م' : 'ص') : h >= 12 ? 'PM' : 'AM'
        return `${h12}:${mStr.padStart(2, '0')} ${suffix}`
      },
    }
  }, [locale, dir])

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useI18n(): I18nValue {
  const ctx = React.useContext(I18nContext)
  if (!ctx) throw new Error('useI18n must be used inside <I18nProvider>')
  return ctx
}

/** Shorthand for components that only need the translate function. */
export function useT(): I18nValue['t'] {
  return useI18n().t
}

export { DEFAULT_LOCALE, LOCALES, directionFor, type Direction, type Locale } from '@/i18n/types'
