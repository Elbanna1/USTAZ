using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>Join entity: the subjects a teacher is qualified to teach.</summary>
public class TeacherSubject : BaseEntity
{
    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;
}
