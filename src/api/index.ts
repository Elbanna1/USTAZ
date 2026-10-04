/** One import surface for the whole data layer. */

export { api, request, buildUrl, refreshAccessToken, AUTH_EXPIRED_EVENT } from '@/api/client'
export { API_BASE_URL, API_ORIGIN, resolveMediaUrl } from '@/api/config'
export { ApiError, isApiError, type ApiErrorKind } from '@/api/errors'
export {
  clearSession,
  getSession,
  setSessionFromAuth,
  subscribeToSession,
  updateSessionUser,
  type Session,
} from '@/api/session'

export { accountApi } from '@/api/endpoints/account'
export { authApi } from '@/api/endpoints/auth'
export {
  coursesApi,
  discountsApi,
  enrollmentsApi,
  groupsApi,
  inquiriesApi,
  reviewsApi,
  scheduleApi,
  searchApi,
  taxonomyApi,
  teachersApi,
  type CourseQuery,
  type GroupQuery,
  type ReviewQuery,
  type ScheduleQuery,
  type TeacherQuery,
} from '@/api/endpoints/catalog'
export { studentApi } from '@/api/endpoints/student'
export {
  teacherAnalyticsApi,
  teacherAnnouncementsApi,
  teacherAttendanceApi,
  teacherCertificatesApi,
  teacherCoursesApi,
  teacherDiscountsApi,
  teacherEnrollmentsApi,
  teacherExamsApi,
  teacherGroupsApi,
  teacherHomeworkApi,
  teacherInquiriesApi,
  teacherPaymentsApi,
  teacherProfileApi,
  teacherScheduleApi,
  type TeacherCourseQuery,
  type TeacherExamQuery,
  type TeacherGroupQuery,
  type TeacherHomeworkQuery,
} from '@/api/endpoints/teacher'
export { parentApi } from '@/api/endpoints/parent'
export { adminApi, type AdminTeacherQuery, type AuditLogQuery, type TenantQuery } from '@/api/endpoints/admin'
export { assistantsApi, teamApi, tenantApi, type MemberQuery } from '@/api/endpoints/tenant'
export {
  billingApi,
  calendarApi,
  certificatesApi,
  messagesApi,
  notificationsApi,
  platformApi,
  publicSiteApi,
  type CalendarEventKind,
  type CalendarQuery,
  type NotificationQuery,
  type PlatformUserQuery,
} from '@/api/endpoints/platform'
export {
  DEFAULT_PAGE_SIZE,
  emptyPage,
  params,
  type AnalyticsPeriod,
  type AnalyticsQuery,
  type PageQuery,
} from '@/api/endpoints/common'

export type * from '@/api/schema'
