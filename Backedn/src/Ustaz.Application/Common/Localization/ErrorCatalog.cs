namespace Ustaz.Application.Common.Localization;

/// <summary>
/// The Arabic wording for every error code the API can return, in one place.
///
/// How this composes with the rest of the error pipeline:
///
/// * The <em>code</em> is the contract and is never translated. The domain already attaches a
///   stable code to each <c>BusinessRuleException</c> it throws, which is what makes a central
///   catalogue possible at all — none of the ninety-odd throw sites had to be touched.
/// * The <em>English</em> text stays where it is: the message authored at the throw site. Those
///   messages are written for a person to read and several interpolate a real value ("the
///   score must be between 0 and 40"), so reproducing them here would only let the two drift.
/// * The <em>Arabic</em> text lives here. Where the English interpolates a number, the Arabic
///   is phrased to carry the same meaning without it, because a catalogue entry cannot see the
///   runtime value. Those entries are marked below.
///
/// The Arabic is Egyptian and plain, the same register as
/// <see cref="Notifications.NotificationCatalog"/>: a parent reading that a deadline has
/// passed should not have to parse a sentence written for a lawyer.
/// </summary>
public static class ErrorCatalog
{
    /// <summary>Arabic for a code, or <c>null</c> when the code has no entry yet.</summary>
    public static string? ArabicFor(string? code) =>
        code is not null && Arabic.TryGetValue(code, out var message) ? message : null;

    /// <summary>
    /// The Arabic fallback for an HTTP status, used when a failure carries no code we know.
    /// Deliberately vague: a message shown to a user who tried to reach something that is not
    /// theirs must not describe what is on the other side.
    /// </summary>
    public static string ArabicForStatus(int status) => status switch
    {
        400 => "البيانات المرسلة غير صحيحة. راجع الحقول وجرّب تاني.",
        401 => "محتاج تسجّل دخول الأول.",
        403 => "مش مسموح لك بالعملية دي.",
        404 => "مش لاقيين اللي بتدوّر عليه.",
        409 => "فيه تعارض مع بيانات موجودة بالفعل.",
        413 => "حجم الطلب أكبر من المسموح.",
        422 => "مش قادرين ننفّذ العملية دي.",
        429 => "بعتت طلبات كتير في وقت قصير. استنى شوية وجرّب تاني.",
        499 => "الطلب اتلغى.",
        _ => "حصلت مشكلة غير متوقعة. جرّب تاني بعد شوية."
    };

    /// <summary>
    /// The English fallback for an HTTP status. Used only when nothing authored a message —
    /// the throw site's own text is always preferred.
    /// </summary>
    public static string EnglishForStatus(int status) => status switch
    {
        400 => "The request is not valid. Review the fields and try again.",
        401 => "Please sign in first.",
        403 => "You are not allowed to perform this action.",
        404 => "We could not find what you are looking for.",
        409 => "This conflicts with data that already exists.",
        413 => "The request is larger than allowed.",
        422 => "This operation cannot be completed.",
        429 => "You have sent too many requests. Please slow down and try again shortly.",
        499 => "The request was cancelled.",
        _ => "An unexpected error occurred. Please try again later."
    };

    /// <summary>Every code that has an Arabic translation. Used by the tests to spot gaps.</summary>
    public static IReadOnlyCollection<string> TranslatedCodes => Arabic.Keys;

