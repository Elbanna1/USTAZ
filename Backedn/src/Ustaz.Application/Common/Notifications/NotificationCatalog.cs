using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Notifications;

/// <summary>
/// The single place every notification's wording, category, priority and deep link lives.
///
/// The Arabic is Egyptian dialect on purpose. USTAZ is used by teachers, students and parents
/// in Egypt, and a notification that reads like a bank letter gets ignored; one that reads
/// like a person gets opened. Emojis are used sparingly — on the moments that genuinely feel
/// like good news — and never on money, security or absence.
/// </summary>
public static class NotificationCatalog
{
    private static readonly Dictionary<string, NotificationTemplate> Templates = Build();

    public static IReadOnlyCollection<NotificationTemplate> All => Templates.Values;

    public static NotificationTemplate Get(string @event) =>
        Templates.TryGetValue(@event, out var template)
            ? template
            : throw new InvalidOperationException(
                "No notification template is registered for the event '" + @event + "'.");

    public static bool TryGet(string @event, out NotificationTemplate template) =>
        Templates.TryGetValue(@event, out template!);

    private static Dictionary<string, NotificationTemplate> Build()
    {
        var templates = new List<NotificationTemplate>
        {
            // -----------------------------------------------------------------
            // Security. Critical, so preferences never silence them.
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.PasswordChanged,
                Type = NotificationType.Security,
                Category = NotificationCategory.Security,
                Priority = NotificationPriority.Critical,
                TitleAr = "الباسورد بتاعك اتغير 🔐",
                BodyAr = "غيّرنا الباسورد بتاعك وقفلنا كل الجلسات المفتوحة. لو مش إنت اللي عملت كده، كلّمنا حالًا.",
                TitleEn = "Your password was changed",
                BodyEn = "Your password was changed and every open session was signed out. If this wasn't you, contact us right away.",
                ActionUrl = "/settings/security"
            },
            new()
            {
                Event = NotificationEvents.PasswordReset,
                Type = NotificationType.Security,
                Category = NotificationCategory.Security,
                Priority = NotificationPriority.Critical,
                TitleAr = "اتعمل ريسيت للباسورد 🔐",
                BodyAr = "الباسورد بتاعك اتعمله ريسيت. لو مش إنت، غيّره تاني حالًا.",
                TitleEn = "Your password was reset",
                BodyEn = "Your password has been reset. If this wasn't you, change it again immediately.",
                ActionUrl = "/settings/security"
            },
            new()
            {
                Event = NotificationEvents.AccountActivated,
                Type = NotificationType.Security,
                Category = NotificationCategory.Security,
                Priority = NotificationPriority.Critical,
                TitleAr = "حسابك رجع شغال",
                BodyAr = "تم تفعيل حسابك تاني على أستاذ. أهلًا بيك من جديد.",
                TitleEn = "Your account is active again",
                BodyEn = "Your USTAZ account has been reactivated. Welcome back.",
                ActionUrl = "/"
            },
            new()
            {
                Event = NotificationEvents.AccountSuspended,
                Type = NotificationType.Security,
                Category = NotificationCategory.Security,
                Priority = NotificationPriority.Critical,
                TitleAr = "حسابك اتوقف",
                BodyAr = "حسابك اتوقف. السبب: {reason}",
                TitleEn = "Your account was suspended",
                BodyEn = "Your account has been suspended. Reason: {reason}",
                RequiredValues = ["reason"]
            },
            new()
            {
                Event = NotificationEvents.EmailVerified,
                Type = NotificationType.Security,
                Category = NotificationCategory.Security,
                Priority = NotificationPriority.Normal,
                TitleAr = "الإيميل بتاعك اتأكد ✅",
                BodyAr = "تمام، الإيميل بتاعك اتفعّل وحسابك جاهز.",
                TitleEn = "Your email is verified",
                BodyEn = "Your email address is confirmed and your account is ready.",
                ActionUrl = "/"
            },

            // -----------------------------------------------------------------
            // Courses
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.CoursePublished,
                Type = NotificationType.Course,
                Category = NotificationCategory.Course,
                Priority = NotificationPriority.Normal,
                TitleAr = "الكورس بقى متاح للطلاب 🚀",
                BodyAr = "كورس {course} اتنشر ودلوقتي الطلاب يقدروا يشتركوا فيه.",
                TitleEn = "Your course is live",
                BodyEn = "{course} has been published and students can now enrol.",
                ActionUrl = "/teacher/courses/{courseId}",
                RelatedEntityType = "Course",
                RequiredValues = ["course", "courseId"]
            },
            new()
            {
                Event = NotificationEvents.CourseUnpublished,
                Type = NotificationType.Course,
                Category = NotificationCategory.Course,
                Priority = NotificationPriority.Low,
                TitleAr = "الكورس اتشال من النشر",
                BodyAr = "كورس {course} بقى مخفي عن الطلاب دلوقتي.",
                TitleEn = "Your course is no longer listed",
                BodyEn = "{course} is now hidden from students.",
                ActionUrl = "/teacher/courses/{courseId}",
                RelatedEntityType = "Course",
                RequiredValues = ["course", "courseId"]
            },
            new()
            {
                Event = NotificationEvents.CourseLessonAdded,
                Type = NotificationType.Course,
                Category = NotificationCategory.Course,
                Priority = NotificationPriority.Normal,
                TitleAr = "درس جديد نزل 🎬",
                BodyAr = "نزل درس جديد في كورس {course}: {lesson}",
                TitleEn = "A new lesson is out",
                BodyEn = "A new lesson was added to {course}: {lesson}",
                ActionUrl = "/courses/{courseId}",
                RelatedEntityType = "Course",
                RequiredValues = ["course", "courseId", "lesson"]
            },
            new()
            {
                Event = NotificationEvents.CourseStudentEnrolled,
                Type = NotificationType.Course,
                Category = NotificationCategory.Enrollment,
                Priority = NotificationPriority.Normal,
                TitleAr = "طالب جديد دخل الكورس",
                BodyAr = "{student} اشترك في كورس {course}.",
                TitleEn = "A new student joined your course",
                BodyEn = "{student} enrolled in {course}.",
                ActionUrl = "/teacher/courses/{courseId}",
                RelatedEntityType = "CourseEnrollment",
                RequiredValues = ["student", "course", "courseId"]
            },
            new()
            {
                Event = NotificationEvents.CourseCompleted,
                Type = NotificationType.Course,
                Category = NotificationCategory.Course,
                Priority = NotificationPriority.High,
                TitleAr = "خلصت الكورس! 🎉",
                BodyAr = "مبروك، خلصت كل دروس كورس {course}.",
                TitleEn = "You finished the course",
                BodyEn = "Congratulations — you completed every lesson of {course}.",
                ActionUrl = "/student/courses/{courseId}/progress",
                RelatedEntityType = "CourseEnrollment",
                RequiredValues = ["course", "courseId"]
            },
            new()
            {
                Event = NotificationEvents.CourseCompletedByStudent,
                Type = NotificationType.Course,
                Category = NotificationCategory.Course,
                Priority = NotificationPriority.Normal,
                TitleAr = "طالب خلّص الكورس 🎓",
                BodyAr = "{student} خلّص كورس {course} بالكامل.",
                TitleEn = "A student completed your course",
                BodyEn = "{student} has completed {course}.",
                ActionUrl = "/teacher/courses/{courseId}",
                RelatedEntityType = "CourseEnrollment",
                RequiredValues = ["student", "course", "courseId"]
            },

