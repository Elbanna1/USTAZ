import * as React from 'react'
import { useMutation } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Plus, Trash2 } from 'lucide-react'
import { motion, AnimatePresence } from 'framer-motion'
import { teacherGroupsApi } from '@/api/endpoints/teacher'
import type { GroupListItemDto, GroupScheduleInput } from '@/api/schema'
import { useToast } from '@/hooks/use-toast'
import { useGrades, useSubjects } from '@/hooks/use-taxonomy'
import { useI18n } from '@/i18n'
import { applyServerErrors } from '@/lib/form'
import { KEY_TO_API_DAY, WEEK_ORDER, dayLabel } from '@/lib/schedule'
import { transitions } from '@/lib/motion'
import { FormDialog } from '@/components/common/FormDialog'
import { Input, Textarea } from '@/components/ui/input'
import { DateField } from '@/components/ui/date-field'
import { Field } from '@/components/ui/label'
import { Button } from '@/components/ui/button'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'

const TYPES = [
  { value: 'Regular', ar: 'منتظمة', en: 'Regular' },
  { value: 'Revision', ar: 'مراجعة نهائية', en: 'Revision' },
  { value: 'Intensive', ar: 'مكثّفة', en: 'Intensive' },
  { value: 'ExamPreparation', ar: 'استعداد للامتحان', en: 'Exam prep' },
] as const

const MODES = [
  { value: 'Online', ar: 'أونلاين', en: 'Online' },
  { value: 'Offline', ar: 'حضوري', en: 'In person' },
  { value: 'Hybrid', ar: 'أونلاين وحضوري', en: 'Hybrid' },
] as const

interface FormValues {
  name: string
  description: string
  subjectId: string
  gradeId: string
  type: (typeof TYPES)[number]['value']
  teachingMode: (typeof MODES)[number]['value']
  location: string
  monthlyPrice: string
  originalPrice: string
  capacity: string
  startDate: string
  endDate: string
  whatsAppLink: string
}

const TODAY = new Date().toISOString().slice(0, 10)

