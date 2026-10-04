import { api, request } from '@/api/client'
import { params, type AnalyticsQuery, type PageQuery } from '@/api/endpoints/common'
import type {
  ActivityEventDto,
  AddCourseLessonCommand,
  AddCourseSectionCommand,
  AddExamQuestionCommand,
  AddGroupCurriculumCommand,
  AddGroupScheduleCommand,
  AnnouncementDto,
  AtRiskStudentDto,
  AttendanceRecordDto,
  AttendanceSessionDto,
  AttendanceStatisticsDto,
  BulkGradeEntry,
  BulkGradeResultDto,
  CertificateDto,
  CourseDetailsDto,
  CourseEnrollmentDto,
  CourseLessonDto,
  CourseListItemDto,
  CoursePerformanceDto,
  CourseSectionDto,
  CreateAnnouncementCommand,
  CreateAttendanceSessionCommand,
  CreateCourseCommand,
  CreateDiscountCommand,
  CreateExamCommand,
  CreateGroupCommand,
  CreateHomeworkCommand,
  DiscountDto,
  EducationalPaymentDto,
  EnrollStudentInGroupCommand,
  ExamAttemptDto,
  ExamDto,
  ExamQuestionDto,
  ExamResultDto,
  GradeAnalyticsDto,
  GradeDto,
  GradeExamAttemptCommand,
  GradeHomeworkSubmissionCommand,
  GroupCurriculumDto,
  GroupDetailsDto,
  GroupEnrollmentDto,
  GroupListItemDto,
  GroupPerformanceDto,
  GroupScheduleDto,
  HomeworkDto,
  HomeworkSubmissionDto,
  InquiryDto,
  MediaUploadResult,
  Paginated,
  RecordAttendanceCommand,
  RecordEducationalPaymentCommand,
  RecordExamResultsCommand,
  RevenuePointDto,
  ScheduleSlotDto,
  StudentGradesSummaryDto,
  StudentAnalyticsDto,
  StudentGrowthPointDto,
  SubjectDto,
  TeacherAchievementDto,
  TeacherAnalyticsDto,
  TeacherAnalyticsOverviewDto,
  TeacherDashboardDto,
  TeacherDetailsDto,
  TeacherSocialLinkDto,
  UpdateAnnouncementCommand,
  UpdateAttendanceRecordCommand,
  UpdateAttendanceSessionCommand,
  UpdateCourseCommand,
  UpdateCourseLessonCommand,
  UpdateCourseSectionCommand,
  UpdateDiscountCommand,
  UpdateExamCommand,
  UpdateExamQuestionCommand,
  UpdateExamResultCommand,
  UpdateGroupCommand,
  UpdateGroupCurriculumCommand,
  UpdateGroupScheduleCommand,
  UpdateHomeworkCommand,
  UpdateInquiryStatusCommand,
  UpdateTeacherAboutRequest,
  UpdateTeacherAchievementsCommand,
  UpdateTeacherGradesCommand,
  UpdateTeacherProfileCommand,
  UpdateTeacherSocialLinksCommand,
  UpdateTeacherSubjectsCommand,
} from '@/api/schema'

function fileForm(file: File): FormData {
  const form = new FormData()
  form.append('file', file)
  return form
}

/* -------------------------------------------------------------------------- */
/*                                   Profile                                  */
/* -------------------------------------------------------------------------- */

export const teacherProfileApi = {
  get: () => api.get<TeacherDetailsDto>('/teacher/profile'),
  update: (body: UpdateTeacherProfileCommand) =>
    api.put<TeacherDetailsDto>('/teacher/profile', body),
  updateAbout: (body: UpdateTeacherAboutRequest) =>
    api.put<TeacherDetailsDto>('/teacher/about', body),
  updateSubjects: (body: UpdateTeacherSubjectsCommand) =>
    api.put<SubjectDto[]>('/teacher/subjects', body),
  updateGrades: (body: UpdateTeacherGradesCommand) => api.put<GradeDto[]>('/teacher/grades', body),
  updateAchievements: (body: UpdateTeacherAchievementsCommand) =>
    api.put<TeacherAchievementDto[]>('/teacher/achievements', body),
  updateSocialLinks: (body: UpdateTeacherSocialLinksCommand) =>
    api.put<TeacherSocialLinkDto[]>('/teacher/social-links', body),
  uploadImage: (file: File) =>
    api.upload<MediaUploadResult>('/teacher/profile/image', fileForm(file)),
  uploadCover: (file: File) =>
    api.upload<MediaUploadResult>('/teacher/profile/cover', fileForm(file)),
}

