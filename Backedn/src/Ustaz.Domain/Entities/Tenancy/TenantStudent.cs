using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>
/// Explicit link between a workspace and a student. Students remain platform-level accounts
/// (they can learn with several teachers), but this row is what makes a student "belong" to a
/// tenant for attendance, exams, homework and the plan's student limit.
/// </summary>
public class TenantStudent : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LeftAt { get; set; }

    /// <summary>Internal reference the teacher uses on paper registers.</summary>
    public string? StudentCode { get; set; }

    public string? Notes { get; set; }
}
