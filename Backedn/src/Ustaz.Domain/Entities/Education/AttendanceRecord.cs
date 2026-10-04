using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One student's attendance for one session.</summary>
public class AttendanceRecord : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid AttendanceSessionId { get; set; }

    public AttendanceSession Session { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    /// <summary>Minutes late; only meaningful when <see cref="Status"/> is Late.</summary>
    public int? MinutesLate { get; set; }

    public string? Note { get; set; }

    public Guid RecordedByUserId { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Guards against sending the guardian the same absence alert twice.</summary>
    public bool ParentNotified { get; set; }

    public DateTime? ParentNotifiedAt { get; set; }

    public bool CountsAsMissed => Status is AttendanceStatus.Absent or AttendanceStatus.Late;
}
