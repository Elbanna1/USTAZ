import {
  Activity,
  Award,
  BarChart3,
  Bell,
  BookOpen,
  Building2,
  CalendarDays,
  CalendarRange,
  ClipboardCheck,
  CreditCard,
  FileText,
  GraduationCap,
  Heart,
  LayoutDashboard,
  Megaphone,
  MessageSquare,
  ScrollText,
  Settings,
  ShieldCheck,
  Star,
  Tag,
  UserCog,
  Users,
  Wallet,
  type LucideIcon,
} from 'lucide-react'
import type { AppRole } from '@/hooks/use-auth'

export interface NavEntry {
  href: string
  icon: LucideIcon
  label: { ar: string; en: string }
  /** `notifications` / `messages` wire this row to a live unread badge. */
  badge?: 'notifications' | 'messages'
  /** Assistants only see rows they hold the permission for. */
  permission?: string
}

export interface NavSection {
  title?: { ar: string; en: string }
  items: NavEntry[]
}

const STUDENT_NAV: NavSection[] = [
  {
    items: [
      { href: '/student/dashboard', icon: LayoutDashboard, label: { ar: 'نظرة عامة', en: 'Overview' } },
      { href: '/student/courses', icon: BookOpen, label: { ar: 'كورساتي', en: 'My courses' } },
      { href: '/student/schedule', icon: CalendarDays, label: { ar: 'جدولي', en: 'Schedule' } },
      { href: '/student/calendar', icon: CalendarRange, label: { ar: 'التقويم', en: 'Calendar' } },
    ],
  },
  {
    title: { ar: 'الدراسة', en: 'Study' },
    items: [
      { href: '/student/exams', icon: FileText, label: { ar: 'الامتحانات', en: 'Exams' } },
      { href: '/student/homework', icon: ClipboardCheck, label: { ar: 'الواجبات', en: 'Homework' } },
      { href: '/student/grades', icon: GraduationCap, label: { ar: 'الدرجات', en: 'Grades' } },
      { href: '/student/attendance', icon: CalendarDays, label: { ar: 'الحضور', en: 'Attendance' } },
      { href: '/student/certificates', icon: Award, label: { ar: 'الشهادات', en: 'Certificates' } },
    ],
  },
  {
    title: { ar: 'حسابي', en: 'Account' },
    items: [
      { href: '/student/payments', icon: Wallet, label: { ar: 'المدفوعات', en: 'Payments' } },
      { href: '/student/favorites', icon: Heart, label: { ar: 'المفضّلة', en: 'Favorites' } },
      { href: '/student/messages', icon: MessageSquare, label: { ar: 'الرسائل', en: 'Messages' }, badge: 'messages' },
      { href: '/student/notifications', icon: Bell, label: { ar: 'الإشعارات', en: 'Notifications' }, badge: 'notifications' },
      { href: '/student/settings', icon: Settings, label: { ar: 'الإعدادات', en: 'Settings' } },
    ],
  },
]

