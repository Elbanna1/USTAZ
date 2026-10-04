using System.Text;

namespace Ustaz.Application.Common.Emails;

/// <summary>Which language an email is rendered in. Arabic is the platform's default.</summary>
public enum EmailLanguage
{
    /// <summary>Egyptian Arabic, right-to-left.</summary>
    Arabic = 1,

    English = 2
}

/// <summary>
/// The copy and routing for one transactional email.
///
/// Arabic is written the way people actually speak in Egypt — "الباسورد بتاعك اتغير" rather
/// than a formal translation — except where a security warning needs to be unambiguous.
/// English is kept alongside for accounts that read the platform in English.
/// </summary>
public sealed record EmailTemplate
{
    /// <summary>Stable key, from <see cref="EmailEvents"/>.</summary>
    public required string Event { get; init; }

    public required string SubjectAr { get; init; }

    public required string SubjectEn { get; init; }

    /// <summary>Large heading at the top of the message body.</summary>
    public required string HeadingAr { get; init; }

    public required string HeadingEn { get; init; }

    /// <summary>Body paragraphs. Each entry becomes its own paragraph.</summary>
    public required IReadOnlyList<string> BodyAr { get; init; }

    public required IReadOnlyList<string> BodyEn { get; init; }

    /// <summary>Call-to-action label. Omitted together with <see cref="CtaPath"/>.</summary>
    public string? CtaLabelAr { get; init; }

    public string? CtaLabelEn { get; init; }

    /// <summary>
    /// Path appended to the configured client base URL, with the same <c>{placeholder}</c>
    /// syntax as the copy. Absolute URLs are passed through untouched.
    /// </summary>
    public string? CtaPath { get; init; }

    /// <summary>Small print under the CTA, e.g. how long a link stays valid.</summary>
    public string? FootnoteAr { get; init; }

    public string? FootnoteEn { get; init; }

    /// <summary>
    /// A security email is delivered regardless of notification preferences, and is never
    /// retried from the outbox because its content is time-sensitive and may embed a token.
    /// </summary>
    public bool IsSecurity { get; init; }

    /// <summary>
    /// True when this email is only worth sending to someone who is not currently connected.
    /// A new-message notice belongs in the inbox of someone who has the app closed, not of
    /// someone already reading the thread.
    /// </summary>
    public bool SkipWhenRecipientOnline { get; init; }

    /// <summary>Placeholders the copy needs. A missing one is a programming error.</summary>
    public IReadOnlyList<string> RequiredValues { get; init; } = [];

    /// <summary>Name of the entity this email is about, e.g. "Exam". Used for tracking.</summary>
    public string? RelatedEntityType { get; init; }

    public string Subject(EmailLanguage language) => language == EmailLanguage.English ? SubjectEn : SubjectAr;

    public string Heading(EmailLanguage language) => language == EmailLanguage.English ? HeadingEn : HeadingAr;

    public IReadOnlyList<string> Body(EmailLanguage language) =>
        language == EmailLanguage.English ? BodyEn : BodyAr;

    public string? CtaLabel(EmailLanguage language) =>
        language == EmailLanguage.English ? CtaLabelEn : CtaLabelAr;

    public string? Footnote(EmailLanguage language) =>
        language == EmailLanguage.English ? FootnoteEn : FootnoteAr;

    /// <summary>Replaces every <c>{placeholder}</c> with its value.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0 || !template.Contains('{'))
        {
            return template;
        }

        var builder = new StringBuilder(template);

        foreach (var (key, value) in values)
        {
            builder.Replace("{" + key + "}", value);
        }

        return builder.ToString();
    }
}