            // -----------------------------------------------------------------
            // Enrollment
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.GroupEnrollmentCreated,
                Type = NotificationType.Enrollment,
                Category = NotificationCategory.Enrollment,
                Priority = NotificationPriority.Normal,
                TitleAr = "اشتراك جديد في مجموعتك",
                BodyAr = "{student} انضم لمجموعة {group}.",
                TitleEn = "A new student joined your group",
                BodyEn = "{student} joined {group}.",
                ActionUrl = "/teacher/groups/{groupId}",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["student", "group", "groupId"]
            },
            new()
            {
                Event = NotificationEvents.StudentAddedToGroup,
                Type = NotificationType.Enrollment,
                Category = NotificationCategory.Enrollment,
                Priority = NotificationPriority.High,
                TitleAr = "اتسجلت في مجموعة جديدة 🎉",
                BodyAr = "اتسجلت في مجموعة {group}. شوف المواعيد من هنا.",
                TitleEn = "You've been added to a group",
                BodyEn = "You were enrolled in {group}. Check the schedule here.",
                ActionUrl = "/student/enrollments/groups",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["group"]
            },
            new()
            {
                Event = NotificationEvents.GroupEnrollmentCancelled,
                Type = NotificationType.Enrollment,
                Category = NotificationCategory.Enrollment,
                Priority = NotificationPriority.High,
                TitleAr = "اشتراكك في المجموعة اتلغى",
                BodyAr = "اشتراكك في مجموعة {group} اتلغى. لو ده مش صح كلّم المدرس.",
                TitleEn = "Your group enrollment was cancelled",
                BodyEn = "Your enrollment in {group} has been cancelled. Contact your teacher if this looks wrong.",
                ActionUrl = "/student/enrollments/groups",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["group"]
            },
            new()
            {
                Event = NotificationEvents.ChildEnrolled,
                Type = NotificationType.Enrollment,
                Category = NotificationCategory.Enrollment,
                Priority = NotificationPriority.Normal,
                TitleAr = "تسجيل جديد لـ{student}",
                BodyAr = "{student} اتسجل في {group}.",
                TitleEn = "A new enrollment for {student}",
                BodyEn = "{student} has been enrolled in {group}.",
                ActionUrl = "/parent/children/{studentId}",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["student", "studentId", "group"]
            },

            // -----------------------------------------------------------------
            // Attendance
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.AttendancePresent,
                Type = NotificationType.Attendance,
                Category = NotificationCategory.Attendance,
                Priority = NotificationPriority.Low,
                TitleAr = "تم تسجيل حضورك ✅",
                BodyAr = "اتسجل حضورك في {group} النهارده.",
                TitleEn = "Your attendance was recorded",
                BodyEn = "You were marked present in {group} today.",
                ActionUrl = "/student/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["group"]
            },
            new()
            {
                Event = NotificationEvents.AttendanceAbsent,
                Type = NotificationType.Attendance,
                Category = NotificationCategory.Attendance,
                Priority = NotificationPriority.High,
                TitleAr = "اتسجل عليك غياب",
                BodyAr = "اتسجل عليك غياب في {group} النهارده.",
                TitleEn = "You were marked absent",
                BodyEn = "You were marked absent in {group} today.",
                ActionUrl = "/student/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["group"]
            },
            new()
            {
                Event = NotificationEvents.AttendanceLate,
                Type = NotificationType.Attendance,
                Category = NotificationCategory.Attendance,
                Priority = NotificationPriority.Normal,
                TitleAr = "اتسجل عليك تأخير",
                BodyAr = "وصلت متأخر في {group} النهارده.",
                TitleEn = "You were marked late",
                BodyEn = "You arrived late to {group} today.",
                ActionUrl = "/student/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["group"]
            },
            new()
            {
                Event = NotificationEvents.AttendanceUpdated,
                Type = NotificationType.Attendance,
                Category = NotificationCategory.Attendance,
                Priority = NotificationPriority.Low,
                TitleAr = "الحضور بتاعك اتعدل",
                BodyAr = "المدرس عدّل تسجيل حضورك في {group} وبقى: {status}",
                TitleEn = "Your attendance was updated",
                BodyEn = "Your attendance for {group} was changed to: {status}",
                ActionUrl = "/student/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["group", "status"]
            },
            new()
            {
                Event = NotificationEvents.ChildAbsent,
                Type = NotificationType.Attendance,
                Category = NotificationCategory.Attendance,
                Priority = NotificationPriority.High,
                TitleAr = "غياب النهارده",
                BodyAr = "{student} اتسجل عليه غياب في {group} النهارده.",
                TitleEn = "Absence today",
                BodyEn = "{student} was marked absent in {group} today.",
                ActionUrl = "/parent/children/{studentId}/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["student", "studentId", "group"]
            },

            // -----------------------------------------------------------------
            // Homework
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.HomeworkAssigned,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.High,
                TitleAr = "عندك واجب جديد 📚",
                BodyAr = "المدرس نزّل واجب جديد: {homework}. آخر ميعاد {due}.",
                TitleEn = "You have new homework",
                BodyEn = "New homework: {homework}. Due {due}.",
                ActionUrl = "/student/homework/{homeworkId}",
                RelatedEntityType = "Homework",
                RequiredValues = ["homework", "due", "homeworkId"]
            },
            new()
            {
                Event = NotificationEvents.HomeworkDueSoon,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.High,
                TitleAr = "الديدلاين قرب ⏰",
                BodyAr = "متنساش تسلّم واجب {homework}، آخر ميعاد {due}.",
                TitleEn = "Deadline is close",
                BodyEn = "Don't forget to hand in {homework} — it's due {due}.",
                ActionUrl = "/student/homework/{homeworkId}",
                RelatedEntityType = "Homework",
                RequiredValues = ["homework", "due", "homeworkId"]
            },
            new()
            {
                Event = NotificationEvents.HomeworkGraded,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.High,
                TitleAr = "المدرس صحّح الواجب بتاعك ✍️",
                BodyAr = "جبت {score} من {max} في واجب {homework}.",
                TitleEn = "Your homework was graded",
                BodyEn = "You scored {score} out of {max} on {homework}.",
                ActionUrl = "/student/homework/{homeworkId}",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["homework", "homeworkId", "score", "max"]
            },
            new()
            {
                Event = NotificationEvents.HomeworkReturned,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.High,
                TitleAr = "المدرس رجّعلك الواجب للتعديل ✏️",
                BodyAr = "واجب {homework} محتاج تعديل: {reason}",
                TitleEn = "Your homework was returned for revision",
                BodyEn = "{homework} needs a revision: {reason}",
                ActionUrl = "/student/homework/{homeworkId}",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["homework", "homeworkId", "reason"]
            },
            new()
            {
                Event = NotificationEvents.HomeworkClosed,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.Normal,
                TitleAr = "اتقفل باب التسليم",
                BodyAr = "واجب {homework} مابقاش بيقبل تسليم.",
                TitleEn = "Submissions are closed",
                BodyEn = "{homework} is no longer accepting submissions.",
                ActionUrl = "/student/homework/{homeworkId}",
                RelatedEntityType = "Homework",
                RequiredValues = ["homework", "homeworkId"]
            },
            new()
            {
                Event = NotificationEvents.HomeworkSubmitted,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.Low,
                TitleAr = "تسليم جديد",
                BodyAr = "{student} سلّم واجب {homework}.",
                TitleEn = "New submission",
                BodyEn = "{student} submitted {homework}.",
                ActionUrl = "/teacher/homework/{homeworkId}/submissions",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["student", "homework", "homeworkId"]
            },
            new()
            {
                Event = NotificationEvents.HomeworkSubmittedLate,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.Normal,
                TitleAr = "تسليم متأخر",
                BodyAr = "{student} سلّم واجب {homework} بعد الميعاد.",
                TitleEn = "Late submission",
                BodyEn = "{student} submitted {homework} after the deadline.",
                ActionUrl = "/teacher/homework/{homeworkId}/submissions",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["student", "homework", "homeworkId"]
            },
            new()
            {
                Event = NotificationEvents.ChildHomeworkMissing,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.High,
                TitleAr = "واجب لسه متسلّمش",
                BodyAr = "{student} لسه مسلّمش واجب {homework} والميعاد {due}.",
                TitleEn = "Homework not handed in",
                BodyEn = "{student} hasn't submitted {homework}, due {due}.",
                ActionUrl = "/parent/children/{studentId}/homework",
                RelatedEntityType = "Homework",
                RequiredValues = ["student", "studentId", "homework", "due"]
            },
            new()
            {
                Event = NotificationEvents.ChildHomeworkGraded,
                Type = NotificationType.Homework,
                Category = NotificationCategory.Homework,
                Priority = NotificationPriority.Normal,
                TitleAr = "درجة الواجب ظهرت",
                BodyAr = "{student} جاب {score} من {max} في واجب {homework}.",
                TitleEn = "Homework graded",
                BodyEn = "{student} scored {score} out of {max} on {homework}.",
                ActionUrl = "/parent/children/{studentId}/homework",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["student", "studentId", "homework", "score", "max"]
            },

            // -----------------------------------------------------------------
            // Exams
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.ExamPublished,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Exam,
                Priority = NotificationPriority.High,
                TitleAr = "عندك امتحان جديد 📝",
                BodyAr = "امتحان {exam} اتحدد يوم {date}. جهّز نفسك.",
                TitleEn = "A new exam is scheduled",
                BodyEn = "{exam} is scheduled for {date}. Get ready.",
                ActionUrl = "/student/exams/{examId}",
                RelatedEntityType = "Exam",
                RequiredValues = ["exam", "date", "examId"]
            },
            new()
            {
                Event = NotificationEvents.ExamStartingSoon,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Exam,
                Priority = NotificationPriority.High,
                TitleAr = "الامتحان قرب يبدأ ⏳",
                BodyAr = "امتحان {exam} هيبدأ {date}. متتأخرش.",
                TitleEn = "Your exam starts soon",
                BodyEn = "{exam} starts {date}. Don't be late.",
                ActionUrl = "/student/exams/{examId}",
                RelatedEntityType = "Exam",
                RequiredValues = ["exam", "date", "examId"]
            },
            new()
            {
                Event = NotificationEvents.ExamSubmitted,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Exam,
                Priority = NotificationPriority.Normal,
                TitleAr = "تسليم الامتحان تم ✅",
                BodyAr = "إجاباتك في امتحان {exam} اتسجلت. هنبلغك أول ما النتيجة تظهر.",
                TitleEn = "Your exam was submitted",
                BodyEn = "Your answers for {exam} were recorded. We'll let you know when the result is out.",
                ActionUrl = "/student/exams/attempts/{attemptId}",
                RelatedEntityType = "ExamAttempt",
                RequiredValues = ["exam", "attemptId"]
            },
            new()
            {
                Event = NotificationEvents.ExamAttemptExpired,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Exam,
                Priority = NotificationPriority.High,
                TitleAr = "وقت الامتحان خلص",
                BodyAr = "وقت امتحان {exam} خلص، واتسجل اللي كنت جاوبته.",
                TitleEn = "Your exam time is up",
                BodyEn = "Time ran out on {exam}. Whatever you had answered was submitted.",
                ActionUrl = "/student/exams/attempts/{attemptId}",
                RelatedEntityType = "ExamAttempt",
                RequiredValues = ["exam", "attemptId"]
            },
            new()
            {
                Event = NotificationEvents.ExamAwaitingGrading,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Exam,
                Priority = NotificationPriority.Normal,
                TitleAr = "إجابات مستنية تصحيح",
                BodyAr = "في محاولة في امتحان {exam} فيها أسئلة مقالية محتاجة تصحيح.",
                TitleEn = "Answers awaiting grading",
                BodyEn = "An attempt on {exam} has essay answers waiting for you.",
                ActionUrl = "/teacher/exams/{examId}/attempts",
                RelatedEntityType = "Exam",
                RequiredValues = ["exam", "examId"]
            },
            new()
            {
                Event = NotificationEvents.ExamStudentSubmitted,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Exam,
                Priority = NotificationPriority.Low,
                TitleAr = "طالب سلّم الامتحان",
                BodyAr = "{student} سلّم امتحان {exam}.",
                TitleEn = "A student submitted the exam",
                BodyEn = "{student} submitted {exam}.",
                ActionUrl = "/teacher/exams/{examId}/attempts",
                RelatedEntityType = "ExamAttempt",
                RequiredValues = ["student", "exam", "examId"]
            },
            new()
            {
                Event = NotificationEvents.ExamResultsPublished,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Grade,
                Priority = NotificationPriority.High,
                TitleAr = "نتيجة الامتحان ظهرت 🎯",
                BodyAr = "نتيجة امتحان {exam} بقت متاحة. شوف درجتك.",
                TitleEn = "Your exam result is out",
                BodyEn = "Results for {exam} are available. Check your score.",
                ActionUrl = "/student/grades",
                RelatedEntityType = "Exam",
                RequiredValues = ["exam"]
            },
            new()
            {
                Event = NotificationEvents.ChildExamResult,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Grade,
                Priority = NotificationPriority.Normal,
                TitleAr = "نتيجة امتحان {student}",
                BodyAr = "{student} جاب {score} من {max} في امتحان {exam}.",
                TitleEn = "{student}'s exam result",
                BodyEn = "{student} scored {score} out of {max} on {exam}.",
                ActionUrl = "/parent/children/{studentId}/grades",
                RelatedEntityType = "ExamResult",
                RequiredValues = ["student", "studentId", "exam", "score", "max"]
            },
            new()
            {
                Event = NotificationEvents.ChildLowGrade,
                Type = NotificationType.Exam,
                Category = NotificationCategory.Grade,
                Priority = NotificationPriority.High,
                TitleAr = "نتيجة محتاجة وقفة",
                BodyAr = "{student} جاب {score} من {max} ({percentage}%) في امتحان {exam}.",
                TitleEn = "A result worth a look",
                BodyEn = "{student} scored {score} out of {max} ({percentage}%) on {exam}.",
                ActionUrl = "/parent/children/{studentId}/grades",
                RelatedEntityType = "ExamResult",
                RequiredValues = ["student", "studentId", "exam", "score", "max", "percentage"]
            },

            // -----------------------------------------------------------------
            // Messaging
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.MessageReceived,
                Type = NotificationType.Message,
                Category = NotificationCategory.Message,
                Priority = NotificationPriority.Normal,
                TitleAr = "رسالة جديدة من {sender}",
                BodyAr = "{preview}",
                TitleEn = "New message from {sender}",
                BodyEn = "{preview}",
                ActionUrl = "/messages/conversations/{conversationId}",
                RelatedEntityType = "Conversation",
                RequiredValues = ["sender", "preview", "conversationId"]
            },
            new()
            {
                Event = NotificationEvents.ConversationStarted,
                Type = NotificationType.Message,
                Category = NotificationCategory.Message,
                Priority = NotificationPriority.Normal,
                TitleAr = "{sender} فتح معاك محادثة",
                BodyAr = "في محادثة جديدة مستنية ردّك.",
                TitleEn = "{sender} started a conversation",
                BodyEn = "A new conversation is waiting for your reply.",
                ActionUrl = "/messages/conversations/{conversationId}",
                RelatedEntityType = "Conversation",
                RequiredValues = ["sender", "conversationId"]
            },

            // -----------------------------------------------------------------
            // Payments
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.PaymentRecorded,
                Type = NotificationType.Payment,
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Normal,
                TitleAr = "اتسجل عليك مبلغ مستحق",
                BodyAr = "مستحق عليك {amount} جنيه — {description}. آخر ميعاد {due}.",
                TitleEn = "A payment was recorded",
                BodyEn = "{amount} EGP is due — {description}. Due {due}.",
                ActionUrl = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["amount", "description", "due"]
            },
            new()
            {
                Event = NotificationEvents.PaymentSucceeded,
                Type = NotificationType.Payment,
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.High,
                TitleAr = "الدفع تم بنجاح 💳",
                BodyAr = "استلمنا {amount} جنيه. شكرًا ليك.",
                TitleEn = "Payment received",
                BodyEn = "We received {amount} EGP. Thank you.",
                ActionUrl = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["amount"]
            },
            new()
            {
                Event = NotificationEvents.PaymentFailed,
                Type = NotificationType.Payment,
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Critical,
                TitleAr = "عملية الدفع ماكملتش",
                BodyAr = "الدفع بمبلغ {amount} جنيه مانجحش. جرّب تاني أو استخدم كارت تاني.",
                TitleEn = "Your payment didn't go through",
                BodyEn = "The {amount} EGP payment failed. Try again or use another card.",
                ActionUrl = "/billing",
                RelatedEntityType = "BillingTransaction",
                RequiredValues = ["amount"]
            },
            new()
            {
                Event = NotificationEvents.PaymentRefunded,
                Type = NotificationType.Payment,
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.High,
                TitleAr = "تم استرجاع المبلغ",
                BodyAr = "رجّعنالك {amount} جنيه.",
                TitleEn = "Your payment was refunded",
                BodyEn = "{amount} EGP has been refunded to you.",
                ActionUrl = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["amount"]
            },
            new()
            {
                Event = NotificationEvents.PaymentDueSoon,
                Type = NotificationType.Payment,
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.High,
                TitleAr = "ميعاد الدفع قرب",
                BodyAr = "مستحق {amount} جنيه قبل {due}.",
                TitleEn = "A payment is due soon",
                BodyEn = "{amount} EGP is due before {due}.",
                ActionUrl = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["amount", "due"]
            },
            new()
            {
                Event = NotificationEvents.PaymentReceived,
                Type = NotificationType.Payment,
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.Normal,
                TitleAr = "وصلك تحصيل جديد",
                BodyAr = "{student} دفع {amount} جنيه.",
                TitleEn = "Payment received",
                BodyEn = "{student} paid {amount} EGP.",
                ActionUrl = "/teacher/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["student", "amount"]
            },

            // -----------------------------------------------------------------
            // Subscriptions
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.SubscriptionActivated,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.High,
                TitleAr = "الاشتراك اتفعّل 🚀",
                BodyAr = "باقة {plan} بقت شغالة على مساحتك.",
                TitleEn = "Your subscription is active",
                BodyEn = "The {plan} plan is now active on your workspace.",
                ActionUrl = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["plan"]
            },
            new()
            {
                Event = NotificationEvents.SubscriptionRenewed,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.Normal,
                TitleAr = "الاشتراك اتجدد",
                BodyAr = "باقة {plan} اتجددت لحد {date}.",
                TitleEn = "Your subscription renewed",
                BodyEn = "The {plan} plan renewed until {date}.",
                ActionUrl = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["plan", "date"]
            },
            new()
            {
                Event = NotificationEvents.SubscriptionExpiring,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.High,
                TitleAr = "الاشتراك قرب يخلص",
                BodyAr = "باقة {plan} هتخلص يوم {date}. جدّدها عشان الخدمة ماتقفش.",
                TitleEn = "Your subscription is expiring",
                BodyEn = "The {plan} plan ends on {date}. Renew to avoid interruption.",
                ActionUrl = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["plan", "date"]
            },
            new()
            {
                Event = NotificationEvents.SubscriptionExpired,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.Critical,
                TitleAr = "الاشتراك خلص",
                BodyAr = "باقة {plan} خلصت. جدّد عشان ترجع تستخدم كل المميزات.",
                TitleEn = "Your subscription expired",
                BodyEn = "The {plan} plan has expired. Renew to restore full access.",
                ActionUrl = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["plan"]
            },
            new()
            {
                Event = NotificationEvents.SubscriptionPaymentFailed,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.Critical,
                TitleAr = "فاتورة الاشتراك ماتدفعتش",
                BodyAr = "محاولة تحصيل {amount} جنيه للاشتراك مانجحتش. راجع طريقة الدفع.",
                TitleEn = "Subscription payment failed",
                BodyEn = "We couldn't collect {amount} EGP for your subscription. Please check your payment method.",
                ActionUrl = "/billing",
                RelatedEntityType = "BillingTransaction",
                RequiredValues = ["amount"]
            },
            new()
            {
                Event = NotificationEvents.UsageLimitApproaching,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.Normal,
                TitleAr = "قربت توصل لحد الباقة",
                BodyAr = "استخدمت {used} من {limit} {resource}. فكّر في ترقية الباقة.",
                TitleEn = "You're close to your plan limit",
                BodyEn = "You've used {used} of {limit} {resource}. Consider upgrading.",
                ActionUrl = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["used", "limit", "resource"]
            },
            new()
            {
                Event = NotificationEvents.UsageLimitReached,
                Type = NotificationType.Subscription,
                Category = NotificationCategory.Subscription,
                Priority = NotificationPriority.High,
                TitleAr = "وصلت لحد الباقة",
                BodyAr = "خلصت الـ{limit} {resource} المتاحين في باقتك. رقّي الباقة عشان تكمّل.",
                TitleEn = "You've reached your plan limit",
                BodyEn = "You've used all {limit} {resource} in your plan. Upgrade to add more.",
                ActionUrl = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["limit", "resource"]
            },

            // -----------------------------------------------------------------
            // Community
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.ReviewReceived,
                Type = NotificationType.Review,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.Low,
                TitleAr = "تقييم جديد ⭐",
                BodyAr = "{student} سابلك تقييم {rating} من 5.",
                TitleEn = "You received a new review",
                BodyEn = "{student} rated you {rating} out of 5.",
                ActionUrl = "/teacher/reviews",
                RelatedEntityType = "Review",
                RequiredValues = ["student", "rating"]
            },
            new()
            {
                Event = NotificationEvents.InquiryReceived,
                Type = NotificationType.Inquiry,
                Category = NotificationCategory.Message,
                Priority = NotificationPriority.Normal,
                TitleAr = "استفسار جديد",
                BodyAr = "وصلك استفسار جديد من {sender}.",
                TitleEn = "New inquiry",
                BodyEn = "You received a new inquiry from {sender}.",
                ActionUrl = "/teacher/inquiries/{inquiryId}",
                RelatedEntityType = "Inquiry",
                RequiredValues = ["sender", "inquiryId"]
            },
            new()
            {
                Event = NotificationEvents.InquiryAnswered,
                Type = NotificationType.Inquiry,
                Category = NotificationCategory.Message,
                Priority = NotificationPriority.Normal,
                TitleAr = "في رد على استفسارك",
                BodyAr = "المدرس رد على استفسارك.",
                TitleEn = "Your inquiry was answered",
                BodyEn = "The teacher replied to your inquiry.",
                ActionUrl = "/student/inquiries",
                RelatedEntityType = "Inquiry"
            },
            new()
            {
                Event = NotificationEvents.AnnouncementPublished,
                Type = NotificationType.Announcement,
                Category = NotificationCategory.Announcement,
                Priority = NotificationPriority.Normal,
                TitleAr = "إعلان جديد 📢",
                BodyAr = "{title}",
                TitleEn = "New announcement",
                BodyEn = "{title}",
                ActionUrl = "/announcements/{announcementId}",
                RelatedEntityType = "Announcement",
                RequiredValues = ["title", "announcementId"]
            },
            new()
            {
                Event = NotificationEvents.TeacherVerified,
                Type = NotificationType.Verification,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.High,
                TitleAr = "حسابك اتوثّق ✅",
                BodyAr = "مبروك، حسابك بقى موثّق وتقدر تنشر المجموعات والكورسات.",
                TitleEn = "Your account is verified",
                BodyEn = "Your account is verified — you can now publish groups and courses.",
                ActionUrl = "/teacher/profile"
            },
            new()
            {
                Event = NotificationEvents.TeacherVerificationSubmitted,
                Type = NotificationType.Verification,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.Normal,
                TitleAr = "طلب التوثيق اتبعت",
                BodyAr = "استلمنا طلبك وهنراجع بياناتك ونرد عليك في أقرب وقت.",
                TitleEn = "Verification request sent",
                BodyEn = "We have your request and will review your details and get back to you shortly.",
                ActionUrl = "/teacher/settings"
            },
            new()
            {
                Event = NotificationEvents.TeacherVerificationUnderReview,
                Type = NotificationType.Verification,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.Normal,
                TitleAr = "بنراجع طلب التوثيق",
                BodyAr = "فريق المراجعة بدأ يبص على بياناتك دلوقتي.",
                TitleEn = "Your verification is being reviewed",
                BodyEn = "Our review team has started looking at your details.",
                ActionUrl = "/teacher/settings"
            },
            new()
            {
                Event = NotificationEvents.TeacherVerificationRejected,
                Type = NotificationType.Verification,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.High,
                TitleAr = "طلب التوثيق محتاج تعديل",
                BodyAr = "ماقدرناش نوثّق الحساب دلوقتي. السبب: {reason}. عدّل بياناتك وابعت الطلب تاني.",
                TitleEn = "Your verification needs changes",
                BodyEn = "We could not verify the account yet. Reason: {reason}. Update your details and submit again.",
                ActionUrl = "/teacher/settings",
                RequiredValues = ["reason"]
            },

            // -----------------------------------------------------------------
            // Certificates
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.CertificateIssued,
                Type = NotificationType.Certificate,
                Category = NotificationCategory.Certificate,
                Priority = NotificationPriority.High,
                TitleAr = "مبروك 🎉 الشهادة بتاعتك جاهزة",
                BodyAr = "شهادة إتمام كورس {course} بقت جاهزة. رقمها {number}.",
                TitleEn = "Your certificate is ready",
                BodyEn = "Your certificate for {course} is ready. Number {number}.",
                ActionUrl = "/certificates/{certificateId}",
                RelatedEntityType = "Certificate",
                RequiredValues = ["course", "number", "certificateId"]
            },

            // -----------------------------------------------------------------
            // Early warning
            // -----------------------------------------------------------------
            new()
            {
                Event = NotificationEvents.StudentAtRisk,
                Type = NotificationType.General,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.High,
                TitleAr = "طالب محتاج متابعة",
                BodyAr = "{student} مستواه نزل: {reasons}",
                TitleEn = "A student needs attention",
                BodyEn = "{student} is falling behind: {reasons}",
                ActionUrl = "/teacher/analytics/students",
                RelatedEntityType = "StudentProfile",
                RequiredValues = ["student", "reasons"]
            },
            new()
            {
                Event = NotificationEvents.ChildFallingBehind,
                Type = NotificationType.General,
                Category = NotificationCategory.System,
                Priority = NotificationPriority.High,
                TitleAr = "{student} محتاج متابعة",
                BodyAr = "لاحظنا إن {student} متأخر شوية: {reasons}",
                TitleEn = "{student} needs a hand",
                BodyEn = "We noticed {student} is falling behind: {reasons}",
                ActionUrl = "/parent/children/{studentId}",
                RelatedEntityType = "StudentProfile",
                RequiredValues = ["student", "studentId", "reasons"]
            }
        };

        return templates.ToDictionary(template => template.Event, StringComparer.Ordinal);
    }
}