const TEACHER_NAV: NavSection[] = [
  {
    items: [
      { href: '/teacher/dashboard', icon: LayoutDashboard, label: { ar: 'نظرة عامة', en: 'Overview' } },
      { href: '/teacher/analytics', icon: BarChart3, label: { ar: 'التحليلات', en: 'Analytics' }, permission: 'Analytics.View' },
      { href: '/teacher/activity', icon: Activity, label: { ar: 'النشاط', en: 'Activity' }, permission: 'Analytics.View' },
    ],
  },
  {
    title: { ar: 'التدريس', en: 'Teaching' },
    items: [
      { href: '/teacher/groups', icon: Users, label: { ar: 'المجموعات', en: 'Groups' }, permission: 'Groups.View' },
      { href: '/teacher/courses', icon: BookOpen, label: { ar: 'الكورسات', en: 'Courses' }, permission: 'Courses.View' },
      { href: '/teacher/schedule', icon: CalendarDays, label: { ar: 'الجدول', en: 'Schedule' }, permission: 'Schedule.View' },
      { href: '/teacher/calendar', icon: CalendarRange, label: { ar: 'التقويم', en: 'Calendar' }, permission: 'Schedule.View' },
      { href: '/teacher/attendance', icon: ClipboardCheck, label: { ar: 'الحضور', en: 'Attendance' }, permission: 'Attendance.View' },
      { href: '/teacher/exams', icon: FileText, label: { ar: 'الامتحانات', en: 'Exams' }, permission: 'Exams.View' },
      { href: '/teacher/homework', icon: ScrollText, label: { ar: 'الواجبات', en: 'Homework' }, permission: 'Homework.View' },
    ],
  },
  {
    title: { ar: 'الطلاب', en: 'Students' },
    items: [
      { href: '/teacher/students', icon: GraduationCap, label: { ar: 'الطلاب', en: 'Students' }, permission: 'Students.View' },
      { href: '/teacher/announcements', icon: Megaphone, label: { ar: 'الإعلانات', en: 'Announcements' }, permission: 'Announcements.View' },
      { href: '/teacher/certificates', icon: Award, label: { ar: 'الشهادات', en: 'Certificates' } },
      { href: '/teacher/reviews', icon: Star, label: { ar: 'التقييمات', en: 'Reviews' } },
    ],
  },
  {
    title: { ar: 'الأعمال', en: 'Business' },
    items: [
      { href: '/teacher/payments', icon: Wallet, label: { ar: 'المدفوعات', en: 'Payments' }, permission: 'Payments.View' },
      { href: '/teacher/discounts', icon: Tag, label: { ar: 'الخصومات', en: 'Discounts' } },
      { href: '/teacher/billing', icon: CreditCard, label: { ar: 'الاشتراك', en: 'Subscription' } },
      { href: '/teacher/assistants', icon: UserCog, label: { ar: 'المساعدين', en: 'Assistants' } },
    ],
  },
  {
    title: { ar: 'حسابي', en: 'Account' },
    items: [
      { href: '/teacher/messages', icon: MessageSquare, label: { ar: 'الرسائل', en: 'Messages' }, badge: 'messages' },
      { href: '/teacher/notifications', icon: Bell, label: { ar: 'الإشعارات', en: 'Notifications' }, badge: 'notifications' },
      { href: '/teacher/settings', icon: Settings, label: { ar: 'الإعدادات', en: 'Settings' } },
    ],
  },
]

const PARENT_NAV: NavSection[] = [
  {
    items: [
      { href: '/parent/dashboard', icon: LayoutDashboard, label: { ar: 'نظرة عامة', en: 'Overview' } },
      { href: '/parent/children', icon: Users, label: { ar: 'الأبناء', en: 'Children' } },
      { href: '/parent/calendar', icon: CalendarRange, label: { ar: 'التقويم', en: 'Calendar' } },
      { href: '/parent/announcements', icon: Megaphone, label: { ar: 'الإعلانات', en: 'Announcements' } },
    ],
  },
  {
    title: { ar: 'حسابي', en: 'Account' },
    items: [
      { href: '/parent/messages', icon: MessageSquare, label: { ar: 'الرسائل', en: 'Messages' }, badge: 'messages' },
      { href: '/parent/notifications', icon: Bell, label: { ar: 'الإشعارات', en: 'Notifications' }, badge: 'notifications' },
      { href: '/parent/settings', icon: Settings, label: { ar: 'الإعدادات', en: 'Settings' } },
    ],
  },
]