/* -------------------------------------------------------------------------- */
/*                             Dashboard & analytics                          */
/* -------------------------------------------------------------------------- */

export const teacherAnalyticsApi = {
  dashboard: () => api.get<TeacherDashboardDto>('/teacher/dashboard'),
  overview: (months = 6) => api.get<TeacherAnalyticsDto>('/teacher/analytics', { months }),
  revenue: (months = 6) => api.get<RevenuePointDto[]>('/teacher/analytics/revenue', { months }),
  studentGrowth: (months = 6) =>
    api.get<StudentGrowthPointDto[]>('/teacher/analytics/students', { months }),
  courses: () => api.get<CoursePerformanceDto[]>('/teacher/analytics/courses'),
  groups: () => api.get<GroupPerformanceDto[]>('/teacher/analytics/groups'),
  grades: (query: { GroupId?: string; SubjectId?: string; Type?: string; From?: string; To?: string } = {}) =>
    api.get<GradeAnalyticsDto>('/teacher/analytics/grades', params(query)),
  attendance: (query: { GroupId?: string; StudentId?: string; From?: string; To?: string } = {}) =>
    api.get<AttendanceStatisticsDto>('/teacher/analytics/attendance', params(query)),
  atRisk: (query: { WindowInDays?: number; MinimumLevel?: string; Take?: number } = {}) =>
    api.get<AtRiskStudentDto[]>('/teacher/analytics/at-risk', params(query)),
  atRiskStudent: (studentId: string) =>
    api.get<AtRiskStudentDto>(`/teacher/analytics/at-risk/${studentId}`),

  /**
   * The windowed roll-up: students, catalogue, teaching load, performance and
   * revenue in one call. `overview` above is the older months-based summary and
   * returns a different shape — they are separate endpoints, not aliases.
   */
  windowedOverview: (query: AnalyticsQuery = {}) =>
    api.get<TeacherAnalyticsOverviewDto>('/teacher/analytics/overview', params(query)),

  /** One of this teacher's students, measured the same way the student sees it. */
  studentAnalytics: (studentId: string, query: AnalyticsQuery = {}) =>
    api.get<StudentAnalyticsDto>(`/teacher/analytics/students/${studentId}`, params(query)),
}

/* -------------------------------------------------------------------------- */
/*                                   Groups                                   */
/* -------------------------------------------------------------------------- */

export interface TeacherGroupQuery extends PageQuery {
  IsPublished?: boolean
  IsActive?: boolean
  SubjectId?: string
  GradeId?: string
}

export const teacherGroupsApi = {
  list: (query: TeacherGroupQuery = {}) =>
    api.get<Paginated<GroupListItemDto>>('/teacher/groups', params(query)),
  details: (id: string) => api.get<GroupDetailsDto>(`/teacher/groups/${id}`),
  create: (body: CreateGroupCommand) => api.post<GroupDetailsDto>('/teacher/groups', body),
  update: (id: string, body: UpdateGroupCommand) =>
    api.put<GroupDetailsDto>(`/teacher/groups/${id}`, { ...body, groupId: id }),
  remove: (id: string) => api.delete<void>(`/teacher/groups/${id}`),
  publish: (id: string) => api.post<void>(`/teacher/groups/${id}/publish`),
  unpublish: (id: string) => api.post<void>(`/teacher/groups/${id}/unpublish`),
  enrollStudent: (groupId: string, body: Omit<EnrollStudentInGroupCommand, 'groupId'>) =>
    api.post<GroupEnrollmentDto>(`/teacher/groups/${groupId}/students`, { ...body, groupId }),

  addCurriculum: (id: string, body: AddGroupCurriculumCommand) =>
    api.post<GroupCurriculumDto>(`/teacher/groups/${id}/curriculum`, { ...body, groupId: id }),
  updateCurriculum: (id: string, curriculumId: string, body: UpdateGroupCurriculumCommand) =>
    api.put<GroupCurriculumDto>(`/teacher/groups/${id}/curriculum/${curriculumId}`, {
      ...body,
      groupId: id,
      curriculumId,
    }),
  removeCurriculum: (id: string, curriculumId: string) =>
    api.delete<void>(`/teacher/groups/${id}/curriculum/${curriculumId}`),

  addSchedule: (id: string, body: AddGroupScheduleCommand) =>
    api.post<GroupScheduleDto>(`/teacher/groups/${id}/schedule`, { ...body, groupId: id }),
  updateSchedule: (id: string, scheduleId: string, body: UpdateGroupScheduleCommand) =>
    api.put<GroupScheduleDto>(`/teacher/groups/${id}/schedule/${scheduleId}`, {
      ...body,
      groupId: id,
      scheduleId,
    }),
  removeSchedule: (id: string, scheduleId: string) =>
    api.delete<void>(`/teacher/groups/${id}/schedule/${scheduleId}`),
}

