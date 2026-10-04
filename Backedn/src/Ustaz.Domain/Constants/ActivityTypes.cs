namespace Ustaz.Domain.Constants;

/// <summary>
/// Every kind of entry the activity timeline can hold, as a stable key.
///
/// The key is the contract: the client switches on it to pick an icon and a sentence, so the
/// wording lives in the client and a phrasing change never needs a migration. Keys are never
/// renamed once shipped, because rows already reference them.
/// </summary>
public static class ActivityTypes
{
    // Learning ---------------------------------------------------------------
    public const string LessonCompleted = "lesson.completed";
    public const string CourseJoined = "course.joined";
    public const string CourseCompleted = "course.completed";
    public const string GroupJoined = "group.joined";
    public const string GroupLeft = "group.left";

    // Assessment -------------------------------------------------------------
    public const string HomeworkSubmitted = "homework.submitted";
    public const string HomeworkGraded = "homework.graded";
    public const string HomeworkReturned = "homework.returned";
    public const string ExamCompleted = "exam.completed";
    public const string ExamGraded = "exam.graded";

    // Attendance -------------------------------------------------------------
    public const string AttendanceRecorded = "attendance.recorded";

    // Teaching ---------------------------------------------------------------
    public const string LessonPublished = "lesson.published";
    public const string CoursePublished = "course.published";
    public const string GroupPublished = "group.published";
    public const string ExamPublished = "exam.published";
    public const string HomeworkPublished = "homework.published";

    // Commercial -------------------------------------------------------------
    public const string PaymentReceived = "payment.received";
    public const string ReviewReceived = "review.received";

    // Account ----------------------------------------------------------------
    public const string StudentJoinedWorkspace = "student.joined_workspace";
    public const string TeacherVerified = "teacher.verified";
    public const string CertificateIssued = "certificate.issued";

    /// <summary>Every key the system writes. Used to validate a filter before it reaches SQL.</summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        LessonCompleted, CourseJoined, CourseCompleted, GroupJoined, GroupLeft,
        HomeworkSubmitted, HomeworkGraded, HomeworkReturned, ExamCompleted, ExamGraded,
        AttendanceRecorded,
        LessonPublished, CoursePublished, GroupPublished, ExamPublished, HomeworkPublished,
        PaymentReceived, ReviewReceived,
        StudentJoinedWorkspace, TeacherVerified, CertificateIssued
    };

    public static bool IsKnown(string type) => All.Contains(type);
}
