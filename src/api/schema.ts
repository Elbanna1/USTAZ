/* eslint-disable */
/**
 * AUTO-GENERATED from the USTAZ Production OpenAPI document.
 * Source: https://ustaz.runasp.net/swagger/v1/swagger.json
 *
 * Do not edit by hand — regenerate with `scripts/generate-api-types.mjs`.
 */

/** Envelope every successful USTAZ response is wrapped in. */
export interface ApiEnvelope<T> {
  success: boolean
  message?: string
  data: T
}

/** Server-side paging envelope used by every list endpoint. */
export interface Paginated<T> {
  items: T[]
  pageNumber: number
  pageSize: number
  totalCount: number
  totalPages: number
  hasPrevious: boolean
  hasNext: boolean
}

/** RFC 7807 problem document returned for every failure. */
export interface ProblemDocument {
  type?: string | null
  title?: string | null
  status?: number | null
  detail?: string | null
  instance?: string | null
  traceId?: string
  /**
   * Machine-readable business-rule code, e.g. FEATURE_NOT_AVAILABLE or
   * TEACHER_NOT_VERIFIED. Present on 422 responses. Swagger does not declare it
   * on ProblemDetails, but the API emits it and the client keys its plan and
   * verification gates off it — so it lives in this template rather than being
   * re-patched by hand after every regeneration.
   */
  code?: string
  errors?: Record<string, string[]>
  success?: boolean
}

export interface AcceptInvitationCommand {
  token?: string
}

export interface AchievementInput {
  id?: string | null
  title?: string
  description?: string | null
  year?: number | null
  imageUrl?: string | null
  sortOrder?: number
}

export interface ActivityEventDto {
  id?: string
  type?: string
  actorUserId?: string | null
  actorName?: string
  studentId?: string | null
  studentName?: string | null
  relatedEntityType?: string | null
  relatedEntityId?: string | null
  metadata?: string | null
  occurredAt?: string
}

export interface ActivityItemDto {
  type?: string
  title?: string
  description?: string
  referenceId?: string | null
  actionUrl?: string | null
  occurredAt?: string
}

export interface AddCourseLessonCommand {
  courseId?: string
  sectionId?: string
  title: string
  description?: string | null
  videoUrl?: string | null
  durationInMinutes?: number
  sortOrder?: number
  isPreview?: boolean
  isPublished?: boolean
}

export interface AddCourseSectionCommand {
  courseId?: string
  title: string
  description?: string | null
  sortOrder?: number
}

export interface AddExamQuestionCommand {
  examId?: string
  text: string
  type?: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer' | 'Essay'
  points?: number
  sortOrder?: number | null
  correctAnswer?: string | null
  explanation?: string | null
  options?: ExamQuestionOptionInput[]
}

export interface AddGroupCurriculumCommand {
  groupId?: string
  title: string
  description?: string | null
  sortOrder?: number
}

export interface AddGroupScheduleCommand {
  groupId?: string
  dayOfWeek?: 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'
  startTime?: string
  endTime?: string
  location?: string | null
  meetingUrl?: string | null
}

export interface AddTenantDomainCommand {
  host: string
  makePrimary?: boolean
}

export interface AdminDashboardDto {
  totalUsers?: number
  totalTeachers?: number
  verifiedTeachers?: number
  pendingVerifications?: number
  totalStudents?: number
  totalGroups?: number
  publishedGroups?: number
  totalCourses?: number
  publishedCourses?: number
  totalGroupEnrollments?: number
  totalCourseEnrollments?: number
  totalReviews?: number
  pendingReviews?: number
  newInquiries?: number
  platformRevenue?: number
  revenueTrend?: RevenuePointDto[]
  recentTeachers?: TeacherListItemDto[]
}

export interface AdminTenantDto {
  id?: string
  slug?: string
  name?: string
  status?: 'Active' | 'Suspended' | 'Archived'
  statusName?: string
  ownerDisplayName?: string | null
  ownerEmail?: string | null
  planCode?: string | null
  subscriptionStatus?: 'Trial' | 'Active' | 'PastDue' | 'GracePeriod' | 'Canceled' | 'Expired' | null
  currentPeriodEnd?: string | null
  membersCount?: number
  studentsCount?: number
  groupsCount?: number
  lifetimeRevenue?: number
  createdAt?: string
}

export interface AnalyticsWindowDto {
  period?: 'Today' | 'Last7Days' | 'Last30Days' | 'Last90Days' | 'Last12Months' | 'AllTime' | 'Custom'
  periodName?: string
  fromUtc?: string | null
  toUtc?: string
}

export interface AnnouncementDto {
  id?: string
  tenantId?: string
  tenantName?: string
  groupId?: string | null
  groupName?: string | null
  title?: string
  body?: string
  audience?: 'TenantWide' | 'Group' | 'Students' | 'Parents'
  audienceName?: string
  isPinned?: boolean
  isPublished?: boolean
  publishedAt?: string | null
  expiresAt?: string | null
  imageUrl?: string | null
  attachmentUrl?: string | null
  createdAt?: string
}

export interface AtRiskStudentDto {
  studentId?: string
  studentName?: string
  riskLevel?: 'None' | 'Low' | 'Medium' | 'High'
  riskLevelName?: string
  riskScore?: number
  reasons?: RiskReasonDto[]
  attendanceRate?: number
  absenceCount?: number
  homeworkCompletionRate?: number
  missingHomeworkCount?: number
  averageExamPercentage?: number | null
  failedExamCount?: number
  averageCourseProgress?: number
  lastActivityAt?: string | null
  daysSinceLastActivity?: number
}

export interface AttendanceBreakdownDto {
  groupId?: string
  groupName?: string
  totalSessions?: number
  present?: number
  absent?: number
  attendanceRate?: number
}

export interface AttendanceEntryInput {
  studentId?: string
  status?: 'Present' | 'Absent' | 'Late' | 'Excused'
  minutesLate?: number | null
  note?: string | null
}

export interface AttendanceRecordDto {
  id?: string
  attendanceSessionId?: string
  studentId?: string
  studentName?: string
  studentImageUrl?: string | null
  status?: 'Present' | 'Absent' | 'Late' | 'Excused'
  statusName?: string
  minutesLate?: number | null
  note?: string | null
  recordedAt?: string
  groupId?: string
  groupName?: string
  sessionDate?: string
}

export interface AttendanceSessionDto {
  id?: string
  tenantId?: string
  groupId?: string
  groupName?: string
  groupScheduleId?: string | null
  sessionDate?: string
  startTime?: string | null
  endTime?: string | null
  title?: string | null
  notes?: string | null
  status?: 'Draft' | 'Open' | 'Closed'
  statusName?: string
  totalStudents?: number
  presentCount?: number
  absentCount?: number
  lateCount?: number
  excusedCount?: number
  createdAt?: string
  records?: AttendanceRecordDto[]
}

export interface AttendanceStatisticsDto {
  totalSessions?: number
  present?: number
  absent?: number
  late?: number
  excused?: number
  attendanceRate?: number
  absenceRate?: number
  byGroup?: AttendanceBreakdownDto[]
}

export interface AuditLogDto {
  id?: string
  tenantId?: string | null
  userId?: string | null
  userEmail?: string | null
  action?: string
  entityName?: string | null
  entityId?: string | null
  oldValues?: string | null
  newValues?: string | null
  ipAddress?: string | null
  succeeded?: boolean
  failureReason?: string | null
  createdAt?: string
}

export interface AuthenticationResponse {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  expiresInSeconds?: number
  tokenType?: string
  user: CurrentUserDto
}

export interface BillingTransactionDto {
  id?: string
  tenantId?: string
  invoiceId?: string | null
  invoiceNumber?: string | null
  amount?: number
  currency?: string
  status?: 'Pending' | 'Paid' | 'Failed' | 'Refunded' | 'Canceled'
  statusName?: string
  provider?: string
  providerTransactionId?: string | null
  failureReason?: string | null
  paidAt?: string | null
  createdAt?: string
}

export interface BulkGradeEntry {
  submissionId?: string
  score?: number
  feedback?: string | null
}

export interface BulkGradeHomeworkCommand {
  homeworkId?: string
  grades?: BulkGradeEntry[]
}

export interface BulkGradeResultDto {
  homeworkId?: string
  gradedCount?: number
  submissions?: HomeworkSubmissionDto[]
}

export interface CalendarEventDto {
  id?: string
  type?: 'Class' | 'Exam' | 'HomeworkDeadline' | 'AttendanceSession'
  typeName?: string
  title?: string
  description?: string | null
  startUtc?: string
  endUtc?: string
  isDeadline?: boolean
  groupId?: string | null
  groupName?: string | null
  subjectId?: string | null
  subjectNameAr?: string | null
  teacherId?: string | null
  teacherName?: string | null
  location?: string | null
  meetingUrl?: string | null
  relatedEntityType?: string
  relatedEntityId?: string
  status?: string
}

