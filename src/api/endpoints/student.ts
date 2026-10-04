import { api, request } from '@/api/client'
import { params, type AnalyticsQuery, type PageQuery } from '@/api/endpoints/common'
import type {
  ActivityItemDto,
  AnnouncementDto,
  AttendanceRecordDto,
  CertificateDto,
  CourseEnrollmentDto,
  CourseListItemDto,
  CourseProgressDto,
  CourseProgressReportDto,
  EducationalPaymentDto,
  ExamAttemptDto,
  ExamAttemptSessionDto,
  ExamDto,
  GroupEnrollmentDto,
  HomeworkDto,
  HomeworkSubmissionDto,
  InquiryDto,
  LessonProgressSummaryDto,
  Paginated,
  ScheduleSlotDto,
  GuardianDto,
  StudentAnalyticsDto,
  StudentDashboardDto,
  StudentGradesSummaryDto,
  StudentProfileDto,
  SubmitExamAttemptCommand,
  SubmitHomeworkCommand,
  TeacherListItemDto,
  UpdateStudentProfileCommand,
} from '@/api/schema'

export const studentApi = {
  /* ------------------------------- Overview ------------------------------- */
  dashboard: () => api.get<StudentDashboardDto>('/student/dashboard'),
  progress: () => api.get<CourseProgressDto[]>('/student/progress'),
  recentActivity: (take = 10) => api.get<ActivityItemDto[]>('/student/recent-activity', { take }),
  upcomingClasses: (take = 6) => api.get<ScheduleSlotDto[]>('/student/upcoming-classes', { take }),
  schedule: () => api.get<ScheduleSlotDto[]>('/student/schedule'),
  upcomingSchedule: (take = 8) => api.get<ScheduleSlotDto[]>('/student/schedule/upcoming', { take }),
  announcements: (query: PageQuery = {}) =>
    api.get<Paginated<AnnouncementDto>>('/student/announcements', params(query)),
  /**
   * Scores, attendance and subject strengths over a window. Computed server
   * side — the figures here are not derivable from the dashboard counters.
   */
  analytics: (query: AnalyticsQuery = {}) =>
    api.get<StudentAnalyticsDto>('/student/analytics', params(query)),

  /* ------------------------------ Enrollments ----------------------------- */
  courseEnrollments: (query: PageQuery & { Status?: string } = {}) =>
    api.get<Paginated<CourseEnrollmentDto>>('/student/enrollments/courses', params(query)),
  groupEnrollments: (query: PageQuery & { Status?: string } = {}) =>
    api.get<Paginated<GroupEnrollmentDto>>('/student/enrollments/groups', params(query)),

  /* ------------------------------- Learning ------------------------------- */
  courseProgress: (courseId: string) =>
    api.get<CourseProgressReportDto>(`/student/courses/${courseId}/progress`),
  completeLesson: (courseId: string, lessonId: string, watchedSeconds?: number) =>
    api.post<LessonProgressSummaryDto>(
      `/student/courses/${courseId}/lessons/${lessonId}/complete`,
      { watchedSeconds: watchedSeconds ?? null },
    ),
  uncompleteLesson: (courseId: string, lessonId: string) =>
    api.delete<LessonProgressSummaryDto>(
      `/student/courses/${courseId}/lessons/${lessonId}/complete`,
    ),

  /* --------------------------------- Exams -------------------------------- */
  exams: (query: PageQuery & { Upcoming?: boolean } = {}) =>
    api.get<Paginated<ExamDto>>('/student/exams', params(query)),
  exam: (id: string) => api.get<ExamDto>(`/student/exams/${id}`),
  startAttempt: (examId: string) =>
    api.post<ExamAttemptSessionDto>(`/student/exams/${examId}/attempts`),
  examAttempts: (examId: string) => api.get<ExamAttemptDto[]>(`/student/exams/${examId}/attempts`),
  attempt: (attemptId: string) => api.get<ExamAttemptDto>(`/student/exams/attempts/${attemptId}`),
  submitAttempt: (attemptId: string, body: Omit<SubmitExamAttemptCommand, 'attemptId'>) =>
    api.post<ExamAttemptDto>(`/student/exams/attempts/${attemptId}/submit`, {
      ...body,
      attemptId,
    }),
  grades: () => api.get<StudentGradesSummaryDto>('/student/grades'),

  /* ------------------------------- Homework ------------------------------- */
  homework: (query: PageQuery & { Status?: string } = {}) =>
    api.get<Paginated<HomeworkDto>>('/student/homework', params(query)),
  homeworkDetails: (id: string) => api.get<HomeworkDto>(`/student/homework/${id}`),
  submitHomework: (id: string, body: Omit<SubmitHomeworkCommand, 'homeworkId'>) =>
    api.post<HomeworkSubmissionDto>(`/student/homework/${id}/submit`, { ...body, homeworkId: id }),
  attachHomeworkFile: (id: string, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.upload<HomeworkSubmissionDto>(`/student/homework/${id}/attachment`, form)
  },
  updateSubmission: (submissionId: string, body: { content?: string | null; fileUrl?: string | null }) =>
    api.put<HomeworkSubmissionDto>(`/student/homework/submissions/${submissionId}`, body),

  /* ------------------------ Attendance & payments ------------------------- */
  attendance: (query: PageQuery & { GroupId?: string } = {}) =>
    api.get<Paginated<AttendanceRecordDto>>('/student/attendance', params(query)),
  payments: (query: PageQuery = {}) =>
    api.get<Paginated<EducationalPaymentDto>>('/student/payments', params(query)),

  /* ------------------------------ Favorites ------------------------------- */
  favoriteTeachers: (query: PageQuery = {}) =>
    api.get<Paginated<TeacherListItemDto>>('/student/favorites/teachers', params(query)),
  addFavoriteTeacher: (teacherId: string) =>
    api.post<void>(`/student/favorites/teachers/${teacherId}`),
  removeFavoriteTeacher: (teacherId: string) =>
    api.delete<void>(`/student/favorites/teachers/${teacherId}`),
  favoriteCourses: (query: PageQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>('/student/favorites/courses', params(query)),
  addFavoriteCourse: (courseId: string) => api.post<void>(`/student/favorites/courses/${courseId}`),
  removeFavoriteCourse: (courseId: string) =>
    api.delete<void>(`/student/favorites/courses/${courseId}`),

  /* ----------------------------- Certificates ----------------------------- */
  certificates: (query: PageQuery = {}) =>
    api.get<Paginated<CertificateDto>>('/student/certificates', params(query)),
  certificate: (id: string) => api.get<CertificateDto>(`/student/certificates/${id}`),

  /* ------------------------------- Identity ------------------------------- */
  profile: () => api.get<StudentProfileDto>('/student/profile'),
  updateProfile: (body: UpdateStudentProfileCommand) =>
    api.put<StudentProfileDto>('/student/profile', body),
  inquiries: (query: PageQuery = {}) =>
    api.get<Paginated<InquiryDto>>('/student/inquiries', params(query)),
  /** Parents who have asked to follow this student, confirmed or still pending. */
  guardians: () => api.get<GuardianDto[]>('/student/guardians'),
  respondToGuardian: (linkId: string, approve: boolean) =>
    request<void>(`/student/guardians/${linkId}/respond`, {
      method: 'POST',
      query: { approve },
    }),
}