/* -------------------------------------------------------------------------- */
/*                                   Courses                                  */
/* -------------------------------------------------------------------------- */

export interface TeacherCourseQuery extends PageQuery {
  IsPublished?: boolean
  SubjectId?: string
  GradeId?: string
}

export const teacherCoursesApi = {
  list: (query: TeacherCourseQuery = {}) =>
    api.get<Paginated<CourseListItemDto>>('/teacher/courses', params(query)),
  details: (id: string) => api.get<CourseDetailsDto>(`/teacher/courses/${id}`),
  create: (body: CreateCourseCommand) => api.post<CourseDetailsDto>('/teacher/courses', body),
  update: (id: string, body: UpdateCourseCommand) =>
    api.put<CourseDetailsDto>(`/teacher/courses/${id}`, { ...body, courseId: id }),
  remove: (id: string) => api.delete<void>(`/teacher/courses/${id}`),
  publish: (id: string) => api.post<void>(`/teacher/courses/${id}/publish`),
  unpublish: (id: string) => api.post<void>(`/teacher/courses/${id}/unpublish`),

  addSection: (id: string, body: AddCourseSectionCommand) =>
    api.post<CourseSectionDto>(`/teacher/courses/${id}/sections`, { ...body, courseId: id }),
  updateSection: (id: string, sectionId: string, body: UpdateCourseSectionCommand) =>
    api.put<CourseSectionDto>(`/teacher/courses/${id}/sections/${sectionId}`, {
      ...body,
      courseId: id,
      sectionId,
    }),
  removeSection: (id: string, sectionId: string) =>
    api.delete<void>(`/teacher/courses/${id}/sections/${sectionId}`),

  addLesson: (id: string, body: AddCourseLessonCommand) =>
    api.post<CourseLessonDto>(`/teacher/courses/${id}/lessons`, { ...body, courseId: id }),
  updateLesson: (id: string, lessonId: string, body: UpdateCourseLessonCommand) =>
    api.put<CourseLessonDto>(`/teacher/courses/${id}/lessons/${lessonId}`, {
      ...body,
      courseId: id,
      lessonId,
    }),
  removeLesson: (id: string, lessonId: string) =>
    api.delete<void>(`/teacher/courses/${id}/lessons/${lessonId}`),
}

/* -------------------------------------------------------------------------- */
/*                                    Exams                                   */
/* -------------------------------------------------------------------------- */

export interface TeacherExamQuery extends PageQuery {
  GroupId?: string
  SubjectId?: string
  Type?: string
  IsPublished?: boolean
  From?: string
  To?: string
}

