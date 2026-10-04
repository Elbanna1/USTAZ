using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.ValueObjects;

/// <summary>An inclusive UTC date range with an optional open end.</summary>
public sealed class DateRange : ValueObject
{
    private DateRange(DateTime start, DateTime? end)
    {
        Start = start;
        End = end;
    }

    public DateTime Start { get; }

    public DateTime? End { get; }

    public static DateRange Create(DateTime start, DateTime? end)
    {
        if (end.HasValue && end.Value < start)
        {
            throw new BusinessRuleException("The end date must be on or after the start date.");
        }

        return new DateRange(
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            end.HasValue ? DateTime.SpecifyKind(end.Value, DateTimeKind.Utc) : null);
    }

    public bool Contains(DateTime instant) => instant >= Start && (End is null || instant <= End);

    public bool IsActiveAt(DateTime instant) => Contains(instant);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