export interface CalendarResponseDto {
  fromUtc?: string
  toUtc?: string
  timeZoneId?: string
  totalEvents?: number
  events?: CalendarEventDto[]
}

export interface CancelGroupEnrollmentRequest {
  reason?: string | null
}

export interface CancelSubscriptionCommand {
  immediately?: boolean
  reason?: string | null
}

export interface CertificateDto {
  id?: string
  certificateNumber?: string
  verificationCode?: string
  courseId?: string
  courseTitle?: string
  studentId?: string
  studentName?: string
  teacherName?: string
  workspaceName?: string
  lessonsCompleted?: number
  status?: 'Issued' | 'Revoked'
  statusName?: string
  isValid?: boolean
  completedAt?: string
  issuedAt?: string
  revokedAt?: string | null
}

export interface CertificateVerificationDto {
  isValid: boolean
  certificateNumber?: string | null
  studentName?: string | null
  courseTitle?: string | null
  teacherName?: string | null
  workspaceName?: string | null
  completedAt?: string | null
  issuedAt?: string | null
  status?: string | null
}

export interface ChangeMemberRoleCommand {
  memberId?: string
  role?: 'Owner' | 'Admin' | 'Assistant' | 'Moderator' | 'ContentManager'
  title?: string | null
}

export interface ChangeMemberStatusCommand {
  memberId?: string
  isActive?: boolean
}

