namespace Ustaz.Application.Common.Emails;

/// <summary>
/// Maps a stored language tag onto the rendering language. Arabic is the default, so anything
/// that is not explicitly English reads as Egyptian Arabic.
/// </summary>
public static class TransactionalEmailLanguage
{
    public const string Arabic = "ar-EG";
    public const string English = "en";

    public static EmailLanguage For(string? languageTag) =>
        languageTag is not null && languageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? EmailLanguage.English
            : EmailLanguage.Arabic;

    /// <summary>The supported tags, for validating a user's choice.</summary>
    public static bool IsSupported(string? languageTag) =>
        languageTag is not null
        && (languageTag.Equals(Arabic, StringComparison.OrdinalIgnoreCase)
            || languageTag.Equals(English, StringComparison.OrdinalIgnoreCase));
}
