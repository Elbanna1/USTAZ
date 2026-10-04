using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>A highlight on the teacher profile, e.g. an award or a notable result.</summary>
public class TeacherAchievement : BaseEntity
{
    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? Year { get; set; }

    public string? ImageUrl { get; set; }

    public string? ImagePublicId { get; set; }

    public int SortOrder { get; set; }
}
