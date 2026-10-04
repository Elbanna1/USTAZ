import * as React from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Building2, Camera, KeyRound, Palette, User } from 'lucide-react'
import { motion } from 'framer-motion'
import { teacherProfileApi } from '@/api/endpoints/teacher'
import { tenantApi } from '@/api/endpoints/tenant'
import { authApi } from '@/api/endpoints/auth'
import { isApiError } from '@/api/errors'
import { qk } from '@/lib/query-client'
import { usePageTitle } from '@/hooks/use-page-title'
import { useAuth } from '@/hooks/use-auth'
import { useToast } from '@/hooks/use-toast'
import { useGrades, useSubjects } from '@/hooks/use-taxonomy'
import { useTheme } from '@/hooks/use-theme'
import { useI18n } from '@/i18n'
import { applyServerErrors, EG_MOBILE, STRONG_PASSWORD, normalizeEgyptianMobile } from '@/lib/form'
import { cn } from '@/lib/utils'
import { avatarTraitsFor, imageUrl } from '@/lib/identity'
import { transitions } from '@/lib/motion'
import { DashboardHeader } from '@/components/dashboard/DashboardHeader'
import { VerificationPanel } from '@/components/teacher/VerificationPanel'
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs'
import { Card } from '@/components/ui/card'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Switch } from '@/components/ui/primitives'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { LanguageToggle } from '@/components/layout/LanguageToggle'
import { QueryError } from '@/components/common/QueryBoundary'
import { RowSkeleton } from '@/components/ui/skeleton'
import { Avatar } from '@/components/illustrations/PersonAvatar'
import { Badge } from '@/components/ui/badge'
import { useApiErrorToast } from '@/hooks/use-api-toast'

export default function TeacherSettingsPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('nav.settings'))

  return (
    <>
      <DashboardHeader
        title={t('nav.settings')}
        description={
          locale === 'ar-EG'
            ? 'بروفايلك العام، بيانات مساحة العمل، وأمان حسابك.'
            : 'Your public profile, workspace details, and account security.'
        }
      />

      <Tabs defaultValue="profile">
        <TabsList>
          <TabsTrigger value="profile">
            <User className="size-4" />
            {t('nav.profile')}
          </TabsTrigger>
          <TabsTrigger value="workspace">
            <Building2 className="size-4" />
            {locale === 'ar-EG' ? 'مساحة العمل' : 'Workspace'}
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

        <TabsContent value="profile" className="mt-6 space-y-6">
          {/* Sits above the fields it asks the teacher to fill in. */}
          <VerificationPanel />
          <ProfilePanel />
        </TabsContent>
        <TabsContent value="workspace" className="mt-6">
          <WorkspacePanel />
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
  headline: string
  bio: string
  phoneNumber: string
  whatsAppNumber: string
  city: string
  yearsOfExperience: string
  preferredTeachingMode: 'Online' | 'Offline' | 'Hybrid'
}

