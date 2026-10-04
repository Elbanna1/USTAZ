using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>A course saved by a student. Unique per (student, course).</summary>
public class CourseFavorite : BaseEntity
{
    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public Guid CourseId { get; set; }

    public Course Course { get; set; } = null!;
}
