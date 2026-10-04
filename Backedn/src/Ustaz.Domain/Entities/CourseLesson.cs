using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>A single video lesson. Preview lessons are watchable without enrolling.</summary>
public class CourseLesson : BaseEntity
{
    public Guid SectionId { get; set; }

    public CourseSection Section { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? VideoUrl { get; set; }

    public string? VideoPublicId { get; set; }

    public int DurationInMinutes { get; set; }

    public int SortOrder { get; set; }

    public bool IsPreview { get; set; }

    public bool IsPublished { get; set; }
}