export const teacherExamsApi = {
  list: (query: TeacherExamQuery = {}) =>
    api.get<Paginated<ExamDto>>('/teacher/exams', params(query)),
  details: (id: string) => api.get<ExamDto>(`/teacher/exams/${id}`),
  create: (body: CreateExamCommand) => api.post<ExamDto>('/teacher/exams', body),
  update: (id: string, body: UpdateExamCommand) =>
    api.put<ExamDto>(`/teacher/exams/${id}`, { ...body, examId: id }),
  remove: (id: string) => api.delete<void>(`/teacher/exams/${id}`),
  publish: (id: string, publishResults = false) =>
    request<void>(`/teacher/exams/${id}/publish`, {
      method: 'POST',
      query: { publishResults },
    }),

  questions: (id: string) => api.get<ExamQuestionDto[]>(`/teacher/exams/${id}/questions`),
  addQuestion: (id: string, body: AddExamQuestionCommand) =>
    api.post<ExamQuestionDto>(`/teacher/exams/${id}/questions`, { ...body, examId: id }),
  updateQuestion: (id: string, questionId: string, body: UpdateExamQuestionCommand) =>
    api.put<ExamQuestionDto>(`/teacher/exams/${id}/questions/${questionId}`, {
      ...body,
      examId: id,
      questionId,
    }),
  removeQuestion: (id: string, questionId: string) =>
    api.delete<void>(`/teacher/exams/${id}/questions/${questionId}`),

  attempts: (id: string, query: PageQuery & { Status?: string; AwaitingGrading?: boolean } = {}) =>
    api.get<Paginated<ExamAttemptDto>>(`/teacher/exams/${id}/attempts`, params(query)),
  attempt: (attemptId: string) => api.get<ExamAttemptDto>(`/teacher/exams/attempts/${attemptId}`),
  gradeAttempt: (attemptId: string, body: Omit<GradeExamAttemptCommand, 'attemptId'>) =>
    api.put<ExamAttemptDto>(`/teacher/exams/attempts/${attemptId}/grade`, { ...body, attemptId }),

  results: (id: string) => api.get<ExamResultDto[]>(`/teacher/exams/${id}/results`),
  recordResults: (id: string, body: Omit<RecordExamResultsCommand, 'examId'>) =>
    api.post<ExamResultDto[]>(`/teacher/exams/${id}/results`, { ...body, examId: id }),
  recordResultsBulk: (id: string, body: Omit<RecordExamResultsCommand, 'examId'>) =>
    api.post<ExamResultDto[]>(`/teacher/exams/${id}/results/bulk`, { ...body, examId: id }),
  updateResult: (id: string, studentId: string, body: UpdateExamResultCommand) =>
    api.put<ExamResultDto>(`/teacher/exams/${id}/results/${studentId}`, {
      ...body,
      examId: id,
      studentId,
    }),
  studentGrades: (studentId: string) =>
    api.get<StudentGradesSummaryDto>(`/teacher/exams/students/${studentId}/grades`),
}

/* -------------------------------------------------------------------------- */
/*                                  Homework                                  */
/* -------------------------------------------------------------------------- */

export interface TeacherHomeworkQuery extends PageQuery {
  GroupId?: string
  IsPublished?: boolean
  Overdue?: boolean
}

export const teacherHomeworkApi = {
  list: (query: TeacherHomeworkQuery = {}) =>
    api.get<Paginated<HomeworkDto>>('/teacher/homework', params(query)),
  details: (id: string) => api.get<HomeworkDto>(`/teacher/homework/${id}`),
  create: (body: CreateHomeworkCommand) => api.post<HomeworkDto>('/teacher/homework', body),
  update: (id: string, body: UpdateHomeworkCommand) =>
    api.put<HomeworkDto>(`/teacher/homework/${id}`, { ...body, homeworkId: id }),
  remove: (id: string) => api.delete<void>(`/teacher/homework/${id}`),
  publish: (id: string) => api.post<void>(`/teacher/homework/${id}/publish`),
  submissions: (id: string, query: PageQuery & { Status?: string } = {}) =>
    api.get<Paginated<HomeworkSubmissionDto>>(`/teacher/homework/${id}/submissions`, params(query)),
  submission: (submissionId: string) =>
    api.get<HomeworkSubmissionDto>(`/teacher/homework/submissions/${submissionId}`),
  grade: (submissionId: string, body: Omit<GradeHomeworkSubmissionCommand, 'submissionId'>) =>
    api.put<HomeworkSubmissionDto>(`/teacher/homework/submissions/${submissionId}/grade`, {
      ...body,
      submissionId,
    }),

  /** Grades many submissions in one round trip, for a marking session. */
  bulkGrade: (id: string, grades: BulkGradeEntry[]) =>
    api.post<BulkGradeResultDto>(`/teacher/homework/${id}/submissions/grade`, {
      homeworkId: id,
      grades,
    }),

  /**
   * Hands a submission back for another attempt. The reason is required — a
   * student cannot act on "returned" without being told what to change.
   */
  returnSubmission: (submissionId: string, reason: string) =>
    api.post<HomeworkSubmissionDto>(
      `/teacher/homework/submissions/${submissionId}/return`,
      { submissionId, reason },
    ),

  /** Stops accepting submissions. Reversible via `reopen`. */
  close: (id: string) => api.post<HomeworkDto>(`/teacher/homework/${id}/close`),

  reopen: (id: string) => api.post<HomeworkDto>(`/teacher/homework/${id}/reopen`),
}

