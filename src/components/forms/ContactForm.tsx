import * as React from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Mail, MessageCircle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input, Textarea } from '@/components/ui/input'
import { Field } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import { buildWhatsAppUrl, isValidEgyptianMobile } from '@/lib/whatsapp'
import { SITE } from '@/lib/constants'
import { useI18n } from '@/i18n'
import { cn } from '@/lib/utils'

/**
 * There is no general "contact the platform" endpoint — `/inquiries` is
 * addressed to a specific teacher. Rather than pretend a message was filed,
 * this form composes what the visitor wrote and hands it to a channel that
 * actually delivers: WhatsApp or their mail client.
 */

const TOPICS = [
  {
    value: 'teacher',
    label: { ar: 'أنا مدرّس وعايز أبدأ على المنصة', en: 'I am a teacher who wants to start' },
  },
  {
    value: 'student',
    label: {
      ar: 'أنا طالب أو ولي أمر ومحتاج مساعدة',
      en: 'I am a student or parent who needs help',
    },
  },
  { value: 'center', label: { ar: 'عندي سنتر تعليمي', en: 'I run an educational centre' } },
  { value: 'partnership', label: { ar: 'شراكة أو تعاون', en: 'Partnership or collaboration' } },
  { value: 'other', label: { ar: 'موضوع تاني', en: 'Something else' } },
] as const

