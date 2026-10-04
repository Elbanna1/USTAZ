/**
 * WhatsApp is the real communication rail for Egyptian teachers, so every CTA
 * that says "كلّمه على واتساب" funnels through this one helper. It builds a
 * safe wa.me URL: digits-only phone, encoded message, no user input inlined raw.
 *
 * All numbers in this build are placeholders (2010-XXXX-XXXX demo range).
 */

export const DEMO_PLATFORM_PHONE = '201000000000'

export type WhatsAppIntent =
  | 'contact-teacher'
  | 'join-group'
  | 'ask-course'
  | 'book-class'
  | 'send-inquiry'
  | 'continue-subscription'
  | 'support'

export interface WhatsAppOptions {
  phone: string
  intent: WhatsAppIntent
  /** Free-form context merged into the templated message. */
  context?: {
    teacherName?: string
    groupName?: string
    courseName?: string
    subject?: string
    grade?: string
    price?: string
    studentName?: string
    note?: string
  }
  /** Overrides the templated message entirely. */
  message?: string
}

/** Strips everything but digits and normalises common Egyptian formats to E.164-ish. */
export function normalizePhone(raw: string): string {
  let digits = raw.replace(/\D/g, '')
  if (digits.startsWith('00')) digits = digits.slice(2)
  if (digits.startsWith('0')) digits = `20${digits.slice(1)}`
  if (digits.startsWith('1') && digits.length === 10) digits = `20${digits}`
  return digits
}

export function isValidEgyptianMobile(raw: string): boolean {
  const digits = raw.replace(/\D/g, '')
  // 01X XXXX XXXX (11 digits) or the same with a 20 country prefix.
  return /^01[0125]\d{8}$/.test(digits) || /^201[0125]\d{8}$/.test(digits)
}

export function formatPhoneDisplay(raw: string): string {
  const digits = raw.replace(/\D/g, '')
  const local = digits.startsWith('20') ? `0${digits.slice(2)}` : digits
  if (local.length !== 11) return local
  return `${local.slice(0, 3)} ${local.slice(3, 7)} ${local.slice(7)}`
}

function buildMessage(intent: WhatsAppIntent, ctx: WhatsAppOptions['context'] = {}): string {
  const who = ctx.teacherName ? ` أ. ${ctx.teacherName}` : ''
  const from = ctx.studentName ? `\nالاسم: ${ctx.studentName}` : ''
  const grade = ctx.grade ? `\nالصف: ${ctx.grade}` : ''
  const note = ctx.note ? `\n\n${ctx.note}` : ''

  switch (intent) {
    case 'contact-teacher':
      return `السلام عليكم${who} 👋\nأنا شفت بروفايلك على منصة أستاذ وحابب أعرف تفاصيل أكتر عن المجموعات المتاحة.${grade}${from}${note}`
    case 'join-group':
      return `السلام عليكم${who} 👋\nحابب أشترك في مجموعة «${ctx.groupName ?? ''}»${
        ctx.subject ? ` — ${ctx.subject}` : ''
      }.${grade}${ctx.price ? `\nالسعر المعروض: ${ctx.price}` : ''}${from}${note}\n\nممكن أعرف المواعيد المتاحة؟`
    case 'ask-course':
      return `السلام عليكم${who} 👋\nعندي استفسار بخصوص كورس «${ctx.courseName ?? ''}».${grade}${from}${note}`
    case 'book-class':
      return `السلام عليكم${who} 👋\nحابب أحجز حصة تجريبية.${grade}${from}${note}\n\nإيه المواعيد المتاحة عندك؟`
    case 'send-inquiry':
      return `السلام عليكم${who} 👋${from}${grade}${note || '\nعندي استفسار.'}`
    case 'continue-subscription':
      return `السلام عليكم${who} 👋\nأنا كملت بيانات الاشتراك على منصة أستاذ${
        ctx.groupName ? ` لمجموعة «${ctx.groupName}»` : ''
      }.${from}${grade}${
        ctx.price ? `\nالإجمالي بعد الخصم: ${ctx.price}` : ''
      }\n\nحابب أكمّل باقي الخطوات.`
    case 'support':
      return `السلام عليكم 👋\nمحتاج مساعدة بخصوص منصة أستاذ.${from}${note}`
    default:
      return 'السلام عليكم 👋'
  }
}

/** Builds a wa.me deep link. Never returns an unencoded or partial URL. */
export function buildWhatsAppUrl({ phone, intent, context, message }: WhatsAppOptions): string {
  const to = normalizePhone(phone || DEMO_PLATFORM_PHONE)
  const text = message ?? buildMessage(intent, context)
  return `https://wa.me/${to}?text=${encodeURIComponent(text)}`
}

/** Opens the link in a new tab with safe rel attributes applied by the browser. */
export function openWhatsApp(options: WhatsAppOptions): void {
  const url = buildWhatsAppUrl(options)
  window.open(url, '_blank', 'noopener,noreferrer')
}

export const WHATSAPP_INTENT_LABELS: Record<WhatsAppIntent, string> = {
  'contact-teacher': 'كلّم المدرّس على واتساب',
  'join-group': 'انضم للمجموعة على واتساب',
  'ask-course': 'اسأل عن الكورس',
  'book-class': 'احجز حصة تجريبية',
  'send-inquiry': 'ابعت استفسار',
  'continue-subscription': 'كمّل الاشتراك على واتساب',
  support: 'تواصل مع الدعم',
}