/* -------------------------------------------------------------------------- */
/*                                 Attendance                                 */
/* -------------------------------------------------------------------------- */

export interface AttendanceSessionQuery extends PageQuery {
  GroupId?: string
  From?: string
  To?: string
  Status?: string
}

export const teacherAttendanceApi = {
  sessions: (query: AttendanceSessionQuery = {}) =>
    api.get<Paginated<AttendanceSessionDto>>('/teacher/attendance/sessions', params(query)),
  session: (id: string) => api.get<AttendanceSessionDto>(`/teacher/attendance/sessions/${id}`),
  createSession: (body: CreateAttendanceSessionCommand) =>
    api.post<AttendanceSessionDto>('/teacher/attendance/sessions', body),
  updateSession: (id: string, body: UpdateAttendanceSessionCommand) =>
    api.put<AttendanceSessionDto>(`/teacher/attendance/sessions/${id}`, { ...body, sessionId: id }),
  recordAttendance: (id: string, body: Omit<RecordAttendanceCommand, 'sessionId'>) =>
    api.post<AttendanceSessionDto>(`/teacher/attendance/sessions/${id}/records`, {
      ...body,
      sessionId: id,
    }),
  recordBulk: (id: string, body: Omit<RecordAttendanceCommand, 'sessionId'>) =>
    api.post<AttendanceSessionDto>(`/teacher/attendance/sessions/${id}/bulk`, {
      ...body,
      sessionId: id,
    }),
  updateRecord: (recordId: string, body: UpdateAttendanceRecordCommand) =>
    api.put<AttendanceRecordDto>(`/teacher/attendance/records/${recordId}`, {
      ...body,
      recordId,
    }),
  byGroup: (groupId: string, query: AttendanceSessionQuery = {}) =>
    api.get<Paginated<AttendanceSessionDto>>(
      `/teacher/attendance/group/${groupId}`,
      params(query),
    ),
  byStudent: (studentId: string, query: PageQuery & { GroupId?: string; Status?: string } = {}) =>
    api.get<Paginated<AttendanceRecordDto>>(
      `/teacher/attendance/student/${studentId}`,
      params(query),
    ),
  statistics: (query: { GroupId?: string; StudentId?: string; From?: string; To?: string } = {}) =>
    api.get<AttendanceStatisticsDto>('/teacher/attendance/statistics', params(query)),
}

/* -------------------------------------------------------------------------- */
/*                      Enrollments, payments, engagement                     */
/* -------------------------------------------------------------------------- */

export const teacherEnrollmentsApi = {
  courses: (query: PageQuery & { CourseId?: string; Status?: string } = {}) =>
    api.get<Paginated<CourseEnrollmentDto>>('/teacher/enrollments/courses', params(query)),
  groups: (query: PageQuery & { GroupId?: string; Status?: string } = {}) =>
    api.get<Paginated<GroupEnrollmentDto>>('/teacher/enrollments/groups', params(query)),
}

export const teacherPaymentsApi = {
  list: (
    query: PageQuery & {
      StudentId?: string
      GroupId?: string
      Status?: string
      From?: string
      To?: string
    } = {},
  ) => api.get<Paginated<EducationalPaymentDto>>('/teacher/payments', params(query)),
  record: (body: RecordEducationalPaymentCommand) =>
    api.post<EducationalPaymentDto>('/teacher/payments', body),
  markPaid: (id: string) => api.post<EducationalPaymentDto>(`/teacher/payments/${id}/mark-paid`),
  refund: (id: string, reason?: string) =>
    api.post<EducationalPaymentDto>(`/teacher/payments/${id}/refund`, { reason }),
}

