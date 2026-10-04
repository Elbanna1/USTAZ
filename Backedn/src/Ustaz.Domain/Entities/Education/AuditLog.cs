using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// Append-only record of a sensitive action. Values are stored as redacted JSON; secrets,
/// passwords and tokens are stripped before anything is written.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid? TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string? UserEmail { get; set; }

    /// <summary>A value from <see cref="Ustaz.Domain.Constants.AuditActions"/>.</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public bool Succeeded { get; set; } = true;

    public string? FailureReason { get; set; }
}
