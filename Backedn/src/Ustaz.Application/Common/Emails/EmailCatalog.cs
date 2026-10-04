namespace Ustaz.Application.Common.Emails;

/// <summary>
/// Every transactional email's subject, copy, call to action and footnote, in one place.
///
/// No handler writes email HTML or an email subject. The Arabic is Egyptian dialect, the way
/// a teacher or a parent actually reads — with one deliberate exception: security emails stay
/// plain and unambiguous, because "أمّن حسابك فورًا" has to land as an instruction, not as
/// friendly chatter.
/// </summary>
public static class EmailCatalog
{
    private static readonly Dictionary<string, EmailTemplate> Templates = Build();

    public static IReadOnlyCollection<EmailTemplate> All => Templates.Values;

    public static EmailTemplate Get(string @event) =>
        Templates.TryGetValue(@event, out var template)
            ? template
            : throw new InvalidOperationException(
                "No email template is registered for the event '" + @event + "'.");

    public static bool TryGet(string @event, out EmailTemplate template) =>
        Templates.TryGetValue(@event, out template!);

    private static Dictionary<string, EmailTemplate> Build()
    {
        var templates = new List<EmailTemplate>
        {
            // -----------------------------------------------------------------
            // Account and security
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.Welcome,
                SubjectAr = "أهلًا بيك في Ustaz 🎉",
                SubjectEn = "Welcome to Ustaz 🎉",
                HeadingAr = "أهلًا بيك يا {userName}",
                HeadingEn = "Welcome, {userName}",
                BodyAr =
                [
                    "حسابك على Ustaz اتفتح بنجاح، ومن دلوقتي تقدر تتابع دروسك وواجباتك وامتحاناتك من مكان واحد.",
                    "ابدأ من لوحة التحكم بتاعتك واختار اللي يناسبك."
                ],
                BodyEn =
                [
                    "Your Ustaz account is ready. From here you can follow your lessons, homework and exams in one place.",
                    "Start from your dashboard and pick up where you like."
                ],
                CtaLabelAr = "ابدأ دلوقتي",
                CtaLabelEn = "Get started",
                CtaPath = "/",
                RequiredValues = ["userName"]
            },
            new()
            {
                Event = EmailEvents.EmailVerification,
                SubjectAr = "فعّل إيميلك على Ustaz",
                SubjectEn = "Confirm your Ustaz email",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "فاضل خطوة واحدة بس: أكّد إن الإيميل ده بتاعك عشان نقدر نبعتلك كل حاجة مهمة.",
                    "اضغط على الزر تحت وهيتفعّل حسابك على طول."
                ],
                BodyEn =
                [
                    "One step left: confirm this address so we can send you everything that matters.",
                    "Press the button below and your account is activated."
                ],
                CtaLabelAr = "أكّد الإيميل",
                CtaLabelEn = "Confirm email",
                CtaPath = "{verificationUrl}",
                FootnoteAr = "الرابط ده صالح لفترة محدودة، ومينفعش يتستخدم أكتر من مرة. لو مش إنت اللي عملت الحساب، تجاهل الرسالة.",
                FootnoteEn = "This link is valid for a limited time and can only be used once. If you didn't create this account, ignore this message.",
                IsSecurity = true,
                RequiredValues = ["userName", "verificationUrl"]
            },
            new()
            {
                Event = EmailEvents.EmailVerified,
                SubjectAr = "إيميلك اتأكد ✅",
                SubjectEn = "Your email is confirmed",
                HeadingAr = "تمام يا {userName}",
                HeadingEn = "All set, {userName}",
                BodyAr = ["الإيميل بتاعك اتأكد وحسابك بقى شغال بالكامل."],
                BodyEn = ["Your email is confirmed and your account is fully active."],
                CtaLabelAr = "روح للوحة التحكم",
                CtaLabelEn = "Open your dashboard",
                CtaPath = "/",
                IsSecurity = true,
                RequiredValues = ["userName"]
            },
            new()
            {
                Event = EmailEvents.PasswordResetRequested,
                SubjectAr = "تقدر تغيّر الباسورد بتاعك 🔐",
                SubjectEn = "Reset your Ustaz password 🔐",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "وصلنا طلب لتغيير الباسورد بتاع حسابك على Ustaz.",
                    "اضغط على الزر تحت عشان تختار باسورد جديد."
                ],
                BodyEn =
                [
                    "We received a request to reset the password on your Ustaz account.",
                    "Use the button below to choose a new one."
                ],
                CtaLabelAr = "غيّر الباسورد",
                CtaLabelEn = "Reset password",
                CtaPath = "{resetUrl}",
                FootnoteAr = "الرابط صالح لفترة قصيرة ولمرة واحدة بس. لو مش إنت اللي طلبت ده، تجاهل الرسالة والباسورد بتاعك هيفضل زي ما هو.",
                FootnoteEn = "This link is short-lived and single-use. If you didn't request it, ignore this message — your password stays as it is.",
                IsSecurity = true,
                RequiredValues = ["userName", "resetUrl"]
            },
            new()
            {
                Event = EmailEvents.PasswordReset,
                SubjectAr = "الباسورد بتاعك اتغير 🔐",
                SubjectEn = "Your password was reset 🔐",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "الباسورد بتاع حسابك على Ustaz اتعمله ريسيت بنجاح، وكل الجلسات المفتوحة اتقفلت.",
                    "لو إنت اللي عملت ده، مفيش أي حاجة تانية مطلوبة منك.",
                    "لو مش إنت، غيّر الباسورد فورًا وكلّمنا."
                ],
                BodyEn =
                [
                    "The password on your Ustaz account was reset and every open session was signed out.",
                    "If this was you, there's nothing else to do.",
                    "If it wasn't, reset your password again immediately and contact us."
                ],
                CtaLabelAr = "أمّن حسابك",
                CtaLabelEn = "Secure your account",
                CtaPath = "/settings/security",
                IsSecurity = true,
                RequiredValues = ["userName"]
            },
            new()
            {
                Event = EmailEvents.PasswordChanged,
                SubjectAr = "الباسورد بتاعك اتغير 🔐",
                SubjectEn = "Your password was changed 🔐",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "الباسورد بتاع حسابك على Ustaz اتغير بنجاح، وقفلنا كل الجلسات المفتوحة.",
                    "لو إنت اللي عملت التغيير، مفيش أي حاجة تعملها.",
                    "لو مش إنت، أمّن حسابك فورًا."
                ],
                BodyEn =
                [
                    "The password on your Ustaz account was changed and every open session was signed out.",
                    "If you made this change, there's nothing to do.",
                    "If you didn't, secure your account immediately."
                ],
                CtaLabelAr = "أمّن حسابك",
                CtaLabelEn = "Secure your account",
                CtaPath = "/settings/security",
                IsSecurity = true,
                RequiredValues = ["userName"]
            },
            new()
            {
                Event = EmailEvents.AccountActivated,
                SubjectAr = "حسابك رجع شغال",
                SubjectEn = "Your account is active again",
                HeadingAr = "أهلًا بيك تاني {userName}",
                HeadingEn = "Welcome back, {userName}",
                BodyAr = ["حسابك على Ustaz اتفعّل تاني وتقدر تستخدمه عادي من دلوقتي."],
                BodyEn = ["Your Ustaz account has been reactivated and is ready to use."],
                CtaLabelAr = "ادخل على حسابك",
                CtaLabelEn = "Sign in",
                CtaPath = "/login",
                IsSecurity = true,
                RequiredValues = ["userName"]
            },
            new()
            {
                Event = EmailEvents.AccountSuspended,
                SubjectAr = "حسابك على Ustaz اتوقف",
                SubjectEn = "Your Ustaz account was suspended",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "حسابك اتوقف مؤقتًا.",
                    "السبب: {reason}",
                    "لو شايف إن في خطأ، كلّمنا وهنراجع الموضوع."
                ],
                BodyEn =
                [
                    "Your account has been suspended.",
                    "Reason: {reason}",
                    "If you believe this is a mistake, contact us and we'll review it."
                ],
                IsSecurity = true,
                RequiredValues = ["userName", "reason"]
            },

            // -----------------------------------------------------------------
            // Enrollment and courses
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.StudentAddedToGroup,
                SubjectAr = "اتسجلت في الكورس بنجاح 🎓",
                SubjectEn = "You're enrolled 🎓",
                HeadingAr = "مبروك يا {userName}",
                HeadingEn = "You're in, {userName}",
                BodyAr =
                [
                    "اتسجلت في مجموعة {group}.",
                    "تقدر تشوف المواعيد والمحتوى من حسابك."
                ],
                BodyEn =
                [
                    "You have been enrolled in {group}.",
                    "Your schedule and materials are in your account."
                ],
                CtaLabelAr = "شوف المجموعة",
                CtaLabelEn = "View the group",
                CtaPath = "/student/enrollments/groups",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["userName", "group"]
            },
            new()
            {
                Event = EmailEvents.GroupEnrollmentCreated,
                SubjectAr = "طالب جديد في مجموعتك",
                SubjectEn = "A new student joined your group",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} انضم لمجموعة {group}."],
                BodyEn = ["{student} joined {group}."],
                CtaLabelAr = "شوف المجموعة",
                CtaLabelEn = "Open the group",
                CtaPath = "/teacher/groups/{groupId}",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["userName", "student", "group", "groupId"]
            },
            new()
            {
                Event = EmailEvents.GroupEnrollmentCancelled,
                SubjectAr = "اشتراكك في المجموعة اتلغى",
                SubjectEn = "Your enrollment was cancelled",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "اشتراكك في مجموعة {group} اتلغى.",
                    "لو ده مش صح، كلّم المدرس أو الدعم."
                ],
                BodyEn =
                [
                    "Your enrollment in {group} has been cancelled.",
                    "If that looks wrong, contact your teacher or support."
                ],
                CtaLabelAr = "شوف اشتراكاتي",
                CtaLabelEn = "View my enrollments",
                CtaPath = "/student/enrollments/groups",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["userName", "group"]
            },
            new()
            {
                Event = EmailEvents.CourseStudentEnrolled,
                SubjectAr = "طالب جديد دخل الكورس بتاعك 🎓",
                SubjectEn = "A new student joined your course 🎓",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} اشترك في كورس {course}."],
                BodyEn = ["{student} enrolled in {course}."],
                CtaLabelAr = "شوف الكورس",
                CtaLabelEn = "Open the course",
                CtaPath = "/teacher/courses/{courseId}",
                RelatedEntityType = "CourseEnrollment",
                RequiredValues = ["userName", "student", "course", "courseId"]
            },
            new()
            {
                Event = EmailEvents.CourseLessonAdded,
                SubjectAr = "درس جديد نزل في الكورس 🎬",
                SubjectEn = "A new lesson is out 🎬",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["نزل درس جديد في كورس {course}: {lesson}"],
                BodyEn = ["A new lesson was added to {course}: {lesson}"],
                CtaLabelAr = "اتفرج على الدرس",
                CtaLabelEn = "Watch the lesson",
                CtaPath = "/courses/{courseId}",
                RelatedEntityType = "Course",
                RequiredValues = ["userName", "course", "courseId", "lesson"]
            },
            new()
            {
                Event = EmailEvents.CourseCompleted,
                SubjectAr = "مبروك، خلصت الكورس 🎉",
                SubjectEn = "You finished the course 🎉",
                HeadingAr = "برافو يا {userName}",
                HeadingEn = "Well done, {userName}",
                BodyAr = ["خلصت كل دروس كورس {course}. لو الكورس بيدي شهادة، هتوصلك على طول."],
                BodyEn = ["You completed every lesson of {course}. If the course issues a certificate, it's on its way."],
                CtaLabelAr = "شوف تقدمك",
                CtaLabelEn = "View your progress",
                CtaPath = "/student/courses/{courseId}/progress",
                RelatedEntityType = "CourseEnrollment",
                RequiredValues = ["userName", "course", "courseId"]
            },
            new()
            {
                Event = EmailEvents.CourseCompletedByStudent,
                SubjectAr = "طالب خلّص الكورس بتاعك 🎓",
                SubjectEn = "A student completed your course 🎓",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} خلّص كورس {course} بالكامل."],
                BodyEn = ["{student} has completed {course}."],
                CtaLabelAr = "شوف الكورس",
                CtaLabelEn = "Open the course",
                CtaPath = "/teacher/courses/{courseId}",
                RelatedEntityType = "CourseEnrollment",
                RequiredValues = ["userName", "student", "course", "courseId"]
            },
            new()
            {
                Event = EmailEvents.ChildEnrolled,
                SubjectAr = "تسجيل جديد لـ{student} 🎓",
                SubjectEn = "A new enrollment for {student} 🎓",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} اتسجل في {group}."],
                BodyEn = ["{student} has been enrolled in {group}."],
                CtaLabelAr = "تابع من هنا",
                CtaLabelEn = "Follow along",
                CtaPath = "/parent/children/{studentId}",
                RelatedEntityType = "GroupEnrollment",
                RequiredValues = ["userName", "student", "studentId", "group"]
            },

            // -----------------------------------------------------------------
            // Homework
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.HomeworkAssigned,
                SubjectAr = "عندك واجب جديد 📚",
                SubjectEn = "You have new homework 📚",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "المدرس نزّل واجب جديد: {homework}",
                    "آخر ميعاد للتسليم: {due}"
                ],
                BodyEn =
                [
                    "New homework has been assigned: {homework}",
                    "Due: {due}"
                ],
                CtaLabelAr = "افتح الواجب",
                CtaLabelEn = "Open the homework",
                CtaPath = "/student/homework/{homeworkId}",
                RelatedEntityType = "Homework",
                RequiredValues = ["userName", "homework", "due", "homeworkId"]
            },
            new()
            {
                Event = EmailEvents.HomeworkDueSoon,
                SubjectAr = "الديدلاين قرب ⏰",
                SubjectEn = "Your homework is due soon ⏰",
                HeadingAr = "متنساش يا {userName}",
                HeadingEn = "Don't forget, {userName}",
                BodyAr =
                [
                    "واجب {homework} لسه متسلّمش.",
                    "آخر ميعاد: {due}"
                ],
                BodyEn =
                [
                    "{homework} hasn't been handed in yet.",
                    "Due: {due}"
                ],
                CtaLabelAr = "سلّم الواجب",
                CtaLabelEn = "Hand it in",
                CtaPath = "/student/homework/{homeworkId}",
                RelatedEntityType = "Homework",
                RequiredValues = ["userName", "homework", "due", "homeworkId"]
            },
            new()
            {
                Event = EmailEvents.HomeworkGraded,
                SubjectAr = "المدرس صحّح الواجب بتاعك ✍️",
                SubjectEn = "Your homework was graded ✍️",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["جبت {score} من {max} في واجب {homework}."],
                BodyEn = ["You scored {score} out of {max} on {homework}."],
                CtaLabelAr = "شوف التصحيح",
                CtaLabelEn = "See the feedback",
                CtaPath = "/student/homework/{homeworkId}",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["userName", "homework", "homeworkId", "score", "max"]
            },
            new()
            {
                Event = EmailEvents.HomeworkSubmittedLate,
                SubjectAr = "تسليم متأخر لواجب {homework}",
                SubjectEn = "Late submission for {homework}",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} سلّم واجب {homework} بعد الميعاد."],
                BodyEn = ["{student} submitted {homework} after the deadline."],
                CtaLabelAr = "شوف التسليم",
                CtaLabelEn = "Review the submission",
                CtaPath = "/teacher/homework/{homeworkId}/submissions",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["userName", "student", "homework", "homeworkId"]
            },
            new()
            {
                Event = EmailEvents.ChildHomeworkMissing,
                SubjectAr = "{student} لسه مسلّمش واجب",
                SubjectEn = "{student} hasn't handed in homework",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "{student} لسه مسلّمش واجب {homework}.",
                    "الميعاد كان: {due}"
                ],
                BodyEn =
                [
                    "{student} hasn't submitted {homework}.",
                    "It was due: {due}"
                ],
                CtaLabelAr = "تابع الواجبات",
                CtaLabelEn = "Review homework",
                CtaPath = "/parent/children/{studentId}/homework",
                RelatedEntityType = "Homework",
                RequiredValues = ["userName", "student", "studentId", "homework", "due"]
            },
            new()
            {
                Event = EmailEvents.ChildHomeworkGraded,
                SubjectAr = "درجة واجب {student} ظهرت",
                SubjectEn = "{student}'s homework was graded",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} جاب {score} من {max} في واجب {homework}."],
                BodyEn = ["{student} scored {score} out of {max} on {homework}."],
                CtaLabelAr = "شوف التفاصيل",
                CtaLabelEn = "See the details",
                CtaPath = "/parent/children/{studentId}/homework",
                RelatedEntityType = "HomeworkSubmission",
                RequiredValues = ["userName", "student", "studentId", "homework", "score", "max"]
            },

            // -----------------------------------------------------------------
            // Exams
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.ExamPublished,
                SubjectAr = "عندك امتحان جديد 📝",
                SubjectEn = "A new exam is scheduled 📝",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "امتحان {exam} اتحدد.",
                    "الميعاد: {date}"
                ],
                BodyEn =
                [
                    "{exam} has been scheduled.",
                    "Date: {date}"
                ],
                CtaLabelAr = "شوف تفاصيل الامتحان",
                CtaLabelEn = "View the exam",
                CtaPath = "/student/exams/{examId}",
                RelatedEntityType = "Exam",
                RequiredValues = ["userName", "exam", "date", "examId"]
            },
            new()
            {
                Event = EmailEvents.ExamStartingSoon,
                SubjectAr = "الامتحان قرب يبدأ ⏳",
                SubjectEn = "Your exam starts soon ⏳",
                HeadingAr = "جهّز نفسك يا {userName}",
                HeadingEn = "Get ready, {userName}",
                BodyAr = ["امتحان {exam} هيبدأ {date}."],
                BodyEn = ["{exam} starts {date}."],
                CtaLabelAr = "افتح الامتحان",
                CtaLabelEn = "Open the exam",
                CtaPath = "/student/exams/{examId}",
                RelatedEntityType = "Exam",
                RequiredValues = ["userName", "exam", "date", "examId"]
            },
            new()
            {
                Event = EmailEvents.ExamSubmitted,
                SubjectAr = "تسليم الامتحان تم ✅",
                SubjectEn = "Your exam was submitted ✅",
                HeadingAr = "تمام يا {userName}",
                HeadingEn = "All done, {userName}",
                BodyAr = ["إجاباتك في امتحان {exam} اتسجلت. هنبلغك أول ما النتيجة تظهر."],
                BodyEn = ["Your answers for {exam} were recorded. We'll let you know when the result is out."],
                CtaLabelAr = "شوف المحاولة",
                CtaLabelEn = "View your attempt",
                CtaPath = "/student/exams/attempts/{attemptId}",
                RelatedEntityType = "ExamAttempt",
                RequiredValues = ["userName", "exam", "attemptId"]
            },
            new()
            {
                Event = EmailEvents.ExamAttemptExpired,
                SubjectAr = "وقت الامتحان خلص",
                SubjectEn = "Your exam time is up",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["وقت امتحان {exam} خلص، واتسجل اللي كنت جاوبته."],
                BodyEn = ["Time ran out on {exam}. Whatever you had answered was submitted."],
                CtaLabelAr = "شوف المحاولة",
                CtaLabelEn = "View your attempt",
                CtaPath = "/student/exams/attempts/{attemptId}",
                RelatedEntityType = "ExamAttempt",
                RequiredValues = ["userName", "exam", "attemptId"]
            },
            new()
            {
                Event = EmailEvents.ExamResultsPublished,
                SubjectAr = "نتيجة الامتحان ظهرت 🎯",
                SubjectEn = "Your exam result is out 🎯",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["نتيجة امتحان {exam} بقت متاحة."],
                BodyEn = ["Results for {exam} are now available."],
                CtaLabelAr = "شوف درجتك",
                CtaLabelEn = "See your score",
                CtaPath = "/student/grades",
                RelatedEntityType = "Exam",
                RequiredValues = ["userName", "exam"]
            },
            new()
            {
                Event = EmailEvents.ExamAwaitingGrading,
                SubjectAr = "في إجابات مستنية تصحيح",
                SubjectEn = "Answers are waiting to be graded",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["في محاولة في امتحان {exam} فيها أسئلة مقالية محتاجة تصحيح منك."],
                BodyEn = ["An attempt on {exam} has essay answers waiting for you."],
                CtaLabelAr = "ابدأ التصحيح",
                CtaLabelEn = "Start grading",
                CtaPath = "/teacher/exams/{examId}/attempts",
                RelatedEntityType = "Exam",
                RequiredValues = ["userName", "exam", "examId"]
            },
            new()
            {
                Event = EmailEvents.ChildExamResult,
                SubjectAr = "نتيجة امتحان {student} 🎯",
                SubjectEn = "{student}'s exam result 🎯",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} جاب {score} من {max} في امتحان {exam}."],
                BodyEn = ["{student} scored {score} out of {max} on {exam}."],
                CtaLabelAr = "شوف النتيجة",
                CtaLabelEn = "See the result",
                CtaPath = "/parent/children/{studentId}/grades",
                RelatedEntityType = "ExamResult",
                RequiredValues = ["userName", "student", "studentId", "exam", "score", "max"]
            },
            new()
            {
                Event = EmailEvents.ChildLowGrade,
                SubjectAr = "نتيجة {student} محتاجة وقفة",
                SubjectEn = "A result worth reviewing for {student}",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} جاب {score} من {max} ({percentage}%) في امتحان {exam}."],
                BodyEn = ["{student} scored {score} out of {max} ({percentage}%) on {exam}."],
                CtaLabelAr = "شوف التفاصيل",
                CtaLabelEn = "See the details",
                CtaPath = "/parent/children/{studentId}/grades",
                RelatedEntityType = "ExamResult",
                RequiredValues = ["userName", "student", "studentId", "exam", "score", "max", "percentage"]
            },

            // -----------------------------------------------------------------
            // Attendance
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.AttendanceAbsent,
                SubjectAr = "اتسجل عليك غياب النهارده",
                SubjectEn = "You were marked absent today",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["اتسجل عليك غياب في {group} النهارده. لو في خطأ، كلّم المدرس."],
                BodyEn = ["You were marked absent in {group} today. If that's wrong, speak to your teacher."],
                CtaLabelAr = "شوف سجل الحضور",
                CtaLabelEn = "View attendance",
                CtaPath = "/student/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["userName", "group"]
            },
            new()
            {
                Event = EmailEvents.AttendanceLate,
                SubjectAr = "اتسجل عليك تأخير النهارده",
                SubjectEn = "You were marked late today",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["وصلت متأخر في {group} النهارده. لو في خطأ، كلّم المدرس."],
                BodyEn = ["You arrived late to {group} today. If that's wrong, speak to your teacher."],
                CtaLabelAr = "شوف سجل الحضور",
                CtaLabelEn = "View attendance",
                CtaPath = "/student/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["userName", "group"]
            },
            new()
            {
                Event = EmailEvents.ChildAbsent,
                SubjectAr = "{student} اتسجل عليه غياب النهارده",
                SubjectEn = "{student} was marked absent today",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} اتسجل عليه غياب في {group} النهارده."],
                BodyEn = ["{student} was marked absent in {group} today."],
                CtaLabelAr = "شوف سجل الحضور",
                CtaLabelEn = "View attendance",
                CtaPath = "/parent/children/{studentId}/attendance",
                RelatedEntityType = "AttendanceRecord",
                RequiredValues = ["userName", "student", "studentId", "group"]
            },

            // -----------------------------------------------------------------
            // Messaging — offline only, and never the message text itself
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.MessageReceived,
                SubjectAr = "عندك رسالة جديدة من {sender}",
                SubjectEn = "New message from {sender}",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{sender} بعتلك رسالة جديدة على Ustaz. افتح المحادثة عشان تقراها وترد."],
                BodyEn = ["{sender} sent you a message on Ustaz. Open the conversation to read and reply."],
                CtaLabelAr = "افتح المحادثة",
                CtaLabelEn = "Open the conversation",
                CtaPath = "/messages/conversations/{conversationId}",
                SkipWhenRecipientOnline = true,
                RelatedEntityType = "Conversation",
                RequiredValues = ["userName", "sender", "conversationId"]
            },

            // -----------------------------------------------------------------
            // Payments
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.PaymentRecorded,
                SubjectAr = "اتسجل عليك مبلغ مستحق",
                SubjectEn = "A payment has been recorded",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "اتسجل مبلغ مستحق قدره {amount} جنيه — {description}.",
                    "آخر ميعاد: {due}"
                ],
                BodyEn =
                [
                    "{amount} EGP is due — {description}.",
                    "Due by: {due}"
                ],
                CtaLabelAr = "شوف المدفوعات",
                CtaLabelEn = "View payments",
                CtaPath = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["userName", "amount", "description", "due"]
            },
            new()
            {
                Event = EmailEvents.PaymentSucceeded,
                SubjectAr = "الدفع تم بنجاح 💳",
                SubjectEn = "Payment received 💳",
                HeadingAr = "شكرًا يا {userName}",
                HeadingEn = "Thank you, {userName}",
                BodyAr = ["استلمنا مبلغ {amount} جنيه. تقدر تشوف الإيصال من حسابك."],
                BodyEn = ["We received {amount} EGP. Your receipt is available in your account."],
                CtaLabelAr = "شوف الإيصال",
                CtaLabelEn = "View the receipt",
                CtaPath = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["userName", "amount"]
            },
            new()
            {
                Event = EmailEvents.PaymentFailed,
                SubjectAr = "عملية الدفع ماكملتش",
                SubjectEn = "Your payment didn't go through",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["الدفع بمبلغ {amount} جنيه مانجحش. جرّب تاني أو استخدم وسيلة دفع تانية."],
                BodyEn = ["The {amount} EGP payment failed. Please try again or use another payment method."],
                CtaLabelAr = "جرّب تاني",
                CtaLabelEn = "Try again",
                CtaPath = "/billing",
                RelatedEntityType = "BillingTransaction",
                RequiredValues = ["userName", "amount"]
            },
            new()
            {
                Event = EmailEvents.PaymentRefunded,
                SubjectAr = "تم استرجاع المبلغ",
                SubjectEn = "Your payment was refunded",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["رجّعنالك مبلغ {amount} جنيه."],
                BodyEn = ["{amount} EGP has been refunded to you."],
                CtaLabelAr = "شوف المدفوعات",
                CtaLabelEn = "View payments",
                CtaPath = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["userName", "amount"]
            },
            new()
            {
                Event = EmailEvents.PaymentDueSoon,
                SubjectAr = "ميعاد الدفع قرب",
                SubjectEn = "A payment is due soon",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["مستحق {amount} جنيه قبل {due}."],
                BodyEn = ["{amount} EGP is due before {due}."],
                CtaLabelAr = "ادفع دلوقتي",
                CtaLabelEn = "Pay now",
                CtaPath = "/student/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["userName", "amount", "due"]
            },
            new()
            {
                Event = EmailEvents.PaymentReceived,
                SubjectAr = "وصلك تحصيل جديد 💳",
                SubjectEn = "You received a payment 💳",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{student} دفع {amount} جنيه."],
                BodyEn = ["{student} paid {amount} EGP."],
                CtaLabelAr = "شوف التحصيلات",
                CtaLabelEn = "View payments",
                CtaPath = "/teacher/payments",
                RelatedEntityType = "EducationalPayment",
                RequiredValues = ["userName", "student", "amount"]
            },

            // -----------------------------------------------------------------
            // Subscriptions
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.SubscriptionActivated,
                SubjectAr = "الاشتراك اتفعّل 🚀",
                SubjectEn = "Your subscription is active 🚀",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["باقة {plan} بقت شغالة على مساحتك على Ustaz."],
                BodyEn = ["The {plan} plan is now active on your Ustaz workspace."],
                CtaLabelAr = "شوف الاشتراك",
                CtaLabelEn = "View subscription",
                CtaPath = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["userName", "plan"]
            },
            new()
            {
                Event = EmailEvents.SubscriptionRenewed,
                SubjectAr = "الاشتراك اتجدد",
                SubjectEn = "Your subscription renewed",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["باقة {plan} اتجددت لحد {date}."],
                BodyEn = ["The {plan} plan renewed until {date}."],
                CtaLabelAr = "شوف الفواتير",
                CtaLabelEn = "View invoices",
                CtaPath = "/billing/invoices",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["userName", "plan", "date"]
            },
            new()
            {
                Event = EmailEvents.SubscriptionExpiring,
                SubjectAr = "الاشتراك قرب يخلص",
                SubjectEn = "Your subscription is expiring",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["باقة {plan} هتخلص يوم {date}. جدّدها عشان الخدمة ماتقفش على طلابك."],
                BodyEn = ["The {plan} plan ends on {date}. Renew it so your students aren't interrupted."],
                CtaLabelAr = "جدّد الاشتراك",
                CtaLabelEn = "Renew now",
                CtaPath = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["userName", "plan", "date"]
            },
            new()
            {
                Event = EmailEvents.SubscriptionExpired,
                SubjectAr = "الاشتراك خلص",
                SubjectEn = "Your subscription has expired",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["باقة {plan} خلصت. جدّد عشان ترجع تستخدم كل المميزات."],
                BodyEn = ["The {plan} plan has expired. Renew to restore full access."],
                CtaLabelAr = "جدّد الاشتراك",
                CtaLabelEn = "Renew now",
                CtaPath = "/billing",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["userName", "plan"]
            },
            new()
            {
                Event = EmailEvents.SubscriptionPaymentFailed,
                SubjectAr = "فاتورة الاشتراك ماتدفعتش",
                SubjectEn = "Subscription payment failed",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["محاولة تحصيل {amount} جنيه للاشتراك مانجحتش. راجع وسيلة الدفع بتاعتك."],
                BodyEn = ["We couldn't collect {amount} EGP for your subscription. Please check your payment method."],
                CtaLabelAr = "راجع وسيلة الدفع",
                CtaLabelEn = "Update payment method",
                CtaPath = "/billing",
                RelatedEntityType = "BillingTransaction",
                RequiredValues = ["userName", "amount"]
            },
            new()
            {
                Event = EmailEvents.UsageLimitApproaching,
                SubjectAr = "قربت توصل لحد الباقة",
                SubjectEn = "You're close to your plan limit",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["استخدمت {used} من {limit} {resource} في باقتك."],
                BodyEn = ["You've used {used} of {limit} {resource} on your plan."],
                CtaLabelAr = "شوف الباقات",
                CtaLabelEn = "View plans",
                CtaPath = "/billing/plans",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["userName", "used", "limit", "resource"]
            },
            new()
            {
                Event = EmailEvents.UsageLimitReached,
                SubjectAr = "وصلت لحد الباقة",
                SubjectEn = "You've reached your plan limit",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["خلصت الـ{limit} {resource} المتاحين في باقتك. رقّي الباقة عشان تكمّل."],
                BodyEn = ["You've used all {limit} {resource} in your plan. Upgrade to add more."],
                CtaLabelAr = "رقّي الباقة",
                CtaLabelEn = "Upgrade",
                CtaPath = "/billing/plans",
                RelatedEntityType = "TenantSubscription",
                RequiredValues = ["userName", "limit", "resource"]
            },

            // -----------------------------------------------------------------
            // Community, team and certificates
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.TeacherVerified,
                SubjectAr = "حسابك اتوثّق ✅",
                SubjectEn = "Your account is verified ✅",
                HeadingAr = "مبروك يا {userName}",
                HeadingEn = "Congratulations, {userName}",
                BodyAr = ["حسابك بقى موثّق، وتقدر دلوقتي تنشر المجموعات والكورسات بتاعتك."],
                BodyEn = ["Your account is verified — you can now publish your groups and courses."],
                CtaLabelAr = "ابدأ النشر",
                CtaLabelEn = "Start publishing",
                CtaPath = "/teacher/profile",
                RequiredValues = ["userName"]
            },
            new()
            {
                Event = EmailEvents.AnnouncementPublished,
                SubjectAr = "إعلان جديد 📢",
                SubjectEn = "New announcement 📢",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr = ["{title}"],
                BodyEn = ["{title}"],
                CtaLabelAr = "اقرا الإعلان",
                CtaLabelEn = "Read the announcement",
                CtaPath = "/announcements/{announcementId}",
                RelatedEntityType = "Announcement",
                RequiredValues = ["userName", "title", "announcementId"]
            },
            new()
            {
                Event = EmailEvents.TeamMemberInvited,
                SubjectAr = "دعوة للانضمام لفريق {workspace} على Ustaz",
                SubjectEn = "You are invited to join {workspace} on Ustaz",
                HeadingAr = "اتدعيت تنضم لـ{workspace}",
                HeadingEn = "You have been invited to {workspace}",
                BodyAr =
                [
                    "اتدعيت تنضم لفريق {workspace} على Ustaz بصلاحية {role}.",
                    "اقبل الدعوة من حسابك، أو اعملك حساب جديد بنفس الإيميل ده."
                ],
                BodyEn =
                [
                    "You have been invited to join {workspace} on Ustaz as {role}.",
                    "Accept the invitation from your account, or create one with this email address."
                ],
                CtaLabelAr = "اقبل الدعوة",
                CtaLabelEn = "Accept the invitation",
                CtaPath = "/team/invitations",
                FootnoteAr = "الدعوة صالحة لفترة محدودة. لو مش متوقع الرسالة دي، تجاهلها.",
                FootnoteEn = "This invitation is valid for a limited time. If you weren't expecting it, ignore this message.",
                IsSecurity = true,
                RelatedEntityType = "TenantInvitation",
                RequiredValues = ["workspace", "role"]
            },
            new()
            {
                Event = EmailEvents.CertificateIssued,
                SubjectAr = "شهادة الكورس بتاعتك جاهزة 🏆",
                SubjectEn = "Your course certificate is ready 🏆",
                HeadingAr = "مبروك يا {studentName}",
                HeadingEn = "Congratulations, {studentName}",
                BodyAr =
                [
                    "خلصت كورس {course} بنجاح، والشهادة بقت جاهزة.",
                    "تاريخ الإتمام: {completedAt}",
                    "رقم الشهادة: {number}",
                    "أي حد يقدر يتأكد من الشهادة من الرابط ده: {verificationUrl}"
                ],
                BodyEn =
                [
                    "You've completed {course} and your certificate is ready.",
                    "Completed on: {completedAt}",
                    "Certificate number: {number}",
                    "Anyone can verify it here: {verificationUrl}"
                ],
                CtaLabelAr = "حمّل الشهادة",
                CtaLabelEn = "View your certificate",
                CtaPath = "/certificates/{number}",
                RelatedEntityType = "Certificate",
                RequiredValues = ["studentName", "course", "completedAt", "number", "verificationUrl"]
            },

            // -----------------------------------------------------------------
            // Early warning
            // -----------------------------------------------------------------
            new()
            {
                Event = EmailEvents.ChildFallingBehind,
                SubjectAr = "{student} محتاج متابعة",
                SubjectEn = "{student} needs a hand",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "لاحظنا إن {student} متأخر شوية الفترة اللي فاتت.",
                    "الأسباب: {reasons}"
                ],
                BodyEn =
                [
                    "We noticed {student} has been falling behind recently.",
                    "Why: {reasons}"
                ],
                CtaLabelAr = "شوف التفاصيل",
                CtaLabelEn = "See the details",
                CtaPath = "/parent/children/{studentId}",
                RelatedEntityType = "StudentProfile",
                RequiredValues = ["userName", "student", "studentId", "reasons"]
            },
            new()
            {
                Event = EmailEvents.StudentAtRisk,
                SubjectAr = "طالب محتاج متابعة",
                SubjectEn = "A student needs attention",
                HeadingAr = "أهلًا {userName}",
                HeadingEn = "Hi {userName}",
                BodyAr =
                [
                    "{student} مستواه نزل الفترة اللي فاتت.",
                    "الأسباب: {reasons}"
                ],
                BodyEn =
                [
                    "{student} has been slipping recently.",
                    "Why: {reasons}"
                ],
                CtaLabelAr = "شوف التحليل",
                CtaLabelEn = "Open analytics",
                CtaPath = "/teacher/analytics/students",
                RelatedEntityType = "StudentProfile",
                RequiredValues = ["userName", "student", "reasons"]
            }
        };

        return templates.ToDictionary(template => template.Event, StringComparer.Ordinal);
    }
}
