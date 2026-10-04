import { lazy, Suspense } from 'react'
import { createBrowserRouter, Navigate, type RouteObject } from 'react-router-dom'
import { SiteLayout } from '@/components/layout/SiteLayout'
import { DashboardLayout } from '@/components/layout/DashboardLayout'
import { RouteFallback } from '@/routes/RouteFallback'
import { RouteError } from '@/routes/RouteError'
import { RedirectIfAuthenticated, RequireRole } from '@/routes/guards'

/* Code-split every route so the first paint only ships the homepage. */
const HomePage = lazy(() => import('@/pages/HomePage'))
const TeachersPage = lazy(() => import('@/pages/TeachersPage'))
const TeacherProfilePage = lazy(() => import('@/pages/TeacherProfilePage'))
const GroupsPage = lazy(() => import('@/pages/GroupsPage'))
const GroupDetailPage = lazy(() => import('@/pages/GroupDetailPage'))
const CoursesPage = lazy(() => import('@/pages/CoursesPage'))
const CourseDetailPage = lazy(() => import('@/pages/CourseDetailPage'))
const SchedulePage = lazy(() => import('@/pages/SchedulePage'))
const AboutPage = lazy(() => import('@/pages/AboutPage'))
const ContactPage = lazy(() => import('@/pages/ContactPage'))
const PricingPage = lazy(() => import('@/pages/PricingPage'))
const FaqPage = lazy(() => import('@/pages/FaqPage'))
const VerifyCertificatePage = lazy(() => import('@/pages/VerifyCertificatePage'))
const NotFoundPage = lazy(() => import('@/pages/NotFoundPage'))

/* ------------------------------ Auth screens ----------------------------- */
const LoginPage = lazy(() => import('@/pages/auth/LoginPage'))
const RegisterPage = lazy(() => import('@/pages/auth/RegisterPage'))
const ForgotPasswordPage = lazy(() =>
  import('@/pages/auth/PasswordPages').then((m) => ({ default: m.ForgotPasswordPage })),
)
const ResetPasswordPage = lazy(() =>
  import('@/pages/auth/PasswordPages').then((m) => ({ default: m.ResetPasswordPage })),
)
const VerifyEmailPage = lazy(() =>
  import('@/pages/auth/PasswordPages').then((m) => ({ default: m.VerifyEmailPage })),
)

/* --------------------------------- Shared -------------------------------- */
const NotificationsPage = lazy(() => import('@/pages/shared/NotificationsPage'))
const MessagesPage = lazy(() => import('@/pages/shared/MessagesPage'))
const CalendarPage = lazy(() => import('@/pages/shared/CalendarPage'))
const ParentAnnouncementsPage = lazy(() =>
  import('@/pages/shared/AnnouncementsPage').then((m) => ({ default: m.ParentAnnouncementsPage })),
)

/* -------------------------------- Student -------------------------------- */
const StudentDashboardPage = lazy(() => import('@/pages/student/StudentDashboardPage'))
const StudentCoursesPage = lazy(() => import('@/pages/student/StudentCoursesPage'))
const StudentCourseLearnPage = lazy(() => import('@/pages/student/StudentCourseLearnPage'))
const StudentSchedulePage = lazy(() => import('@/pages/student/StudentSchedulePage'))
const StudentExamsPage = lazy(() => import('@/pages/student/StudentExamsPage'))
const StudentExamDetailPage = lazy(() => import('@/pages/student/StudentExamDetailPage'))
const StudentExamAttemptPage = lazy(() => import('@/pages/student/StudentExamAttemptPage'))
const StudentHomeworkPage = lazy(() => import('@/pages/student/StudentHomeworkPage'))
const StudentHomeworkDetailPage = lazy(() => import('@/pages/student/StudentHomeworkDetailPage'))
const StudentGradesPage = lazy(() => import('@/pages/student/StudentGradesPage'))
const StudentAttendancePage = lazy(() => import('@/pages/student/StudentAttendancePage'))
const StudentPaymentsPage = lazy(() => import('@/pages/student/StudentPaymentsPage'))
const StudentFavoritesPage = lazy(() => import('@/pages/student/StudentFavoritesPage'))
const StudentCertificatesPage = lazy(() => import('@/pages/student/StudentCertificatesPage'))
const StudentSettingsPage = lazy(() => import('@/pages/student/StudentSettingsPage'))

