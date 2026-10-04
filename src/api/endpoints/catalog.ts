import { api } from '@/api/client'
import { params, type PageQuery } from '@/api/endpoints/common'
import type {
  CourseDetailsDto,
  CourseEnrollmentDto,
  CourseListItemDto,
  CourseSectionDto,
  CreateInquiryCommand,
  CreateReviewCommand,
  DiscountValidationResultDto,
  EnrollInCourseCommand,
  EnrollInGroupCommand,
  GlobalSearchResultDto,
  GradeDto,
  GroupEnrollmentDto,
  InquiryDto,
  GroupCurriculumDto,
  GroupDetailsDto,
  GroupListItemDto,
  GroupMaterialDto,
  GroupScheduleDto,
  Paginated,
  ReviewDto,
  ScheduleSlotDto,
  SubjectDto,
  TeacherAchievementDto,
  TeacherDetailsDto,
  TeacherListItemDto,
  UpdateProgressRequest,
  UpdateReviewCommand,
  ValidateDiscountQuery,
} from '@/api/schema'

export interface TeacherQuery extends PageQuery {
  SubjectId?: string
  GradeId?: string
  City?: string
  TeachingMode?: 'Online' | 'Offline' | 'Hybrid'
  MinRating?: number
  FeaturedOnly?: boolean
  IncludeUnverified?: boolean
}

export interface CourseQuery extends PageQuery {
  SubjectId?: string
  GradeId?: string
  TeacherId?: string
  MinPrice?: number
  MaxPrice?: number
  IsFree?: boolean
  FeaturedOnly?: boolean
  MinRating?: number
  IncludeUnpublished?: boolean
}

export interface GroupQuery extends PageQuery {
  SubjectId?: string
  GradeId?: string
  TeacherId?: string
  TeachingMode?: 'Online' | 'Offline' | 'Hybrid'
  Type?: 'Regular' | 'Revision' | 'Intensive' | 'ExamPreparation'
  MinPrice?: number
  MaxPrice?: number
  HasDiscount?: boolean
  HasAvailableSeats?: boolean
  City?: string
  IncludeUnpublished?: boolean
}

export interface ReviewQuery extends PageQuery {
  TeacherId?: string
  GroupId?: string
  CourseId?: string
  MinRating?: number
  IncludeUnapproved?: boolean
}

/** Reference data — subjects and grades power every filter in the product. */
export const taxonomyApi = {
  subjects: () => api.get<SubjectDto[]>('/subjects', undefined, { anonymous: true }),
  subject: (idOrSlug: string) =>
    api.get<SubjectDto>(`/subjects/${idOrSlug}`, undefined, { anonymous: true }),
  grades: () => api.get<GradeDto[]>('/grades', undefined, { anonymous: true }),
  grade: (idOrSlug: string) =>
    api.get<GradeDto>(`/grades/${idOrSlug}`, undefined, { anonymous: true }),
}

