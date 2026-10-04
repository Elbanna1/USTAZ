import { Languages } from 'lucide-react'
import { motion } from 'framer-motion'
import { authApi } from '@/api/endpoints/auth'
import { useAuth } from '@/hooks/use-auth'
import { LOCALES, useI18n, type Locale } from '@/i18n'
import { cn } from '@/lib/utils'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'

/**
 * Switches the interface language and, for signed-in users, persists the choice
 * on their account so the API also answers in the right language next session.
 */
export function LanguageToggle({
  className,
  variant = 'icon',
}: {
  className?: string
  variant?: 'icon' | 'inline'
}) {
  const { locale, setLocale, t } = useI18n()
  const { isAuthenticated, refreshUser } = useAuth()

  async function choose(next: Locale) {
    if (next === locale) return
    setLocale(next)
    if (isAuthenticated) {
      // Best effort — the UI has already switched either way.
      await authApi.setLanguage({ language: next }).catch(() => undefined)
      await refreshUser()
    }
  }

  if (variant === 'inline') {
    return (
      <div
        className={cn(
          'inline-flex items-center rounded-lg border border-border bg-card p-1',
          className,
        )}
        role="group"
        aria-label={t('nav.language')}
      >
        {LOCALES.map((option) => (
          <button
            key={option.value}
            type="button"
            onClick={() => choose(option.value)}
            aria-pressed={locale === option.value}
            className={cn(
              'relative rounded-md px-3 py-1.5 font-display text-caption font-bold transition-colors',
              locale === option.value
                ? 'text-primary-foreground'
                : 'text-muted-foreground hover:text-foreground',
            )}
          >
            {locale === option.value && (
              <motion.span
                layoutId="language-toggle-active"
                className="absolute inset-0 rounded-md bg-primary"
                transition={{ type: 'spring', stiffness: 400, damping: 32 }}
              />
            )}
            <span className="relative">{option.nativeLabel}</span>
          </button>
        ))}
      </div>
    )
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={t('nav.language')}
          title={t('nav.language')}
          className={cn(
            'grid size-10 place-items-center rounded-lg border border-border bg-card text-foreground transition-colors',
            'hover:border-foreground/20 hover:bg-muted',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:ring-offset-background',
            className,
          )}
        >
          <Languages className="size-[1.125rem]" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-44">
        {LOCALES.map((option) => (
          <DropdownMenuItem
            key={option.value}
            onSelect={() => void choose(option.value)}
            className={cn(locale === option.value && 'font-bold text-primary')}
          >
            <span className="flex-1">{option.nativeLabel}</span>
            <span className="text-caption text-muted-foreground">{option.label}</span>
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  )
}
