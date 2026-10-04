namespace Ustaz.Application.Common.Notifications;

/// <summary>
/// Every notification the platform can raise, as a stable key. Handlers reference these
/// constants and never the wording: the copy lives in <see cref="NotificationCatalog"/>, so
/// a phrase is changed in exactly one place and the client can switch on the key.
/// </summary>
public static class NotificationEvents
{
    // Security and account -----------------------------------------------------
    public const string PasswordChanged = "security.password_changed";
    public const string PasswordReset = "security.password_reset";
    public const string AccountActivated = "security.account_activated";
    public const string AccountSuspended = "security.account_suspended";
    public const string EmailVerified = "security.email_verified";

    // Courses ------------------------------------------------------------------
    public const string CoursePublished = "course.published";
    public const string CourseUnpublished = "course.unpublished";
    public const string CourseLessonAdded = "course.lesson_added";
    public const string CourseStudentEnrolled = "course.student_enrolled";
    public const string CourseCompletedByStudent = "course.completed_by_student";
    public const string CourseCompleted = "course.completed";

    // Enrollment ---------------------------------------------------------------
    public const string GroupEnrollmentCreated = "enrollment.group_created";
    public const string GroupEnrollmentCancelled = "enrollment.group_cancelled";
    public const string StudentAddedToGroup = "enrollment.student_added";
    public const string ChildEnrolled = "enrollment.child_enrolled";

    // Attendance ---------------------------------------------------------------
    public const string AttendancePresent = "attendance.present";
    public const string AttendanceAbsent = "attendance.absent";
    public const string AttendanceLate = "attendance.late";
    public const string AttendanceUpdated = "attendance.updated";
    public const string ChildAbsent = "attendance.child_absent";

    // Homework -----------------------------------------------------------------
    public const string HomeworkAssigned = "homework.assigned";
    public const string HomeworkDueSoon = "homework.due_soon";
    public const string HomeworkGraded = "homework.graded";
    public const string HomeworkSubmitted = "homework.submitted";
    public const string HomeworkSubmittedLate = "homework.submitted_late";
    public const string HomeworkReturned = "homework.returned";
    public const string HomeworkClosed = "homework.closed";
    public const string ChildHomeworkMissing = "homework.child_missing";
    public const string ChildHomeworkGraded = "homework.child_graded";

    // Exams --------------------------------------------------------------------
    public const string ExamPublished = "exam.published";
    public const string ExamStartingSoon = "exam.starting_soon";
    public const string ExamSubmitted = "exam.submitted";
    public const string ExamAttemptExpired = "exam.attempt_expired";
    public const string ExamAwaitingGrading = "exam.awaiting_grading";
    public const string ExamResultsPublished = "exam.results_published";
    public const string ExamStudentSubmitted = "exam.student_submitted";
    public const string ChildExamResult = "exam.child_result";
    public const string ChildLowGrade = "exam.child_low_grade";

    // Messaging ----------------------------------------------------------------
    public const string MessageReceived = "message.received";
    public const string ConversationStarted = "message.conversation_started";

    // Payments -----------------------------------------------------------------
    public const string PaymentRecorded = "payment.recorded";
    public const string PaymentSucceeded = "payment.succeeded";
    public const string PaymentFailed = "payment.failed";
    public const string PaymentRefunded = "payment.refunded";
    public const string PaymentDueSoon = "payment.due_soon";
    public const string PaymentReceived = "payment.received";

    // Subscriptions ------------------------------------------------------------
    public const string SubscriptionActivated = "subscription.activated";
    public const string SubscriptionRenewed = "subscription.renewed";
    public const string SubscriptionExpiring = "subscription.expiring";
    public const string SubscriptionExpired = "subscription.expired";
    public const string SubscriptionPaymentFailed = "subscription.payment_failed";
    public const string UsageLimitApproaching = "subscription.usage_limit_approaching";
    public const string UsageLimitReached = "subscription.usage_limit_reached";

    // Community ----------------------------------------------------------------
    public const string ReviewReceived = "review.received";
    public const string InquiryReceived = "inquiry.received";
    public const string InquiryAnswered = "inquiry.answered";
    public const string AnnouncementPublished = "announcement.published";
    public const string TeacherVerified = "teacher.verified";
    public const string TeacherVerificationSubmitted = "teacher.verification_submitted";
    public const string TeacherVerificationRejected = "teacher.verification_rejected";
    public const string TeacherVerificationUnderReview = "teacher.verification_under_review";

    // Certificates -------------------------------------------------------------
    public const string CertificateIssued = "certificate.issued";

    // Early warning ------------------------------------------------------------
    public const string StudentAtRisk = "risk.student_at_risk";
    public const string ChildFallingBehind = "risk.child_falling_behind";
}
