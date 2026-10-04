using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>A teacher saved by a student. Unique per (student, teacher).</summary>
public class TeacherFavorite : BaseEntity
{
    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;
}
