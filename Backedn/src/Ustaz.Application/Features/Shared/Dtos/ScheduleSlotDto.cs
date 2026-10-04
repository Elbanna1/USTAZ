namespace Ustaz.Application.Features.Shared.Dtos;

/// <summary>A weekly recurring session, plus the next concrete occurrence in UTC.</summary>
public sealed record ScheduleSlotDto
{
    public Guid Id { get; init; }

    public Guid GroupId { get; init; }

    public string GroupName { get; init; } = string.Empty;

    public string? GroupSlug { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherName { get; init; } = string.Empty;

    public Guid SubjectId { get; init; }

    public string SubjectNameAr { get; init; } = string.Empty;

    public string SubjectNameEn { get; init; } = string.Empty;

    public DayOfWeek DayOfWeek { get; init; }

    public string DayName => DayOfWeek.ToString();

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public int DurationInMinutes => (int)(EndTime - StartTime).TotalMinutes;

    public string? Location { get; init; }

    public string? MeetingUrl { get; init; }

    public bool IsActive { get; init; }

    public DateTime? NextOccurrenceUtc { get; init; }
}
