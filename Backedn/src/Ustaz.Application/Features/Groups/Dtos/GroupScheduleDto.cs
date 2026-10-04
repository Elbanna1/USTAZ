namespace Ustaz.Application.Features.Groups.Dtos;

public sealed record GroupScheduleDto
{
    public Guid Id { get; init; }

    public Guid GroupId { get; init; }

    public DayOfWeek DayOfWeek { get; init; }

    public string DayName => DayOfWeek.ToString();

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public int DurationInMinutes => (int)(EndTime - StartTime).TotalMinutes;

    public string? Location { get; init; }

    public string? MeetingUrl { get; init; }

    public bool IsActive { get; init; }
}
