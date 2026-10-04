using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>A chapter grouping lessons inside a course.</summary>
public class CourseSection : BaseEntity
{
    public Guid CourseId { get; set; }

    public Course Course { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }

    public ICollection<CourseLesson> Lessons { get; set; } = [];
}
