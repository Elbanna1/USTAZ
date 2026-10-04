using System.Text;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Common.Notifications;

/// <summary>
/// The copy and routing for one notification event.
///
/// Arabic is the platform's default and is written in Egyptian dialect, the way a teacher or
/// a student actually speaks — "المدرس صحّح الواجب بتاعك" rather than a stiff translation.
/// English is kept alongside for accounts that read the app in English.
/// </summary>
public sealed record NotificationTemplate
{
    public required string Event { get; init; }

    public required NotificationType Type { get; init; }

    public required NotificationCategory Category { get; init; }

    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    public required string TitleAr { get; init; }

    public required string BodyAr { get; init; }

    public required string TitleEn { get; init; }

    public required string BodyEn { get; init; }

    /// <summary>Deep link, with the same <c>{placeholder}</c> syntax as the copy.</summary>
    public string? ActionUrl { get; init; }

    /// <summary>Name of the entity the notification points at, e.g. "Exam".</summary>
    public string? RelatedEntityType { get; init; }

    /// <summary>
    /// Placeholders the copy needs. Rendering with a missing value is a programming error and
    /// is reported as such rather than shipping "{student}" to a parent's phone.
    /// </summary>
    public IReadOnlyList<string> RequiredValues { get; init; } = [];

    /// <summary>Critical notifications ignore per-category preferences.</summary>
    public bool IsMandatory => Priority == NotificationPriority.Critical;

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