export const teachersApi = {
  list: (query: TeacherQuery = {}) =>
    api.get<Paginated<TeacherListItemDto>>('/teachers', params(query)),
  details: (id: string) => api.get<TeacherDetailsDto>(`/teachers/${id}`),
  profile: (id: string) => api.get<TeacherDetailsDto>(`/teachers/${id}/profile`),
  achievements: (id: string) => api.get<TeacherAchievementDto[]>(`/teachers/${id}/achievements`),
  /**
   * Both routes take paging and search only — the subject/price/mode filters
   * that `/courses` and `/groups` accept are not declared here, so they would
   * be dropped silently. Narrow the type rather than advertise them.
   */
  courses: (id: string, query: PageQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>(`/teachers/${id}/courses`, params(query)),
  groups: (id: string, query: PageQuery = {}) =>
    api.get<Paginated<GroupListItemDto>>(`/teachers/${id}/groups`, params(query)),
  reviews: (id: string, query: ReviewQuery = {}) =>
    api.get<Paginated<ReviewDto>>(`/teachers/${id}/reviews`, params(query)),
  reviewsSummary: (id: string) =>
    api.get<Record<string, unknown>>(`/teachers/${id}/reviews/summary`),
  schedule: (id: string) => api.get<ScheduleSlotDto[]>(`/teachers/${id}/schedule`),
  /**
   * The same teacher list narrowed to one subject or grade. Both routes accept
   * the full teacher filter set on top of the path id, and neither declares a
   * response schema in the OpenAPI document — the shape matches `/teachers`.
   */
  bySubject: (subjectId: string, query: TeacherQuery = {}) =>
    api.get<Paginated<TeacherListItemDto>>(`/subjects/${subjectId}/teachers`, params(query)),
  byGrade: (gradeId: string, query: TeacherQuery = {}) =>
    api.get<Paginated<TeacherListItemDto>>(`/grades/${gradeId}/teachers`, params(query)),
}

export const coursesApi = {
  list: (query: CourseQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>('/courses', params(query)),
  details: (id: string) => api.get<CourseDetailsDto>(`/courses/${id}`),
  sections: (id: string) => api.get<CourseSectionDto[]>(`/courses/${id}/sections`),
  reviews: (id: string, query: ReviewQuery = {}) =>
    api.get<Paginated<ReviewDto>>(`/courses/${id}/reviews`, params(query)),
  bySubject: (subjectId: string, query: CourseQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>(`/subjects/${subjectId}/courses`, params(query)),
  byGrade: (gradeId: string, query: CourseQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>(`/grades/${gradeId}/courses`, params(query)),
}

export const groupsApi = {
  list: (query: GroupQuery = {}) => api.get<Paginated<GroupListItemDto>>('/groups', params(query)),
  details: (id: string) => api.get<GroupDetailsDto>(`/groups/${id}`),
  curriculum: (id: string) => api.get<GroupCurriculumDto[]>(`/groups/${id}/curriculum`),
  materials: (id: string) => api.get<GroupMaterialDto[]>(`/groups/${id}/materials`),
  schedule: (id: string) => api.get<GroupScheduleDto[]>(`/groups/${id}/schedule`),
  reviews: (id: string, query: ReviewQuery = {}) =>
    api.get<Paginated<ReviewDto>>(`/groups/${id}/reviews`, params(query)),
  bySubject: (subjectId: string, query: GroupQuery = {}) =>
    api.get<Paginated<GroupListItemDto>>(`/subjects/${subjectId}/groups`, params(query)),
  byGrade: (gradeId: string, query: GroupQuery = {}) =>
    api.get<Paginated<GroupListItemDto>>(`/grades/${gradeId}/groups`, params(query)),
}

export interface ScheduleQuery {
  TeacherId?: string
  SubjectId?: string
  GradeId?: string
  DayOfWeek?: string
  TeachingMode?: 'Online' | 'Offline' | 'Hybrid'
  Take?: number
}

export const scheduleApi = {
  list: (query: ScheduleQuery = {}) => api.get<ScheduleSlotDto[]>('/schedule', params(query)),
  upcoming: (take = 8) => api.get<ScheduleSlotDto[]>('/schedule/upcoming', { take }),
}

export const searchApi = {
  global: (q: string, options: { Take?: number; SubjectId?: string; GradeId?: string } = {}) =>
    api.get<GlobalSearchResultDto>('/search', params({ Q: q, ...options })),
}

export const reviewsApi = {
  list: (query: ReviewQuery = {}) => api.get<Paginated<ReviewDto>>('/reviews', params(query)),
  create: (body: CreateReviewCommand) => api.post<ReviewDto>('/reviews', body),
  update: (id: string, body: Omit<UpdateReviewCommand, 'reviewId'>) =>
    api.put<ReviewDto>(`/reviews/${id}`, { ...body, reviewId: id }),
  remove: (id: string) => api.delete<void>(`/reviews/${id}`),
}

export const inquiriesApi = {
  create: (body: CreateInquiryCommand) => api.post<InquiryDto>('/inquiries', body),
  details: (id: string) => api.get<InquiryDto>(`/inquiries/${id}`),
}

export const enrollmentsApi = {
  enrollInCourse: (body: EnrollInCourseCommand) =>
    api.post<CourseEnrollmentDto>('/enrollments/courses', body),
  courseEnrollment: (id: string) => api.get<CourseEnrollmentDto>(`/enrollments/courses/${id}`),
  cancelCourse: (id: string, reason?: string) =>
    api.post<void>(`/enrollments/courses/${id}/cancel`, { reason }),
  enrollInGroup: (body: EnrollInGroupCommand) =>
    api.post<GroupEnrollmentDto>('/enrollments/groups', body),
  groupEnrollment: (id: string) => api.get<GroupEnrollmentDto>(`/enrollments/groups/${id}`),
  cancelGroup: (id: string, reason?: string) =>
    api.post<void>(`/enrollments/groups/${id}/cancel`, { reason }),
  /**
   * Sets the headline percentage on a course enrollment. Per-lesson completion
   * goes through `studentApi.completeLesson`, which recomputes this value — use
   * this only where the learner reports progress directly.
   */
  updateCourseProgress: (enrollmentId: string, progressPercentage: number) =>
    api.put<CourseEnrollmentDto>(`/enrollments/courses/${enrollmentId}/progress`, {
      progressPercentage,
    } satisfies UpdateProgressRequest),
}

export const discountsApi = {
  validate: (body: ValidateDiscountQuery) =>
    api.post<DiscountValidationResultDto>('/discounts/validate', body),
}
