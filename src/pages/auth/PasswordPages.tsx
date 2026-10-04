import * as React from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, ArrowLeft, Check, Eye, EyeOff, MailCheck, ShieldX } from 'lucide-react'
import { motion } from 'framer-motion'
import { authApi } from '@/api/endpoints/auth'
import { AuthShell } from '@/components/layout/AuthShell'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useI18n } from '@/i18n'
import { applyServerErrors, STRONG_PASSWORD } from '@/lib/form'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

function FormAlert({ message }: { message: string }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: -6 }}
      animate={{ opacity: 1, y: 0 }}
      transition={transitions.fast}
      role="alert"
      className="flex items-start gap-2.5 rounded-lg border border-destructive/30 bg-destructive/8 p-3.5 text-body-sm text-destructive-strong"
    >
      <AlertCircle className="mt-0.5 size-4 shrink-0" aria-hidden />
      {message}
    </motion.div>
  )
}

/* -------------------------------------------------------------------------- */
/*                              Forgot password                               */
/* -------------------------------------------------------------------------- */

type ForgotValues = { email: string }

export function ForgotPasswordPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('auth.forgotTitle'))

  const { toast } = useToast()
  const [sentTo, setSentTo] = React.useState<string | null>(null)
  const [formError, setFormError] = React.useState<string | null>(null)

  const schema = React.useMemo(
    () =>
      z.object({
        email: z.string().min(1, t('validation.required')).email(t('validation.email')),
      }),
    [t],
  )

  const {
    register,
    handleSubmit,
    setError,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<ForgotValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: '' },
    mode: 'onBlur',
  })

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await authApi.forgotPassword({ email: values.email.trim() })
      setSentTo(values.email.trim())
    } catch (error) {
      setFormError(applyServerErrors<ForgotValues>(error, setError, ['email']))
    }
  })

  if (sentTo) {
    return (
      <AuthShell
        title={locale === 'ar-EG' ? 'بصّ في بريدك' : 'Check your inbox'}
        description={t('auth.forgotSent')}
        highlight="teacher"
      >
        <motion.div
          initial={{ opacity: 0, scale: 0.97 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={transitions.spring}
          className="rounded-2xl border border-success/25 bg-success/8 p-6 text-center"
        >
          <span className="mx-auto grid size-14 place-items-center rounded-full bg-success/15 text-success-strong">
            <MailCheck className="size-7" />
          </span>
          <p className="mt-5 break-all font-latin text-body-sm font-semibold text-foreground">
            {sentTo}
          </p>
          <p className="mt-2 text-body-sm leading-relaxed text-muted-foreground">
            {locale === 'ar-EG'
              ? 'لو مالقتش الرسالة، اتأكد من مجلد الـ Spam أو جرّب تبعت تاني بعد دقيقة.'
              : 'If it has not arrived, check your spam folder or try again in a minute.'}
          </p>
        </motion.div>

        <div className="mt-6 space-y-2.5">
          <Button
            variant="outline"
            size="lg"
            block
            onClick={async () => {
              await authApi.forgotPassword({ email: getValues('email').trim() }).catch(() => undefined)
              toast({ title: t('auth.forgotSent'), variant: 'success' })
            }}
          >
            {locale === 'ar-EG' ? 'ابعت اللينك تاني' : 'Resend link'}
          </Button>
          <Button asChild variant="ghost" size="lg" block>
            <Link to="/login">
              <ArrowLeft className="size-4 flip-rtl" />
              {t('nav.login')}
            </Link>
          </Button>
        </div>
      </AuthShell>
    )
  }

  return (
    <AuthShell
      title={t('auth.forgotTitle')}
      description={t('auth.forgotSubtitle')}
      highlight="teacher"
      footer={
        <p className="text-center text-body-sm text-muted-foreground">
          <Link to="/login" className="font-semibold text-primary hover:underline">
            {t('common.back')} — {t('nav.login')}
          </Link>
        </p>
      }
    >
      <form onSubmit={onSubmit} className="space-y-5" noValidate>
        {formError && <FormAlert message={formError} />}
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
        <Button type="submit" size="lg" block loading={isSubmitting}>
          {t('auth.forgotSubmit')}
        </Button>
      </form>
    </AuthShell>
  )
}

/* -------------------------------------------------------------------------- */
/*                               Reset password                               */
/* -------------------------------------------------------------------------- */

type ResetValues = { email: string; newPassword: string; confirmPassword: string }

export function ResetPasswordPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('auth.resetTitle'))

  const navigate = useNavigate()
  const { toast } = useToast()
  const [searchParams] = useSearchParams()
  const [showPassword, setShowPassword] = React.useState(false)
  const [formError, setFormError] = React.useState<string | null>(null)

  // Both values arrive in the emailed link.
  const token = searchParams.get('token') ?? ''
  const emailFromLink = searchParams.get('email') ?? ''

  const schema = React.useMemo(
    () =>
      z
        .object({
          email: z.string().min(1, t('validation.required')).email(t('validation.email')),
          newPassword: z.string().regex(STRONG_PASSWORD, t('validation.passwordWeak')),
          confirmPassword: z.string().min(1, t('validation.required')),
        })
        .refine((v) => v.newPassword === v.confirmPassword, {
          path: ['confirmPassword'],
          message: t('validation.passwordMismatch'),
        }),
    [t],
  )

  const {
    register,
    handleSubmit,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<ResetValues>({
    resolver: zodResolver(schema),
    defaultValues: { email: emailFromLink, newPassword: '', confirmPassword: '' },
    mode: 'onBlur',
  })

  const password = watch('newPassword') ?? ''
  const rules = [
    { label: locale === 'ar-EG' ? '٨ حروف على الأقل' : 'At least 8 characters', ok: password.length >= 8 },
    { label: locale === 'ar-EG' ? 'حرف إنجليزي' : 'A letter', ok: /[A-Za-z]/.test(password) },
    { label: locale === 'ar-EG' ? 'رقم' : 'A number', ok: /\d/.test(password) },
  ]

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      await authApi.resetPassword({
        email: values.email.trim(),
        token,
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      })
      toast({ title: t('auth.resetDone'), variant: 'success' })
      navigate('/login', { replace: true })
    } catch (error) {
      setFormError(
        applyServerErrors<ResetValues>(error, setError, [
          'email',
          'newPassword',
          'confirmPassword',
        ]),
      )
    }
  })

  if (!token) {
    return (
      <AuthShell
        title={t('auth.resetTitle')}
        description={t('auth.verifyEmailFailed')}
        highlight="teacher"
      >
        <div className="rounded-2xl border border-destructive/25 bg-destructive/8 p-6 text-center">
          <span className="mx-auto grid size-14 place-items-center rounded-full bg-destructive/15 text-destructive-strong">
            <ShieldX className="size-7" />
          </span>
          <p className="mt-5 text-body-sm leading-relaxed text-muted-foreground">
            {locale === 'ar-EG'
              ? 'افتح اللينك من رسالة البريد عشان نقدر نتأكد من هويتك.'
              : 'Open the link from your email so we can verify it is you.'}
          </p>
        </div>
        <Button asChild size="lg" block className="mt-6">
          <Link to="/forgot-password">{t('auth.forgotSubmit')}</Link>
        </Button>
      </AuthShell>
    )
  }

  return (
    <AuthShell
      title={t('auth.resetTitle')}
      description={t('auth.resetSubtitle')}
      highlight="teacher"
    >
      <form onSubmit={onSubmit} className="space-y-5" noValidate>
        {formError && <FormAlert message={formError} />}

        <Field label={t('auth.email')} htmlFor="reset-email" required error={errors.email?.message}>
          <Input
            id="reset-email"
            type="email"
            dir="ltr"
            className="text-start"
            readOnly={Boolean(emailFromLink)}
            invalid={!!errors.email}
            {...register('email')}
          />
        </Field>

        <Field
          label={t('auth.newPassword')}
          htmlFor="newPassword"
          required
          error={errors.newPassword?.message}
        >
          <div className="relative" dir="ltr">
            <Input
              id="newPassword"
              type={showPassword ? 'text' : 'password'}
              autoComplete="new-password"
              dir="ltr"
              className="pe-11 text-start"
              invalid={!!errors.newPassword}
              {...register('newPassword')}
            />
            <button
              type="button"
              onClick={() => setShowPassword((v) => !v)}
              aria-label={showPassword ? 'Hide password' : 'Show password'}
              className="absolute end-3 top-1/2 -translate-y-1/2 rounded-md p-1 text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
            </button>
          </div>
        </Field>

        {password.length > 0 && (
          <ul className="grid grid-cols-3 gap-1.5">
            {rules.map((rule) => (
              <li
                key={rule.label}
                className={cn(
                  'flex items-center gap-1.5 text-caption transition-colors',
                  rule.ok ? 'text-success-strong' : 'text-muted-foreground',
                )}
              >
                <Check className={cn('size-3.5', !rule.ok && 'opacity-30')} />
                {rule.label}
              </li>
            ))}
          </ul>
        )}

        <Field
          label={t('auth.confirmPassword')}
          htmlFor="confirmPassword"
          required
          error={errors.confirmPassword?.message}
        >
          <Input
            id="confirmPassword"
            type={showPassword ? 'text' : 'password'}
            autoComplete="new-password"
            dir="ltr"
            className="text-start"
            invalid={!!errors.confirmPassword}
            {...register('confirmPassword')}
          />
        </Field>

        <Button type="submit" size="lg" block loading={isSubmitting}>
          {t('auth.resetSubmit')}
        </Button>
      </form>
    </AuthShell>
  )
}

