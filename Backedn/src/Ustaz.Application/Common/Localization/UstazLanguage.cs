using System.Globalization;

namespace Ustaz.Application.Common.Localization;

/// <summary>The languages the USTAZ API answers in. Arabic is the product's default.</summary>
public enum UstazLanguage
{
    Arabic = 0,
    English = 1
}

/// <summary>
/// Resolves a language from whatever the request offered.
///
/// Arabic is the default on purpose: USTAZ is used by teachers, students and parents in Egypt,
/// and a caller that expresses no preference is far more likely to want Arabic than English.
/// Only an explicit <c>en</c> switches away from it, which matches how
/// <see cref="Emails.TransactionalEmailLanguage"/> already decides an email's language.
/// </summary>
public static class UstazLanguages
{
    public const string ArabicTag = "ar-EG";
    public const string EnglishTag = "en";

    /// <summary>The cultures the request-localization middleware accepts.</summary>
    public static readonly string[] SupportedTags = ["ar-EG", "ar", "en", "en-US", "en-GB"];

    public static UstazLanguage From(string? languageTag) =>
        languageTag is not null && languageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? UstazLanguage.English
            : UstazLanguage.Arabic;

    /// <summary>
    /// The language the ambient culture implies. This is what the exception handler and the
    /// validation-response factory read, because ASP.NET Core's request-localization
    /// middleware has already set it from the request by the time either runs.
    /// </summary>
    public static UstazLanguage FromCurrentCulture() =>
        From(CultureInfo.CurrentUICulture.Name);

    /// <summary>The canonical tag, for echoing back in a <c>Content-Language</c> header.</summary>
    public static string TagFor(UstazLanguage language) =>
        language == UstazLanguage.English ? EnglishTag : ArabicTag;
}