function ProfilePanel() {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const showApiError = useApiErrorToast()
  const { refreshUser, user } = useAuth()
  const queryClient = useQueryClient()
  const subjects = useSubjects()
  const grades = useGrades()
  const [formError, setFormError] = React.useState<string | null>(null)
  const imageInputRef = React.useRef<HTMLInputElement>(null)

  const profile = useQuery({
    queryKey: qk.teacher.profile,
    queryFn: teacherProfileApi.get,
  })

  const schema = React.useMemo(
    () =>
      z.object({
        firstName: z.string().min(2, t('validation.required')),
        lastName: z.string().min(2, t('validation.required')),
        headline: z.string().optional().default(''),
        bio: z.string().optional().default(''),
        phoneNumber: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        whatsAppNumber: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        city: z.string().optional().default(''),
        yearsOfExperience: z.string().optional().default(''),
        preferredTeachingMode: z.enum(['Online', 'Offline', 'Hybrid']),
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
    formState: { errors, isDirty },
  } = useForm<ProfileValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      firstName: '',
      lastName: '',
      headline: '',
      bio: '',
      phoneNumber: '',
      whatsAppNumber: '',
      city: '',
      yearsOfExperience: '',
      preferredTeachingMode: 'Hybrid',
    },
  })

  React.useEffect(() => {
    if (!profile.data) return
    reset({
      firstName: profile.data.firstName ?? '',
      lastName: profile.data.lastName ?? '',
      headline: profile.data.headline ?? '',
      bio: profile.data.bio ?? '',
      phoneNumber: user?.phoneNumber ?? '',
      whatsAppNumber: '',
      city: profile.data.city ?? '',
      yearsOfExperience: String(profile.data.yearsOfExperience ?? ''),
      preferredTeachingMode: profile.data.preferredTeachingMode ?? 'Hybrid',
    })
  }, [profile.data, user, reset])

  const save = useMutation({
    mutationFn: (values: ProfileValues) =>
      teacherProfileApi.update({
        firstName: values.firstName.trim(),
        lastName: values.lastName.trim(),
        headline: values.headline.trim() || null,
        bio: values.bio.trim() || null,
        phoneNumber: values.phoneNumber ? normalizeEgyptianMobile(values.phoneNumber) : null,
        whatsAppNumber: values.whatsAppNumber
          ? normalizeEgyptianMobile(values.whatsAppNumber)
          : null,
        city: values.city.trim() || null,
        yearsOfExperience: Number(values.yearsOfExperience) || 0,
        preferredTeachingMode: values.preferredTeachingMode,
      }),
    onSuccess: async () => {
      queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      await refreshUser()
      setFormError(null)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<ProfileValues>(error, setError, [
          'firstName',
          'lastName',
          'headline',
          'phoneNumber',
          'whatsAppNumber',
          'city',
          'yearsOfExperience',
        ]),
      ),
  })

  const uploadImage = useMutation({
    mutationFn: (file: File) => teacherProfileApi.uploadImage(file),
    onSuccess: async () => {
      queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      // This route and `/me/profile-picture` write the same row, so the
      // account-level avatar query has to be dropped too or it serves a stale
      // URL for the rest of its stale time.
      queryClient.invalidateQueries({ queryKey: qk.account.profilePicture })
      await refreshUser()
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      showApiError(error),
  })

  const updateSubjects = useMutation({
    mutationFn: (subjectIds: string[]) => teacherProfileApi.updateSubjects({ subjectIds }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      toast({ title: t('common.saved'), variant: 'success' })
    },
    // The chips save on click with no submit button, so a failure would
    // otherwise read as "it saved" — the chip stays lit either way.
    onError: (error) => {
      queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      showApiError(error)
    },
  })

  const updateGrades = useMutation({
    mutationFn: (gradeIds: string[]) => teacherProfileApi.updateGrades({ gradeIds }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) => {
      queryClient.invalidateQueries({ queryKey: qk.teacher.profile })
      showApiError(error)
    },
  })

  if (profile.isPending) return <RowSkeleton count={5} />
  if (profile.isError) return <QueryError error={profile.error} onRetry={() => profile.refetch()} />

  const data = profile.data!
  const selectedSubjects = (data.subjects ?? []).map((s) => s.id!).filter(Boolean)
  const selectedGrades = (data.grades ?? []).map((g) => g.id!).filter(Boolean)

  return (
    <div className="space-y-6">
      {/* Avatar */}
      <Card className="flex flex-wrap items-center gap-5 p-6">
        <div className="relative">
          <Avatar
            traits={avatarTraitsFor(data.id ?? '')}
            seed={data.id ?? ''}
            name={data.displayName ?? ''}
            src={imageUrl(data.profileImageUrl)}
            size="2xl"
          />
          <button
            type="button"
            onClick={() => imageInputRef.current?.click()}
            aria-label={t('common.upload')}
            className="absolute -bottom-1 -end-1 grid size-9 place-items-center rounded-full border-2 border-card bg-primary text-primary-foreground shadow-soft transition-transform hover:scale-105"
          >
            <Camera className="size-4" />
          </button>
          <input
            ref={imageInputRef}
            type="file"
            accept="image/*"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) uploadImage.mutate(file)
            }}
          />
        </div>

        <div className="min-w-0 flex-1">
          <h2 className="font-display text-heading-sm font-bold text-foreground">
            {data.displayName}
          </h2>
          <p className="mt-0.5 text-body-sm text-muted-foreground">{data.headline}</p>
          <div className="mt-2 flex flex-wrap gap-1.5">
            {data.isVerified && (
              <Badge variant="success" size="sm">
                {locale === 'ar-EG' ? 'موثّق' : 'Verified'}
              </Badge>
            )}
            {data.isFeatured && (
              <Badge variant="gold" size="sm">
                {locale === 'ar-EG' ? 'مميّز' : 'Featured'}
              </Badge>
            )}
            {user?.tenantSlug && (
              <Badge variant="outline" size="sm" className="font-latin">
                {user.tenantSlug}
              </Badge>
            )}
          </div>
        </div>
      </Card>

      {/* Details */}
      <Card className="p-6">
        <form
          onSubmit={handleSubmit((values) => save.mutate(values))}
          className="space-y-5"
          noValidate
        >
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
            <Field
              label={t('auth.firstName')}
              htmlFor="t-firstName"
              required
              error={errors.firstName?.message}
            >
              <Input id="t-firstName" invalid={!!errors.firstName} {...register('firstName')} />
            </Field>
            <Field
              label={t('auth.lastName')}
              htmlFor="t-lastName"
              required
              error={errors.lastName?.message}
            >
              <Input id="t-lastName" invalid={!!errors.lastName} {...register('lastName')} />
            </Field>
          </div>

          <Field label={t('auth.headline')} htmlFor="t-headline" error={errors.headline?.message}>
            <Input id="t-headline" {...register('headline')} />
          </Field>

          <Field label={locale === 'ar-EG' ? 'نبذة عنك' : 'About you'} htmlFor="t-bio">
            <Textarea id="t-bio" rows={5} {...register('bio')} />
          </Field>

          <div className="grid gap-5 sm:grid-cols-2">
            <Field label={t('auth.phone')} htmlFor="t-phone" error={errors.phoneNumber?.message}>
              <Input
                id="t-phone"
                type="tel"
                dir="ltr"
                className="text-start"
                invalid={!!errors.phoneNumber}
                {...register('phoneNumber')}
              />
            </Field>
            <Field
              label={locale === 'ar-EG' ? 'رقم واتساب' : 'WhatsApp number'}
              htmlFor="t-whatsapp"
              error={errors.whatsAppNumber?.message}
            >
              <Input
                id="t-whatsapp"
                type="tel"
                dir="ltr"
                className="text-start"
                invalid={!!errors.whatsAppNumber}
                {...register('whatsAppNumber')}
              />
            </Field>
          </div>

          <div className="grid gap-5 sm:grid-cols-3">
            <Field label={t('auth.city')} htmlFor="t-city">
              <Input id="t-city" {...register('city')} />
            </Field>
            <Field label={t('auth.yearsOfExperience')} htmlFor="t-years">
              <Input
                id="t-years"
                type="number"
                min={0}
                dir="ltr"
                className="text-start"
                {...register('yearsOfExperience')}
              />
            </Field>
            <Field label={t('auth.teachingMode')} htmlFor="t-mode">
              <Select
                value={watch('preferredTeachingMode')}
                onValueChange={(v) =>
                  setValue('preferredTeachingMode', v as ProfileValues['preferredTeachingMode'], {
                    shouldDirty: true,
                  })
                }
              >
                <SelectTrigger id="t-mode">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Online">{locale === 'ar-EG' ? 'أونلاين' : 'Online'}</SelectItem>
                  <SelectItem value="Offline">
                    {locale === 'ar-EG' ? 'حضوري' : 'In person'}
                  </SelectItem>
                  <SelectItem value="Hybrid">
                    {locale === 'ar-EG' ? 'الاتنين' : 'Hybrid'}
                  </SelectItem>
                </SelectContent>
              </Select>
            </Field>
          </div>

          <Button type="submit" size="lg" loading={save.isPending} disabled={!isDirty}>
            {t('common.save')}
          </Button>
        </form>
      </Card>

      {/* Subjects + grades */}
      <Card className="space-y-6 p-6">
        <div>
          <h3 className="font-display text-heading-sm text-foreground">{t('auth.subjects')}</h3>
          <div className="mt-3 flex flex-wrap gap-2">
            {subjects.subjects.map((subject) => {
              const active = selectedSubjects.includes(subject.id!)
              return (
                <button
                  key={subject.id}
                  type="button"
                  disabled={updateSubjects.isPending}
                  onClick={() =>
                    updateSubjects.mutate(
                      active
                        ? selectedSubjects.filter((id) => id !== subject.id)
                        : [...selectedSubjects, subject.id!],
                    )
                  }
                  aria-pressed={active}
                  className={cn(
                    'rounded-full border px-3.5 py-1.5 font-display text-body-sm font-semibold transition-all duration-200',
                    active
                      ? 'border-primary bg-primary text-primary-foreground shadow-soft'
                      : 'border-border bg-card text-muted-foreground hover:border-foreground/20 hover:text-foreground',
                  )}
                >
                  {subjects.label(subject)}
                </button>
              )
            })}
          </div>
        </div>

        <div>
          <h3 className="font-display text-heading-sm text-foreground">{t('auth.grades')}</h3>
          <div className="mt-3 flex flex-wrap gap-2">
            {grades.grades.map((grade) => {
              const active = selectedGrades.includes(grade.id!)
              return (
                <button
                  key={grade.id}
                  type="button"
                  disabled={updateGrades.isPending}
                  onClick={() =>
                    updateGrades.mutate(
                      active
                        ? selectedGrades.filter((id) => id !== grade.id)
                        : [...selectedGrades, grade.id!],
                    )
                  }
                  aria-pressed={active}
                  className={cn(
                    'rounded-full border px-3.5 py-1.5 font-display text-body-sm font-semibold transition-all duration-200',
                    active
                      ? 'border-primary bg-primary text-primary-foreground shadow-soft'
                      : 'border-border bg-card text-muted-foreground hover:border-foreground/20 hover:text-foreground',
                  )}
                >
                  {grades.label(grade)}
                </button>
              )
            })}
          </div>
        </div>
      </Card>
    </div>
  )
}

