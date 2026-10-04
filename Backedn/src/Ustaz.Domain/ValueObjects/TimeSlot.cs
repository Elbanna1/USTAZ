using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.ValueObjects;

/// <summary>A weekly recurring time slot used by group schedules.</summary>
public sealed class TimeSlot : ValueObject
{
    private TimeSlot(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        Day = day;
        Start = start;
        End = end;
    }

    public DayOfWeek Day { get; }

    public TimeOnly Start { get; }

    public TimeOnly End { get; }

    public TimeSpan Duration => End - Start;

    public static TimeSlot Create(DayOfWeek day, TimeOnly start, TimeOnly end)
    {
        if (end <= start)
        {
            throw new BusinessRuleException("A session end time must be after its start time.");
        }

        return new TimeSlot(day, start, end);
    }

    /// <summary>True when two slots fall on the same day and share at least one minute.</summary>
    public bool Overlaps(TimeSlot other) =>
        Day == other.Day && Start < other.End && other.Start < End;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Day;
        yield return Start;
        yield return End;
    }
}