export interface ChangePasswordCommand {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export interface ChangePlanCommand {
  planId?: string
  billingCycle?: 'Monthly' | 'Yearly'
}

export interface CheckoutSessionDto {
  invoiceId?: string
  invoiceNumber?: string
  transactionId?: string
  amount?: number
  currency?: string
  provider?: string
  providerReference?: string
  checkoutUrl?: string | null
  expiresAt?: string
}

export interface ChildDto {
  studentId?: string
  linkId?: string
  displayName?: string
  profileImageUrl?: string | null
  email?: string | null
  phoneNumber?: string | null
  schoolName?: string | null
  gradeName?: string | null
  relationship?: string | null
  isConfirmed?: boolean
  isPrimaryContact?: boolean
  activeGroupsCount?: number
  activeCoursesCount?: number
  linkedAt?: string
}

export interface ChildOverviewDto {
  studentId?: string
  displayName?: string
  profileImageUrl?: string | null
  gradeName?: string | null
  activeGroupsCount?: number
  activeCoursesCount?: number
  attendanceRate?: number
  averageGradePercentage?: number
  pendingHomeworkCount?: number
  upcomingExamsCount?: number
}

export interface CompleteLessonRequest {
  watchedSeconds?: number | null
}

export interface ConversationDto {
  id?: string
  tenantId?: string
  type?: 'ParentTeacher' | 'StudentTeacher' | 'TeacherAssistant' | 'Group'
  typeName?: string
  subject?: string | null
  groupId?: string | null
  groupName?: string | null
  lastMessageAt?: string | null
  lastMessagePreview?: string | null
  unreadCount?: number
  participants?: ConversationParticipantDto[]
  createdAt?: string
}

export interface ConversationParticipantDto {
  userId?: string
  displayName?: string
  profileImageUrl?: string | null
  lastReadAt?: string | null
}

export interface CourseDetailsDto {
  id?: string
  title?: string
  slug?: string
  description?: string | null
  thumbnailUrl?: string | null
  pricing?: PricingDto
  durationInMinutes?: number
  lessonsCount?: number
  rating?: number
  reviewCount?: number
  studentCount?: number
  isPublished?: boolean
  isFeatured?: boolean
  teacher?: TeacherSummaryDto
  subject?: SubjectDto
  grade?: GradeDto
  sections?: CourseSectionDto[]
  isEnrolled?: boolean
  isOwner?: boolean
  progressPercentage?: number
  createdAt?: string
  updatedAt?: string | null
}

export interface CourseEnrollmentDto {
  id?: string
  studentId?: string
  studentName?: string
  courseId?: string
  courseTitle?: string
  courseSlug?: string
  courseThumbnailUrl?: string | null
  status?: 'Pending' | 'Active' | 'Cancelled' | 'Completed'
  statusName?: string
  enrolledAt?: string
  cancelledAt?: string | null
  completedAt?: string | null
  lastAccessedAt?: string | null
  pricePaid?: number
  discountAmount?: number
  finalPrice?: number
  discountCode?: string | null
  progressPercentage?: number
  lessonsCount?: number
  durationInMinutes?: number
  teacher?: TeacherSummaryDto
  subject?: SubjectDto
  grade?: GradeDto
}

export interface CourseLessonDto {
  id?: string
  sectionId?: string
  title?: string
  description?: string | null
  videoUrl?: string | null
  durationInMinutes?: number
  sortOrder?: number
  isPreview?: boolean
  isPublished?: boolean
  isLocked?: boolean
}

export interface CourseListItemDto {
  id?: string
  title?: string
  slug?: string
  description?: string | null
  thumbnailUrl?: string | null
  pricing?: PricingDto
  durationInMinutes?: number
  lessonsCount?: number
  rating?: number
  reviewCount?: number
  studentCount?: number
  isPublished?: boolean
  isFeatured?: boolean
  teacher?: TeacherSummaryDto
  subject?: SubjectDto
  grade?: GradeDto
  createdAt?: string
}

export interface CoursePerformanceDto {
  courseId?: string
  title?: string
  slug?: string
  isPublished?: boolean
  price?: number
  enrollmentsCount?: number
  completedCount?: number
  totalRevenue?: number
  averageProgressPercentage?: number
  rating?: number
  reviewCount?: number
  lessonsCount?: number
}

export interface CourseProgressDto {
  enrollmentId?: string
  courseId?: string
  courseTitle?: string
  thumbnailUrl?: string | null
  progressPercentage?: number
  lessonsCount?: number
  durationInMinutes?: number
  lastAccessedAt?: string | null
  teacher?: TeacherSummaryDto
}

export interface CourseProgressReportDto {
  courseId?: string
  enrollmentId?: string
  courseTitle?: string
  totalLessons?: number
  completedLessons?: number
  progressPercentage?: number
  status?: 'Pending' | 'Active' | 'Cancelled' | 'Completed'
  statusName?: string
  completedAt?: string | null
  lastAccessedAt?: string | null
  currentLessonId?: string | null
  currentLessonTitle?: string | null
  nextLessonId?: string | null
  nextLessonTitle?: string | null
  watchedSeconds?: number
  lessons?: LessonProgressItemDto[]
}

export interface CourseSectionDto {
  id?: string
  courseId?: string
  title?: string
  description?: string | null
  sortOrder?: number
  lessonsCount?: number
  durationInMinutes?: number
  lessons?: CourseLessonDto[]
}

export interface CreateAnnouncementCommand {
  title: string
  body: string
  audience?: 'TenantWide' | 'Group' | 'Students' | 'Parents'
  groupId?: string | null
  isPinned?: boolean
  expiresAt?: string | null
  imageUrl?: string | null
  attachmentUrl?: string | null
}

export interface CreateAttendanceSessionCommand {
  groupId?: string
  groupScheduleId?: string | null
  sessionDate?: string
  startTime?: string | null
  endTime?: string | null
  title?: string | null
  notes?: string | null
  prefillPresent?: boolean
}

export interface CreateCheckoutCommand {
  planId?: string
  billingCycle?: 'Monthly' | 'Yearly'
  provider?: string
}

export interface CreateCourseCommand {
  subjectId: string
  gradeId: string
  title: string
  description?: string | null
  thumbnailUrl?: string | null
  price?: number
  originalPrice?: number | null
  discountPercentage?: number
}

export interface CreateDiscountCommand {
  code: string
  description?: string | null
  type?: 'Percentage' | 'FixedAmount'
  value?: number
  minimumAmount?: number | null
  maximumDiscount?: number | null
  startDate?: string
  endDate?: string | null
  usageLimit?: number | null
}

export interface CreateExamCommand {
  groupId?: string | null
  subjectId?: string
  gradeId?: string | null
  title: string
  description?: string | null
  type?: 'Quiz' | 'Monthly' | 'Midterm' | 'Final' | 'Practice'
  examDate?: string
  durationInMinutes?: number
  maximumScore?: number
  passingScore?: number
  isOnline?: boolean
  maxAttempts?: number
  showResultsImmediately?: boolean
}

export interface CreateGroupCommand {
  subjectId: string
  gradeId: string
  name: string
  description?: string | null
  type?: 'Regular' | 'Revision' | 'Intensive' | 'ExamPreparation'
  teachingMode?: 'Online' | 'Offline' | 'Hybrid'
  location?: string | null
  monthlyPrice?: number
  originalPrice?: number | null
  discountPercentage?: number
  capacity?: number
  startDate?: string
  endDate?: string | null
  whatsAppLink?: string | null
  schedules?: GroupScheduleInput[]
}

export interface CreateHomeworkCommand {
  groupId?: string
  subjectId?: string | null
  title: string
  description?: string | null
  instructions?: string | null
  dueAt?: string
  maximumScore?: number
  allowLateSubmission?: boolean
  allowResubmission?: boolean
  maxSubmissions?: number | null
  attachments?: HomeworkAttachmentInput[]
}

export interface CreateInquiryCommand {
  teacherId?: string
  groupId?: string | null
  courseId?: string | null
  name: string
  phoneNumber: string
  email?: string | null
  message: string
}

export interface CreateReviewCommand {
  teacherId?: string | null
  groupId?: string | null
  courseId?: string | null
  rating?: number
  comment?: string | null
}

export interface CreateWebsiteSectionCommand {
  type?: 'Hero' | 'About' | 'Achievements' | 'Testimonials' | 'FeaturedGroups' | 'FeaturedCourses' | 'Teachers' | 'Contact' | 'Custom'
  title?: string | null
  subtitle?: string | null
  body?: string | null
  imageUrl?: string | null
  settingsJson?: string | null
  isVisible?: boolean
  sortOrder?: number | null
}

export interface CurrentUserDto {
  id?: string
  email?: string
  firstName?: string
  lastName?: string
  displayName?: string
  profileImageUrl?: string | null
  phoneNumber?: string | null
  city?: string | null
  preferredLanguage?: string
  emailConfirmed?: boolean
  isActive?: boolean
  isVerified?: boolean
  roles?: string[]
  teacherId?: string | null
  studentId?: string | null
  parentId?: string | null
  tenantId?: string | null
  tenantSlug?: string | null
  tenantName?: string | null
  tenantRole?: 'Owner' | 'Admin' | 'Assistant' | 'Moderator' | 'ContentManager' | null
  createdAt?: string
}

export interface DeclineInvitationCommand {
  token?: string
}

export interface DiscountDto {
  id?: string
  teacherId?: string
  code?: string
  description?: string | null
  type?: 'Percentage' | 'FixedAmount'
  typeName?: string
  value?: number
  minimumAmount?: number | null
  maximumDiscount?: number | null
  startDate?: string
  endDate?: string | null
  usageLimit?: number | null
  usedCount?: number
  remainingUses?: number | null
  isActive?: boolean
  isCurrentlyValid?: boolean
  createdAt?: string
}

export interface DiscountValidationResultDto {
  isValid?: boolean
  failureReason?: string | null
  code?: string
  type?: 'Percentage' | 'FixedAmount' | null
  originalAmount?: number
  discountAmount?: number
  finalAmount?: number
  currency?: string
}

export interface EducationalPaymentDto {
  id?: string
  tenantId?: string
  studentId?: string
  studentName?: string
  parentId?: string | null
  type?: 'GroupEnrollment' | 'CourseEnrollment' | 'MonthlySubscription' | 'OneTime'
  typeName?: string
  groupId?: string | null
  groupName?: string | null
  courseId?: string | null
  courseTitle?: string | null
  invoiceNumber?: string
  amount?: number
  discountAmount?: number
  netAmount?: number
  currency?: string
  discountCode?: string | null
  status?: 'Pending' | 'Paid' | 'Failed' | 'Refunded' | 'Canceled'
  statusName?: string
  provider?: string | null
  periodMonth?: string | null
  dueAt?: string | null
  paidAt?: string | null
  notes?: string | null
  createdAt?: string
}

export interface EnrollInCourseCommand {
  courseId?: string
  discountCode?: string | null
}

export interface EnrollInGroupCommand {
  groupId?: string
  discountCode?: string | null
}

export interface EnrollStudentInGroupCommand {
  groupId?: string
  studentId?: string
  monthlyPrice?: number | null
  notes?: string | null
}

export interface ExamAnalyticsDto {
  overview?: ExamAnalyticsOverviewDto
  performance?: ExamPerformanceSummaryDto
  scoreDistribution?: ScoreBucketDto[]
  questions?: QuestionAnalyticsDto[]
  mostDifficultQuestions?: QuestionAnalyticsDto[]
  mostSuccessfulQuestions?: QuestionAnalyticsDto[]
  topicPerformance?: ExamTopicPerformanceDto[]
  topPerformers?: ExamStudentStandingDto[]
  lowestPerformers?: ExamStudentStandingDto[]
  notAttempted?: ExamStudentStandingDto[]
  insights?: ExamInsightDto[]
}

export interface ExamAnalyticsOverviewDto {
  examId?: string
  title?: string
  type?: 'Quiz' | 'Monthly' | 'Midterm' | 'Final' | 'Practice'
  typeName?: string
  isOnline?: boolean
  examDate?: string
  durationInMinutes?: number
  maximumScore?: number
  passingScore?: number
  isPublished?: boolean
  resultsPublished?: boolean
  questionsCount?: number
  groupId?: string | null
  groupName?: string | null
  subjectId?: string
  subjectNameAr?: string
  subjectNameEn?: string
  expectedParticipants?: number | null
}

export interface ExamAnswerInput {
  questionId?: string
  selectedOptionId?: string | null
  textAnswer?: string | null
}

export interface ExamAttemptAnswerDto {
  id?: string
  questionId?: string
  questionText?: string
  questionType?: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer' | 'Essay'
  selectedOptionId?: string | null
  textAnswer?: string | null
  isCorrect?: boolean | null
  awardedPoints?: number
  maximumPoints?: number
  teacherFeedback?: string | null
  correctAnswer?: string | null
  explanation?: string | null
}

export interface ExamAttemptDto {
  id?: string
  examId?: string
  examTitle?: string
  studentId?: string
  studentName?: string
  attemptNumber?: number
  status?: 'InProgress' | 'Submitted' | 'Graded' | 'Expired'
  statusName?: string
  startedAt?: string
  expiresAt?: string | null
  submittedAt?: string | null
  requiresManualGrading?: boolean
  scoresVisible?: boolean
  score?: number
  maximumScore?: number
  percentage?: number
  isPassed?: boolean
  teacherComment?: string | null
  gradedAt?: string | null
  answers?: ExamAttemptAnswerDto[]
}

export interface ExamAttemptSessionDto {
  attemptId?: string
  examId?: string
  examTitle?: string
  description?: string | null
  attemptNumber?: number
  maxAttempts?: number
  durationInMinutes?: number
  startedAt?: string
  expiresAt?: string | null
  maximumScore?: number
  questions?: StudentExamQuestionDto[]
}

export interface ExamDto {
  id?: string
  tenantId?: string
  teacherId?: string
  teacherName?: string
  groupId?: string | null
  groupName?: string | null
  subjectId?: string
  subjectNameAr?: string
  subjectNameEn?: string
  gradeId?: string | null
  gradeName?: string | null
  title?: string
  description?: string | null
  type?: 'Quiz' | 'Monthly' | 'Midterm' | 'Final' | 'Practice'
  typeName?: string
  examDate?: string
  durationInMinutes?: number
  maximumScore?: number
  passingScore?: number
  isPublished?: boolean
  resultsPublished?: boolean
  isOnline?: boolean
  maxAttempts?: number
  showResultsImmediately?: boolean
  questionsCount?: number
  resultsCount?: number
  averageScore?: number | null
  averagePercentage?: number | null
  highestScore?: number | null
  lowestScore?: number | null
  passedCount?: number
  createdAt?: string
}

export interface ExamInsightDto {
  code?: string
  severity?: 'Info' | 'Warning' | 'Critical'
  severityName?: string
  message?: string
  value?: number | null
}

export interface ExamPerformanceDto {
  examId?: string
  title?: string
  type?: 'Quiz' | 'Monthly' | 'Midterm' | 'Final' | 'Practice'
  examDate?: string
  resultsCount?: number
  averagePercentage?: number
  passedCount?: number
  passRate?: number
}

export interface ExamPerformanceSummaryDto {
  totalAttempts?: number
  scoredAttempts?: number
  absentCount?: number
  inProgressCount?: number
  expiredCount?: number
  awaitingGradingCount?: number
  averageScore?: number
  averagePercentage?: number
  highestPercentage?: number
  lowestPercentage?: number
  medianPercentage?: number
  standardDeviation?: number
  passedCount?: number
  failedCount?: number
  passRate?: number
  failRate?: number
  averageDurationMinutes?: number | null
}

export interface ExamQuestionDto {
  id?: string
  examId?: string
  text?: string
  type?: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer' | 'Essay'
  typeName?: string
  points?: number
  sortOrder?: number
  correctAnswer?: string | null
  explanation?: string | null
  options?: ExamQuestionOptionDto[]
}

export interface ExamQuestionOptionDto {
  id?: string
  text?: string
  isCorrect?: boolean
  sortOrder?: number
}

export interface ExamQuestionOptionInput {
  text: string
  isCorrect?: boolean
  sortOrder?: number
}

export interface ExamResultDto {
  id?: string
  examId?: string
  examTitle?: string
  examDate?: string
  studentId?: string
  studentName?: string
  score?: number
  maximumScore?: number
  percentage?: number
  isPassed?: boolean
  isAbsent?: boolean
  teacherComment?: string | null
  subjectNameAr?: string
  groupName?: string | null
  recordedAt?: string
}

export interface ExamResultInput {
  studentId?: string
  score?: number
  isAbsent?: boolean
  teacherComment?: string | null
}

export interface ExamStudentStandingDto {
  studentId?: string
  studentName?: string
  score?: number | null
  percentage?: number | null
  isPassed?: boolean | null
  isAbsent?: boolean
  attemptStatus?: 'InProgress' | 'Submitted' | 'Graded' | 'Expired' | null
  attemptStatusName?: string | null
  submittedAt?: string | null
  durationMinutes?: number | null
}

export interface ExamTopicPerformanceDto {
  topic?: string
  questionsCount?: number
  gradedAnswers?: number
  correctAnswers?: number
  correctPercentage?: number
}

export interface ForgotPasswordCommand {
  email: string
}

export interface GlobalSearchResultDto {
  query?: string
  totalResults?: number
  teachers?: TeacherListItemDto[]
  groups?: GroupListItemDto[]
  courses?: CourseListItemDto[]
  subjects?: SubjectDto[]
}

export interface GradeAnalyticsDto {
  totalResults?: number
  absentCount?: number
  passedCount?: number
  failedCount?: number
  passRate?: number
  averagePercentage?: number
  highestPercentage?: number
  lowestPercentage?: number
  bySubject?: SubjectPerformanceDto[]
  byExam?: ExamPerformanceDto[]
}

export interface GradeAnswerInput {
  answerId?: string
  awardedPoints?: number
  feedback?: string | null
}

export interface GradeDto {
  id?: string
  nameAr?: string
  nameEn?: string
  slug?: string
  stage?: 'Primary' | 'Preparatory' | 'Secondary'
  stageName?: string
  sortOrder?: number
  isActive?: boolean
  teachersCount?: number
  groupsCount?: number
  coursesCount?: number
}

export interface GradeExamAttemptCommand {
  attemptId?: string
  answers?: GradeAnswerInput[]
  teacherComment?: string | null
}

export interface GradeHomeworkSubmissionCommand {
  submissionId?: string
  score?: number
  feedback?: string | null
}

export interface GrantPlatformAdminCommand {
  userId?: string
  reason?: string | null
}

export interface GroupCurriculumDto {
  id?: string
  groupId?: string
  title?: string
  description?: string | null
  sortOrder?: number
}

export interface GroupDetailsDto {
  id?: string
  name?: string
  slug?: string
  description?: string | null
  type?: 'Regular' | 'Revision' | 'Intensive' | 'ExamPreparation'
  typeName?: string
  teachingMode?: 'Online' | 'Offline' | 'Hybrid'
  teachingModeName?: string
  location?: string | null
  pricing?: PricingDto
  capacity?: number
  enrolledStudentsCount?: number
  availableSeats?: number
  isFull?: boolean
  startDate?: string
  endDate?: string | null
  isPublished?: boolean
  isActive?: boolean
  rating?: number
  reviewCount?: number
  whatsAppLink?: string | null
  teacher?: TeacherSummaryDto
  subject?: SubjectDto
  grade?: GradeDto
  schedules?: GroupScheduleDto[]
  curriculum?: GroupCurriculumDto[]
  materials?: GroupMaterialDto[]
  isEnrolled?: boolean
  isOwner?: boolean
  createdAt?: string
  updatedAt?: string | null
}

export interface GroupEnrollmentDto {
  id?: string
  studentId?: string
  studentName?: string
  studentPhoneNumber?: string | null
  groupId?: string
  groupName?: string
  groupSlug?: string
  status?: 'Pending' | 'Active' | 'Cancelled' | 'Completed'
  statusName?: string
  enrolledAt?: string
  cancelledAt?: string | null
  completedAt?: string | null
  pricePaid?: number
  discountAmount?: number
  finalPrice?: number
  discountCode?: string | null
  teacher?: TeacherSummaryDto
  subject?: SubjectDto
  grade?: GradeDto
  whatsAppLink?: string | null
  schedule?: ScheduleSlotDto[]
}

export interface GroupListItemDto {
  id?: string
  name?: string
  slug?: string
  description?: string | null
  type?: 'Regular' | 'Revision' | 'Intensive' | 'ExamPreparation'
  typeName?: string
  teachingMode?: 'Online' | 'Offline' | 'Hybrid'
  teachingModeName?: string
  location?: string | null
  pricing?: PricingDto
  capacity?: number
  enrolledStudentsCount?: number
  availableSeats?: number
  isFull?: boolean
  startDate?: string
  endDate?: string | null
  isPublished?: boolean
  isActive?: boolean
  rating?: number
  reviewCount?: number
  whatsAppLink?: string | null
  teacher?: TeacherSummaryDto
  subject?: SubjectDto
  grade?: GradeDto
  schedules?: GroupScheduleDto[]
  createdAt?: string
}

export interface GroupMaterialDto {
  id?: string
  groupId?: string
  title?: string
  description?: string | null
  fileUrl?: string | null
  fileType?: 'Pdf' | 'Image' | 'Video' | 'Audio' | 'Document' | 'Presentation' | 'Archive' | 'Link'
  fileTypeName?: string
  fileSizeInBytes?: number | null
  isEnrolledOnly?: boolean
  isLocked?: boolean
  sortOrder?: number
}

export interface GroupPerformanceDto {
  groupId?: string
  name?: string
  capacity?: number
  enrolledStudentsCount?: number
  fillRatePercentage?: number
  monthlyRevenue?: number
  rating?: number
  reviewCount?: number
  isPublished?: boolean
}

export interface GroupScheduleDto {
  id?: string
  groupId?: string
  dayOfWeek?: 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'
  dayName?: string
  startTime?: string
  endTime?: string
  durationInMinutes?: number
  location?: string | null
  meetingUrl?: string | null
  isActive?: boolean
}

export interface GroupScheduleInput {
  dayOfWeek?: 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'
  startTime?: string
  endTime?: string
  location?: string | null
  meetingUrl?: string | null
}

export interface GuardianDto {
  linkId?: string
  parentId?: string
  displayName?: string
  profileImageUrl?: string | null
  email?: string | null
  phoneNumber?: string | null
  relationship?: string | null
  isConfirmed?: boolean
  isPrimaryContact?: boolean
  requestedAt?: string
  confirmedAt?: string | null
}

export interface HomeworkAttachmentDto {
  id?: string
  fileName?: string
  fileUrl?: string
  fileType?: 'Pdf' | 'Image' | 'Video' | 'Audio' | 'Document' | 'Presentation' | 'Archive' | 'Link'
  fileSizeInBytes?: number | null
}

export interface HomeworkAttachmentInput {
  fileName?: string
  fileUrl?: string
  fileType?: 'Pdf' | 'Image' | 'Video' | 'Audio' | 'Document' | 'Presentation' | 'Archive' | 'Link'
  fileSizeInBytes?: number | null
}

export interface HomeworkDto {
  id?: string
  tenantId?: string
  groupId?: string
  groupName?: string
  subjectId?: string | null
  subjectNameAr?: string | null
  title?: string
  description?: string | null
  instructions?: string | null
  assignedAt?: string
  dueAt?: string
  maximumScore?: number
  allowLateSubmission?: boolean
  allowResubmission?: boolean
  maxSubmissions?: number | null
  isPublished?: boolean
  closedAt?: string | null
  isClosed?: boolean
  submissionsCount?: number
  gradedCount?: number
  pendingGradingCount?: number
  returnedCount?: number
  attachments?: HomeworkAttachmentDto[]
  mySubmission?: HomeworkSubmissionDto
  createdAt?: string
}

export interface HomeworkSubmissionDto {
  id?: string
  homeworkId?: string
  homeworkTitle?: string
  dueAt?: string
  studentId?: string
  studentName?: string
  status?: 'NotSubmitted' | 'Submitted' | 'Late' | 'Graded' | 'Returned'
  statusName?: string
  content?: string | null
  fileUrl?: string | null
  submittedAt?: string | null
  isLate?: boolean
  score?: number | null
  maximumScore?: number | null
  percentage?: number | null
  teacherFeedback?: string | null
  gradedAt?: string | null
  submissionCount?: number
  returnedAt?: string | null
  returnReason?: string | null
}

export interface InquiryDto {
  id?: string
  studentId?: string | null
  teacherId?: string
  teacherName?: string
  groupId?: string | null
  groupName?: string | null
  courseId?: string | null
  courseTitle?: string | null
  name?: string
  phoneNumber?: string
  email?: string | null
  message?: string
  status?: 'New' | 'Contacted' | 'Converted' | 'Closed'
  statusName?: string
  teacherNote?: string | null
  whatsAppLink?: string | null
  respondedAt?: string | null
  createdAt?: string
}

export interface InviteTeamMemberCommand {
  email: string
  role?: 'Owner' | 'Admin' | 'Assistant' | 'Moderator' | 'ContentManager'
  title?: string | null
  permissions?: string[]
}

export interface InvoiceDto {
  id?: string
  tenantId?: string
  number?: string
  status?: 'Draft' | 'Issued' | 'Paid' | 'Void' | 'Overdue'
  statusName?: string
  subtotal?: number
  taxAmount?: number
  total?: number
  currency?: string
  issuedAt?: string
  dueAt?: string | null
  paidAt?: string | null
  description?: string | null
  periodStart?: string | null
  periodEnd?: string | null
}

export interface LessonProgressItemDto {
  lessonId?: string
  sectionId?: string
  sectionTitle?: string
  title?: string
  sortOrder?: number
  durationInMinutes?: number
  isPreview?: boolean
  isCompleted?: boolean
  watchedSeconds?: number
  completedAt?: string | null
  lastAccessedAt?: string | null
}

export interface LessonProgressSummaryDto {
  courseId?: string
  enrollmentId?: string
  totalLessons?: number
  completedLessons?: number
  progressPercentage?: number
  status?: 'Pending' | 'Active' | 'Cancelled' | 'Completed'
  statusName?: string
  isCompleted?: boolean
  completedAt?: string | null
}

export interface LinkChildCommand {
  studentEmail: string
  verificationPhoneNumber?: string | null
  relationship?: string | null
  isPrimaryContact?: boolean
}

export interface LoginCommand {
  email: string
  password: string
}

export interface LogoutCommand {
  refreshToken: string
}

export interface MediaUploadResult {
  url: string
  publicId: string
  secureUrl?: string | null
  format?: string | null
  bytes?: number
  width?: number | null
  height?: number | null
  resourceType?: string | null
}

export interface MessageAttachmentDto {
  id?: string
  fileName?: string
  fileUrl?: string
  fileType?: 'Pdf' | 'Image' | 'Video' | 'Audio' | 'Document' | 'Presentation' | 'Archive' | 'Link'
}

export interface MessageDto {
  id?: string
  conversationId?: string
  senderUserId?: string
  senderName?: string
  senderImageUrl?: string | null
  body?: string
  sentAt?: string
  editedAt?: string | null
  isMine?: boolean
  attachments?: MessageAttachmentDto[]
}

export interface ModerateReviewCommand {
  reviewId?: string
  isApproved?: boolean
  note?: string | null
}

export interface NotificationDto {
  id?: string
  title?: string
  message?: string
  titleEn?: string | null
  messageEn?: string | null
  type?: 'General' | 'Enrollment' | 'Review' | 'Inquiry' | 'Schedule' | 'Verification' | 'Discount' | 'System' | 'Course' | 'Exam' | 'Homework' | 'Attendance' | 'Payment' | 'Message' | 'Announcement' | 'Security' | 'Certificate' | 'Subscription'
  typeName?: string
  category?: 'Attendance' | 'Grade' | 'Homework' | 'Exam' | 'Schedule' | 'Payment' | 'Subscription' | 'Announcement' | 'Message' | 'System' | 'Course' | 'Security' | 'Enrollment' | 'Certificate'
  categoryName?: string
  priority?: 'Low' | 'Normal' | 'High' | 'Critical'
  priorityName?: string
  event?: string | null
  isRead?: boolean
  actionUrl?: string | null
  referenceId?: string | null
  relatedEntityType?: string | null
  tenantId?: string | null
  createdAt?: string
  readAt?: string | null
}

export interface NotificationPreferenceDto {
  category?: 'Attendance' | 'Grade' | 'Homework' | 'Exam' | 'Schedule' | 'Payment' | 'Subscription' | 'Announcement' | 'Message' | 'System' | 'Course' | 'Security' | 'Enrollment' | 'Certificate'
  categoryName?: string
  isEnabled?: boolean
  inApp?: boolean
  email?: boolean
  whatsApp?: boolean
}

export interface NotificationPreferenceInput {
  category?: 'Attendance' | 'Grade' | 'Homework' | 'Exam' | 'Schedule' | 'Payment' | 'Subscription' | 'Announcement' | 'Message' | 'System' | 'Course' | 'Security' | 'Enrollment' | 'Certificate'
  isEnabled?: boolean
  inApp?: boolean
  email?: boolean
  whatsApp?: boolean
}

export interface ParentDashboardDto {
  childrenCount?: number
  unreadNotificationsCount?: number
  absenceAlertsCount?: number
  pendingHomeworkCount?: number
  upcomingExamsCount?: number
  outstandingPayments?: number
  children?: ChildOverviewDto[]
  todaysSchedule?: ScheduleSlotDto[]
  upcomingClasses?: ScheduleSlotDto[]
  recentAbsences?: AttendanceRecordDto[]
  recentGrades?: ExamResultDto[]
  recentNotifications?: NotificationDto[]
}

export interface ParentProfileDto {
  id?: string
  userId?: string
  displayName?: string
  email?: string
  phoneNumber?: string | null
  alternatePhoneNumber?: string | null
  city?: string | null
  occupation?: string | null
  childrenCount?: number
  createdAt?: string
}

export interface PaymentWebhookResultDto {
  status: string
  message: string
  webhookEventId?: string | null
  wasDuplicate?: boolean
}

export interface PermissionCatalogDto {
  groups?: PermissionGroupDto[]
}

export interface PermissionGroupDto {
  name?: string
  permissions?: string[]
}

export interface PlanQuotaUsageDto {
  quota?: string
  limit?: number | null
  used?: number
  remaining?: number | null
  isUnlimited?: boolean
}

export interface PlatformAdministratorDto {
  userId?: string
  displayName: string
  email: string
  isActive?: boolean
  emailConfirmed?: boolean
  createdAt?: string
  lastLoginAt?: string | null
  isLastAdministrator?: boolean
  isCurrentUser?: boolean
}

export interface PlatformOverviewDto {
  totalSchools?: number
  activeSchools?: number
  suspendedSchools?: number
  archivedSchools?: number
  newSchoolsLast30Days?: number
  totalUsers?: number
  activeUsers?: number
  suspendedUsers?: number
  totalStudents?: number
  totalTeachers?: number
  totalParents?: number
  platformAdministrators?: number
  schoolStaffMemberships?: number
  activeSubscriptions?: number
  trialSubscriptions?: number
  expiredSubscriptions?: number
  cancelledSubscriptions?: number
  atRiskSubscriptions?: number
  generatedAt?: string
}

export interface PlatformUserDto {
  userId?: string
  displayName: string
  email: string
  phoneNumber?: string | null
  isActive?: boolean
  emailConfirmed?: boolean
  profileImageUrl?: string | null
  createdAt?: string
  lastLoginAt?: string | null
  isStudent?: boolean
  isTeacher?: boolean
  isParent?: boolean
  schoolMemberships?: number
}

export interface PricingDto {
  price?: number
  originalPrice?: number | null
  discountPercentage?: number
  hasDiscount?: boolean
  isFree?: boolean
  currency?: string
}

export interface ProfilePictureDto {
  url?: string | null
  hasPicture?: boolean
}

export interface PublicTenantDto {
  id?: string
  slug?: string
  name?: string
  displayName?: string | null
  city?: string | null
  whatsAppLink?: string | null
  branding?: TenantBrandingDto
  website?: TenantWebsiteDto
  groupsCount?: number
  coursesCount?: number
  studentsCount?: number
  rating?: number
  reviewCount?: number
}

export interface QuestionAnalyticsDto {
  questionId?: string
  text?: string
  type?: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer' | 'Essay'
  typeName?: string
  points?: number
  sortOrder?: number
  answeredCount?: number
  skippedCount?: number
  correctCount?: number
  incorrectCount?: number
  awaitingGradingCount?: number
  correctPercentage?: number
  incorrectPercentage?: number
  averageAwardedPoints?: number
  difficulty?: 'Unknown' | 'Easy' | 'Moderate' | 'Hard' | 'VeryHard'
  difficultyName?: string
}

export interface RecordAttendanceCommand {
  sessionId?: string
  entries?: AttendanceEntryInput[]
}

export interface RecordEducationalPaymentCommand {
  studentId?: string
  type?: 'GroupEnrollment' | 'CourseEnrollment' | 'MonthlySubscription' | 'OneTime'
  groupId?: string | null
  courseId?: string | null
  amount?: number
  discountAmount?: number
  discountCode?: string | null
  periodMonth?: string | null
  dueAt?: string | null
  provider?: string | null
  markAsPaid?: boolean
  notes?: string | null
}

export interface RecordExamResultsCommand {
  examId?: string
  results?: ExamResultInput[]
}

export interface RefreshTokenCommand {
  refreshToken: string
}

export interface RefundPaymentRequest {
  reason?: string | null
}

export interface RegisterParentCommand {
  firstName: string
  lastName: string
  email: string
  password: string
  phoneNumber: string
  alternatePhoneNumber?: string | null
  city?: string | null
  occupation?: string | null
  childEmail?: string | null
  relationship?: string | null
}

export interface RegisterStudentCommand {
  firstName: string
  lastName: string
  email: string
  password: string
  phoneNumber?: string | null
  gradeId?: string | null
  schoolName?: string | null
  city?: string | null
  parentPhoneNumber?: string | null
}

export interface RegisterTeacherCommand {
  firstName: string
  lastName: string
  email: string
  password: string
  phoneNumber: string
  headline?: string | null
  bio?: string | null
  yearsOfExperience?: number
  city?: string | null
  whatsAppNumber?: string | null
  preferredTeachingMode?: 'Online' | 'Offline' | 'Hybrid'
  subjectIds?: string[]
  gradeIds?: string[]
  workspaceName?: string | null
}

export interface ReorderWebsiteSectionsCommand {
  sectionIds?: string[]
}

export interface ResendVerificationCommand {
  email: string
}

export interface ResetPasswordCommand {
  email: string
  token: string
  newPassword: string
  confirmPassword: string
}

export interface ReturnHomeworkSubmissionCommand {
  submissionId?: string
  reason: string
}

export interface RevenuePointDto {
  year?: number
  month?: number
  label?: string
  groupRevenue?: number
  courseRevenue?: number
  totalRevenue?: number
  enrollmentsCount?: number
}

export interface ReviewDto {
  id?: string
  studentId?: string
  studentName?: string
  studentImageUrl?: string | null
  teacherId?: string
  teacherName?: string
  groupId?: string | null
  groupName?: string | null
  courseId?: string | null
  courseTitle?: string | null
  targetType?: 'Teacher' | 'Group' | 'Course'
  targetTypeName?: string
  rating?: number
  comment?: string | null
  isApproved?: boolean
  createdAt?: string
  updatedAt?: string | null
  isOwn?: boolean
}

export interface RevokeCertificateRequest {
  reason?: string | null
}

export interface RevokePlatformAdminCommand {
  userId?: string
  reason?: string | null
}

export interface RiskReasonDto {
  code: string
  messageAr: string
  messageEn: string
  value?: number
  threshold?: number
}

export interface ScheduleSlotDto {
  id?: string
  groupId?: string
  groupName?: string
  groupSlug?: string | null
  teacherId?: string
  teacherName?: string
  subjectId?: string
  subjectNameAr?: string
  subjectNameEn?: string
  dayOfWeek?: 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday'
  dayName?: string
  startTime?: string
  endTime?: string
  durationInMinutes?: number
  location?: string | null
  meetingUrl?: string | null
  isActive?: boolean
  nextOccurrenceUtc?: string | null
}

export interface ScoreBucketDto {
  fromPercentage?: number
  toPercentage?: number
  label?: string
  count?: number
  percentage?: number
}

export interface SendMessageCommand {
  conversationId?: string
  body: string
}

export interface SetUserActiveStateCommand {
  userId?: string
  isActive?: boolean
  reason?: string | null
}

export interface SocialLinkInput {
  platform?: 'Facebook' | 'Instagram' | 'YouTube' | 'TikTok' | 'LinkedIn' | 'WhatsApp'
  url?: string
  sortOrder?: number
}

export interface StartConversationCommand {
  tenantId?: string
  type?: 'ParentTeacher' | 'StudentTeacher' | 'TeacherAssistant' | 'Group'
  subject?: string | null
  groupId?: string | null
  participantUserIds?: string[]
  firstMessage?: string | null
}

export interface StudentAnalyticsDto {
  studentId?: string
  studentName?: string
  window?: AnalyticsWindowDto
  overallScore?: number | null
  examAverage?: number | null
  homeworkAverage?: number | null
  attendancePercentage?: number | null
  courseCompletion?: number | null
  lessonCompletion?: number | null
  engagement?: StudentEngagementDto
  workload?: StudentWorkloadDto
  strongSubjects?: StudentSubjectStandingDto[]
  weakSubjects?: StudentSubjectStandingDto[]
  subjectBreakdown?: StudentSubjectStandingDto[]
}

export interface StudentDashboardDto {
  activeGroupsCount?: number
  activeCoursesCount?: number
  completedCoursesCount?: number
  savedTeachersCount?: number
  unreadNotificationsCount?: number
  totalSpent?: number
  averageCourseProgress?: number
  activeGroups?: GroupListItemDto[]
  activeCourses?: CourseListItemDto[]
  upcomingClasses?: ScheduleSlotDto[]
  savedTeachers?: TeacherListItemDto[]
  recentNotifications?: NotificationDto[]
}

export interface StudentEngagementDto {
  learningStreakDays?: number
  longestStreakDays?: number
  activeDaysInWindow?: number
  lastActivityAt?: string | null
  lessonsCompletedInWindow?: number
  totalLearningMinutes?: number
}

export interface StudentExamOptionDto {
  id?: string
  text?: string
  sortOrder?: number
}

export interface StudentExamQuestionDto {
  id?: string
  text?: string
  type?: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer' | 'Essay'
  typeName?: string
  points?: number
  sortOrder?: number
  options?: StudentExamOptionDto[]
  selectedOptionId?: string | null
  textAnswer?: string | null
}

export interface StudentGradesSummaryDto {
  studentId?: string
  studentName?: string
  examsTaken?: number
  examsPassed?: number
  averagePercentage?: number
  highestPercentage?: number
  lowestPercentage?: number
  results?: ExamResultDto[]
  bySubject?: SubjectAverageDto[]
}

export interface StudentGrowthPointDto {
  year?: number
  month?: number
  label?: string
  newStudents?: number
  cumulativeStudents?: number
}

export interface StudentProfileDto {
  id?: string
  userId?: string
  firstName?: string
  lastName?: string
  displayName?: string
  email?: string
  profileImageUrl?: string | null
  phoneNumber?: string | null
  parentPhoneNumber?: string | null
  schoolName?: string | null
  city?: string | null
  dateOfBirth?: string | null
  gradeId?: string | null
  gradeNameAr?: string | null
  gradeNameEn?: string | null
  emailConfirmed?: boolean
  isActive?: boolean
  activeGroupsCount?: number
  activeCoursesCount?: number
  guardiansCount?: number
  createdAt?: string
}

export interface StudentSubjectStandingDto {
  subjectId?: string
  subjectNameAr?: string
  subjectNameEn?: string
  markCount?: number
  averagePercentage?: number
  passedCount?: number
  passRate?: number
}

export interface StudentWorkloadDto {
  activeGroups?: number
  activeCourses?: number
  completedCourses?: number
  overdueHomework?: number
  pendingHomework?: number
  upcomingExams?: number
}

export interface SubjectAverageDto {
  subjectId?: string
  subjectNameAr?: string
  subjectNameEn?: string
  examsTaken?: number
  averagePercentage?: number
}

export interface SubjectDto {
  id?: string
  nameAr?: string
  nameEn?: string
  slug?: string
  description?: string | null
  icon?: string | null
  colorHex?: string | null
  sortOrder?: number
  isActive?: boolean
  teachersCount?: number
  groupsCount?: number
  coursesCount?: number
}

export interface SubjectPerformanceDto {
  subjectId?: string
  subjectNameAr?: string
  subjectNameEn?: string
  resultsCount?: number
  averagePercentage?: number
  highestPercentage?: number
  lowestPercentage?: number
  passedCount?: number
  passRate?: number
}

export interface SubmitExamAttemptCommand {
  attemptId?: string
  answers?: ExamAnswerInput[]
}

export interface SubmitHomeworkCommand {
  homeworkId?: string
  content?: string | null
  fileUrl?: string | null
}

export interface SubscriptionPlanDto {
  id?: string
  code?: string
  nameAr?: string
  nameEn?: string
  description?: string | null
  monthlyPrice?: number
  yearlyPrice?: number | null
  currency?: string
  trialDays?: number
  sortOrder?: number
  isActive?: boolean
  maxStudents?: number | null
  maxGroups?: number | null
  maxCourses?: number | null
  maxAssistants?: number | null
  maxTeamMembers?: number | null
  maxStorageMegabytes?: number | null
  features?: string[]
}

export interface SuspendUserRequest {
  reason: string
}

export interface TeacherAchievementDto {
  id?: string
  title?: string
  description?: string | null
  year?: number | null
  imageUrl?: string | null
  sortOrder?: number
}

export interface TeacherAnalyticsDto {
  totalRevenue?: number
  monthlyRevenue?: number
  totalStudents?: number
  newStudentsThisMonth?: number
  averageRating?: number
  totalReviews?: number
  totalGroups?: number
  totalCourses?: number
  conversionPercentage?: number
  revenueTrend?: RevenuePointDto[]
  studentGrowth?: StudentGrowthPointDto[]
  groupPerformance?: GroupPerformanceDto[]
}

export interface TeacherAnalyticsOverviewDto {
  window?: AnalyticsWindowDto
  students?: TeacherStudentMetricsDto
  catalogue?: TeacherCatalogueMetricsDto
  teaching?: TeacherTeachingMetricsDto
  performance?: TeacherPerformanceMetricsDto
  revenue?: TeacherRevenueMetricsDto
}

export interface TeacherCatalogueMetricsDto {
  groups?: number
  publishedGroups?: number
  courses?: number
  publishedCourses?: number
  publishedLessons?: number
  subjects?: number
}

export interface TeacherDashboardDto {
  totalStudents?: number
  activeGroupsCount?: number
  publishedCoursesCount?: number
  draftGroupsCount?: number
  monthlyRevenue?: number
  totalRevenue?: number
  averageRating?: number
  reviewCount?: number
  newInquiriesCount?: number
  unreadNotificationsCount?: number
  isVerified?: boolean
  upcomingClasses?: ScheduleSlotDto[]
  topGroups?: GroupPerformanceDto[]
  recentActivity?: ActivityItemDto[]
}

export interface TeacherDetailsDto {
  id?: string
  userId?: string
  displayName?: string
  firstName?: string
  lastName?: string
  headline?: string | null
  bio?: string | null
  profileImageUrl?: string | null
  coverImageUrl?: string | null
  city?: string | null
  yearsOfExperience?: number
  studentsCount?: number
  rating?: number
  reviewCount?: number
  isFeatured?: boolean
  isVerified?: boolean
  verificationStatus?: 'NotSubmitted' | 'Pending' | 'UnderReview' | 'Verified' | 'Rejected'
  preferredTeachingMode?: 'Online' | 'Offline' | 'Hybrid'
  whatsAppLink?: string | null
  groupsCount?: number
  coursesCount?: number
  createdAt?: string
  subjects?: SubjectDto[]
  grades?: GradeDto[]
  achievements?: TeacherAchievementDto[]
  socialLinks?: TeacherSocialLinkDto[]
}

export interface TeacherListItemDto {
  id?: string
  userId?: string
  displayName?: string
  headline?: string | null
  profileImageUrl?: string | null
  coverImageUrl?: string | null
  city?: string | null
  yearsOfExperience?: number
  studentsCount?: number
  rating?: number
  reviewCount?: number
  isFeatured?: boolean
  isVerified?: boolean
  preferredTeachingMode?: 'Online' | 'Offline' | 'Hybrid'
  whatsAppLink?: string | null
  groupsCount?: number
  coursesCount?: number
  subjects?: SubjectDto[]
  grades?: GradeDto[]
}

export interface TeacherPerformanceMetricsDto {
  examAveragePercentage?: number | null
  examPassRate?: number | null
  homeworkAveragePercentage?: number | null
  homeworkSubmissionRate?: number | null
  attendanceRate?: number | null
  courseCompletionRate?: number | null
  averageCourseProgress?: number | null
}

export interface TeacherRevenueMetricsDto {
  total?: TrendDto
  groupRevenue?: number
  courseRevenue?: number
  enrollments?: TrendDto
  averageOrderValue?: number | null
  currency?: string
}

export interface TeacherSocialLinkDto {
  id?: string
  platform?: 'Facebook' | 'Instagram' | 'YouTube' | 'TikTok' | 'LinkedIn' | 'WhatsApp'
  platformName?: string
  url?: string
  sortOrder?: number
}

export interface TeacherStudentMetricsDto {
  total?: number
  active?: TrendDto
  new?: TrendDto
  churned?: TrendDto
}

export interface TeacherSummaryDto {
  id?: string
  userId?: string
  displayName?: string
  headline?: string | null
  profileImageUrl?: string | null
  city?: string | null
  rating?: number
  reviewCount?: number
  isVerified?: boolean
  whatsAppLink?: string | null
}

export interface TeacherTeachingMetricsDto {
  examsPublished?: TrendDto
  homeworkPublished?: TrendDto
  attendanceSessions?: TrendDto
  lessonsCompleted?: TrendDto
  homeworkAwaitingGrading?: number
  examAttemptsAwaitingGrading?: number
}

export interface TeacherVerificationDto {
  status?: 'NotSubmitted' | 'Pending' | 'UnderReview' | 'Verified' | 'Rejected'
  statusName?: string
  isVerified?: boolean
  canSubmit?: boolean
  isUnderReview?: boolean
  isProfileComplete?: boolean
  submittedAt?: string | null
  decidedAt?: string | null
  notes?: string | null
  requirements?: VerificationRequirementDto[]
}

export interface TenantBrandingDto {
  tenantId?: string
  logoUrl?: string | null
  coverImageUrl?: string | null
  faviconUrl?: string | null
  primaryColorHex?: string
  secondaryColorHex?: string
  accentColorHex?: string
  fontFamily?: string | null
  tagline?: string | null
}

export interface TenantDomainDto {
  id?: string
  tenantId?: string
  host?: string
  isPrimary?: boolean
  verificationStatus?: 'Pending' | 'Verified' | 'Failed'
  verificationStatusName?: string
  verificationRecordName?: string
  verificationRecordValue?: string
  verifiedAt?: string | null
  lastVerificationError?: string | null
  createdAt?: string
}

export interface TenantDto {
  id?: string
  slug?: string
  name?: string
  displayName?: string | null
  status?: 'Active' | 'Suspended' | 'Archived'
  statusName?: string
  ownerUserId?: string
  ownerDisplayName?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  whatsAppNumber?: string | null
  whatsAppLink?: string | null
  city?: string | null
  membersCount?: number
  studentsCount?: number
  groupsCount?: number
  coursesCount?: number
  branding?: TenantBrandingDto
  createdAt?: string
}

export interface TenantInvitationDto {
  id?: string
  tenantId?: string
  tenantName?: string
  email?: string
  role?: 'Owner' | 'Admin' | 'Assistant' | 'Moderator' | 'ContentManager'
  roleName?: string
  title?: string | null
  permissions?: string[]
  expiresAt?: string
  createdAt?: string
  token?: string | null
}

export interface TenantMemberDto {
  id?: string
  tenantId?: string
  userId?: string
  displayName?: string
  email?: string
  profileImageUrl?: string | null
  phoneNumber?: string | null
  role?: 'Owner' | 'Admin' | 'Assistant' | 'Moderator' | 'ContentManager'
  roleName?: string
  status?: 'Invited' | 'Active' | 'Inactive' | 'Declined' | 'Removed'
  statusName?: string
  title?: string | null
  permissions?: string[]
  explicitPermissions?: string[]
  invitedAt?: string | null
  joinedAt?: string | null
  createdAt?: string
}

export interface TenantSettingsDto {
  tenantId?: string
  defaultLanguage?: string
  timeZoneId?: string
  currency?: string
  gradeNotificationThresholdPercentage?: number
  notifyParentsOnAbsence?: boolean
  notifyParentsOnLowGrade?: boolean
  notifyParentsOnHomeworkMissed?: boolean
  allowStudentSelfEnrollment?: boolean
  autoApproveReviews?: boolean
  lateThresholdMinutes?: number
}

export interface TenantSubscriptionDto {
  id?: string
  tenantId?: string
  plan?: SubscriptionPlanDto
  status?: 'Trial' | 'Active' | 'PastDue' | 'GracePeriod' | 'Canceled' | 'Expired'
  statusName?: string
  billingCycle?: 'Monthly' | 'Yearly'
  billingCycleName?: string
  amount?: number
  currency?: string
  startedAt?: string
  trialEndsAt?: string | null
  currentPeriodStart?: string
  currentPeriodEnd?: string
  gracePeriodEndsAt?: string | null
  cancelAtPeriodEnd?: boolean
  canceledAt?: string | null
  isUsable?: boolean
  isTrialing?: boolean
  daysRemaining?: number | null
  usage?: PlanQuotaUsageDto[]
}

export interface TenantWebsiteDto {
  tenantId?: string
  isPublished?: boolean
  heroTitle?: string | null
  heroSubtitle?: string | null
  heroImageUrl?: string | null
  heroCtaLabel?: string | null
  heroCtaUrl?: string | null
  aboutTitle?: string | null
  aboutBody?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  contactAddress?: string | null
  whatsAppNumber?: string | null
  whatsAppLink?: string | null
  mapEmbedUrl?: string | null
  seo?: WebsiteSeoDto
  sections?: WebsiteSectionDto[]
  testimonials?: TestimonialDto[]
}

export interface TestimonialDto {
  id?: string
  authorName?: string
  authorTitle?: string | null
  authorImageUrl?: string | null
  quote?: string
  rating?: number
  isVisible?: boolean
  sortOrder?: number
}

export interface TrendDto {
  current?: number
  previous?: number | null
  change?: number | null
  changePercentage?: number | null
}

export interface UnreadCountDto {
  count?: number
}

export interface UnreadMessagesDto {
  totalUnread?: number
  conversationsWithUnread?: number
}

export interface UpdateAnnouncementCommand {
  announcementId?: string
  title?: string | null
  body?: string | null
  isPinned?: boolean | null
  expiresAt?: string | null
}

export interface UpdateAttendanceRecordCommand {
  recordId?: string
  status?: 'Present' | 'Absent' | 'Late' | 'Excused'
  minutesLate?: number | null
  note?: string | null
}

export interface UpdateAttendanceSessionCommand {
  sessionId?: string
  title?: string | null
  notes?: string | null
  startTime?: string | null
  endTime?: string | null
  status?: 'Draft' | 'Open' | 'Closed' | null
}

export interface UpdateCourseCommand {
  courseId?: string
  subjectId?: string | null
  gradeId?: string | null
  title?: string | null
  description?: string | null
  thumbnailUrl?: string | null
  price?: number | null
  originalPrice?: number | null
  discountPercentage?: number | null
}

export interface UpdateCourseLessonCommand {
  courseId?: string
  lessonId?: string
  sectionId?: string | null
  title?: string | null
  description?: string | null
  videoUrl?: string | null
  durationInMinutes?: number | null
  sortOrder?: number | null
  isPreview?: boolean | null
  isPublished?: boolean | null
}

export interface UpdateCourseSectionCommand {
  courseId?: string
  sectionId?: string
  title?: string | null
  description?: string | null
  sortOrder?: number | null
}

export interface UpdateDiscountCommand {
  discountId?: string
  description?: string | null
  value?: number | null
  minimumAmount?: number | null
  maximumDiscount?: number | null
  startDate?: string | null
  endDate?: string | null
  usageLimit?: number | null
}

export interface UpdateExamCommand {
  examId?: string
  title?: string | null
  description?: string | null
  type?: 'Quiz' | 'Monthly' | 'Midterm' | 'Final' | 'Practice' | null
  examDate?: string | null
  durationInMinutes?: number | null
  maximumScore?: number | null
  passingScore?: number | null
  groupId?: string | null
  isOnline?: boolean | null
  maxAttempts?: number | null
  showResultsImmediately?: boolean | null
}

export interface UpdateExamQuestionCommand {
  examId?: string
  questionId?: string
  text?: string | null
  type?: 'MultipleChoice' | 'TrueFalse' | 'ShortAnswer' | 'Essay' | null
  points?: number | null
  sortOrder?: number | null
  correctAnswer?: string | null
  explanation?: string | null
  options?: ExamQuestionOptionInput[] | null
}

export interface UpdateExamResultCommand {
  examId?: string
  studentId?: string
  score?: number
  isAbsent?: boolean
  teacherComment?: string | null
}

export interface UpdateGroupCommand {
  groupId?: string
  subjectId?: string | null
  gradeId?: string | null
  name?: string | null
  description?: string | null
  type?: 'Regular' | 'Revision' | 'Intensive' | 'ExamPreparation' | null
  teachingMode?: 'Online' | 'Offline' | 'Hybrid' | null
  location?: string | null
  monthlyPrice?: number | null
  originalPrice?: number | null
  discountPercentage?: number | null
  capacity?: number | null
  startDate?: string | null
  endDate?: string | null
  whatsAppLink?: string | null
  isActive?: boolean | null
}

export interface UpdateGroupCurriculumCommand {
  groupId?: string
  curriculumId?: string
  title?: string | null
  description?: string | null
  sortOrder?: number | null
}

export interface UpdateGroupScheduleCommand {
  groupId?: string
  scheduleId?: string
  dayOfWeek?: 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday' | null
  startTime?: string | null
  endTime?: string | null
  location?: string | null
  meetingUrl?: string | null
  isActive?: boolean | null
}

export interface UpdateHomeworkCommand {
  homeworkId?: string
  title?: string | null
  description?: string | null
  instructions?: string | null
  dueAt?: string | null
  maximumScore?: number | null
  allowLateSubmission?: boolean | null
  allowResubmission?: boolean | null
  maxSubmissions?: number | null
}

export interface UpdateInquiryStatusCommand {
  inquiryId?: string
  status?: 'New' | 'Contacted' | 'Converted' | 'Closed'
  note?: string | null
}

export interface UpdateLanguagePreferenceCommand {
  language: string
}

export interface UpdateMemberPermissionsCommand {
  memberId?: string
  permissions?: string[]
}

export interface UpdateNotificationPreferencesCommand {
  preferences?: NotificationPreferenceInput[]
}

export interface UpdateParentProfileCommand {
  firstName?: string | null
  lastName?: string | null
  phoneNumber?: string | null
  alternatePhoneNumber?: string | null
  city?: string | null
  occupation?: string | null
}

export interface UpdateProgressRequest {
  progressPercentage?: number
}

export interface UpdateReviewCommand {
  reviewId?: string
  rating?: number | null
  comment?: string | null
}

export interface UpdateStudentProfileCommand {
  firstName?: string | null
  lastName?: string | null
  phoneNumber?: string | null
  parentPhoneNumber?: string | null
  schoolName?: string | null
  city?: string | null
  dateOfBirth?: string | null
  gradeId?: string | null
}

export interface UpdateSubmissionRequest {
  content?: string | null
  fileUrl?: string | null
}

export interface UpdateTeacherAboutRequest {
  headline?: string | null
  bio?: string | null
  yearsOfExperience?: number | null
}

export interface UpdateTeacherAchievementsCommand {
  achievements?: AchievementInput[]
}

export interface UpdateTeacherGradesCommand {
  gradeIds?: string[]
}

export interface UpdateTeacherProfileCommand {
  firstName?: string | null
  lastName?: string | null
  displayName?: string | null
  headline?: string | null
  bio?: string | null
  yearsOfExperience?: number | null
  city?: string | null
  phoneNumber?: string | null
  whatsAppNumber?: string | null
  preferredTeachingMode?: 'Online' | 'Offline' | 'Hybrid' | null
}

export interface UpdateTeacherSocialLinksCommand {
  links?: SocialLinkInput[]
}

export interface UpdateTeacherSubjectsCommand {
  subjectIds?: string[]
}

export interface UpdateTenantBrandingCommand {
  primaryColorHex?: string | null
  secondaryColorHex?: string | null
  accentColorHex?: string | null
  fontFamily?: string | null
  tagline?: string | null
  faviconUrl?: string | null
}

export interface UpdateTenantCommand {
  name?: string | null
  displayName?: string | null
  slug?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  whatsAppNumber?: string | null
  city?: string | null
}

export interface UpdateTenantDomainCommand {
  domainId?: string
  isPrimary?: boolean | null
}

export interface UpdateTenantSettingsCommand {
  defaultLanguage?: string | null
  timeZoneId?: string | null
  currency?: string | null
  gradeNotificationThresholdPercentage?: number | null
  notifyParentsOnAbsence?: boolean | null
  notifyParentsOnLowGrade?: boolean | null
  notifyParentsOnHomeworkMissed?: boolean | null
  allowStudentSelfEnrollment?: boolean | null
  autoApproveReviews?: boolean | null
  lateThresholdMinutes?: number | null
}

export interface UpdateTenantWebsiteCommand {
  isPublished?: boolean | null
  heroTitle?: string | null
  heroSubtitle?: string | null
  heroImageUrl?: string | null
  heroCtaLabel?: string | null
  heroCtaUrl?: string | null
  aboutTitle?: string | null
  aboutBody?: string | null
  contactEmail?: string | null
  contactPhone?: string | null
  contactAddress?: string | null
  whatsAppNumber?: string | null
  mapEmbedUrl?: string | null
  seoTitle?: string | null
  seoDescription?: string | null
  seoKeywords?: string | null
  seoImageUrl?: string | null
  googleAnalyticsId?: string | null
  facebookPixelId?: string | null
}

export interface UpdateWebsiteSectionCommand {
  sectionId?: string
  title?: string | null
  subtitle?: string | null
  body?: string | null
  imageUrl?: string | null
  settingsJson?: string | null
  isVisible?: boolean | null
  sortOrder?: number | null
}

export interface ValidateDiscountQuery {
  code: string
  groupId?: string | null
  courseId?: string | null
  teacherId?: string | null
  amount?: number | null
}

export interface VerificationRequirementDto {
  key: string
  isMet?: boolean
}

export interface VerifyEmailCommand {
  email: string
  token: string
}

export interface VerifyTeacherRequest {
  approve?: boolean
  notes?: string | null
}

export interface WebsiteSectionDto {
  id?: string
  type?: 'Hero' | 'About' | 'Achievements' | 'Testimonials' | 'FeaturedGroups' | 'FeaturedCourses' | 'Teachers' | 'Contact' | 'Custom'
  typeName?: string
  title?: string | null
  subtitle?: string | null
  body?: string | null
  imageUrl?: string | null
  settingsJson?: string | null
  isVisible?: boolean
  sortOrder?: number
}

export interface WebsiteSeoDto {
  title?: string | null
  description?: string | null
  keywords?: string | null
  imageUrl?: string | null
  googleAnalyticsId?: string | null
  facebookPixelId?: string | null
  canonicalUrl?: string | null
}
