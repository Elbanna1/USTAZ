import { useEffect } from 'react'
import { SITE } from '@/lib/constants'
import { useI18n } from '@/i18n'

/**
 * Sets document.title per route. A tiny stand-in for a head manager — enough
 * for correct tab titles and browser history without another dependency.
 */
export function usePageTitle(title?: string, description?: string): void {
  const { locale } = useI18n()
  // The suffix follows the reader's language, not the brand's original script.
  const siteName = locale === 'en' ? SITE.nameEn : SITE.nameAr

  useEffect(() => {
    const previous = document.title
    document.title = title ? (title.includes(siteName) ? title : `${title} · ${siteName}`) : siteName

    let metaRestore: (() => void) | undefined
    if (description) {
      const meta = document.querySelector('meta[name="description"]')
      if (meta) {
        const prev = meta.getAttribute('content') ?? ''
        meta.setAttribute('content', description)
        metaRestore = () => meta.setAttribute('content', prev)
      }
    }

    return () => {
      document.title = previous
      metaRestore?.()
    }
  }, [title, description, siteName])
}