/* -------------------------------- Teacher -------------------------------- */
const TeacherDashboardPage = lazy(() => import('@/pages/teacher/TeacherDashboardPage'))
const TeacherAnalyticsPage = lazy(() => import('@/pages/teacher/TeacherAnalyticsPage'))
const TeacherGroupsPage = lazy(() => import('@/pages/teacher/TeacherGroupsPage'))
const TeacherCoursesPage = lazy(() => import('@/pages/teacher/TeacherCoursesPage'))
const TeacherSchedulePage = lazy(() => import('@/pages/teacher/TeacherSchedulePage'))
const TeacherAttendancePage = lazy(() => import('@/pages/teacher/TeacherAttendancePage'))
const TeacherExamsPage = lazy(() => import('@/pages/teacher/TeacherExamsPage'))
const TeacherExamDetailPage = lazy(() => import('@/pages/teacher/TeacherExamDetailPage'))
const TeacherExamAnalyticsPage = lazy(() => import('@/pages/teacher/TeacherExamAnalyticsPage'))
const TeacherActivityPage = lazy(() => import('@/pages/teacher/TeacherActivityPage'))
const TeacherHomeworkPage = lazy(() => import('@/pages/teacher/TeacherHomeworkPage'))
const TeacherHomeworkDetailPage = lazy(() => import('@/pages/teacher/TeacherHomeworkDetailPage'))
const TeacherStudentsPage = lazy(() => import('@/pages/teacher/TeacherStudentsPage'))
const TeacherAnnouncementsPage = lazy(() => import('@/pages/teacher/TeacherAnnouncementsPage'))
const TeacherCertificatesPage = lazy(() => import('@/pages/teacher/TeacherCertificatesPage'))
const TeacherReviewsPage = lazy(() => import('@/pages/teacher/TeacherReviewsPage'))
const TeacherPaymentsPage = lazy(() => import('@/pages/teacher/TeacherPaymentsPage'))
const TeacherDiscountsPage = lazy(() => import('@/pages/teacher/TeacherDiscountsPage'))
const TeacherBillingPage = lazy(() => import('@/pages/teacher/TeacherBillingPage'))
const TeacherAssistantsPage = lazy(() => import('@/pages/teacher/TeacherAssistantsPage'))
const TeacherSettingsPage = lazy(() => import('@/pages/teacher/TeacherSettingsPage'))

/* --------------------------------- Parent -------------------------------- */
const ParentDashboardPage = lazy(() => import('@/pages/parent/ParentDashboardPage'))
const ParentChildrenPage = lazy(() => import('@/pages/parent/ParentChildrenPage'))
const ParentChildDetailPage = lazy(() => import('@/pages/parent/ParentChildDetailPage'))
const ParentSettingsPage = lazy(() => import('@/pages/parent/ParentSettingsPage'))

/* --------------------------------- Admin --------------------------------- */
const AdminDashboardPage = lazy(() => import('@/pages/admin/AdminDashboardPage'))
const AdminTenantsPage = lazy(() => import('@/pages/admin/AdminTenantsPage'))
const AdminTeachersPage = lazy(() => import('@/pages/admin/AdminTeachersPage'))
const AdminStudentsPage = lazy(() =>
  import('@/pages/admin/AdminListPages').then((m) => ({ default: m.AdminStudentsPage })),
)
const AdminCoursesPage = lazy(() =>
  import('@/pages/admin/AdminListPages').then((m) => ({ default: m.AdminCoursesPage })),
)
const AdminGroupsPage = lazy(() =>
  import('@/pages/admin/AdminListPages').then((m) => ({ default: m.AdminGroupsPage })),
)
const AdminEnrollmentsPage = lazy(() =>
  import('@/pages/admin/AdminListPages').then((m) => ({ default: m.AdminEnrollmentsPage })),
)
const AdminInquiriesPage = lazy(() =>
  import('@/pages/admin/AdminListPages').then((m) => ({ default: m.AdminInquiriesPage })),
)
const AdminReviewsPage = lazy(() =>
  import('@/pages/admin/AdminModerationPages').then((m) => ({ default: m.AdminReviewsPage })),
)
const AdminAuditLogsPage = lazy(() =>
  import('@/pages/admin/AdminModerationPages').then((m) => ({ default: m.AdminAuditLogsPage })),
)
const AdminPlansPage = lazy(() =>
  import('@/pages/admin/AdminModerationPages').then((m) => ({ default: m.AdminPlansPage })),
)
const AdminReportsPage = lazy(() =>
  import('@/pages/admin/AdminModerationPages').then((m) => ({ default: m.AdminReportsPage })),
)

