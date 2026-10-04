namespace Ustaz.Application.Common.Localization;

/// <summary>
/// The field-level wording the shared validation rules use.
///
/// Every rule in <see cref="Validation.CommonValidationRules"/> and
/// <see cref="Validation.FileUploadRules"/> resolves its message through here, so a request
/// made in Arabic gets Arabic field errors from the one place they are all defined. The
/// messages are resolved lazily, per failure, against the ambient request culture — a
/// validator instance is reused across requests and must not capture a language.
///
/// Rules that are <em>not</em> overridden with an explicit message fall through to
/// FluentValidation's own translations, which ship Arabic; that is why a validator written as
/// a bare <c>NotEmpty()</c> still answers in Arabic without appearing here.
/// </summary>
public static class ValidationMessages
{
    private static UstazLanguage Language => UstazLanguages.FromCurrentCulture();

    private static string Pick(string arabic, string english) =>
        Language == UstazLanguage.English ? english : arabic;

    // Email -----------------------------------------------------------------
    public static string EmailRequired() => Pick("الإيميل مطلوب.", "Email is required.");

    public static string EmailTooLong(int max) => Pick(
        "الإيميل مينفعش يزيد عن " + max + " حرف.",
        "Email must not exceed " + max + " characters.");

    public static string EmailInvalid() => Pick(
        "اكتب إيميل صحيح.",
        "Email must be a valid email address.");

    // Password --------------------------------------------------------------
    public static string PasswordRequired() => Pick("الباسورد مطلوب.", "Password is required.");

    public static string PasswordTooShort(int min) => Pick(
        "الباسورد لازم يكون " + min + " حروف على الأقل.",
        "Password must be at least " + min + " characters long.");

    public static string PasswordTooLong(int max) => Pick(
        "الباسورد مينفعش يزيد عن " + max + " حرف.",
        "Password must not exceed " + max + " characters.");

    public static string PasswordNeedsUppercase() => Pick(
        "الباسورد لازم يحتوي على حرف كبير واحد على الأقل.",
        "Password must contain at least one uppercase letter.");

    public static string PasswordNeedsLowercase() => Pick(
        "الباسورد لازم يحتوي على حرف صغير واحد على الأقل.",
        "Password must contain at least one lowercase letter.");

    public static string PasswordNeedsDigit() => Pick(
        "الباسورد لازم يحتوي على رقم واحد على الأقل.",
        "Password must contain at least one digit.");

    // Phone -----------------------------------------------------------------
    public static string PhoneRequired() => Pick("رقم الموبايل مطلوب.", "Phone number is required.");

    public static string PhoneInvalid() => Pick(
        "اكتب رقم موبايل مصري صحيح، مثال: 01012345678.",
        "Phone number must be a valid Egyptian mobile number, e.g. 01012345678.");

    // URLs ------------------------------------------------------------------
    public static string UrlRequired() => Pick("اللينك مطلوب.", "A URL is required.");

    public static string UrlInvalid() => Pick(
        "اللينك لازم يبدأ بـ http أو https.",
        "The value must be a valid http or https URL.");

    // Names and text --------------------------------------------------------
    public static string FieldRequired() => Pick("الحقل ده مطلوب.", "This field is required.");

    public static string FieldTooShort(int min) => Pick(
        "الحقل ده لازم يكون " + min + " حروف على الأقل.",
        "This field must be at least " + min + " characters long.");

    public static string FieldTooLong(int max) => Pick(
        "الحقل ده مينفعش يزيد عن " + max + " حرف.",
        "This field must not exceed " + max + " characters.");

    // Money and ratings -----------------------------------------------------
    public static string PriceNegative() => Pick(
        "السعر مينفعش يكون بالسالب.",
        "The price cannot be negative.");

    public static string PriceTooHigh() => Pick(
        "السعر ده غير منطقي.",
        "The price is unrealistically high.");

    public static string RatingOutOfRange(int min, int max) => Pick(
        "التقييم لازم يكون من " + min + " لـ " + max + ".",
        "Rating must be between " + min + " and " + max + ".");

    // Dates and ranges ------------------------------------------------------
    public static string DateRangeInvalid() => Pick(
        "تاريخ النهاية لازم يكون بعد تاريخ البداية.",
        "The end date must be after the start date.");

    public static string DateRangeTooWide(int maxDays) => Pick(
        "الفترة مينفعش تزيد عن " + maxDays + " يوم.",
        "The range must not exceed " + maxDays + " days.");

    // Uploads ---------------------------------------------------------------
    public static string FileRequired() => Pick("لازم ترفع ملف.", "A file is required.");

    public static string FileTooLarge(int maxMegabytes) => Pick(
        "حجم الملف مينفعش يزيد عن " + maxMegabytes + " ميجا.",
        "The file must not exceed " + maxMegabytes + " MB.");

    public static string FileTypeNotAllowed() => Pick(
        "نوع الملف ده غير مسموح.",
        "That file type is not allowed.");
}