export const teacherAnnouncementsApi = {
  list: (query: PageQuery & { GroupId?: string; Audience?: string; IsPublished?: boolean } = {}) =>
    api.get<Paginated<AnnouncementDto>>('/teacher/announcements', params(query)),
  details: (id: string) => api.get<AnnouncementDto>(`/teacher/announcements/${id}`),
  create: (body: CreateAnnouncementCommand) =>
    api.post<AnnouncementDto>('/teacher/announcements', body),
  update: (id: string, body: UpdateAnnouncementCommand) =>
    api.put<AnnouncementDto>(`/teacher/announcements/${id}`, { ...body, announcementId: id }),
  remove: (id: string) => api.delete<void>(`/teacher/announcements/${id}`),
  publish: (id: string) => api.post<void>(`/teacher/announcements/${id}/publish`),
}

export const teacherDiscountsApi = {
  list: (query: PageQuery & { IsActive?: boolean; CurrentlyValidOnly?: boolean } = {}) =>
    api.get<Paginated<DiscountDto>>('/teacher/discounts', params(query)),
  details: (id: string) => api.get<DiscountDto>(`/teacher/discounts/${id}`),
  create: (body: CreateDiscountCommand) => api.post<DiscountDto>('/teacher/discounts', body),
  update: (id: string, body: UpdateDiscountCommand) =>
    api.put<DiscountDto>(`/teacher/discounts/${id}`, { ...body, discountId: id }),
  remove: (id: string) => api.delete<void>(`/teacher/discounts/${id}`),
  activate: (id: string) => api.post<void>(`/teacher/discounts/${id}/activate`),
  deactivate: (id: string) => api.post<void>(`/teacher/discounts/${id}/deactivate`),
}

export const teacherInquiriesApi = {
  list: (query: PageQuery & { Status?: string; GroupId?: string } = {}) =>
    api.get<Paginated<InquiryDto>>('/teacher/inquiries', params(query)),
  details: (id: string) => api.get<InquiryDto>(`/teacher/inquiries/${id}`),
  updateStatus: (id: string, body: Omit<UpdateInquiryStatusCommand, 'inquiryId'>) =>
    api.put<InquiryDto>(`/teacher/inquiries/${id}/status`, { ...body, inquiryId: id }),
}

export const teacherScheduleApi = {
  list: () => api.get<ScheduleSlotDto[]>('/teacher/schedule'),
  add: (body: AddGroupScheduleCommand) => api.post<GroupScheduleDto>('/teacher/schedule', body),
  update: (id: string, body: UpdateGroupScheduleCommand) =>
    api.put<GroupScheduleDto>(`/teacher/schedule/${id}`, { ...body, scheduleId: id }),
  remove: (id: string, groupId?: string) =>
    request<void>(`/teacher/schedule/${id}`, { method: 'DELETE', query: { groupId } }),
}

export const teacherCertificatesApi = {
  list: (query: PageQuery & { CourseId?: string; Status?: string } = {}) =>
    api.get<Paginated<CertificateDto>>('/teacher/certificates', params(query)),
  details: (id: string) => api.get<CertificateDto>(`/teacher/certificates/${id}`),
  revoke: (id: string, reason?: string) =>
    api.post<CertificateDto>(`/teacher/certificates/${id}/revoke`, { reason }),
}

/* -------------------------------------------------------------------------- */
/*                            Teacher verification                            */
/* -------------------------------------------------------------------------- */

/** The five states the backend models. `UnderReview` means a person is looking at it. */
export type TeacherVerificationStatus =
  | 'NotSubmitted'
  | 'Pending'
  | 'UnderReview'
  | 'Verified'
  | 'Rejected'

/** One profile requirement the reviewer needs before they can judge the profile. */
export interface VerificationRequirement {
  key: string
  isMet: boolean
}

export interface TeacherVerification {
  status: number
  statusName: TeacherVerificationStatus
  isVerified: boolean
  canSubmit: boolean
  isUnderReview: boolean
  isProfileComplete: boolean
  submittedAt?: string | null
  decidedAt?: string | null
  /** The reviewer's reason, shown to the teacher on rejection. */
  notes?: string | null
  requirements: VerificationRequirement[]
}

