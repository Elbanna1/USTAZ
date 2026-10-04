using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>Join entity: the educational grades a teacher serves.</summary>
public class TeacherGrade : BaseEntity
{
    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid GradeId { get; set; }

    public EducationalGrade Grade { get; set; } = null!;
}