/* -------------------------------------------------------------------------- */
/*                              Verify email                                  */
/* -------------------------------------------------------------------------- */

export function VerifyEmailPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('auth.verifyEmailTitle'))

  const [searchParams] = useSearchParams()
  const { toast } = useToast()
  const token = searchParams.get('token') ?? ''
  const email = searchParams.get('email') ?? ''

  const [state, setState] = React.useState<'pending' | 'done' | 'failed'>(
    token && email ? 'pending' : 'failed',
  )
  const [resending, setResending] = React.useState(false)

  React.useEffect(() => {
    if (!token || !email) return
    let cancelled = false
    authApi
      .verifyEmail({ email, token })
      .then(() => !cancelled && setState('done'))
      .catch(() => !cancelled && setState('failed'))
    return () => {
      cancelled = true
    }
  }, [token, email])

  async function resend() {
    if (!email) return
    setResending(true)
    try {
      await authApi.resendVerification({ email })
      toast({ title: t('auth.verificationSent'), variant: 'success' })
    } catch {
      toast({ title: t('states.errorTitle'), variant: 'error' })
    } finally {
      setResending(false)
    }
  }

  return (
    <AuthShell
      title={t('auth.verifyEmailTitle')}
      description={
        state === 'pending'
          ? t('auth.verifyEmailPending')
          : state === 'done'
            ? t('auth.verifyEmailDone')
            : t('auth.verifyEmailFailed')
      }
      highlight="student"
    >
      <motion.div
        key={state}
        initial={{ opacity: 0, scale: 0.97 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={transitions.spring}
        className={cn(
          'rounded-2xl border p-6 text-center',
          state === 'done'
            ? 'border-success/25 bg-success/8'
            : state === 'failed'
              ? 'border-destructive/25 bg-destructive/8'
              : 'border-border bg-muted/30',
        )}
      >
        <span
          className={cn(
            'mx-auto grid size-14 place-items-center rounded-full',
            state === 'done'
              ? 'bg-success/15 text-success-strong'
              : state === 'failed'
                ? 'bg-destructive/15 text-destructive-strong'
                : 'bg-primary/12 text-primary',
          )}
        >
          {state === 'done' ? (
            <Check className="size-7" strokeWidth={3} />
          ) : state === 'failed' ? (
            <ShieldX className="size-7" />
          ) : (
            <MailCheck className="size-7 animate-pulse" />
          )}
        </span>
        {email && (
          <p className="mt-5 break-all font-latin text-body-sm font-semibold text-foreground">
            {email}
          </p>
        )}
      </motion.div>

      <div className="mt-6 space-y-2.5">
        {state === 'done' ? (
          <Button asChild size="lg" block>
            <Link to="/login">{t('nav.login')}</Link>
          </Button>
        ) : state === 'failed' && email ? (
          <Button size="lg" block loading={resending} onClick={resend}>
            {t('auth.resendVerification')}
          </Button>
        ) : null}
        <Button asChild variant="ghost" size="lg" block>
          <Link to="/">
            <ArrowLeft className="size-4 flip-rtl" />
            {locale === 'ar-EG' ? 'رجوع للموقع' : 'Back to site'}
          </Link>
        </Button>
      </div>
    </AuthShell>
  )
}