const ADMIN_NAV: NavSection[] = [
  {
    items: [
      { href: '/admin/dashboard', icon: LayoutDashboard, label: { ar: 'نظرة عامة', en: 'Overview' } },
      { href: '/admin/analytics', icon: BarChart3, label: { ar: 'التحليلات', en: 'Analytics' } },
    ],
  },
  {
    title: { ar: 'المنصة', en: 'Platform' },
    items: [
      { href: '/admin/tenants', icon: Building2, label: { ar: 'المنصّات', en: 'Workspaces' } },
      { href: '/admin/teachers', icon: GraduationCap, label: { ar: 'المدرّسين', en: 'Teachers' } },
      { href: '/admin/students', icon: Users, label: { ar: 'الطلاب', en: 'Students' } },
      { href: '/admin/courses', icon: BookOpen, label: { ar: 'الكورسات', en: 'Courses' } },
      { href: '/admin/groups', icon: Users, label: { ar: 'المجموعات', en: 'Groups' } },
      { href: '/admin/enrollments', icon: ClipboardCheck, label: { ar: 'الاشتراكات', en: 'Enrollments' } },
    ],
  },
  {
    title: { ar: 'الإدارة', en: 'Operations' },
    items: [
      { href: '/admin/reviews', icon: Star, label: { ar: 'مراجعة التقييمات', en: 'Review moderation' } },
      { href: '/admin/inquiries', icon: MessageSquare, label: { ar: 'الاستفسارات', en: 'Inquiries' } },
      { href: '/admin/plans', icon: CreditCard, label: { ar: 'الباقات', en: 'Plans' } },
      { href: '/admin/reports', icon: BarChart3, label: { ar: 'التقارير', en: 'Reports' } },
      { href: '/admin/audit-logs', icon: ShieldCheck, label: { ar: 'سجل العمليات', en: 'Audit log' } },
    ],
  },
  {
    title: { ar: 'حسابي', en: 'Account' },
    items: [
      { href: '/admin/notifications', icon: Bell, label: { ar: 'الإشعارات', en: 'Notifications' }, badge: 'notifications' },
    ],
  },
]

export const ROLE_NAV: Record<AppRole, NavSection[]> = {
  student: STUDENT_NAV,
  teacher: TEACHER_NAV,
  // An assistant works inside the teacher console; permissions trim the rows.
  assistant: TEACHER_NAV,
  parent: PARENT_NAV,
  admin: ADMIN_NAV,
}

/** The four rows promoted to the mobile bottom bar, per role. */
export const MOBILE_PRIMARY: Record<AppRole, string[]> = {
  student: ['/student/dashboard', '/student/courses', '/student/schedule', '/student/homework'],
  teacher: ['/teacher/dashboard', '/teacher/groups', '/teacher/schedule', '/teacher/students'],
  assistant: ['/teacher/dashboard', '/teacher/groups', '/teacher/attendance', '/teacher/homework'],
  parent: ['/parent/dashboard', '/parent/children', '/parent/announcements', '/parent/messages'],
  admin: ['/admin/dashboard', '/admin/tenants', '/admin/teachers', '/admin/reviews'],
}

export const ROLE_LABELS: Record<AppRole, { ar: string; en: string }> = {
  student: { ar: 'لوحة الطالب', en: 'Student console' },
  teacher: { ar: 'لوحة المدرّس', en: 'Teacher console' },
  assistant: { ar: 'لوحة المساعد', en: 'Assistant console' },
  parent: { ar: 'لوحة ولي الأمر', en: 'Parent console' },
  admin: { ar: 'لوحة المنصة', en: 'Platform console' },
}

export const ROLE_ICONS: Record<AppRole, LucideIcon> = {
  student: BookOpen,
  teacher: GraduationCap,
  assistant: UserCog,
  parent: Users,
  admin: ShieldCheck,
}

/**
 * Filters the nav for what this user may actually open. Assistants are limited
 * by their granted permissions; everyone else sees their full console.
 */
export function navFor(
  role: AppRole,
  options: { permissions?: string[]; isAssistant?: boolean } = {},
): NavSection[] {
  const sections = ROLE_NAV[role]
  if (!options.isAssistant) return sections

  const granted = new Set(options.permissions ?? [])
  return sections
    .map((section) => ({
      ...section,
      items: section.items.filter((item) => !item.permission || granted.has(item.permission)),
    }))
    .filter((section) => section.items.length > 0)
}
