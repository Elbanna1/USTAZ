using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// Link between a guardian and a student. Every parent-facing query joins through this row,
/// which is what stops a parent reading another family's data.
/// </summary>
public class ParentStudentLink : BaseEntity
{
    public Guid ParentId { get; set; }

    public ParentProfile Parent { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    /// <summary>Father, Mother, Guardian, ...</summary>
    public string? Relationship { get; set; }

    /// <summary>
    /// A link becomes confirmed when the student (or a teacher) approves it. Unconfirmed
    /// links grant no access.
    /// </summary>
    public bool IsConfirmed { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    /// <summary>True for the guardian who receives billing and primary notifications.</summary>
    public bool IsPrimaryContact { get; set; }
}
