import { QueryClient } from '@tanstack/react-query'
import { isApiError } from '@/api/errors'

/**
 * One cache for the whole app. Defaults lean on the API's own semantics:
 * reads are cheap and cacheable, and nothing retries a 4xx the user caused.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      gcTime: 5 * 60_000,
      refetchOnWindowFocus: false,
      retry: (failureCount, error) => {
        if (isApiError(error)) return error.isRetryable && failureCount < 2
        return failureCount < 1
      },
      retryDelay: (attempt) => Math.min(1000 * 2 ** attempt, 8000),
    },
    mutations: {
      retry: false,
    },
  },
})

/**
 * Query keys live in one place so invalidation after a mutation is a lookup,
 * not a guess. Every key starts with its feature area.
 */
export const qk = {
  auth: {
    me: ['auth', 'me'] as const,
  },
  account: {
    profilePicture: ['account', 'profile-picture'] as const,
  },
  taxonomy: {
    subjects: ['taxonomy', 'subjects'] as const,
    grades: ['taxonomy', 'grades'] as const,
  },
  teachers: {
    list: (query: unknown) => ['teachers', 'list', query] as const,
    details: (id: string) => ['teachers', 'details', id] as const,
    courses: (id: string, query: unknown) => ['teachers', id, 'courses', query] as const,
    groups: (id: string, query: unknown) => ['teachers', id, 'groups', query] as const,
    reviews: (id: string, query: unknown) => ['teachers', id, 'reviews', query] as const,
    schedule: (id: string) => ['teachers', id, 'schedule'] as const,
  },
  courses: {
    list: (query: unknown) => ['courses', 'list', query] as const,
    details: (id: string) => ['courses', 'details', id] as const,
    sections: (id: string) => ['courses', id, 'sections'] as const,
    reviews: (id: string, query: unknown) => ['courses', id, 'reviews', query] as const,
  },
  groups: {
    list: (query: unknown) => ['groups', 'list', query] as const,
    details: (id: string) => ['groups', 'details', id] as const,
    curriculum: (id: string) => ['groups', id, 'curriculum'] as const,
    materials: (id: string) => ['groups', id, 'materials'] as const,
    schedule: (id: string) => ['groups', id, 'schedule'] as const,
    reviews: (id: string, query: unknown) => ['groups', id, 'reviews', query] as const,
  },
  schedule: {
    public: (query: unknown) => ['schedule', 'public', query] as const,
    upcoming: ['schedule', 'upcoming'] as const,
  },
  /** Platform-wide approved reviews — the public social proof feed. */
  reviews: {
    list: (query: unknown) => ['reviews', 'list', query] as const,
  },

  search: (q: string) => ['search', q] as const,

  student: {
    dashboard: ['student', 'dashboard'] as const,
    progress: ['student', 'progress'] as const,
    activity: ['student', 'activity'] as const,
    upcoming: ['student', 'upcoming'] as const,
    schedule: ['student', 'schedule'] as const,
    announcements: (query: unknown) => ['student', 'announcements', query] as const,
    courseEnrollments: (query: unknown) => ['student', 'enrollments', 'courses', query] as const,
    groupEnrollments: (query: unknown) => ['student', 'enrollments', 'groups', query] as const,
    courseProgress: (id: string) => ['student', 'course-progress', id] as const,
    exams: (query: unknown) => ['student', 'exams', query] as const,
    exam: (id: string) => ['student', 'exam', id] as const,
    examAttempts: (id: string) => ['student', 'exam', id, 'attempts'] as const,
    attempt: (id: string) => ['student', 'attempt', id] as const,
    grades: ['student', 'grades'] as const,
    homework: (query: unknown) => ['student', 'homework', query] as const,
    homeworkItem: (id: string) => ['student', 'homework', 'item', id] as const,
    attendance: (query: unknown) => ['student', 'attendance', query] as const,
    payments: (query: unknown) => ['student', 'payments', query] as const,
    favoriteTeachers: (query: unknown) => ['student', 'favorites', 'teachers', query] as const,
    favoriteCourses: (query: unknown) => ['student', 'favorites', 'courses', query] as const,
    certificates: (query: unknown) => ['student', 'certificates', query] as const,
    certificate: (id: string) => ['student', 'certificate', id] as const,
    profile: ['student', 'profile'] as const,
    guardians: ['student', 'guardians'] as const,
    analytics: (query: unknown) => ['student', 'analytics', query] as const,
  },

  teacher: {
    dashboard: ['teacher', 'dashboard'] as const,
    analytics: (months: number) => ['teacher', 'analytics', months] as const,
    revenue: (months: number) => ['teacher', 'analytics', 'revenue', months] as const,
    studentGrowth: (months: number) => ['teacher', 'analytics', 'students', months] as const,
    coursePerformance: ['teacher', 'analytics', 'courses'] as const,
    groupPerformance: ['teacher', 'analytics', 'groups'] as const,
    gradeAnalytics: (query: unknown) => ['teacher', 'analytics', 'grades', query] as const,
    attendanceStats: (query: unknown) => ['teacher', 'analytics', 'attendance', query] as const,
    atRisk: (query: unknown) => ['teacher', 'analytics', 'at-risk', query] as const,
    profile: ['teacher', 'profile'] as const,
    verification: ['teacher', 'verification'] as const,
    examAnalytics: (id: string, take: number) => ['teacher', 'exam-analytics', id, take] as const,
    activity: (query: unknown) => ['teacher', 'activity', query] as const,
    studentActivity: (id: string, query: unknown) =>
      ['teacher', 'activity', 'student', id, query] as const,
    groups: (query: unknown) => ['teacher', 'groups', query] as const,
    group: (id: string) => ['teacher', 'group', id] as const,
    courses: (query: unknown) => ['teacher', 'courses', query] as const,
    course: (id: string) => ['teacher', 'course', id] as const,
    exams: (query: unknown) => ['teacher', 'exams', query] as const,
    exam: (id: string) => ['teacher', 'exam', id] as const,
    examQuestions: (id: string) => ['teacher', 'exam', id, 'questions'] as const,
    examAttempts: (id: string, query: unknown) => ['teacher', 'exam', id, 'attempts', query] as const,
    examResults: (id: string) => ['teacher', 'exam', id, 'results'] as const,
    attempt: (id: string) => ['teacher', 'attempt', id] as const,
    homework: (query: unknown) => ['teacher', 'homework', query] as const,
    homeworkItem: (id: string) => ['teacher', 'homework', 'item', id] as const,
    homeworkSubmissions: (id: string, query: unknown) =>
      ['teacher', 'homework', id, 'submissions', query] as const,
    attendanceSessions: (query: unknown) => ['teacher', 'attendance', 'sessions', query] as const,
    attendanceSession: (id: string) => ['teacher', 'attendance', 'session', id] as const,
    enrollments: (kind: string, query: unknown) => ['teacher', 'enrollments', kind, query] as const,
    payments: (query: unknown) => ['teacher', 'payments', query] as const,
    announcements: (query: unknown) => ['teacher', 'announcements', query] as const,
    discounts: (query: unknown) => ['teacher', 'discounts', query] as const,
    inquiries: (query: unknown) => ['teacher', 'inquiries', query] as const,
    schedule: ['teacher', 'schedule'] as const,
    certificates: (query: unknown) => ['teacher', 'certificates', query] as const,
  },

  parent: {
    dashboard: ['parent', 'dashboard'] as const,
    children: ['parent', 'children'] as const,
    child: (id: string) => ['parent', 'child', id] as const,
    progress: (id: string) => ['parent', 'child', id, 'progress'] as const,
    grades: (id: string) => ['parent', 'child', id, 'grades'] as const,
    attendance: (id: string, query: unknown) => ['parent', 'child', id, 'attendance', query] as const,
    exams: (id: string, query: unknown) => ['parent', 'child', id, 'exams', query] as const,
    homework: (id: string, query: unknown) => ['parent', 'child', id, 'homework', query] as const,
    payments: (id: string, query: unknown) => ['parent', 'child', id, 'payments', query] as const,
    announcements: (query: unknown) => ['parent', 'announcements', query] as const,
    profile: ['parent', 'profile'] as const,
  },

  admin: {
    dashboard: (months: number) => ['admin', 'dashboard', months] as const,
    revenue: (months: number) => ['admin', 'revenue', months] as const,
    tenants: (query: unknown) => ['admin', 'tenants', query] as const,
    teachers: (query: unknown) => ['admin', 'teachers', query] as const,
    teacher: (id: string) => ['admin', 'teacher', id] as const,
    students: (query: unknown) => ['admin', 'students', query] as const,
    courses: (query: unknown) => ['admin', 'courses', query] as const,
    groups: (query: unknown) => ['admin', 'groups', query] as const,
    enrollments: (query: unknown) => ['admin', 'enrollments', query] as const,
    reviews: (query: unknown) => ['admin', 'reviews', query] as const,
    inquiries: (query: unknown) => ['admin', 'inquiries', query] as const,
    auditLogs: (query: unknown) => ['admin', 'audit-logs', query] as const,
    plans: ['admin', 'plans'] as const,
    discounts: (query: unknown) => ['admin', 'discounts', query] as const,
  },

  tenant: {
    self: ['tenant', 'self'] as const,
    settings: ['tenant', 'settings'] as const,
    branding: ['tenant', 'branding'] as const,
    assistants: (query: unknown) => ['tenant', 'assistants', query] as const,
    assistant: (id: string) => ['tenant', 'assistant', id] as const,
    permissionCatalog: ['tenant', 'permissions', 'catalog'] as const,
    team: (query: unknown) => ['tenant', 'team', query] as const,
    auditLogs: (query: unknown) => ['tenant', 'audit-logs', query] as const,
  },

  notifications: {
    list: (query: unknown) => ['notifications', 'list', query] as const,
    unreadCount: ['notifications', 'unread-count'] as const,
    preferences: ['notifications', 'preferences'] as const,
  },

  messages: {
    conversations: (query: unknown) => ['messages', 'conversations', query] as const,
    thread: (id: string, query: unknown) => ['messages', 'thread', id, query] as const,
    unreadCount: ['messages', 'unread-count'] as const,
  },

  billing: {
    plans: ['billing', 'plans'] as const,
    subscription: ['billing', 'subscription'] as const,
    invoices: (query: unknown) => ['billing', 'invoices', query] as const,
    transactions: (query: unknown) => ['billing', 'transactions', query] as const,
  },

  certificates: {
    verify: (code: string) => ['certificates', 'verify', code] as const,
  },

  calendar: {
    range: (query: unknown) => ['calendar', 'range', query] as const,
  },
} as const
