using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>One concrete class occurrence against which attendance is taken.</summary>
public class AttendanceSession : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    /// <summary>The weekly slot this occurrence came from, when it was generated from the schedule.</summary>
    public Guid? GroupScheduleId { get; set; }

    public GroupSchedule? GroupSchedule { get; set; }

    public DateTime SessionDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string? Title { get; set; }

    public string? Notes { get; set; }

    public AttendanceSessionStatus Status { get; set; } = AttendanceSessionStatus.Open;

    public Guid RecordedByUserId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public ICollection<AttendanceRecord> Records { get; set; } = [];

    public void EnsureOpen()
    {
        if (Status == AttendanceSessionStatus.Closed)
        {
            throw new BusinessRuleException(
                "ATTENDANCE_SESSION_CLOSED",
                "This attendance session is closed. Reopen it before changing records.");
        }
    }

    public void Close(DateTime utcNow)
    {
        Status = AttendanceSessionStatus.Closed;
        ClosedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
