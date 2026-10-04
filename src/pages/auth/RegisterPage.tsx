import * as React from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Check, Eye, EyeOff, GraduationCap, HeartHandshake, Users } from 'lucide-react'
import { motion } from 'framer-motion'
import { AuthShell } from '@/components/layout/AuthShell'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import { Checkbox } from '@/components/ui/primitives'
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/tabs'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { useGrades, useSubjects } from '@/hooks/use-taxonomy'
import { usePageTitle } from '@/hooks/use-page-title'
import { useToast } from '@/hooks/use-toast'
import { useAuth, rolesForUser, ROLE_HOME } from '@/hooks/use-auth'
import { useI18n } from '@/i18n'
import { applyServerErrors, EG_MOBILE, normalizeEgyptianMobile } from '@/lib/form'
import { cn } from '@/lib/utils'
import { transitions } from '@/lib/motion'

type Role = 'teacher' | 'student' | 'parent'

const ROLE_TABS: Role[] = ['teacher', 'student', 'parent']

function roleFromParam(value: string | null): Role {
  return ROLE_TABS.includes(value as Role) ? (value as Role) : 'teacher'
}

interface FormValues {
  firstName: string
  lastName: string
  email: string
  phoneNumber: string
  password: string
  city: string
  /* Teacher-only */
  subjectId: string
  gradeIds: string[]
  yearsOfExperience: string
  headline: string
  workspaceName: string
  teachingMode: 'Online' | 'Offline' | 'Hybrid'
  /* Student-only */
  gradeId: string
  schoolName: string
  parentPhoneNumber: string
  /* Parent-only */
  childEmail: string
  relationship: string
  occupation: string
  alternatePhoneNumber: string
  terms: boolean
}

const DEFAULTS: FormValues = {
  firstName: '',
  lastName: '',
  email: '',
  phoneNumber: '',
  password: '',
  city: '',
  subjectId: '',
  gradeIds: [],
  yearsOfExperience: '',
  headline: '',
  workspaceName: '',
  teachingMode: 'Hybrid',
  gradeId: '',
  schoolName: '',
  parentPhoneNumber: '',
  childEmail: '',
  relationship: '',
  occupation: '',
  alternatePhoneNumber: '',
  terms: false,
}

/** Heading and sub-heading per role, so the JSX stays one expression deep. */
const COPY: Record<Role, { title: Record<'ar' | 'en', string>; description: Record<'ar' | 'en', string> }> = {
  teacher: {
    title: { ar: 'ابدأ بروفايلك كمدرّس', en: 'Start your teacher profile' },
    description: {
      ar: 'مجانًا بالكامل، ومن غير كارت ائتمان. هتاخد مساحة عمل خاصة بيك على طول.',
      en: 'Free to start, no card needed. Your own workspace is ready immediately.',
    },
  },
  student: {
    title: { ar: 'اعمل حساب طالب', en: 'Create a student account' },
    description: {
      ar: 'عشان تحفظ مدرّسينك وتتابع اشتراكاتك وجدولك في مكان واحد.',
      en: 'Keep your teachers, subscriptions, and schedule in one place.',
    },
  },
  parent: {
    title: { ar: 'اعمل حساب ولي أمر', en: 'Create a parent account' },
    description: {
      ar: 'اربط حساب ابنك وتابع حضوره ودرجاته ومدفوعاته من مكان واحد.',
      en: "Link your child's account and follow attendance, marks, and payments in one place.",
    },
  },
}

/**
 * Sent as the free-text `relationship` the API expects — a closed list here so
 * two parents never describe the same tie two different ways.
 */
const RELATIONSHIPS = [
  { value: 'Father', label: { ar: 'الأب', en: 'Father' } },
  { value: 'Mother', label: { ar: 'الأم', en: 'Mother' } },
  { value: 'Guardian', label: { ar: 'وصي', en: 'Guardian' } },
  { value: 'Sibling', label: { ar: 'أخ / أخت', en: 'Sibling' } },
  { value: 'Relative', label: { ar: 'قريب', en: 'Relative' } },
] as const