export function ContactForm({ className }: { className?: string }) {
  const { locale } = useI18n()
  const isAr = locale === 'ar-EG'
  const lang: 'ar' | 'en' = isAr ? 'ar' : 'en'

  const schema = React.useMemo(
    () =>
      z.object({
        name: z
          .string()
          .min(3, isAr ? 'اكتب اسمك (٣ حروف على الأقل).' : 'Enter your name (3 characters or more).')
          .max(60, isAr ? 'الاسم طويل أوي.' : 'That name is too long.'),
        email: z
          .string()
          .min(1, isAr ? 'البريد الإلكتروني مطلوب.' : 'Email is required.')
          .email(isAr ? 'اكتب بريد إلكتروني صحيح.' : 'Enter a valid email address.'),
        phone: z
          .string()
          .optional()
          .or(z.literal(''))
          .refine(
            (v) => !v || isValidEgyptianMobile(v),
            isAr
              ? 'اكتب رقم موبايل مصري صحيح أو سيبه فاضي.'
              : 'Enter a valid Egyptian mobile number, or leave it empty.',
          ),
        topic: z.string().min(1, isAr ? 'اختار موضوع الرسالة.' : 'Choose a subject.'),
        message: z
          .string()
          .min(
            15,
            isAr ? 'اكتب تفاصيل أكتر شوية (١٥ حرف على الأقل).' : 'Add a little more detail (15 characters or more).',
          )
          .max(1000, isAr ? 'الرسالة أطول من ١٠٠٠ حرف.' : 'That message is over 1000 characters.'),
      }),
    [isAr],
  )

  type FormValues = z.infer<typeof schema>

  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', email: '', phone: '', topic: '', message: '' },
    mode: 'onBlur',
  })

  const message = watch('message') ?? ''

  /** The visitor's answers, formatted once for whichever channel they pick. */
  function composeBody(values: FormValues): string {
    const topic = TOPICS.find((t) => t.value === values.topic)?.label[lang] ?? values.topic
    const lines = isAr
      ? [
          `الاسم: ${values.name}`,
          `الموضوع: ${topic}`,
          `البريد: ${values.email}`,
          values.phone ? `الموبايل: ${values.phone}` : null,
          '',
          values.message,
        ]
      : [
          `Name: ${values.name}`,
          `Subject: ${topic}`,
          `Email: ${values.email}`,
          values.phone ? `Mobile: ${values.phone}` : null,
          '',
          values.message,
        ]
    return lines.filter((l) => l !== null).join('\n')
  }

  const sendOnWhatsApp = handleSubmit((values) => {
    const url = buildWhatsAppUrl({
      phone: SITE.phone,
      intent: 'support',
      message: composeBody(values),
    })
    window.open(url, '_blank', 'noopener,noreferrer')
  })

  const sendByEmail = handleSubmit((values) => {
    const topic = TOPICS.find((t) => t.value === values.topic)?.label[lang] ?? values.topic
    const subject = isAr ? `استفسار: ${topic}` : `Enquiry: ${topic}`
    window.location.href = `mailto:${SITE.email}?subject=${encodeURIComponent(
      subject,
    )}&body=${encodeURIComponent(composeBody(values))}`
  })

  return (
    <form onSubmit={sendOnWhatsApp} className={cn('space-y-5', className)} noValidate>
      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={isAr ? 'الاسم' : 'Name'}
          htmlFor="name"
          required
          error={errors.name?.message}
        >
          <Input
            id="name"
            placeholder={isAr ? 'اسمك بالكامل' : 'Your full name'}
            invalid={!!errors.name}
            {...register('name')}
          />
        </Field>

        <Field
          label={isAr ? 'البريد الإلكتروني' : 'Email'}
          htmlFor="email"
          required
          error={errors.email?.message}
        >
          <Input
            id="email"
            type="email"
            dir="ltr"
            className="text-start"
            placeholder="name@example.com"
            invalid={!!errors.email}
            {...register('email')}
          />
        </Field>
      </div>

      <div className="grid gap-5 sm:grid-cols-2">
        <Field
          label={isAr ? 'رقم الموبايل' : 'Mobile number'}
          htmlFor="contact-phone"
          error={errors.phone?.message}
          hint={isAr ? 'اختياري — لو حابب نكلّمك على واتساب.' : 'Optional — if you would like a WhatsApp reply.'}
        >
          <Input
            id="contact-phone"
            type="tel"
            inputMode="tel"
            dir="ltr"
            className="text-start"
            placeholder="01012345678"
            invalid={!!errors.phone}
            {...register('phone')}
          />
        </Field>

        <Field
          label={isAr ? 'موضوع الرسالة' : 'Subject'}
          htmlFor="topic"
          required
          error={errors.topic?.message}
        >
          <Select
            value={watch('topic')}
            onValueChange={(v) => setValue('topic', v, { shouldValidate: true })}
          >
            <SelectTrigger id="topic" aria-invalid={!!errors.topic}>
              <SelectValue placeholder={isAr ? 'اختار الموضوع' : 'Choose a subject'} />
            </SelectTrigger>
            <SelectContent>
              {TOPICS.map((t) => (
                <SelectItem key={t.value} value={t.value}>
                  {t.label[lang]}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </Field>
      </div>

      <Field
        label={isAr ? 'رسالتك' : 'Your message'}
        htmlFor="message"
        required
        error={errors.message?.message}
        hint={isAr ? `${message.length} / 1000 حرف` : `${message.length} / 1000 characters`}
      >
        <Textarea
          id="message"
          rows={6}
          placeholder={
            isAr
              ? 'اكتبلنا التفاصيل وهنرد عليك في أقرب وقت.'
              : 'Tell us the details and we will get back to you.'
          }
          invalid={!!errors.message}
          {...register('message')}
        />
      </Field>

      <div className="flex flex-col gap-3 sm:flex-row">
        <Button type="submit" size="lg" variant="whatsapp">
          <MessageCircle className="size-4" />
          {isAr ? 'ابعت على واتساب' : 'Send on WhatsApp'}
        </Button>
        <Button type="button" size="lg" variant="outline" onClick={sendByEmail}>
          <Mail className="size-4" />
          {isAr ? 'ابعت بالإيميل' : 'Send by email'}
        </Button>
      </div>

      <p className="text-caption leading-relaxed text-muted-foreground">
        {isAr
          ? 'هنجهّز رسالتك ونفتحها في واتساب أو في برنامج الإيميل عندك — تقدر تراجعها قبل ما تبعتها.'
          : 'We prepare your message and open it in WhatsApp or your mail app — you can read it over before sending.'}
      </p>
    </form>
  )
}
