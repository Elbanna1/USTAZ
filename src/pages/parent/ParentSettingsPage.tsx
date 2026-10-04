import * as React from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { AlertCircle, KeyRound, Palette, User } from 'lucide-react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { parentApi } from '@/api/endpoints/parent'
import { authApi } from '@/api/endpoints/auth'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useTheme } from '@/hooks/use-theme'
import { useI18n } from '@/i18n'
import { applyServerErrors, EG_MOBILE, STRONG_PASSWORD, normalizeEgyptianMobile } from '@/lib/form'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Switch } from '@/components/ui/primitives'
import { LanguageToggle } from '@/components/layout/LanguageToggle'
import { ProfilePictureField } from '@/components/common/ProfilePictureField'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'

interface ProfileValues {
  firstName: string
  lastName: string
  phoneNumber: string
  alternatePhoneNumber: string
  city: string
  occupation: string
}

export default function ParentSettingsPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('nav.settings'))

  return (
    <>
      <DashboardHeader
        title={t('nav.settings')}
        description={
          locale === 'ar-EG'
            ? 'بياناتك وتفضيلاتك وأمان حسابك.'
            : 'Your details, preferences, and account security.'
        }
      />

      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">
            <User className="size-4" />
            {t('nav.profile')}
          </TabsTrigger>
          <TabsTrigger value="preferences">
            <Palette className="size-4" />
            {locale === 'ar-EG' ? 'التفضيلات' : 'Preferences'}
          </TabsTrigger>
          <TabsTrigger value="security">
            <KeyRound className="size-4" />
            {locale === 'ar-EG' ? 'الأمان' : 'Security'}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="profile" className="mt-6">
          <ProfilePanel />
        </TabsContent>
        <TabsContent value="preferences" className="mt-6">
          <PreferencesPanel />
        </TabsContent>
        <TabsContent value="security" className="mt-6">
          <SecurityPanel />
        </TabsContent>
      </Tabs>
    </>
  )
}

function ProfilePanel() {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const { user, refreshUser } = useAuth()
  const queryClient = useQueryClient()
  const [formError, setFormError] = React.useState<string | null>(null)

  const profile = useQuery({ queryKey: qk.parent.profile, queryFn: parentApi.profile })

  const schema = React.useMemo(
    () =>
      z.object({
        firstName: z.string().min(2, t('validation.required')),
        lastName: z.string().min(2, t('validation.required')),
        phoneNumber: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        alternatePhoneNumber: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        city: z.string().optional().default(''),
        occupation: z.string().optional().default(''),
      }),
    [t],
  )

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm<ProfileValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      firstName: '',
      lastName: '',
      phoneNumber: '',
      alternatePhoneNumber: '',
      city: '',
      occupation: '',
    },
  })

  React.useEffect(() => {
    if (!profile.data) return
    const [firstName = '', ...rest] = (profile.data.displayName ?? '').split(' ')
    reset({
      firstName: user?.firstName ?? firstName,
      lastName: user?.lastName ?? rest.join(' '),
      phoneNumber: profile.data.phoneNumber ?? '',
      alternatePhoneNumber: profile.data.alternatePhoneNumber ?? '',
      city: profile.data.city ?? '',
      occupation: profile.data.occupation ?? '',
    })
  }, [profile.data, user, reset])

  const save = useMutation({
    mutationFn: (values: ProfileValues) =>
      parentApi.updateProfile({
        firstName: values.firstName.trim(),
        lastName: values.lastName.trim(),
        phoneNumber: values.phoneNumber ? normalizeEgyptianMobile(values.phoneNumber) : null,
        alternatePhoneNumber: values.alternatePhoneNumber
          ? normalizeEgyptianMobile(values.alternatePhoneNumber)
          : null,
        city: values.city.trim() || null,
        occupation: values.occupation.trim() || null,
      }),
    onSuccess: async () => {
      queryClient.invalidateQueries({ queryKey: qk.parent.profile })
      await refreshUser()
      setFormError(null)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<ProfileValues>(error, setError, [
          'firstName',
          'lastName',
          'phoneNumber',
          'alternatePhoneNumber',
          'city',
          'occupation',
        ]),
      ),
  })

  if (profile.isPending) return <RowSkeleton count={4} />
  if (profile.isError) return <QueryError error={profile.error} onRetry={() => profile.refetch()} />

  return (
    <Card className="p-6">
      <ProfilePictureField
        className="mb-6 border-b border-border pb-6"
        invalidate={[qk.parent.profile]}
      >
        <h2 className="font-display text-heading-sm font-bold text-foreground">
          {profile.data?.displayName}
        </h2>
        <p className="mt-0.5 text-body-sm text-muted-foreground">{profile.data?.email}</p>
      </ProfilePictureField>

      <form
        onSubmit={handleSubmit((values) => save.mutate(values))}
        className="space-y-5"
        noValidate
      >
        {formError && (
          <div
            role="alert"
            className="flex items-start gap-2.5 rounded-lg border border-destructive/30 bg-destructive/8 p-3.5 text-body-sm text-destructive-strong"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            {formError}
          </div>
        )}

        <div className="grid gap-5 sm:grid-cols-2">
          <Field
            label={t('auth.firstName')}
            htmlFor="p-firstName"
            required
            error={errors.firstName?.message}
          >
            <Input id="p-firstName" invalid={!!errors.firstName} {...register('firstName')} />
          </Field>
          <Field
            label={t('auth.lastName')}
            htmlFor="p-lastName"
            required
            error={errors.lastName?.message}
          >
            <Input id="p-lastName" invalid={!!errors.lastName} {...register('lastName')} />
          </Field>
        </div>

        <Field label={t('auth.email')} htmlFor="p-email">
          <Input
            id="p-email"
            readOnly
            dir="ltr"
            className="text-start"
            value={profile.data?.email ?? ''}
          />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t('auth.phone')} htmlFor="p-phone" error={errors.phoneNumber?.message}>
            <Input
              id="p-phone"
              type="tel"
              dir="ltr"
              className="text-start"
              invalid={!!errors.phoneNumber}
              {...register('phoneNumber')}
            />
          </Field>
          <Field
            label={locale === 'ar-EG' ? 'رقم بديل' : 'Alternate phone'}
            htmlFor="p-alt"
            error={errors.alternatePhoneNumber?.message}
          >
            <Input
              id="p-alt"
              type="tel"
              dir="ltr"
              className="text-start"
              invalid={!!errors.alternatePhoneNumber}
              {...register('alternatePhoneNumber')}
            />
          </Field>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t('auth.city')} htmlFor="p-city">
            <Input id="p-city" {...register('city')} />
          </Field>
          <Field label={locale === 'ar-EG' ? 'الوظيفة' : 'Occupation'} htmlFor="p-occupation">
            <Input id="p-occupation" {...register('occupation')} />
          </Field>
        </div>

        <Button type="submit" size="lg" loading={save.isPending} disabled={!isDirty}>
          {t('common.save')}
        </Button>
      </form>
    </Card>
  )
}

