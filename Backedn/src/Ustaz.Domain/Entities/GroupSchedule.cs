using Ustaz.Domain.Common;
using Ustaz.Domain.Exceptions;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Domain.Entities;

/// <summary>A weekly recurring session of a group.</summary>
public class GroupSchedule : BaseEntity
{
    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string? Location { get; set; }

    public string? MeetingUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public TimeSlot ToTimeSlot() => TimeSlot.Create(DayOfWeek, StartTime, EndTime);

    public void EnsureValid()
    {
        if (EndTime <= StartTime)
        {
            throw new BusinessRuleException(
                "SCHEDULE_INVALID_TIME",
                "The session end time must be after its start time.");
        }
    }

    /// <summary>Projects this weekly slot onto the next concrete occurrence at or after <paramref name="fromUtc"/>.</summary>
    public DateTime NextOccurrenceUtc(DateTime fromUtc)
    {
        var daysAhead = ((int)DayOfWeek - (int)fromUtc.DayOfWeek + 7) % 7;
        var candidate = fromUtc.Date.AddDays(daysAhead).Add(StartTime.ToTimeSpan());

        if (candidate < fromUtc)
        {
            candidate = candidate.AddDays(7);
        }

        return DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
    }
}