/* -------------------------------------------------------------------------- */
/*                                  Workspace                                 */
/* -------------------------------------------------------------------------- */

function WorkspacePanel() {
  const { t, locale } = useI18n()
  const { toast } = useToast()
  const queryClient = useQueryClient()
  const [formError, setFormError] = React.useState<string | null>(null)

  const tenant = useQuery({ queryKey: qk.tenant.self, queryFn: tenantApi.get })
  const settings = useQuery({ queryKey: qk.tenant.settings, queryFn: tenantApi.settings })

  const [name, setName] = React.useState('')
  const [displayName, setDisplayName] = React.useState('')
  const [contactEmail, setContactEmail] = React.useState('')
  const [contactPhone, setContactPhone] = React.useState('')
  const [city, setCity] = React.useState('')

  React.useEffect(() => {
    if (!tenant.data) return
    setName(tenant.data.name ?? '')
    setDisplayName(tenant.data.displayName ?? '')
    setContactEmail(tenant.data.contactEmail ?? '')
    setContactPhone(tenant.data.contactPhone ?? '')
    setCity(tenant.data.city ?? '')
  }, [tenant.data])

  const save = useMutation({
    mutationFn: () =>
      tenantApi.update({
        name: name.trim(),
        displayName: displayName.trim() || null,
        contactEmail: contactEmail.trim() || null,
        contactPhone: contactPhone.trim() || null,
        city: city.trim() || null,
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: qk.tenant.self })
      setFormError(null)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) => setFormError(isApiError(error) ? error.message : t('states.errorTitle')),
  })

  if (tenant.isPending) return <RowSkeleton count={4} />
  if (tenant.isError) return <QueryError error={tenant.error} onRetry={() => tenant.refetch()} />

  return (
    <div className="space-y-6">
      <Card className="p-6">
        <form
          onSubmit={(e) => {
            e.preventDefault()
            save.mutate()
          }}
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

          <Field label={locale === 'ar-EG' ? 'اسم مساحة العمل' : 'Workspace name'} htmlFor="w-name" required>
            <Input id="w-name" value={name} onChange={(e) => setName(e.target.value)} />
          </Field>

          <Field label={locale === 'ar-EG' ? 'الاسم المعروض' : 'Display name'} htmlFor="w-display">
            <Input
              id="w-display"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </Field>

          <div className="grid gap-5 sm:grid-cols-2">
            <Field label={locale === 'ar-EG' ? 'بريد التواصل' : 'Contact email'} htmlFor="w-email">
              <Input
                id="w-email"
                type="email"
                dir="ltr"
                className="text-start"
                value={contactEmail}
                onChange={(e) => setContactEmail(e.target.value)}
              />
            </Field>
            <Field label={locale === 'ar-EG' ? 'تليفون التواصل' : 'Contact phone'} htmlFor="w-phone">
              <Input
                id="w-phone"
                type="tel"
                dir="ltr"
                className="text-start"
                value={contactPhone}
                onChange={(e) => setContactPhone(e.target.value)}
              />
            </Field>
          </div>

          <Field label={t('common.city')} htmlFor="w-city">
            <Input id="w-city" value={city} onChange={(e) => setCity(e.target.value)} />
          </Field>

          <Field label={locale === 'ar-EG' ? 'الرابط' : 'Slug'} htmlFor="w-slug">
            <Input
              id="w-slug"
              readOnly
              dir="ltr"
              className="text-start font-latin"
              value={tenant.data?.slug ?? ''}
            />
          </Field>

          <Button type="submit" size="lg" loading={save.isPending}>
            {t('common.save')}
          </Button>
        </form>
      </Card>

      {settings.data && (
        <Card className="p-6">
          <h3 className="font-display text-heading-sm text-foreground">
            {locale === 'ar-EG' ? 'إعدادات المنصة' : 'Workspace settings'}
          </h3>
          <dl className="mt-4 grid gap-4 sm:grid-cols-2">
            <div>
              <dt className="text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'العملة' : 'Currency'}
              </dt>
              <dd className="mt-1 font-display text-body-sm font-bold text-foreground">
                {settings.data.currency}
              </dd>
            </div>
            <div>
              <dt className="text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'المنطقة الزمنية' : 'Time zone'}
              </dt>
              <dd className="mt-1 font-display text-body-sm font-bold text-foreground">
                {settings.data.timeZoneId}
              </dd>
            </div>
            <div>
              <dt className="text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'لغة المنصة' : 'Default language'}
              </dt>
              <dd className="mt-1 font-display text-body-sm font-bold text-foreground">
                {settings.data.defaultLanguage}
              </dd>
            </div>
            <div>
              <dt className="text-caption text-muted-foreground">
                {locale === 'ar-EG' ? 'حد إشعار الدرجات' : 'Grade alert threshold'}
              </dt>
              <dd className="tnum mt-1 font-display text-body-sm font-bold text-foreground">
                {settings.data.gradeNotificationThresholdPercentage}%
              </dd>
            </div>
          </dl>
        </Card>
      )}
    </div>
  )
}

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
          htmlFor="tp-current"
          required
          error={errors.currentPassword?.message}
        >
          <Input
            id="tp-current"
            type="password"
            autoComplete="current-password"
            dir="ltr"
            className="text-start"
            {...register('currentPassword')}
          />
        </Field>

        <Field
          label={t('auth.newPassword')}
          htmlFor="tp-new"
          required
          error={errors.newPassword?.message}
        >
          <Input
            id="tp-new"
            type="password"
            autoComplete="new-password"
            dir="ltr"
            className="text-start"
            {...register('newPassword')}
          />
        </Field>

        <Field
          label={t('auth.confirmPassword')}
          htmlFor="tp-confirm"
          required
          error={errors.confirmPassword?.message}
        >
          <Input
            id="tp-confirm"
            type="password"
            autoComplete="new-password"
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