    private static readonly Dictionary<string, string> Arabic = new(StringComparer.Ordinal)
    {
        // -------------------------------------------------------------------
        // Generic shapes
        // -------------------------------------------------------------------
        [ErrorCodes.ValidationFailed] = "فيه حقول ناقصة أو غير صحيحة. راجع البيانات وجرّب تاني.",
        [ErrorCodes.ResourceNotFound] = "مش لاقيين اللي بتدوّر عليه.",
        [ErrorCodes.Conflict] = "فيه تعارض مع بيانات موجودة بالفعل.",
        [ErrorCodes.DuplicateValue] = "البيانات دي مستخدمة بالفعل. جرّب قيمة تانية.",
        [ErrorCodes.Forbidden] = "مش مسموح لك بالعملية دي.",
        [ErrorCodes.Unauthorized] = "محتاج تسجّل دخول الأول.",
        [ErrorCodes.TokenExpired] = "الجلسة انتهت. سجّل دخول تاني.",

        // Says nothing about whether the account exists, on purpose.
        [ErrorCodes.InvalidCredentials] = "الإيميل أو الباسورد غلط.",
        [ErrorCodes.AccountLocked] = "الحساب متوقف مؤقتًا بعد محاولات دخول كتير. جرّب تاني بعد شوية.",
        [ErrorCodes.AccountInactive] = "الحساب ده غير مفعّل. كلّم الدعم بتاع أستاذ.",
        [ErrorCodes.RefreshTokenInvalid] = "الجلسة مش صالحة. سجّل دخول تاني.",

        [ErrorCodes.ConcurrencyConflict] = "البيانات دي اتغيّرت من حد تاني. حدّث الصفحة وجرّب تاني.",
        [ErrorCodes.BusinessRuleViolation] = "مش قادرين ننفّذ العملية دي.",
        [ErrorCodes.RequestCancelled] = "الطلب اتلغى.",
        [ErrorCodes.RateLimited] = "بعتت طلبات كتير في وقت قصير. استنى شوية وجرّب تاني.",
        [ErrorCodes.PayloadTooLarge] = "حجم الطلب أكبر من المسموح.",
        [ErrorCodes.InternalError] = "حصلت مشكلة غير متوقعة. جرّب تاني بعد شوية.",

        // -------------------------------------------------------------------
        // Accounts, sessions and invitations
        // -------------------------------------------------------------------
        ["EMAIL_VERIFICATION_FAILED"] = "لينك التأكيد غير صحيح أو انتهت صلاحيته. اطلب لينك جديد.",
        ["PASSWORD_RESET_FAILED"] = "لينك إعادة تعيين الباسورد غير صحيح أو انتهت صلاحيته. اطلب لينك جديد.",
        ["INVALID_TOKEN"] = "الكود المرسل غير صحيح.",
        ["INVITATION_EXPIRED"] = "الدعوة دي انتهت صلاحيتها. اطلب دعوة جديدة.",
        ["INVITATION_NOT_PENDING"] = "الدعوة دي مستخدمة بالفعل.",

        // -------------------------------------------------------------------
        // Workspace, team and permissions
        // -------------------------------------------------------------------
        ["TENANT_SUSPENDED"] = "المساحة دي موقوفة حاليًا. كلّم الدعم بتاع أستاذ.",
        ["TENANT_ARCHIVED"] = "المساحة دي مش شغالة.",
        ["TENANT_HAS_NO_TEACHER"] = "المساحة دي مفيهاش بروفايل مدرّس نسجّل عليه السجل.",
        ["OWNER_IMMUTABLE"] = "مالك المساحة مينفعش يتغيّر من هنا.",
        ["OWNER_ROLE_NOT_ASSIGNABLE"] = "الملكية مش بتتمنح من هنا. استخدم خطوة نقل الملكية المخصصة.",
        ["UNKNOWN_PERMISSION"] = "الصلاحية المطلوبة غير معروفة.",
        ["PERMISSION_NOT_DELEGATABLE"] = "الصلاحية دي مينفعش تتمنح لمساعد.",
        ["DOMAIN_NOT_VERIFIED"] = "لازم تتأكد من الدومين الأول قبل ما يشتغل على الموقع العام.",

        // -------------------------------------------------------------------
        // Subscription and plan limits
        // -------------------------------------------------------------------
        // Arabic without the number: the catalogue cannot see the plan's limit.
        ["PLAN_LIMIT_REACHED"] = "وصلت للحد المسموح في باقتك الحالية. اعمل ترقية للباقة.",
        ["FEATURE_NOT_AVAILABLE"] = "الميزة دي مش موجودة في باقتك الحالية. اعمل ترقية لتفعيلها.",
        ["SUBSCRIPTION_REQUIRED"] = "المساحة دي مش عندها اشتراك شغال في أستاذ.",
        ["SUBSCRIPTION_INACTIVE"] = "اشتراكك في أستاذ مش شغال. جدّده تكمل استخدام الميزة دي.",
        ["SUBSCRIPTION_EXPIRED"] = "الاشتراك ده انتهى. ابدأ اشتراك جديد بدل تغيير الباقة.",
        ["SUBSCRIPTION_ALREADY_CANCELED"] = "الاشتراك ده ملغي بالفعل.",
        ["SUBSCRIPTION_NOT_CANCELED"] = "الاشتراك اللي ملغي أو منتهي بس هو اللي ينفع يرجع يشتغل.",

        // -------------------------------------------------------------------
        // Teacher verification
        // -------------------------------------------------------------------
        ["TEACHER_NOT_VERIFIED"] = "المدرّسين الموثّقين بس هم اللي ينفع ينشروا مجموعات أو كورسات. كمّل التوثيق الأول.",
        ["TEACHER_ALREADY_VERIFIED"] = "حسابك موثّق بالفعل.",
        ["VERIFICATION_ALREADY_SUBMITTED"] = "طلبك عند فريق المراجعة بالفعل. هنبلّغك أول ما يتقرر.",
        ["VERIFICATION_NOT_PENDING"] = "الطلب اللي في الانتظار بس هو اللي ينفع يتحوّل للمراجعة.",
        ["VERIFICATION_PROFILE_INCOMPLETE"] = "كمّل بروفايلك قبل ما تطلب التوثيق: عنوان مختصر، نبذة، رقم واتساب، ومادة ومرحلة واحدة على الأقل.",
        ["FEATURE_REQUIRES_VERIFICATION"] = "المدرّسين الموثّقين بس هم اللي ينفع يتميّزوا.",

        // -------------------------------------------------------------------
        // Groups and schedules
        // -------------------------------------------------------------------
        ["GROUP_INACTIVE"] = "المجموعة دي مش شغالة.",
        ["GROUP_NOT_PUBLISHED"] = "المجموعة دي لسه مش مفتوحة للتسجيل.",
        ["GROUP_FULL"] = "المجموعة دي كمّلت العدد.",
        ["GROUP_INVALID_CAPACITY"] = "حدّد عدد أماكن أكبر من صفر قبل النشر.",
        ["GROUP_WITHOUT_SCHEDULE"] = "ضيف ميعاد أسبوعي شغال واحد على الأقل قبل ما تنشر المجموعة.",
        ["CAPACITY_BELOW_ENROLLED"] = "عدد الأماكن مينفعش يقل عن عدد الطلبة المسجّلين بالفعل.",
        ["LOCATION_REQUIRED"] = "المجموعات الحضورية والمدمجة لازم يكون لها مكان.",
        ["SCHEDULE_INVALID_TIME"] = "وقت نهاية الحصة لازم يكون بعد وقت بدايتها.",
        ["SCHEDULE_OVERLAP"] = "فيه ميعادين في المجموعة دي متعارضين مع بعض.",
        ["LAST_ACTIVE_SESSION"] = "المجموعة المنشورة لازم يفضل فيها ميعاد أسبوعي شغال واحد على الأقل. أوقف نشر المجموعة الأول.",
        ["SUBJECT_NOT_TAUGHT"] = "مينفعش تعمل محتوى لمادة مش موجودة في بروفايلك. ضيف المادة الأول.",
        ["GRADE_NOT_TAUGHT"] = "مينفعش تعمل محتوى لمرحلة مش موجودة في بروفايلك. ضيف المرحلة الأول.",

        // -------------------------------------------------------------------
        // Courses and lessons
        // -------------------------------------------------------------------
        ["COURSE_NOT_PUBLISHED"] = "الكورس ده لسه مش متاح للتسجيل.",
        ["COURSE_WITHOUT_CONTENT"] = "ضيف درس منشور واحد على الأقل فيه فيديو قبل ما تنشر الكورس.",
        ["COURSE_WITHOUT_DESCRIPTION"] = "ضيف وصف للكورس قبل النشر.",
        ["COURSE_WOULD_HAVE_NO_CONTENT"] = "لو مسحت الجزء ده الكورس المنشور هيبقى من غير محتوى. أوقف نشره الأول.",
        ["LESSON_NOT_PUBLISHED"] = "الدرس ده لسه مش منشور.",
        ["LESSON_WITHOUT_VIDEO"] = "الدرس المنشور لازم يكون له لينك فيديو.",

        // -------------------------------------------------------------------
        // Enrollment
        // -------------------------------------------------------------------
        ["ENROLLMENT_CANCELLED"] = "التسجيل الملغي مينفعش يرجع يشتغل.",
        ["ENROLLMENT_ALREADY_CANCELLED"] = "التسجيل ده ملغي بالفعل.",
        ["ENROLLMENT_COMPLETED"] = "التسجيل المكتمل مينفعش يتلغي.",

        // -------------------------------------------------------------------
        // Homework
        // -------------------------------------------------------------------
        ["HOMEWORK_NOT_PUBLISHED"] = "الواجب ده لسه مش منشور.",
        ["HOMEWORK_CLOSED"] = "الواجب ده اتقفل ومش بيقبل تسليم تاني.",
        ["HOMEWORK_ALREADY_CLOSED"] = "الواجب ده مقفول بالفعل.",
        ["HOMEWORK_NOT_CLOSED"] = "الواجب ده مش مقفول.",
        ["HOMEWORK_DEADLINE_PASSED"] = "الموعد النهائي فات والتسليم المتأخر مش مقبول.",
        ["HOMEWORK_INVALID_DEADLINE"] = "الموعد النهائي لازم يكون بعد تاريخ التكليف.",
        ["HOMEWORK_EMPTY_SUBMISSION"] = "اكتب إجابة أو ارفع ملف قبل التسليم.",
        ["HOMEWORK_ALREADY_GRADED"] = "التسليم ده اتصحّح بالفعل ومش ممكن يتغيّر.",
        ["HOMEWORK_NOT_SUBMITTED"] = "مفيش حاجة تترجّع: الطالب ده مسلّمش أي حاجة.",
        ["HOMEWORK_ALREADY_RETURNED"] = "التسليم ده اترجّع بالفعل للتعديل.",
        ["HOMEWORK_RETURN_NEEDS_REASON"] = "قول للطالب يعدّل إيه قبل ما ترجّع له الواجب.",
        ["HOMEWORK_RESUBMISSION_NOT_ALLOWED"] = "سلّمت الواجب ده بالفعل ومش ممكن تغيّره.",
        // Arabic without the number: the catalogue cannot see the allowed count.
        ["HOMEWORK_SUBMISSION_LIMIT_REACHED"] = "استهلكت كل محاولات التسليم المسموح بيها للواجب ده.",
        ["HOMEWORK_SCORE_OUT_OF_RANGE"] = "الدرجة لازم تكون في النطاق المسموح للواجب ده.",

        // -------------------------------------------------------------------
        // Exams
        // -------------------------------------------------------------------
        ["EXAM_NOT_ONLINE"] = "الامتحان الأونلاين بس هو اللي له ورقة أسئلة. حوّل الامتحان لأونلاين الأول.",
        ["EXAM_NOT_OPEN"] = "الامتحان ده لسه مفتحش.",
        ["EXAM_NOT_PUBLISHED"] = "انشر الامتحان قبل ما تعلن النتايج.",
        ["EXAM_HAS_NO_QUESTIONS"] = "ضيف سؤال واحد على الأقل قبل ما تنشر امتحان أونلاين.",
        ["EXAM_INVALID_SCORE"] = "الدرجة النهائية لازم تكون أكبر من صفر.",
        ["EXAM_INVALID_PASSING_SCORE"] = "درجة النجاح مينفعش تكون أعلى من الدرجة النهائية.",
        ["EXAM_SCORE_OUT_OF_RANGE"] = "الدرجة لازم تكون في النطاق المسموح للامتحان ده.",
        ["ATTEMPT_ALREADY_SUBMITTED"] = "المحاولة دي اتسلّمت بالفعل.",
        ["ATTEMPT_NOT_SUBMITTED"] = "المحاولة دي لسه ما اتسلّمتش.",
        ["ANSWER_OPTION_MISMATCH"] = "الاختيار اللي اخترته مش من اختيارات السؤال ده.",
        ["ANSWER_SCORE_OUT_OF_RANGE"] = "درجة الإجابة مينفعش تزيد عن درجة السؤال.",
        ["QUESTION_NEEDS_OPTIONS"] = "سؤال الاختيار من متعدد محتاج اختيارين على الأقل.",
        ["QUESTION_NEEDS_ONE_CORRECT_OPTION"] = "سؤال الاختيار من متعدد محتاج اختيار صح واحد بالظبط.",
        ["QUESTION_NEEDS_BOOLEAN_ANSWER"] = "سؤال صح وغلط محتاج تحدّد الإجابة الصحيحة صح ولا غلط.",
        ["QUESTION_NEEDS_MODEL_ANSWER"] = "سؤال الإجابة القصيرة محتاج إجابة نموذجية نصحّح عليها.",

        // -------------------------------------------------------------------
        // Attendance
        // -------------------------------------------------------------------
        ["ATTENDANCE_SESSION_CLOSED"] = "جلسة الحضور دي مقفولة. افتحها تاني قبل ما تعدّل السجلات.",
        ["STUDENT_NOT_IN_GROUP"] = "واحد من الطلبة مش مسجّل في المجموعة دي.",

        // -------------------------------------------------------------------
        // Certificates and reviews
        // -------------------------------------------------------------------
        ["CERTIFICATE_ALREADY_REVOKED"] = "الشهادة دي ملغية بالفعل.",
        ["REVIEW_INVALID_RATING"] = "التقييم لازم يكون من ١ لـ ٥.",
        ["REVIEW_WITHOUT_ENROLLMENT"] = "تقدر تقيّم المدرّس اللي سجّلت في مجموعة أو كورس له بس.",

        // -------------------------------------------------------------------
        // Payments and discounts
        // -------------------------------------------------------------------
        ["PAYMENT_NOT_PAID"] = "الدفعة المحصّلة بس هي اللي ينفع يترجّع فلوسها.",
        ["PAYMENT_REFUNDED"] = "الدفعة اللي اترجّعت فلوسها مينفعش تتسجّل مدفوعة تاني.",
        ["DISCOUNT_NOT_REDEEMABLE"] = "كود الخصم ده مش صالح للاستخدام.",
        ["INVALID_DISCOUNT_VALUE"] = "الخصم بالنسبة المئوية مينفعش يزيد عن ١٠٠٪.",
        ["USAGE_LIMIT_BELOW_USED"] = "حد الاستخدام مينفعش يقل عن عدد المرات اللي اتستخدم فيها الكود بالفعل.",
        ["INVALID_PRICING"] = "السعر الأصلي لازم يكون أكبر من أو يساوي السعر الحالي.",
        ["INVALID_DATE_RANGE"] = "تاريخ النهاية لازم يكون بعد تاريخ البداية.",

        // -------------------------------------------------------------------
        // Uploads and files
        // -------------------------------------------------------------------
        ["FILE_REQUIRED"] = "لازم ترفع ملف.",
        ["FILE_EMPTY"] = "الملف المرفوع فاضي.",
        ["FILE_NAME_REQUIRED"] = "الملف المرفوع لازم يكون له اسم.",
        ["FILE_EXTENSION_REQUIRED"] = "الملف المرفوع لازم يكون له امتداد صحيح.",
        ["FILE_EXTENSION_NOT_ALLOWED"] = "نوع الملف ده غير مسموح.",
        ["FILE_TYPE_NOT_ALLOWED"] = "نوع الملف ده غير مقبول.",
        ["FILE_CONTENT_TYPE_NOT_ALLOWED"] = "نوع محتوى الملف ده غير مسموح.",
        // Arabic without the number: the catalogue cannot see the configured ceiling.
        ["FILE_TOO_LARGE"] = "حجم الملف أكبر من الحد المسموح به.",
        ["FILE_KEY_INVALID"] = "مرجع الملف ده غير صحيح.",
        ["FILE_SIGNATURE_MISMATCH"] = "محتوى الملف مش متطابق مع امتداده. ارفع ملف صحيح.",
        ["MEDIA_ID_INVALID"] = "مرجع الوسائط ده غير صحيح.",
        ["MEDIA_UPLOAD_FAILED"] = "مش قادرين نرفع الملف ده. جرّب تاني.",

        // -------------------------------------------------------------------
        // Links
        // -------------------------------------------------------------------
        ["INVALID_URL"] = "اللينك ده غير صحيح.",
        ["INVALID_WHATSAPP_LINK"] = "لينك جروب الواتساب لازم يكون بالشكل https://chat.whatsapp.com/<invite-code>.",
        ["SOCIAL_LINK_PLATFORM_MISMATCH"] = "اللينك ده مش بيتبع المنصة المحددة.",

        // -------------------------------------------------------------------
        // Platform administration
        // -------------------------------------------------------------------
        ["LAST_PLATFORM_ADMIN"] = "مينفعش توقف أو تمسح آخر أدمن للمنصة.",
        ["PLATFORM_ADMIN_SELF_DEMOTION"] = "مينفعش تشيل صلاحيات الأدمن من حسابك إنت.",
        ["SCHOOL_ALREADY_ACTIVE"] = "المدرسة دي شغالة بالفعل.",
        ["SCHOOL_ALREADY_SUSPENDED"] = "المدرسة دي موقوفة بالفعل."
    };
}