export function GroupFormDialog({
  open,
  onOpenChange,
  group,
  onSaved,
}: {
  open: boolean
  onOpenChange: (open: boolean) => void
  group: GroupListItemDto | null
  onSaved: () => void
}) {
  const { t, locale } = useI18n()
  const lang: 'ar' | 'en' = locale === 'ar-EG' ? 'ar' : 'en'
  const { toast } = useToast()
  const subjects = useSubjects()
  const grades = useGrades()
  const isEdit = Boolean(group?.id)

  const [formError, setFormError] = React.useState<string | null>(null)
  const [schedules, setSchedules] = React.useState<GroupScheduleInput[]>([])

  const schema = React.useMemo(
    () =>
      z.object({
        name: z.string().min(3, t('validation.required')),
        description: z.string().optional().default(''),
        subjectId: z.string().min(1, t('validation.selectOne')),
        gradeId: z.string().min(1, t('validation.selectOne')),
        type: z.enum(['Regular', 'Revision', 'Intensive', 'ExamPreparation']),
        teachingMode: z.enum(['Online', 'Offline', 'Hybrid']),
        location: z.string().optional().default(''),
        monthlyPrice: z.string().refine((v) => Number(v) >= 0, t('validation.number')),
        originalPrice: z.string().optional().default(''),
        capacity: z.string().refine((v) => Number(v) > 0, t('validation.number')),
        startDate: z.string().min(1, t('validation.required')),
        endDate: z.string().optional().default(''),
        whatsAppLink: z.string().optional().default(''),
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
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema) as never,
    defaultValues: {
      name: '',
      description: '',
      subjectId: '',
      gradeId: '',
      type: 'Regular',
      teachingMode: 'Hybrid',
      location: '',
      monthlyPrice: '0',
      originalPrice: '',
      capacity: '20',
      startDate: TODAY,
      endDate: '',
      whatsAppLink: '',
    },
  })

  /* Re-seed whenever the dialog opens for a different record. */
  React.useEffect(() => {
    if (!open) return
    setFormError(null)
    if (group) {
      reset({
        name: group.name ?? '',
        description: group.description ?? '',
        subjectId: group.subject?.id ?? '',
        gradeId: group.grade?.id ?? '',
        type: group.type ?? 'Regular',
        teachingMode: group.teachingMode ?? 'Hybrid',
        location: group.location ?? '',
        monthlyPrice: String(group.pricing?.price ?? 0),
        originalPrice: group.pricing?.originalPrice ? String(group.pricing.originalPrice) : '',
        capacity: String(group.capacity ?? 20),
        startDate: group.startDate?.slice(0, 10) ?? TODAY,
        endDate: group.endDate?.slice(0, 10) ?? '',
        whatsAppLink: group.whatsAppLink ?? '',
      })
      setSchedules(
        (group.schedules ?? []).map((s) => ({
          dayOfWeek: s.dayOfWeek,
          startTime: s.startTime?.slice(0, 5),
          endTime: s.endTime?.slice(0, 5),
          location: s.location,
          meetingUrl: s.meetingUrl,
        })),
      )
    } else {
      reset()
      setSchedules([{ dayOfWeek: 'Saturday', startTime: '16:00', endTime: '18:00' }])
    }
  }, [open, group, reset])

  const save = useMutation({
    mutationFn: async (values: FormValues) => {
      const payload = {
        subjectId: values.subjectId,
        gradeId: values.gradeId,
        name: values.name.trim(),
        description: values.description.trim() || null,
        type: values.type,
        teachingMode: values.teachingMode,
        location: values.location.trim() || null,
        monthlyPrice: Number(values.monthlyPrice) || 0,
        originalPrice: values.originalPrice ? Number(values.originalPrice) : null,
        capacity: Number(values.capacity) || 0,
        startDate: values.startDate,
        endDate: values.endDate || null,
        whatsAppLink: values.whatsAppLink.trim() || null,
      }

      if (isEdit && group?.id) {
        const updated = await teacherGroupsApi.update(group.id, payload)
        return updated
      }

      // A new group carries its weekly slots in the create payload.
      return teacherGroupsApi.create({
        ...payload,
        schedules: schedules.filter((s) => s.dayOfWeek && s.startTime && s.endTime),
      })
    },
    onSuccess: () => {
      onSaved()
      onOpenChange(false)
      toast({ title: t('common.saved'), variant: 'success' })
    },
    onError: (error) =>
      setFormError(
        applyServerErrors<FormValues>(error, setError, [
          'name',
          'subjectId',
          'gradeId',
          'monthlyPrice',
          'capacity',
          'startDate',
          'endDate',
          'whatsAppLink',
        ]),
      ),
  })

  const mode = watch('teachingMode')

  return (
    <FormDialog
      open={open}
      onOpenChange={onOpenChange}
      title={
        isEdit
          ? locale === 'ar-EG'
            ? 'تعديل المجموعة'
            : 'Edit group'
          : locale === 'ar-EG'
            ? 'مجموعة جديدة'
            : 'New group'
      }
      description={
        locale === 'ar-EG'
          ? 'المجموعة هتظهر لطلابك بعد ما تنشرها.'
          : 'The group becomes visible to students once you publish it.'
      }
      error={formError}
      submitting={save.isPending}
      onSubmit={handleSubmit((values) => save.mutate(values))}
    >
      <Field label={locale === 'ar-EG' ? 'اسم المجموعة' : 'Group name'} htmlFor="name" required error={errors.name?.message}>
        <Input id="name" invalid={!!errors.name} {...register('name')} />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={t('common.subject')} htmlFor="subjectId" required error={errors.subjectId?.message}>
          <Select value={watch('subjectId')} onValueChange={(v) => setValue('subjectId', v, { shouldValidate: true })}>
            <SelectTrigger id="subjectId">
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

        <Field label={t('common.grade')} htmlFor="gradeId" required error={errors.gradeId?.message}>
          <Select value={watch('gradeId')} onValueChange={(v) => setValue('gradeId', v, { shouldValidate: true })}>
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
      </div>

      <Field label={locale === 'ar-EG' ? 'الوصف' : 'Description'} htmlFor="description">
        <Textarea id="description" rows={3} {...register('description')} />
      </Field>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field label={locale === 'ar-EG' ? 'نوع المجموعة' : 'Group type'} htmlFor="type">
          <Select value={watch('type')} onValueChange={(v) => setValue('type', v as FormValues['type'])}>
            <SelectTrigger id="type">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {TYPES.map((type) => (
                <SelectItem key={type.value} value={type.value}>
                  {type[lang]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>

        <Field label={locale === 'ar-EG' ? 'طريقة الحضور' : 'Attendance mode'} htmlFor="teachingMode">
          <Select
            value={mode}
            onValueChange={(v) => setValue('teachingMode', v as FormValues['teachingMode'])}
          >
            <SelectTrigger id="teachingMode">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {MODES.map((m) => (
                <SelectItem key={m.value} value={m.value}>
                  {m[lang]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      </div>

      {mode !== 'Online' && (
        <Field label={locale === 'ar-EG' ? 'العنوان' : 'Location'} htmlFor="location">
          <Input id="location" {...register('location')} />
        </Field>
      )}

      <div className="grid gap-5 sm:grid-cols-3">
        <Field
          label={locale === 'ar-EG' ? 'السعر الشهري' : 'Monthly price'}
          htmlFor="monthlyPrice"
          required
          error={errors.monthlyPrice?.message}
        >
          <Input
            id="monthlyPrice"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            invalid={!!errors.monthlyPrice}
            {...register('monthlyPrice')}
          />
        </Field>
        <Field
          label={locale === 'ar-EG' ? 'السعر قبل الخصم' : 'Original price'}
          htmlFor="originalPrice"
        >
          <Input
            id="originalPrice"
            type="number"
            min={0}
            dir="ltr"
            className="text-start"
            {...register('originalPrice')}
          />
        </Field>
        <Field
          label={locale === 'ar-EG' ? 'السعة' : 'Capacity'}
          htmlFor="capacity"
          required
          error={errors.capacity?.message}
        >
          <Input
            id="capacity"
            type="number"
            min={1}
            dir="ltr"
            className="text-start"
            invalid={!!errors.capacity}
            {...register('capacity')}
          />
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={locale === 'ar-EG' ? 'تاريخ البداية' : 'Start date'}
          htmlFor="startDate"
          required
          error={errors.startDate?.message}
        >
          <DateField
 id="startDate"
 invalid={!!errors.startDate}
 {...register('startDate')}
 />
        </Field>
        <Field label={locale === 'ar-EG' ? 'تاريخ النهاية' : 'End date'} htmlFor="endDate">
          <DateField id="endDate" {...register('endDate')} />
        </Field>
      </div>

      <Field label={locale === 'ar-EG' ? 'لينك واتساب' : 'WhatsApp link'} htmlFor="whatsAppLink">
        <Input
          id="whatsAppLink"
          type="url"
          dir="ltr"
          className="text-start"
          placeholder="https://wa.me/20…"
          {...register('whatsAppLink')}
        />
      </Field>

      {/* Weekly slots are only part of the create payload; editing uses the group page. */}
      {!isEdit && (
        <div className="space-y-3 rounded-xl border border-border bg-muted/20 p-4">
          <div className="flex items-center justify-between gap-3">
            <p className="font-display text-body-sm font-bold text-foreground">
              {locale === 'ar-EG' ? 'مواعيد الأسبوع' : 'Weekly schedule'}
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() =>
                setSchedules((prev) => [
                  ...prev,
                  { dayOfWeek: 'Sunday', startTime: '16:00', endTime: '18:00' },
                ])
              }
            >
              <Plus className="size-4" />
              {t('common.add')}
            </Button>
          </div>

          <AnimatePresence initial={false}>
            {schedules.map((slot, index) => (
              <motion.div
                key={index}
                layout
                initial={{ opacity: 0, y: -6 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, height: 0 }}
                transition={transitions.fast}
                className="grid grid-cols-[1fr_auto_auto_auto] items-end gap-2"
              >
                <Select
                  value={slot.dayOfWeek}
                  onValueChange={(v) =>
                    setSchedules((prev) =>
                      prev.map((s, i) =>
                        i === index ? { ...s, dayOfWeek: v as GroupScheduleInput['dayOfWeek'] } : s,
                      ),
                    )
                  }
                >
                  <SelectTrigger aria-label={t('common.date')}>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {WEEK_ORDER.map((day) => (
                      <SelectItem key={day} value={KEY_TO_API_DAY[day]}>
                        {dayLabel(day, locale)}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>

                <DateField
                  kind="time"
                  hideEcho
                  value={slot.startTime ?? ''}
                  onChange={(e) =>
                    setSchedules((prev) =>
                      prev.map((s, i) => (i === index ? { ...s, startTime: e.target.value } : s)),
                    )
                  }
                  className="w-28"
                  aria-label={t('common.from')}
                />
                <DateField
                  kind="time"
                  hideEcho
                  value={slot.endTime ?? ''}
                  onChange={(e) =>
                    setSchedules((prev) =>
                      prev.map((s, i) => (i === index ? { ...s, endTime: e.target.value } : s)),
                    )
                  }
                  className="w-28"
                  aria-label={t('common.to')}
                />

                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  onClick={() => setSchedules((prev) => prev.filter((_, i) => i !== index))}
                  aria-label={t('common.remove')}
                  className="text-muted-foreground hover:text-destructive-strong"
                >
                  <Trash2 className="size-4" />
                </Button>
              </motion.div>
            ))}
          </AnimatePresence>
        </div>
      )}
    </FormDialog>
  )
}
