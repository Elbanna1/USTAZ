namespace Ustaz.Application.Common.Interfaces;

/// <summary>Abstracts the clock so time dependent business rules stay testable.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }

    DateOnly TodayUtc { get; }
}
