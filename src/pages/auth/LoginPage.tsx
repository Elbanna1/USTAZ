import * as React from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Eye, EyeOff } from 'lucide-react'
import { motion } from 'framer-motion'
import { AuthShell } from '@/components/layout/AuthShell'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useAuth, rolesForUser, ROLE_HOME } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { applyServerErrors } from '@/lib/form'
import { transitions } from '@/lib/motion'

type FormValues = { email: string; password: string }

export default function LoginPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('nav.login'))

  const navigate = useNavigate()
  const location = useLocation()
  const { toast } = useToast()
  const { login } = useAuth()
  const [showPassword, setShowPassword] = React.useState(false)
  const [formError, setFormError] = React.useState<string | null>(null)

  const schema = React.useMemo(
    () =>
      z.object({
        email: z.string().min(1, t('validation.required')).email(t('validation.email')),
        password: z.string().min(1, t('validation.required')),
      }),
    [t],
  )

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '', password: '' },
    mode: 'onBlur',
  })

  /** Where the guard bounced them from, if anywhere. */
  const redirectTo = (location.state as { from?: string } | null)?.from

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const user = await login({ email: values.email.trim(), password: values.password })
      const role = rolesForUser(user)[0]
      toast({
        title: t('auth.welcomeBack', { name: user.displayName ?? user.firstName ?? '' }),
        variant: 'success',
      })
      navigate(redirectTo ?? (role ? ROLE_HOME[role] : '/'), { replace: true })
    } catch (error) {
      setFormError(applyServerErrors<FormValues>(error, setError, ['email', 'password']))
    }
  })

  return (
    <AuthShell
      title={t('auth.loginTitle')}
      description={t('auth.loginSubtitle')}
      highlight="teacher"
      footer={
        <p className="text-center text-body-sm text-muted-foreground">
          {t('auth.noAccount')}{' '}
          <Link to="/register" className="font-semibold text-primary hover:underline">
            {t('nav.register')}
          </Link>
        </p>
      }
    >
      <form onSubmit={onSubmit} className="space-y-5" noValidate>
        {formError && (
          <motion.div
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            transition={transitions.fast}
            role="alert"
            className="flex items-start gap-2.5 rounded-lg border border-destructive/30 bg-destructive/8 p-3.5 text-body-sm text-destructive-strong"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden />
            {formError}
          </motion.div>
        )}

        <Field label={t('auth.email')} htmlFor="email" required error={errors.email?.message}>
          <Input
            id="email"
            type="email"
            autoComplete="email"
            dir="ltr"
            className="text-start"
            placeholder="name@example.com"
            invalid={!!errors.email}
            {...register('email')}
          />
        </Field>

        <Field
          label={t('auth.password')}
          htmlFor="password"
          required
          error={errors.password?.message}
        >
          <div className="relative" dir="ltr">
            <Input
              id="password"
              type={showPassword ? 'text' : 'password'}
              autoComplete="current-password"
              dir="ltr"
              className="pe-11 text-start"
              placeholder="••••••••"
              invalid={!!errors.password}
              {...register('password')}
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              aria-label={
                showPassword
                  ? locale === 'ar-EG'
                    ? 'إخفاء كلمة السر'
                    : 'Hide password'
                  : locale === 'ar-EG'
                    ? 'إظهار كلمة السر'
                    : 'Show password'
              }
              className="absolute end-3 top-1/2 -translate-y-1/2 rounded-md p-1 text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </div>
        </Field>

        <div className="flex items-center justify-end">
          <Link
            to="/forgot-password"
            className="text-body-sm font-semibold text-primary hover:underline"
          >
            {t('auth.forgotPassword')}
          </Link>
        </div>

        <Button type="submit" size="lg" block loading={isSubmitting}>
          {isSubmitting ? t('auth.loggingIn') : t('auth.submitLogin')}
        </Button>
      </form>
    </AuthShell>
  )
}