function PreferencesPanel() {
  const { t, locale } = useI18n()
  const { theme, setTheme } = useTheme()

  return (
    <Card className="divide-y divide-border p-0">
      <div className="flex flex-wrap items-center justify-between gap-4 p-5">
        <div>
          <p className="font-display text-body-sm font-bold text-foreground">{t('nav.language')}</p>
          <p className="mt-0.5 text-caption text-muted-foreground">
            {locale === 'ar-EG'
              ? 'بتغيّر لغة الواجهة والرسائل اللي بتوصلك.'
              : 'Changes the interface and the notifications you receive.'}
          </p>
        </div>
        <LanguageToggle variant="inline" />
      </div>

      <div className="flex flex-wrap items-center justify-between gap-4 p-5">
        <div>
          <p className="font-display text-body-sm font-bold text-foreground">{t('nav.theme')}</p>
        </div>
        <div className="flex items-center gap-2.5">
          <span className="text-caption text-muted-foreground">
            {locale === 'ar-EG' ? 'ليلي' : 'Dark'}
          </span>
          <Switch
            checked={theme === 'dark'}
            onCheckedChange={(checked) => setTheme(checked ? 'dark' : 'light')}
            aria-label={t('nav.theme')}
          />
        </div>
      </div>
    </Card>
  )
}

interface PasswordValues {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

function SecurityPanel() {
  const { t } = useI18n()
  const { toast } = useToast()
  const [formError, setFormError] = React.useState<string | null>(null)

  const schema = React.useMemo(
    () =>
      z
        .object({
          currentPassword: z.string().min(1, t('validation.required')),
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
    reset,
    setError,
    formState: { errors },
  } = useForm<PasswordValues>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  })

  const change = useMutation({
    mutationFn: (values: PasswordValues) => authApi.changePassword(values),
    onSuccess: () => {
      reset()
      setFormError(null)
      toast({ title: t('auth.passwordChanged'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<PasswordValues>(error, setError, [
          'currentPassword',
          'newPassword',
          'confirmPassword',
        ]),
      ),
  })

  return (
    <Card className="p-6">
      <h2 className="font-display text-heading-sm text-foreground">{t('auth.changePassword')}</h2>

      <form
        onSubmit={handleSubmit((values) => change.mutate(values))}
        className="mt-5 max-w-md space-y-5"
        noValidate
      >
        {formError && (
          <div
            role="alert"
            className="flex items-start gap-2.5 rounded-lg border border-destructive/30 bg-destructive/8 p-3.5 text-body-sm text-destructive-strong"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            {formError}
          </div>
        )}

        <Field
          label={t('auth.currentPassword')}
          htmlFor="pp-current"
          required
          error={errors.currentPassword?.message}
        >
          <Input
            id="pp-current"
            type="password"
            dir="ltr"
            className="text-start"
            {...register('currentPassword')}
          />
        </Field>
        <Field
          label={t('auth.newPassword')}
          htmlFor="pp-new"
          required
          error={errors.newPassword?.message}
        >
          <Input
            id="pp-new"
            type="password"
            dir="ltr"
            className="text-start"
            {...register('newPassword')}
          />
        </Field>
        <Field
          label={t('auth.confirmPassword')}
          htmlFor="pp-confirm"
          required
          error={errors.confirmPassword?.message}
        >
          <Input
            id="pp-confirm"
            type="password"
            dir="ltr"
            className="text-start"
            {...register('confirmPassword')}
          />
        </Field>

        <Button type="submit" loading={change.isPending}>
          {t('auth.changePassword')}
        </Button>
      </form>
    </Card>
  )
}