export default function RegisterPage() {
  const { t, locale } = useI18n()
  usePageTitle(t('nav.register'))

  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'

  const navigate = useNavigate()
  const [searchParams] = useSearchParams()
  const { toast } = useToast()
  const { registerParent, registerStudent, registerTeacher } = useAuth()
  const subjects = useSubjects()
  const grades = useGrades()

  const [role, setRole] = React.useState<Role>(() => roleFromParam(searchParams.get('role')))
  const [showPassword, setShowPassword] = React.useState(false)
  const [formError, setFormError] = React.useState<string | null>(null)

  const schema = React.useMemo(() => {
    const base = {
      firstName: z.string().min(2, t('validation.required')).max(50),
      lastName: z.string().min(2, t('validation.required')).max(50),
      email: z.string().min(1, t('validation.required')).email(t('validation.email')),
      password: z.string().regex(/^(?=.*[A-Za-z])(?=.*\d).{8,}$/, t('validation.passwordWeak')),
      city: z.string().optional().default(''),
      terms: z.literal(true, { errorMap: () => ({ message: t('validation.required') }) }),
    }

    if (role === 'teacher') {
      return z.object({
        ...base,
        phoneNumber: z
          .string()
          .min(1, t('validation.required'))
          .refine((v) => EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        subjectId: z.string().min(1, t('validation.selectOne')),
        gradeIds: z.array(z.string()).min(1, t('validation.selectOne')),
        yearsOfExperience: z.string().optional().default(''),
        headline: z.string().optional().default(''),
        workspaceName: z.string().optional().default(''),
        teachingMode: z.enum(['Online', 'Offline', 'Hybrid']),
      })
    }

    if (role === 'parent') {
      return z.object({
        ...base,
        phoneNumber: z
          .string()
          .min(1, t('validation.required'))
          .refine((v) => EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        alternatePhoneNumber: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
        // The link is optional: the account is created either way, and the
        // child still has to approve it from their own screen.
        childEmail: z
          .string()
          .optional()
          .default('')
          .refine((v) => !v || z.string().email().safeParse(v).success, t('validation.email')),
        relationship: z.string().optional().default(''),
        occupation: z.string().optional().default(''),
      })
    }

    return z.object({
      ...base,
      phoneNumber: z
        .string()
        .optional()
        .default('')
        .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
      gradeId: z.string().min(1, t('validation.selectOne')),
      schoolName: z.string().optional().default(''),
      parentPhoneNumber: z
        .string()
        .optional()
        .default('')
        .refine((v) => !v || EG_MOBILE.test(v.replace(/\s/g, '')), t('validation.phone')),
    })
  }, [role, t])

  const {
    register,
    handleSubmit,
    setValue,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: DEFAULTS,
    mode: 'onBlur',
  })

  const password = watch('password') ?? ''
  const selectedGradeIds = watch('gradeIds') ?? []
  const termsAccepted = watch('terms')

  const rules = React.useMemo(
    () => [
      { label: locale === 'ar-EG' ? '٨ حروف على الأقل' : 'At least 8 characters', ok: password.length >= 8 },
      { label: locale === 'ar-EG' ? 'حرف إنجليزي' : 'A letter', ok: /[A-Za-z]/.test(password) },
      { label: locale === 'ar-EG' ? 'رقم' : 'A number', ok: /\d/.test(password) },
      {
        label: locale === 'ar-EG' ? 'رمز خاص (اختياري)' : 'A symbol (optional)',
        ok: /[^A-Za-z0-9]/.test(password),
      },
    ],
    [password, locale],
  )
  const strength = rules.filter((r) => r.ok).length

  const onSubmit = handleSubmit(async (values) => {
    setFormError(null)
    try {
      const user =
        role === 'parent'
          ? await registerParent({
              firstName: values.firstName.trim(),
              lastName: values.lastName.trim(),
              email: values.email.trim(),
              password: values.password,
              phoneNumber: normalizeEgyptianMobile(values.phoneNumber),
              alternatePhoneNumber: values.alternatePhoneNumber
                ? normalizeEgyptianMobile(values.alternatePhoneNumber)
                : null,
              city: values.city.trim() || null,
              occupation: values.occupation.trim() || null,
              childEmail: values.childEmail.trim() || null,
              relationship: values.relationship.trim() || null,
            })
          : role === 'teacher'
          ? await registerTeacher({
              firstName: values.firstName.trim(),
              lastName: values.lastName.trim(),
              email: values.email.trim(),
              password: values.password,
              phoneNumber: normalizeEgyptianMobile(values.phoneNumber),
              headline: values.headline.trim() || null,
              yearsOfExperience: Number(values.yearsOfExperience) || 0,
              city: values.city.trim() || null,
              whatsAppNumber: normalizeEgyptianMobile(values.phoneNumber),
              preferredTeachingMode: values.teachingMode,
              subjectIds: [values.subjectId],
              gradeIds: values.gradeIds,
              workspaceName:
                values.workspaceName.trim() ||
                `${values.firstName.trim()} ${values.lastName.trim()}`.trim(),
            })
          : await registerStudent({
              firstName: values.firstName.trim(),
              lastName: values.lastName.trim(),
              email: values.email.trim(),
              password: values.password,
              phoneNumber: values.phoneNumber ? normalizeEgyptianMobile(values.phoneNumber) : null,
              gradeId: values.gradeId || null,
              schoolName: values.schoolName.trim() || null,
              city: values.city.trim() || null,
              parentPhoneNumber: values.parentPhoneNumber
                ? normalizeEgyptianMobile(values.parentPhoneNumber)
                : null,
            })

      toast({ title: t('auth.accountCreated'), variant: 'success' })
      const nextRole = rolesForUser(user)[0]
      navigate(nextRole ? ROLE_HOME[nextRole] : '/', { replace: true })
    } catch (error) {
      setFormError(
        applyServerErrors<FormValues>(error, setError, [
          'firstName',
          'lastName',
          'email',
          'password',
          'phoneNumber',
          'city',
          'subjectId',
          'gradeIds',
          'gradeId',
          'schoolName',
          'parentPhoneNumber',
          'workspaceName',
          'childEmail',
          'relationship',
          'occupation',
          'alternatePhoneNumber',
        ]),
      )
    }
  })

  return (
    <AuthShell
      title={COPY[role].title[lang]}
      description={COPY[role].description[lang]}
      highlight={role}
      footer={
        <p className="text-center text-body-sm text-muted-foreground">
          {t('auth.haveAccount')}{' '}
          <Link to="/login" className="font-semibold text-primary hover:underline">
            {t('nav.login')}
          </Link>
        </p>
      }
    >
      <Tabs value={role} onValueChange={(v) => setRole(v as Role)}>
        <TabsList className="w-full">
          <TabsTrigger value="teacher" className="flex-1">
            <GraduationCap /> {t('auth.asTeacher')}
          </TabsTrigger>
          <TabsTrigger value="student" className="flex-1">
            <Users /> {t('auth.asStudent')}
          </TabsTrigger>
          <TabsTrigger value="parent" className="flex-1">
            <HeartHandshake /> {t('auth.asParent')}
          </TabsTrigger>
        </TabsList>
      </Tabs>

      <form onSubmit={onSubmit} className="mt-6 space-y-5" noValidate>
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

        <div className="grid gap-5 sm:grid-cols-2">
          <Field
            label={t('auth.firstName')}
            htmlFor="firstName"
            required
            error={errors.firstName?.message}
          >
            <Input
              id="firstName"
              autoComplete="given-name"
              invalid={!!errors.firstName}
              {...register('firstName')}
            />
          </Field>
          <Field
            label={t('auth.lastName')}
            htmlFor="lastName"
            required
            error={errors.lastName?.message}
          >
            <Input
              id="lastName"
              autoComplete="family-name"
              invalid={!!errors.lastName}
              {...register('lastName')}
            />
          </Field>
        </div>

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
          label={t('auth.phone')}
          htmlFor="phoneNumber"
          required={role !== 'student'}
          error={errors.phoneNumber?.message}
          hint={role === 'student' ? t('common.optional') : undefined}
        >
          <Input
            id="phoneNumber"
            type="tel"
            autoComplete="tel"
            dir="ltr"
            className="text-start"
            placeholder="01012345678"
            invalid={!!errors.phoneNumber}
            {...register('phoneNumber')}
          />
        </Field>

        {role === 'parent' ? (
          <>
            <Field
              label={t('auth.childEmail')}
              htmlFor="childEmail"
              hint={t('auth.childEmailHint')}
              error={errors.childEmail?.message}
            >
              <Input
                id="childEmail"
                type="email"
                autoComplete="off"
                dir="ltr"
                className="text-start"
                invalid={!!errors.childEmail}
                {...register('childEmail')}
              />
            </Field>

            <div className="grid gap-5 sm:grid-cols-2">
              <Field
                label={t('parent.relationship')}
                htmlFor="relationship"
                hint={t('common.optional')}
                error={errors.relationship?.message}
              >
                <Select
                  value={watch('relationship')}
                  onValueChange={(v) => setValue('relationship', v, { shouldValidate: true })}
                >
                  <SelectTrigger id="relationship" aria-invalid={!!errors.relationship}>
                    <SelectValue placeholder={t('parent.relationship')} />
                  </SelectTrigger>
                  <SelectContent>
                    {RELATIONSHIPS.map((option) => (
                      <SelectItem key={option.value} value={option.value}>
                        {option.label[lang]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </Field>
              <Field label={t('auth.city')} htmlFor="city" error={errors.city?.message}>
                <Input id="city" autoComplete="address-level2" {...register('city')} />
              </Field>
            </div>

            <div className="grid gap-5 sm:grid-cols-2">
              <Field
                label={t('auth.occupation')}
                htmlFor="occupation"
                hint={t('common.optional')}
                error={errors.occupation?.message}
              >
                <Input id="occupation" {...register('occupation')} />
              </Field>
              <Field
                label={t('auth.alternatePhone')}
                htmlFor="alternatePhoneNumber"
                hint={t('common.optional')}
                error={errors.alternatePhoneNumber?.message}
              >
                <Input
                  id="alternatePhoneNumber"
                  type="tel"
                  dir="ltr"
                  className="text-start"
                  placeholder="01012345678"
                  invalid={!!errors.alternatePhoneNumber}
                  {...register('alternatePhoneNumber')}
                />
              </Field>
            </div>
          </>
        ) : role === 'teacher' ? (
          <>
            <Field
              label={t('auth.subjects')}
              htmlFor="subjectId"
              required
              error={errors.subjectId?.message}
            >
              <Select
                value={watch('subjectId')}
                onValueChange={(v) => setValue('subjectId', v, { shouldValidate: true })}
              >
                <SelectTrigger id="subjectId" aria-invalid={!!errors.subjectId}>
                  <SelectValue placeholder={t('common.subject')} />
                </SelectTrigger>
                <SelectContent>
                  {subjects.subjects.map((s) => (
                    <SelectItem key={s.id} value={s.id!}>
                      {subjects.label(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>

            <Field
              label={t('auth.grades')}
              htmlFor="gradeIds"
              required
              error={errors.gradeIds?.message as string | undefined}
            >
              <div className="flex flex-wrap gap-2" id="gradeIds">
                {grades.grades.map((g) => {
                  const active = selectedGradeIds.includes(g.id!)
                  return (
                    <button
                      key={g.id}
                      type="button"
                      onClick={() =>
                        setValue(
                          'gradeIds',
                          active
                            ? selectedGradeIds.filter((id) => id !== g.id)
                            : [...selectedGradeIds, g.id!],
                          { shouldValidate: true },
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
                      {grades.label(g)}
                    </button>
                  )
                })}
              </div>
            </Field>

            <div className="grid gap-5 sm:grid-cols-2">
              <Field
                label={t('auth.yearsOfExperience')}
                htmlFor="yearsOfExperience"
                error={errors.yearsOfExperience?.message}
              >
                <Input
                  id="yearsOfExperience"
                  type="number"
                  min={0}
                  max={60}
                  dir="ltr"
                  className="text-start"
                  {...register('yearsOfExperience')}
                />
              </Field>
              <Field label={t('auth.city')} htmlFor="city" error={errors.city?.message}>
                <Input id="city" autoComplete="address-level2" {...register('city')} />
              </Field>
            </div>

            <Field
              label={t('auth.workspaceName')}
              htmlFor="workspaceName"
              hint={
                locale === 'ar-EG'
                  ? 'الاسم اللي هيظهر لطلابك — تقدر تغيّره بعدين.'
                  : 'The name your students will see — you can change it later.'
              }
              error={errors.workspaceName?.message}
            >
              <Input id="workspaceName" {...register('workspaceName')} />
            </Field>
          </>
        ) : (
          <>
            <Field label={t('common.grade')} htmlFor="gradeId" required error={errors.gradeId?.message}>
              <Select
                value={watch('gradeId')}
                onValueChange={(v) => setValue('gradeId', v, { shouldValidate: true })}
              >
                <SelectTrigger id="gradeId" aria-invalid={!!errors.gradeId}>
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

            <div className="grid gap-5 sm:grid-cols-2">
              <Field label={t('auth.school')} htmlFor="schoolName" error={errors.schoolName?.message}>
                <Input id="schoolName" {...register('schoolName')} />
              </Field>
              <Field label={t('auth.city')} htmlFor="city" error={errors.city?.message}>
                <Input id="city" autoComplete="address-level2" {...register('city')} />
              </Field>
            </div>

            <Field
              label={t('auth.parentPhone')}
              htmlFor="parentPhoneNumber"
              hint={t('common.optional')}
              error={errors.parentPhoneNumber?.message}
            >
              <Input
                id="parentPhoneNumber"
                type="tel"
                dir="ltr"
                className="text-start"
                placeholder="01012345678"
                invalid={!!errors.parentPhoneNumber}
                {...register('parentPhoneNumber')}
              />
            </Field>
          </>
        )}

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
              autoComplete="new-password"
              dir="ltr"
              className="pe-11 text-start"
              placeholder="••••••••"
              invalid={!!errors.password}
              {...register('password')}
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

        {/* Strength meter */}
        {password.length > 0 && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            className="space-y-2.5 overflow-hidden"
          >
            <div className="flex gap-1.5" aria-hidden>
              {[0, 1, 2, 3].map((i) => (
                <span
                  key={i}
                  className={cn(
                    'h-1 flex-1 rounded-full transition-colors duration-300',
                    i < strength
                      ? strength <= 1
                        ? 'bg-destructive'
                        : strength === 2
                          ? 'bg-warning'
                          : 'bg-success'
                      : 'bg-muted',
                  )}
                />
              ))}
            </div>
            <ul className="grid grid-cols-2 gap-1.5">
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
          </motion.div>
        )}

        <div className="flex items-start gap-2.5">
          <Checkbox
            id="terms"
            checked={termsAccepted}
            onCheckedChange={(v) => setValue('terms', v === true, { shouldValidate: true })}
            className="mt-0.5"
          />
          <label htmlFor="terms" className="cursor-pointer text-body-sm text-muted-foreground">
            {locale === 'ar-EG' ? 'أوافق على شروط الاستخدام وسياسة الخصوصية.' : 'I agree to the terms of use and privacy policy.'}
          </label>
        </div>
        {errors.terms && (
          <p role="alert" className="-mt-3 text-caption font-medium text-destructive-strong">
            {errors.terms.message as string}
          </p>
        )}

        <Button type="submit" size="lg" block loading={isSubmitting}>
          {isSubmitting ? t('auth.creating') : t('auth.submitRegister')}
        </Button>
      </form>
    </AuthShell>
  )
}
