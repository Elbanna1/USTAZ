import { api, request } from '@/api/client'
import { params, type PageQuery } from '@/api/endpoints/common'
import type {
  BillingTransactionDto,
  CalendarResponseDto,
  CancelSubscriptionCommand,
  CertificateDto,
  CertificateVerificationDto,
  ChangePlanCommand,
  CheckoutSessionDto,
  ConversationDto,
  CourseListItemDto,
  CreateCheckoutCommand,
  GrantPlatformAdminCommand,
  GroupListItemDto,
  InvoiceDto,
  MessageDto,
  NotificationDto,
  NotificationPreferenceDto,
  Paginated,
  PlatformAdministratorDto,
  PlatformOverviewDto,
  PlatformUserDto,
  PublicTenantDto,
  RevokePlatformAdminCommand,
  SendMessageCommand,
  StartConversationCommand,
  SubscriptionPlanDto,
  TeacherListItemDto,
  TenantBrandingDto,
  TenantSubscriptionDto,
  UnreadCountDto,
  UnreadMessagesDto,
  UpdateNotificationPreferencesCommand,
  WebsiteSeoDto,
} from '@/api/schema'

/* -------------------------------------------------------------------------- */
/*                                Notifications                               */
/* -------------------------------------------------------------------------- */

export interface NotificationQuery extends PageQuery {
  UnreadOnly?: boolean
  Type?: string
  Category?: string
  MinimumPriority?: string
}

export const notificationsApi = {
  list: (query: NotificationQuery = {}) =>
    api.get<Paginated<NotificationDto>>('/notifications', params(query)),
  details: (id: string) => api.get<NotificationDto>(`/notifications/${id}`),
  markRead: (id: string) => api.put<void>(`/notifications/${id}/read`),
  markAllRead: () => api.put<void>('/notifications/read-all'),
  remove: (id: string) => api.delete<void>(`/notifications/${id}`),
  clearRead: () => api.delete<number>('/notifications/read'),
  unreadCount: () => api.get<UnreadCountDto>('/notifications/unread-count'),
  preferences: () => api.get<NotificationPreferenceDto[]>('/notifications/preferences'),
  updatePreferences: (body: UpdateNotificationPreferencesCommand) =>
    api.put<NotificationPreferenceDto[]>('/notifications/preferences', body),
}

/* -------------------------------------------------------------------------- */
/*                                  Messages                                  */
/* -------------------------------------------------------------------------- */

export const messagesApi = {
  conversations: (query: PageQuery & { UnreadOnly?: boolean } = {}) =>
    api.get<Paginated<ConversationDto>>('/messages/conversations', params(query)),
  start: (body: StartConversationCommand) =>
    api.post<ConversationDto>('/messages/conversations', body),
  messages: (conversationId: string, query: PageQuery = {}) =>
    api.get<Paginated<MessageDto>>(
      `/messages/conversations/${conversationId}`,
      params({ ...query, ConversationId: conversationId }),
    ),
  send: (conversationId: string, body: string) =>
    api.post<MessageDto>(`/messages/conversations/${conversationId}/messages`, {
      conversationId,
      body,
    } satisfies SendMessageCommand),
  sendAttachment: (conversationId: string, file: File, body?: string) => {
    const form = new FormData()
    form.append('file', file)
    if (body) form.append('body', body)
    return api.upload<MessageDto>(`/messages/conversations/${conversationId}/attachments`, form)
  },
  markRead: (messageId: string) => api.put<void>(`/messages/messages/${messageId}/read`),
  unreadCount: () => api.get<UnreadMessagesDto>('/messages/unread-count'),
  attachmentUrl: (attachmentId: string) => `/messages/attachments/${attachmentId}`,
}

/* -------------------------------------------------------------------------- */
/*                                   Billing                                  */
/* -------------------------------------------------------------------------- */

export const billingApi = {
  plans: () => api.get<SubscriptionPlanDto[]>('/billing/plans', undefined, { anonymous: true }),
  plan: (id: string) =>
    api.get<SubscriptionPlanDto>(`/billing/plans/${id}`, undefined, { anonymous: true }),
  subscription: () => api.get<TenantSubscriptionDto>('/billing/subscription'),
  checkout: (body: CreateCheckoutCommand) =>
    api.post<CheckoutSessionDto>('/billing/checkout', body),
  changePlan: (body: ChangePlanCommand) =>
    api.post<TenantSubscriptionDto>('/billing/change-plan', body),
  cancel: (body: CancelSubscriptionCommand) =>
    api.post<TenantSubscriptionDto>('/billing/cancel', body),
  reactivate: () => api.post<TenantSubscriptionDto>('/billing/reactivate'),
  invoices: (query: PageQuery & { Status?: string } = {}) =>
    api.get<Paginated<InvoiceDto>>('/billing/invoices', params(query)),
  invoice: (id: string) => api.get<InvoiceDto>(`/billing/invoices/${id}`),
  transactions: (query: PageQuery & { Status?: string } = {}) =>
    api.get<Paginated<BillingTransactionDto>>('/billing/transactions', params(query)),
  transaction: (id: string) => api.get<BillingTransactionDto>(`/billing/transactions/${id}`),
}

