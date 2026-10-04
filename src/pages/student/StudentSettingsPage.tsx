import * as React from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Check, KeyRound, MailCheck, User, UserCheck, X } from 'lucide-react'
import { motion } from 'framer-motion'
import { studentApi } from '@/api/endpoints/student'
import type { GuardianDto } from '@/api/schema'
import { authApi } from '@/api/endpoints/auth'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useGrades } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { applyServerErrors, EG_MOBILE, STRONG_PASSWORD, normalizeEgyptianMobile } from '@/lib/form'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { LanguageToggle } from '@/components/layout/LanguageToggle'
import { NoticeBanner } from '@/components/common/states'
import { ProfilePictureField } from '@/components/common/ProfilePictureField'
import { EmptyState } from '@/components/common/states'
import { StatusBadge } from '@/components/common/StatusBadge'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { Badge } from '@/components/ui/badge'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import { useTheme } from '@/hooks/use-theme'
import { Switch } from '@/components/ui/primitives'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function StudentSettingsPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('nav.settings'))

  return (
    <>
      <DashboardHeader
        title={t('nav.settings')}
        description={
          locale === 'ar-EG'
            ? 'بياناتك الشخصية وتفضيلاتك وأمان حسابك.'
            : 'Your profile, preferences, and account security.'
        }
      />

      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">
            <User className="size-4" />
            {t('nav.profile')}
          </TabsTrigger>
          <TabsTrigger value="guardians">
            <UserCheck className="size-4" />
            {locale === 'ar-EG' ? 'أولياء الأمر' : 'Guardians'}
          </TabsTrigger>
          <TabsTrigger value="preferences">{locale === 'ar-EG' ? 'التفضيلات' : 'Preferences'}</TabsTrigger>
          <TabsTrigger value="security">
            <KeyRound className="size-4" />
            {locale === 'ar-EG' ? 'الأمان' : 'Security'}
          </TabsTrigger>
        </TabsList>

        <TabsContent value="profile" className="mt-6">
          <ProfileForm />
        </TabsContent>
        <TabsContent value="guardians" className="mt-6">
          <GuardiansPanel />
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

/* -------------------------------------------------------------------------- */
/*                                   Profile                                  */
/* -------------------------------------------------------------------------- */

interface ProfileValues {
  firstName: string
  lastName: string
  phoneNumber: string
  parentPhoneNumber: string
  schoolName: string
  city: string
  gradeId: string
  dateOfBirth: string
}

function ProfileForm() {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const { refreshUser } = useAuth()
  const queryClient = useQueryClient()
  const grades = useGrades()
  const [formError, setFormError] = React.useState<string | null>(null)

  const profile = useQuery({
    queryKey: qk.student.profile,
    queryFn: studentApi.profile,
  })

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
        parentPhoneNumber: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        schoolName: z.string().optional().default(''),
        city: z.string().optional().default(''),
        gradeId: z.string().optional().default(''),
        dateOfBirth: z.string().optional().default(''),
      }),
    [t],
  )

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    setError,
    watch,
    formState: { errors, isSubmitting, isDirty },
  } = useForm<ProfileValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      firstName: '',
      lastName: '',
      phoneNumber: '',
      parentPhoneNumber: '',
      schoolName: '',
      city: '',
      gradeId: '',
      dateOfBirth: '',
    },
  })

  React.useEffect(() => {
    if (!profile.data) return
    reset({
      firstName: profile.data.firstName ?? '',
      lastName: profile.data.lastName ?? '',
      phoneNumber: profile.data.phoneNumber ?? '',
      parentPhoneNumber: profile.data.parentPhoneNumber ?? '',
      schoolName: profile.data.schoolName ?? '',
      city: profile.data.city ?? '',
      gradeId: profile.data.gradeId ?? '',
      dateOfBirth: profile.data.dateOfBirth?.slice(0, 10) ?? '',
    })
  }, [profile.data, reset])

  const save = useMutation({
    mutationFn: (values: ProfileValues) =>
      studentApi.updateProfile({
        firstName: values.firstName.trim(),
        lastName: values.lastName.trim(),
        phoneNumber: values.phoneNumber ? normalizeEgyptianMobile(values.phoneNumber) : null,
        parentPhoneNumber: values.parentPhoneNumber
          ? normalizeEgyptianMobile(values.parentPhoneNumber)
          : null,
        schoolName: values.schoolName.trim() || null,
        city: values.city.trim() || null,
        gradeId: values.gradeId || null,
        dateOfBirth: values.dateOfBirth || null,
      }),
    onSuccess: async () => {
      queryClient.invalidateQueries({ queryKey: qk.student.profile })
      await refreshUser()
      toast({ title: t('common.saved'), variant: 'success' })
      setFormError(null)
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<ProfileValues>(error, setError, [
          'firstName',
          'lastName',
          'phoneNumber',
          'parentPhoneNumber',
          'schoolName',
          'city',
          'gradeId',
        ]),
      ),
  })

  if (profile.isPending) return <RowSkeleton count={4} />
  if (profile.isError) return <QueryError error={profile.error} onRetry={() => profile.refetch()} />

  return (
    <Card className="p-6">
      <ProfilePictureField
        className="mb-6 border-b border-border pb-6"
        invalidate={[qk.student.profile]}
      >
        <h2 className="font-display text-heading-sm font-bold text-foreground">
          {profile.data?.displayName}
        </h2>
        <p className="mt-0.5 text-body-sm text-muted-foreground">{profile.data?.email}</p>
      </ProfilePictureField>

      {!profile.data?.emailConfirmed && (
        <NoticeBanner
          tone="warning"
          className="mb-6"
          icon={<MailCheck />}
          title={t('auth.verifyEmailTitle')}
        >
          {t('auth.unverifiedBanner')}
        </NoticeBanner>
      )}

      <form onSubmit={handleSubmit((values) => save.mutate(values))} className="space-y-5" noValidate>
        {formError && (
          <motion.div
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            transition={transitions.fast}
            role="alert"
            className="flex items-start gap-2.5 rounded-lg border border-destructive/30 bg-destructive/8 p-3.5 text-body-sm text-destructive-strong"
          >
            <AlertCircle className="mt-0.5 size-4 shrink-0" />
            {formError}
          </motion.div>
        )}

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t('auth.firstName')} htmlFor="firstName" required error={errors.firstName?.message}>
            <Input id="firstName" invalid={!!errors.firstName} {...register('firstName')} />
          </Field>
          <Field label={t('auth.lastName')} htmlFor="lastName" required error={errors.lastName?.message}>
            <Input id="lastName" invalid={!!errors.lastName} {...register('lastName')} />
          </Field>
        </div>

        <Field label={t('auth.email')} htmlFor="email">
          <Input id="email" value={profile.data?.email ?? ''} readOnly dir="ltr" className="text-start" />
        </Field>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t('auth.phone')} htmlFor="phoneNumber" error={errors.phoneNumber?.message}>
            <Input
              id="phoneNumber"
              type="tel"
              dir="ltr"
              className="text-start"
              invalid={!!errors.phoneNumber}
              {...register('phoneNumber')}
            />
          </Field>
          <Field
            label={t('auth.parentPhone')}
            htmlFor="parentPhoneNumber"
            error={errors.parentPhoneNumber?.message}
          >
            <Input
              id="parentPhoneNumber"
              type="tel"
              dir="ltr"
              className="text-start"
              invalid={!!errors.parentPhoneNumber}
              {...register('parentPhoneNumber')}
            />
          </Field>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t('common.grade')} htmlFor="gradeId">
            <Select value={watch('gradeId')} onValueChange={(v) => setValue('gradeId', v, { shouldDirty: true })}>
              <SelectTrigger id="gradeId">
                <SelectValue placeholder={t('common.grade')} />
              </SelectTrigger>
              <SelectContent>
                {grades.grades.map((g) => (
                  <SelectItem key={g.id} value={g.id!}>
                    {grades.label(g)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </Field>
          <Field label={t('auth.school')} htmlFor="schoolName">
            <Input id="schoolName" {...register('schoolName')} />
          </Field>
        </div>

        <div className="grid gap-5 sm:grid-cols-2">
          <Field label={t('auth.city')} htmlFor="city">
            <Input id="city" {...register('city')} />
          </Field>
          <Field
            label={locale === 'ar-EG' ? 'تاريخ الميلاد' : 'Date of birth'}
            htmlFor="dateOfBirth"
          >
            <DateField id="dateOfBirth" {...register('dateOfBirth')} />
          </Field>
        </div>

        <Button type="submit" size="lg" loading={isSubmitting || save.isPending} disabled={!isDirty}>
          {t('common.save')}
        </Button>
      </form>
    </Card>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Guardians                                 */
/* -------------------------------------------------------------------------- */

/**
 * Parents ask to follow a student from their own screen; nothing happens until
 * the student answers here. Without this panel a link request has no way to be
 * accepted, so the whole guardian feature dead-ends at "pending".
 */
function GuardiansPanel() {
  const { locale, formatDate } = useI18n()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const queryClient = useQueryClient()
  /** Which link is being answered, so only that row shows a spinner. */
  const [answering, setAnswering] = React.useState<string | null>(null)

  const guardians = useQuery({
    queryKey: qk.student.guardians,
    queryFn: studentApi.guardians,
  })

  const respond = useMutation({
    mutationFn: ({ linkId, approve }: { linkId: string; approve: boolean }) =>
      studentApi.respondToGuardian(linkId, approve),
    onSuccess: (_result, { approve }) => {
      queryClient.invalidateQueries({ queryKey: qk.student.guardians })
      toast({
        title: approve
          ? locale === 'ar-EG'
            ? 'تمت الموافقة على الربط'
            : 'Link approved'
          : locale === 'ar-EG'
            ? 'تم رفض الطلب'
            : 'Request declined',
        variant: 'success',
      })
    },
    onError: (error) => showApiError(error),
    onSettled: () => setAnswering(null),
  })

  if (guardians.isPending) return <RowSkeleton count={3} />
  if (guardians.isError)
    return <QueryError error={guardians.error} onRetry={() => guardians.refetch()} />

  const items = guardians.data ?? []
  const pending = items.filter((g) => !g.isConfirmed)
  const confirmed = items.filter((g) => g.isConfirmed)

  if (!items.length) {
    return (
      <EmptyState
        spot="teachers"
        title={locale === 'ar-EG' ? 'مفيش ولي أمر مربوط' : 'No guardian linked'}
        description={
          locale === 'ar-EG'
            ? 'لما ولي أمرك يطلب يتابع حسابك، الطلب هيظهر هنا وتوافق عليه أو ترفضه.'
            : 'When a parent asks to follow your account, the request shows up here for you to approve or decline.'
        }
      />
    )
  }

  function Row({ guardian }: { guardian: GuardianDto }) {
    const linkId = guardian.linkId ?? ''
    const busy = respond.isPending && answering === linkId

    return (
      <div className="flex flex-wrap items-center gap-4 p-5">
        <Avatar
          traits={avatarTraitsFor(guardian.parentId ?? linkId)}
          seed={guardian.parentId ?? linkId}
          name={guardian.displayName ?? ''}
          src={imageUrl(guardian.profileImageUrl)}
          size="md"
        />

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="font-display text-body font-semibold text-foreground">
              {guardian.displayName}
            </p>
            <StatusBadge status={guardian.isConfirmed ? 'Active' : 'Pending'} />
            {guardian.isPrimaryContact && (
              <Badge variant="outline" size="sm">
                {locale === 'ar-EG' ? 'جهة الاتصال الأساسية' : 'Primary contact'}
              </Badge>
            )}
          </div>
          <p className="mt-0.5 truncate text-body-sm text-muted-foreground" dir="ltr">
            {[guardian.relationship, guardian.email, guardian.phoneNumber]
              .filter(Boolean)
              .join(' · ')}
          </p>
          <p className="mt-0.5 text-caption text-muted-foreground">
            {guardian.isConfirmed && guardian.confirmedAt
              ? `${locale === 'ar-EG' ? 'اتربط في' : 'Linked'} ${formatDate(guardian.confirmedAt)}`
              : guardian.requestedAt
                ? `${locale === 'ar-EG' ? 'طلب في' : 'Requested'} ${formatDate(guardian.requestedAt)}`
                : null}
          </p>
        </div>

        {!guardian.isConfirmed && linkId && (
          <div className="flex items-center gap-2">
            <Button
              size="sm"
              loading={busy && respond.variables?.approve === true}
              disabled={respond.isPending}
              onClick={() => {
                setAnswering(linkId)
                respond.mutate({ linkId, approve: true })
              }}
            >
              <Check aria-hidden />
              {locale === 'ar-EG' ? 'موافق' : 'Approve'}
            </Button>
            <Button
              size="sm"
              variant="ghost"
              loading={busy && respond.variables?.approve === false}
              disabled={respond.isPending}
              onClick={() => {
                setAnswering(linkId)
                respond.mutate({ linkId, approve: false })
              }}
              className="text-destructive-strong hover:bg-destructive/8"
            >
              <X aria-hidden />
              {locale === 'ar-EG' ? 'رفض' : 'Decline'}
            </Button>
          </div>
        )}
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {pending.length > 0 && (
        <section>
          <h3 className="mb-2 font-display text-body font-semibold text-foreground">
            {locale === 'ar-EG' ? 'طلبات مستنية ردّك' : 'Waiting for your answer'}
          </h3>
          <Card className="divide-y divide-border p-0">
            {pending.map((guardian) => (
              <Row key={guardian.linkId} guardian={guardian} />
            ))}
          </Card>
        </section>
      )}

      {confirmed.length > 0 && (
        <section>
          <h3 className="mb-2 font-display text-body font-semibold text-foreground">
            {locale === 'ar-EG' ? 'أولياء أمر متابعين حسابك' : 'Guardians following your account'}
          </h3>
          <Card className="divide-y divide-border p-0">
            {confirmed.map((guardian) => (
              <Row key={guardian.linkId} guardian={guardian} />
            ))}
          </Card>
        </section>
      )}

      <p className="text-caption text-muted-foreground">
        {locale === 'ar-EG'
          ? 'ولي الأمر الموافق عليه يشوف حضورك ودرجاتك ومدفوعاتك — مش رسائلك.'
          : 'An approved guardian sees your attendance, marks, and payments — not your messages.'}
      </p>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*                                Preferences                                 */
/* -------------------------------------------------------------------------- */

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
          <p className="mt-0.5 text-caption text-muted-foreground">
            {locale === 'ar-EG' ? 'الوضع الليلي أريح للعين بالليل.' : 'Dark mode is easier at night.'}
          </p>
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

/* -------------------------------------------------------------------------- */
/*                                  Security                                  */
/* -------------------------------------------------------------------------- */

interface PasswordValues {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

function SecurityPanel() {
  const { t } = useI18n()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const { user } = useAuth()
  const [formError, setFormError] = React.useState<string | null>(null)
  const [resending, setResending] = React.useState(false)

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
    formState: { errors, isSubmitting },
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
    <div className="space-y-6">
      {!user?.emailConfirmed && user?.email && (
        <Card className="flex flex-wrap items-center justify-between gap-4 p-5">
          <div className="min-w-0">
            <p className="font-display text-body-sm font-bold text-foreground">
              {t('auth.verifyEmailTitle')}
            </p>
            <p className="mt-0.5 text-caption text-muted-foreground">{t('auth.unverifiedBanner')}</p>
          </div>
          <Button
            variant="outline"
            loading={resending}
            onClick={async () => {
              setResending(true)
              try {
                await authApi.resendVerification({ email: user.email! })
                toast({ title: t('auth.verificationSent'), variant: 'success' })
              } catch (error) {
                showApiError(error)
              } finally {
                setResending(false)
              }
            }}
          >
            {t('auth.resendVerification')}
          </Button>
        </Card>
      )}

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
            htmlFor="currentPassword"
            required
            error={errors.currentPassword?.message}
          >
            <Input
              id="currentPassword"
              type="password"
              autoComplete="current-password"
              dir="ltr"
              className="text-start"
              invalid={!!errors.currentPassword}
              {...register('currentPassword')}
            />
          </Field>

          <Field
            label={t('auth.newPassword')}
            htmlFor="newPassword"
            required
            error={errors.newPassword?.message}
          >
            <Input
              id="newPassword"
              type="password"
              autoComplete="new-password"
              dir="ltr"
              className="text-start"
              invalid={!!errors.newPassword}
              {...register('newPassword')}
            />
          </Field>

          <Field
            label={t('auth.confirmPassword')}
            htmlFor="confirmPassword"
            required
            error={errors.confirmPassword?.message}
          >
            <Input
              id="confirmPassword"
              type="password"
              autoComplete="new-password"
              dir="ltr"
              className="text-start"
              invalid={!!errors.confirmPassword}
              {...register('confirmPassword')}
            />
          </Field>

          <Button type="submit" loading={isSubmitting || change.isPending}>
            {t('auth.changePassword')}
          </Button>
        </form>
      </Card>
    </div>
  )
}
