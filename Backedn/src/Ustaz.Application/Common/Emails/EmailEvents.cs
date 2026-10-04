namespace Ustaz.Application.Common.Emails;

/// <summary>
/// Every transactional email the platform sends.
///
/// Keys deliberately mirror <see cref="Notifications.NotificationEvents"/> wherever the same
/// thing happens in both channels: the notification dispatcher looks a template up by the
/// notification's event key, so wiring one event into both the bell and the inbox needs no
/// extra code — only a template with a matching name.
/// </summary>
public static class EmailEvents
{
    // Account and security --------------------------------------------------
    public const string Welcome = "account.welcome";
    public const string EmailVerification = "account.email_verification";
    public const string EmailVerified = "security.email_verified";
    public const string PasswordResetRequested = "security.password_reset_requested";
    public const string PasswordReset = "security.password_reset";
    public const string PasswordChanged = "security.password_changed";
    public const string AccountActivated = "security.account_activated";
    public const string AccountSuspended = "security.account_suspended";

    // Courses and enrollment ------------------------------------------------
    public const string GroupEnrollmentCreated = "enrollment.group_created";
    public const string StudentAddedToGroup = "enrollment.student_added";
    public const string GroupEnrollmentCancelled = "enrollment.group_cancelled";
    public const string CourseStudentEnrolled = "course.student_enrolled";
    public const string CourseLessonAdded = "course.lesson_added";
    public const string CourseCompleted = "course.completed";
    public const string CourseCompletedByStudent = "course.completed_by_student";
    public const string ChildEnrolled = "enrollment.child_enrolled";

    // Homework --------------------------------------------------------------
    public const string HomeworkAssigned = "homework.assigned";
    public const string HomeworkDueSoon = "homework.due_soon";
    public const string HomeworkGraded = "homework.graded";
    public const string HomeworkSubmittedLate = "homework.submitted_late";
    public const string ChildHomeworkMissing = "homework.child_missing";
    public const string ChildHomeworkGraded = "homework.child_graded";

    // Exams -----------------------------------------------------------------
    public const string ExamPublished = "exam.published";
    public const string ExamStartingSoon = "exam.starting_soon";
    public const string ExamSubmitted = "exam.submitted";
    public const string ExamAttemptExpired = "exam.attempt_expired";
    public const string ExamAwaitingGrading = "exam.awaiting_grading";
    public const string ExamResultsPublished = "exam.results_published";
    public const string ChildExamResult = "exam.child_result";
    public const string ChildLowGrade = "exam.child_low_grade";

    // Attendance ------------------------------------------------------------
    public const string AttendanceAbsent = "attendance.absent";
    public const string AttendanceLate = "attendance.late";
    public const string ChildAbsent = "attendance.child_absent";

    // Messaging -------------------------------------------------------------
    public const string MessageReceived = "message.received";

    // Payments --------------------------------------------------------------
    public const string PaymentRecorded = "payment.recorded";
    public const string PaymentSucceeded = "payment.succeeded";
    public const string PaymentFailed = "payment.failed";
    public const string PaymentRefunded = "payment.refunded";
    public const string PaymentDueSoon = "payment.due_soon";
    public const string PaymentReceived = "payment.received";

    // Subscriptions ---------------------------------------------------------
    public const string SubscriptionActivated = "subscription.activated";
    public const string SubscriptionRenewed = "subscription.renewed";
    public const string SubscriptionExpiring = "subscription.expiring";
    public const string SubscriptionExpired = "subscription.expired";
    public const string SubscriptionPaymentFailed = "subscription.payment_failed";
    public const string UsageLimitApproaching = "subscription.usage_limit_approaching";
    public const string UsageLimitReached = "subscription.usage_limit_reached";

    // Community and team ----------------------------------------------------
    public const string TeacherVerified = "teacher.verified";
    public const string AnnouncementPublished = "announcement.published";
    public const string TeamMemberInvited = "team.member_invited";

    // Certificates ----------------------------------------------------------
    public const string CertificateIssued = "certificate.issued";

    // Early warning ---------------------------------------------------------
    public const string StudentAtRisk = "risk.student_at_risk";
    public const string ChildFallingBehind = "risk.child_falling_behind";
}
