import { api, request } from '@/api/client'
import { params, type PageQuery } from '@/api/endpoints/common'
import type { CourseQuery, GroupQuery, ReviewQuery, TeacherQuery } from '@/api/endpoints/catalog'
import type {
  AdminDashboardDto,
  AdminTenantDto,
  AuditLogDto,
  CourseListItemDto,
  DiscountDto,
  GroupEnrollmentDto,
  GroupListItemDto,
  InquiryDto,
  ModerateReviewCommand,
  Paginated,
  RevenuePointDto,
  ReviewDto,
  StudentProfileDto,
  SubscriptionPlanDto,
  TeacherDetailsDto,
  TeacherListItemDto,
} from '@/api/schema'

export interface TenantQuery extends PageQuery {
  Status?: string
  SubscriptionStatus?: string
  PlanCode?: string
}

export interface AuditLogQuery extends PageQuery {
  TenantId?: string
  UserId?: string
  Action?: string
  EntityName?: string
  From?: string
  To?: string
}

export interface AdminTeacherQuery extends PageQuery {
  VerificationStatus?: string
  IsActive?: boolean
  IsFeatured?: boolean
}

export const adminApi = {
  /* ------------------------------- Overview ------------------------------- */
  dashboard: (months = 6) => api.get<AdminDashboardDto>('/admin/dashboard', { months }),
  analytics: (months = 6) => api.get<AdminDashboardDto>('/admin/analytics', { months }),
  revenue: (months = 6) => api.get<RevenuePointDto[]>('/admin/analytics/revenue', { months }),
  reports: (months = 12) => api.get<RevenuePointDto[]>('/admin/reports', { months }),

  /* -------------------------------- Tenants ------------------------------- */
  tenants: (query: TenantQuery = {}) =>
    api.get<Paginated<AdminTenantDto>>('/admin/tenants', params(query)),
  tenantAnalytics: (query: TenantQuery = {}) =>
    api.get<Paginated<AdminTenantDto>>('/admin/analytics/tenants', params(query)),
  activateTenant: (id: string) => api.post<void>(`/admin/tenants/${id}/activate`),
  suspendTenant: (id: string, reason?: string) =>
    api.post<void>(`/admin/tenants/${id}/suspend`, { reason }),

  /* --------------------------------- People ------------------------------- */
  teachers: (query: AdminTeacherQuery = {}) =>
    api.get<Paginated<TeacherListItemDto>>('/admin/teachers', params(query)),
  teacher: (id: string) => api.get<TeacherDetailsDto>(`/admin/teachers/${id}`),
  /** Moves a Pending request to UnderReview so two admins do not both work it. */
  beginTeacherReview: (id: string) => api.post<void>(`/admin/teachers/${id}/begin-review`),
  verifyTeacher: (id: string, approve: boolean, notes?: string) =>
    api.post<void>(`/admin/teachers/${id}/verify`, { approve, notes }),
  activateTeacher: (id: string) => api.post<void>(`/admin/teachers/${id}/activate`),
  suspendTeacher: (id: string, reason?: string) =>
    api.post<void>(`/admin/teachers/${id}/suspend`, { reason }),
  featureTeacher: (id: string, featured: boolean) =>
    request<void>(`/admin/teachers/${id}/feature`, { method: 'POST', query: { featured } }),
  students: (query: PageQuery & { IsActive?: boolean; GradeId?: string } = {}) =>
    api.get<Paginated<StudentProfileDto>>('/admin/students', params(query)),
  setUserState: (userId: string, isActive: boolean, reason?: string) =>
    api.post<void>(`/admin/users/${userId}/state`, { userId, isActive, reason }),

  /* -------------------------------- Catalog ------------------------------- */
  courses: (query: CourseQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>('/admin/courses', params(query)),
  groups: (query: GroupQuery = {}) =>
    api.get<Paginated<GroupListItemDto>>('/admin/groups', params(query)),
  enrollments: (query: PageQuery & { GroupId?: string; Status?: string } = {}) =>
    api.get<Paginated<GroupEnrollmentDto>>('/admin/enrollments', params(query)),
  discounts: (query: PageQuery & { IsActive?: boolean; CurrentlyValidOnly?: boolean } = {}) =>
    api.get<Paginated<DiscountDto>>('/admin/discounts', params(query)),
  plans: () => api.get<SubscriptionPlanDto[]>('/admin/plans'),

  /* ------------------------------ Moderation ------------------------------ */
  reviews: (query: ReviewQuery = {}) =>
    api.get<Paginated<ReviewDto>>('/admin/reviews', params(query)),
  moderateReview: (id: string, body: Omit<ModerateReviewCommand, 'reviewId'>) =>
    api.post<void>(`/admin/reviews/${id}/moderate`, { ...body, reviewId: id }),
  inquiries: (query: PageQuery & { Status?: string; GroupId?: string } = {}) =>
    api.get<Paginated<InquiryDto>>('/admin/inquiries', params(query)),
  auditLogs: (query: AuditLogQuery = {}) =>
    api.get<Paginated<AuditLogDto>>('/admin/audit-logs', params(query)),
}

export type { TeacherQuery }