/* -------------------------------------------------------------------------- */
/*                          Certificates & public site                        */
/* -------------------------------------------------------------------------- */

export const certificatesApi = {
  verify: (code: string) =>
    api.get<CertificateVerificationDto>(`/public/certificates/verify/${encodeURIComponent(code)}`, undefined, {
      anonymous: true,
    }),
}

export const publicSiteApi = {
  tenant: (slug: string) =>
    api.get<PublicTenantDto>(`/public/tenants/${slug}`, undefined, { anonymous: true }),
  seo: (slug: string) =>
    api.get<WebsiteSeoDto>(`/public/tenants/${slug}/seo`, undefined, { anonymous: true }),
  /**
   * Colours, logo and tagline for a workspace's own public page. The four
   * routes below declare no response schema in the OpenAPI document, so the
   * types here come from the matching authenticated endpoints that do.
   */
  branding: (slug: string) =>
    api.get<TenantBrandingDto>(`/public/tenants/${slug}/branding`, undefined, { anonymous: true }),
  teachers: (slug: string) =>
    api.get<TeacherListItemDto[]>(`/public/tenants/${slug}/teachers`, undefined, {
      anonymous: true,
    }),
  groups: (slug: string, query: PageQuery = {}) =>
    api.get<Paginated<GroupListItemDto>>(`/public/tenants/${slug}/groups`, params(query), {
      anonymous: true,
    }),
  courses: (slug: string, query: PageQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>(`/public/tenants/${slug}/courses`, params(query), {
      anonymous: true,
    }),
}

/* -------------------------------------------------------------------------- */
/*                         Platform super-administration                      */
/* -------------------------------------------------------------------------- */

export interface PlatformUserQuery extends PageQuery {
  Role?: 'Student' | 'Teacher' | 'Parent' | 'Admin'
  IsActive?: boolean
  SchoolId?: string
  SortBy?: string
  SortDescending?: boolean
}

/**
 * Cross-workspace administration — the view that sits above any single tenant.
 * Distinct from `adminApi`: these routes manage who holds the platform Admin
 * role at all, which is why granting and revoking both take a reason.
 */
export const platformApi = {
  overview: () => api.get<PlatformOverviewDto>('/platform/overview'),
  users: (query: PlatformUserQuery = {}) =>
    api.get<Paginated<PlatformUserDto>>('/platform/users', params(query)),
  administrators: () => api.get<PlatformAdministratorDto[]>('/platform/administrators'),
  grantAdmin: (userId: string, reason?: string) =>
    api.post<void>(`/platform/administrators/${userId}`, {
      userId,
      reason: reason ?? null,
    } satisfies GrantPlatformAdminCommand),
  /**
   * Sent with a body, so it cannot go through `api.delete`. The server refuses
   * to revoke the last administrator — surface that 422 rather than hiding it.
   */
  revokeAdmin: (userId: string, reason?: string) =>
    request<void>(`/platform/administrators/${userId}`, {
      method: 'DELETE',
      body: { userId, reason: reason ?? null } satisfies RevokePlatformAdminCommand,
    }),
}

export type { CertificateDto }

/* -------------------------------------------------------------------------- */
/*                                  Calendar                                  */
/* -------------------------------------------------------------------------- */

/** The four kinds of entry the calendar merges. Sent by name, not ordinal. */
export type CalendarEventKind = 'Class' | 'Exam' | 'HomeworkDeadline' | 'AttendanceSession'

export interface CalendarQuery {
  /** Inclusive day bounds. The server refuses a range wider than 180 days. */
  From?: string
  To?: string
  GroupId?: string
  /** Empty means every kind. */
  Types?: CalendarEventKind[]
  /**
   * A guardian must name the child whose calendar they want — there is no
   * default child, and the request is refused without it.
   */
  StudentId?: string
}

export const calendarApi = {
  /**
   * One feed of everything scheduled — classes, exams, homework deadlines and
   * sessions actually held. Scope comes from the caller's role rather than a
   * parameter, so the same call serves students, teachers and parents.
   */
  range: (query: CalendarQuery) => api.get<CalendarResponseDto>('/calendar', params(query)),
}
