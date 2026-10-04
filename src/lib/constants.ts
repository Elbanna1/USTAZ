import type { CourseCategory, CourseLevel, GroupType, StageKey } from '@/types'
import { DEMO_PLATFORM_PHONE } from '@/lib/whatsapp'

export const SITE = {
  nameAr: 'أستاذ',
  nameEn: 'USTAZ',
  tagline: 'كل اللي محتاجه عشان تبني اسمك كمدرّس',
  taglineEn: 'Everything you need to build your name as a teacher',
  description:
    'اعرض خبرتك، نظّم مجموعاتك، وصل لطلاب أكتر، وخلي كل تفاصيل شغلك التعليمي في مكان واحد.',
  descriptionEn:
    'Show your experience, organise your groups, reach more students, and keep every detail of your teaching in one place.',
  phone: DEMO_PLATFORM_PHONE,
  email: 'hello@ustaz.eg',
  addressAr: 'القاهرة الجديدة، القاهرة، مصر',
  addressEn: 'New Cairo, Cairo, Egypt',
  foundedYear: 2024,
  social: {
    facebook: 'https://facebook.com',
    instagram: 'https://instagram.com',
    youtube: 'https://youtube.com',
    x: 'https://x.com',
    linkedin: 'https://linkedin.com',
  },
} as const

export interface NavItem {
  label: string
  href: string
  description?: string
}

export const MAIN_NAV: NavItem[] = [
  { label: 'المدرّسين', href: '/teachers', description: 'اكتشف مدرّسين حسب المادة والمحافظة' },
  { label: 'المجموعات', href: '/groups', description: 'مجموعات شهرية بمواعيد ثابتة' },
  { label: 'الكورسات', href: '/courses', description: 'كورسات مسجّلة ومراجعات نهائية' },
  { label: 'الجدول', href: '/schedule', description: 'حصص الأسبوع كلها في مكان واحد' },
  { label: 'الأسعار', href: '/pricing', description: 'باقات المدرّسين والسناتر' },
]

export const SECONDARY_NAV: NavItem[] = [
  { label: 'عن أستاذ', href: '/about' },
  { label: 'الأسئلة الشائعة', href: '/faq' },
  { label: 'تواصل معنا', href: '/contact' },
]

export const STUDENT_NAV: NavItem[] = [
  { label: 'نظرة عامة', href: '/student/dashboard' },
  { label: 'اشتراكاتي', href: '/student/subscriptions' },
  { label: 'جدولي', href: '/student/schedule' },
  { label: 'المدرّسين المحفوظين', href: '/student/saved-teachers' },
  { label: 'الإشعارات', href: '/student/notifications' },
  { label: 'الإعدادات', href: '/student/settings' },
]

export const TEACHER_NAV: NavItem[] = [
  { label: 'نظرة عامة', href: '/teacher/dashboard' },
  { label: 'المجموعات', href: '/teacher/groups' },
  { label: 'الكورسات', href: '/teacher/courses' },
  { label: 'الجدول', href: '/teacher/schedule' },
  { label: 'الطلاب', href: '/teacher/students' },
  { label: 'الخصومات', href: '/teacher/discounts' },
  { label: 'التقييمات', href: '/teacher/reviews' },
  { label: 'الإعدادات', href: '/teacher/settings' },
]

export const STAGE_LABELS: Record<StageKey, string> = {
  primary: 'ابتدائي',
  preparatory: 'إعدادي',
  secondary: 'ثانوي',
}

export const GROUP_TYPE_LABELS: Record<GroupType, string> = {
  intensive: 'مكثّفة',
  regular: 'منتظمة',
  revision: 'مراجعة نهائية',
  private: 'خاصة',
  workshop: 'ورشة',
}

export const COURSE_CATEGORY_LABELS: Record<CourseCategory, string> = {
  'exam-prep': 'استعداد للامتحان',
  'full-term': 'ترم كامل',
  'problem-solving': 'حل مسائل',
  foundations: 'تأسيس',
  languages: 'لغات',
  skills: 'مهارات',
}

export const COURSE_LEVEL_LABELS: Record<CourseLevel, string> = {
  beginner: 'مبتدئ',
  intermediate: 'متوسط',
  advanced: 'متقدّم',
}

export const GROUP_STATUS_LABELS = {
  open: 'الحجز مفتوح',
  'almost-full': 'فاضل أماكن قليلة',
  full: 'مكتملة',
  'starting-soon': 'بتبدأ قريب',
} as const

export const SUBSCRIPTION_STATUS_LABELS = {
  active: 'نشط',
  pending: 'قيد التأكيد',
  expired: 'منتهي',
  cancelled: 'ملغي',
} as const

export const PAYMENT_STATUS_LABELS = {
  paid: 'مدفوع',
  due: 'مستحق',
  late: 'متأخر',
} as const

export const STORAGE_KEYS = {
  theme: 'ustaz-theme',
  savedTeachers: 'ustaz-saved-teachers',
  role: 'ustaz-demo-role',
} as const