export const teacherVerificationApi = {
  get: () => api.get<TeacherVerification>('/teacher/profile/verification'),
  submit: () => api.post<TeacherVerification>('/teacher/profile/verification'),
}

/* -------------------------------------------------------------------------- */
/*                               Exam analytics                               */
/* -------------------------------------------------------------------------- */

/* Hand-written against the live contract: the generated schema only carries what
   the deployed swagger exposes, and regeneration must not drop these. */

export type QuestionDifficulty = 'Unknown' | 'Easy' | 'Medium' | 'Hard'
export type ExamInsightSeverity = 'Info' | 'Warning' | 'Critical'

export interface ExamAnalyticsOverview {
  examId: string
  title: string
  typeName: string
  isOnline: boolean
  examDate: string
  durationInMinutes: number
  maximumScore: number
  passingScore: number
  isPublished: boolean
  resultsPublished: boolean
  questionsCount: number
  groupId?: string | null
  groupName?: string | null
  subjectNameAr: string
  subjectNameEn: string
  expectedParticipants?: number | null
}

export interface ExamPerformanceSummary {
  totalAttempts: number
  scoredAttempts: number
  absentCount: number
  inProgressCount: number
  expiredCount: number
  awaitingGradingCount: number
  averageScore: number
  averagePercentage: number
  highestPercentage: number
  lowestPercentage: number
  medianPercentage: number
  standardDeviation: number
  passedCount: number
  failedCount: number
  passRate: number
  failRate: number
  averageDurationMinutes?: number | null
}

export interface ScoreBucket {
  fromPercentage: number
  toPercentage: number
  label: string
  count: number
  percentage: number
}

export interface QuestionAnalytics {
  questionId: string
  text: string
  typeName: string
  points: number
  sortOrder: number
  answeredCount: number
  skippedCount: number
  correctCount: number
  incorrectCount: number
  awaitingGradingCount: number
  correctPercentage: number
  incorrectPercentage: number
  averageAwardedPoints: number
  difficultyName: QuestionDifficulty
}

export interface ExamStudentStanding {
  studentId: string
  studentName: string
  score?: number | null
  percentage?: number | null
  isPassed?: boolean | null
  isAbsent: boolean
}

export interface ExamTopicPerformance {
  topic: string
  questionsCount: number
  gradedAnswers: number
  correctAnswers: number
  correctPercentage: number
}

/** `message` is a ready-to-show Arabic sentence; `code` drives our own English copy. */
export interface ExamInsight {
  code: string
  severityName: ExamInsightSeverity
  message: string
  value?: number | null
}

export interface ExamAnalytics {
  overview: ExamAnalyticsOverview
  performance: ExamPerformanceSummary
  scoreDistribution: ScoreBucket[]
  questions: QuestionAnalytics[]
  mostDifficultQuestions: QuestionAnalytics[]
  mostSuccessfulQuestions: QuestionAnalytics[]
  topicPerformance: ExamTopicPerformance[]
  topPerformers: ExamStudentStanding[]
  lowestPerformers: ExamStudentStanding[]
  notAttempted: ExamStudentStanding[]
  insights: ExamInsight[]
}

export const examAnalyticsApi = {
  get: (examId: string, take = 5) =>
    api.get<ExamAnalytics>(`/teacher/exams/${examId}/analytics`, { take }),
}

/* -------------------------------------------------------------------------- */
/*                              Activity timeline                             */
/* -------------------------------------------------------------------------- */

export interface ActivityQuery extends PageQuery {
  StudentId?: string
  /** Filter to specific `ActivityTypes` keys, e.g. `homework.submitted`. */
  Types?: string[]
  From?: string
  To?: string
}

export const teacherActivityApi = {
  /** The workspace feed: everything that happened across the teacher's groups. */
  list: (query: ActivityQuery = {}) =>
    api.get<Paginated<ActivityEventDto>>('/teacher/activity', params(query)),

  /** One student's history, for a parent conversation or an at-risk review. */
  forStudent: (studentId: string, query: ActivityQuery = {}) =>
    api.get<Paginated<ActivityEventDto>>(
      `/teacher/activity/students/${studentId}`,
      params(query),
    ),
}