/** Wraps a lazy element in the shared suspense fallback. */
function page(Component: React.LazyExoticComponent<React.ComponentType>): React.ReactElement {
  return (
    <Suspense fallback={<RouteFallback />}>
      <Component />
    </Suspense>
  )
}

/** Auth screens redirect a signed-in user to their own console. */
function guestPage(
  Component: React.LazyExoticComponent<React.ComponentType>,
): React.ReactElement {
  return (
    <RedirectIfAuthenticated>
      <Suspense fallback={<RouteFallback />}>
        <Component />
      </Suspense>
    </RedirectIfAuthenticated>
  )
}

const routes: RouteObject[] = [
  {
    path: '/',
    element: <SiteLayout />,
    errorElement: <RouteError />,
    children: [
      { index: true, element: page(HomePage) },
      { path: 'teachers', element: page(TeachersPage) },
      { path: 'teachers/:teacherId', element: page(TeacherProfilePage) },
      { path: 'groups', element: page(GroupsPage) },
      { path: 'groups/:groupId', element: page(GroupDetailPage) },
      { path: 'courses', element: page(CoursesPage) },
      { path: 'courses/:courseId', element: page(CourseDetailPage) },
      { path: 'schedule', element: page(SchedulePage) },
      { path: 'pricing', element: page(PricingPage) },
      { path: 'faq', element: page(FaqPage) },
      { path: 'about', element: page(AboutPage) },
      { path: 'contact', element: page(ContactPage) },
      { path: 'verify', element: page(VerifyCertificatePage) },
      { path: 'verify/:code', element: page(VerifyCertificatePage) },
      { path: '404', element: page(NotFoundPage) },
      { path: '*', element: page(NotFoundPage) },
    ],
  },

  /* Auth screens render full-bleed, outside the marketing shell. */
  { path: '/login', element: guestPage(LoginPage), errorElement: <RouteError /> },
  { path: '/register', element: guestPage(RegisterPage), errorElement: <RouteError /> },
  {
    path: '/forgot-password',
    element: guestPage(ForgotPasswordPage),
    errorElement: <RouteError />,
  },
  { path: '/reset-password', element: guestPage(ResetPasswordPage), errorElement: <RouteError /> },
  { path: '/verify-email', element: page(VerifyEmailPage), errorElement: <RouteError /> },

  /* ------------------------------- Student ------------------------------- */
  {
    path: '/student',
    element: (
      <RequireRole roles={['student']}>
        <DashboardLayout role="student" />
      </RequireRole>
    ),
    errorElement: <RouteError />,
    children: [
      { index: true, element: <Navigate to="/student/dashboard" replace /> },
      { path: 'dashboard', element: page(StudentDashboardPage) },
      { path: 'courses', element: page(StudentCoursesPage) },
      { path: 'courses/:courseId', element: page(StudentCourseLearnPage) },
      { path: 'schedule', element: page(StudentSchedulePage) },
      { path: 'calendar', element: page(CalendarPage) },
      { path: 'exams', element: page(StudentExamsPage) },
      { path: 'exams/:examId', element: page(StudentExamDetailPage) },
      { path: 'exams/:examId/attempt/:attemptId', element: page(StudentExamAttemptPage) },
      { path: 'homework', element: page(StudentHomeworkPage) },
      { path: 'homework/:homeworkId', element: page(StudentHomeworkDetailPage) },
      { path: 'grades', element: page(StudentGradesPage) },
      { path: 'attendance', element: page(StudentAttendancePage) },
      { path: 'certificates', element: page(StudentCertificatesPage) },
      { path: 'payments', element: page(StudentPaymentsPage) },
      { path: 'favorites', element: page(StudentFavoritesPage) },
      { path: 'messages', element: page(MessagesPage) },
      { path: 'notifications', element: page(NotificationsPage) },
      { path: 'settings', element: page(StudentSettingsPage) },
      { path: '*', element: page(NotFoundPage) },
    ],
  },

  /* ------------------------------- Teacher ------------------------------- */
  {
    path: '/teacher',
    element: (
      <RequireRole roles={['teacher', 'assistant']}>
        <DashboardLayout role="teacher" />
      </RequireRole>
    ),
    errorElement: <RouteError />,
    children: [
      { index: true, element: <Navigate to="/teacher/dashboard" replace /> },
      { path: 'dashboard', element: page(TeacherDashboardPage) },
      { path: 'analytics', element: page(TeacherAnalyticsPage) },
      { path: 'groups', element: page(TeacherGroupsPage) },
      { path: 'courses', element: page(TeacherCoursesPage) },
      { path: 'schedule', element: page(TeacherSchedulePage) },
      { path: 'calendar', element: page(CalendarPage) },
      { path: 'attendance', element: page(TeacherAttendancePage) },
      { path: 'exams', element: page(TeacherExamsPage) },
      { path: 'exams/:examId', element: page(TeacherExamDetailPage) },
      { path: 'exams/:examId/analytics', element: page(TeacherExamAnalyticsPage) },
      { path: 'activity', element: page(TeacherActivityPage) },
      { path: 'homework', element: page(TeacherHomeworkPage) },
      { path: 'homework/:homeworkId', element: page(TeacherHomeworkDetailPage) },
      { path: 'students', element: page(TeacherStudentsPage) },
      { path: 'announcements', element: page(TeacherAnnouncementsPage) },
      { path: 'certificates', element: page(TeacherCertificatesPage) },
      { path: 'reviews', element: page(TeacherReviewsPage) },
      { path: 'payments', element: page(TeacherPaymentsPage) },
      { path: 'discounts', element: page(TeacherDiscountsPage) },
      { path: 'billing', element: page(TeacherBillingPage) },
      { path: 'assistants', element: page(TeacherAssistantsPage) },
      { path: 'messages', element: page(MessagesPage) },
      { path: 'notifications', element: page(NotificationsPage) },
      { path: 'settings', element: page(TeacherSettingsPage) },
      { path: '*', element: page(NotFoundPage) },
    ],
  },

  /* -------------------------------- Parent ------------------------------- */
  {
    path: '/parent',
    element: (
      <RequireRole roles={['parent']}>
        <DashboardLayout role="parent" />
      </RequireRole>
    ),
    errorElement: <RouteError />,
    children: [
      { index: true, element: <Navigate to="/parent/dashboard" replace /> },
      { path: 'dashboard', element: page(ParentDashboardPage) },
      { path: 'children', element: page(ParentChildrenPage) },
      { path: 'children/:studentId', element: page(ParentChildDetailPage) },
      { path: 'calendar', element: page(CalendarPage) },
      { path: 'announcements', element: page(ParentAnnouncementsPage) },
      { path: 'messages', element: page(MessagesPage) },
      { path: 'notifications', element: page(NotificationsPage) },
      { path: 'settings', element: page(ParentSettingsPage) },
      { path: '*', element: page(NotFoundPage) },
    ],
  },

  /* -------------------------------- Admin -------------------------------- */
  {
    path: '/admin',
    element: (
      <RequireRole roles={['admin']}>
        <DashboardLayout role="admin" />
      </RequireRole>
    ),
    errorElement: <RouteError />,
    children: [
      { index: true, element: <Navigate to="/admin/dashboard" replace /> },
      { path: 'dashboard', element: page(AdminDashboardPage) },
      { path: 'analytics', element: page(AdminReportsPage) },
      { path: 'tenants', element: page(AdminTenantsPage) },
      { path: 'teachers', element: page(AdminTeachersPage) },
      { path: 'students', element: page(AdminStudentsPage) },
      { path: 'courses', element: page(AdminCoursesPage) },
      { path: 'groups', element: page(AdminGroupsPage) },
      { path: 'enrollments', element: page(AdminEnrollmentsPage) },
      { path: 'reviews', element: page(AdminReviewsPage) },
      { path: 'inquiries', element: page(AdminInquiriesPage) },
      { path: 'plans', element: page(AdminPlansPage) },
      { path: 'reports', element: page(AdminReportsPage) },
      { path: 'audit-logs', element: page(AdminAuditLogsPage) },
      { path: 'notifications', element: page(NotificationsPage) },
      { path: '*', element: page(NotFoundPage) },
    ],
  },
]

export const router = createBrowserRouter(routes, {
  // Opt into the v7 behaviour now — it keeps navigations from blocking paint
  // and stops React Router logging upgrade warnings to the console.
  future: {
    v7_relativeSplatPath: true,
    v7_fetcherPersist: true,
    v7_normalizeFormMethod: true,
    v7_partialHydration: true,
    v7_skipActionErrorRevalidation: true,
  },
})
