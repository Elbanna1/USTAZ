using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities;

/// <summary>An external presence of the teacher. URLs are validated before persistence.</summary>
public class TeacherSocialLink : BaseEntity
{
    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public SocialPlatform Platform { get; set; }

    public string Url { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}
