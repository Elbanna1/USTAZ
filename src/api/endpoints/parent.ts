import { api } from '@/api/client'
import { params, type AnalyticsQuery, type PageQuery } from '@/api/endpoints/common'
import type {
  AnnouncementDto,
  AttendanceRecordDto,
  ChildDto,
  ChildOverviewDto,
  EducationalPaymentDto,
  ExamDto,
  HomeworkSubmissionDto,
  LinkChildCommand,
  Paginated,
  ParentDashboardDto,
  ParentProfileDto,
  StudentAnalyticsDto,
  StudentGradesSummaryDto,
  UpdateParentProfileCommand,
} from '@/api/schema'

export const parentApi = {
  dashboard: () => api.get<ParentDashboardDto>('/parent/dashboard'),
  children: () => api.get<ChildDto[]>('/parent/children'),
  child: (studentId: string) => api.get<ChildDto>(`/parent/children/${studentId}`),
  linkChild: (body: LinkChildCommand) => api.post<ChildDto>('/parent/children/link', body),
  unlinkChild: (studentId: string) => api.delete<void>(`/parent/children/${studentId}`),

  progress: (studentId: string) =>
    api.get<ChildOverviewDto>(`/parent/children/${studentId}/progress`),
  grades: (studentId: string) =>
    api.get<StudentGradesSummaryDto>(`/parent/children/${studentId}/grades`),
  attendance: (studentId: string, query: PageQuery = {}) =>
    api.get<Paginated<AttendanceRecordDto>>(
      `/parent/children/${studentId}/attendance`,
      params(query),
    ),
  exams: (studentId: string, query: PageQuery = {}) =>
    api.get<Paginated<ExamDto>>(`/parent/children/${studentId}/exams`, params(query)),
  homework: (studentId: string, query: PageQuery = {}) =>
    api.get<Paginated<HomeworkSubmissionDto>>(
      `/parent/children/${studentId}/homework`,
      params(query),
    ),
  payments: (studentId: string, query: PageQuery = {}) =>
    api.get<Paginated<EducationalPaymentDto>>(
      `/parent/children/${studentId}/payments`,
      params(query),
    ),

  /** The same computed report the child sees, scoped to a linked child. */
  analytics: (studentId: string, query: AnalyticsQuery = {}) =>
    api.get<StudentAnalyticsDto>(`/parent/children/${studentId}/analytics`, params(query)),

  announcements: (query: PageQuery = {}) =>
    api.get<Paginated<AnnouncementDto>>('/parent/announcements', params(query)),

  profile: () => api.get<ParentProfileDto>('/parent/profile'),
  updateProfile: (body: UpdateParentProfileCommand) =>
    api.put<ParentProfileDto>('/parent/profile', body),
}
