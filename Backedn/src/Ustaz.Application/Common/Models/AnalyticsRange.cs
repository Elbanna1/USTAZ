namespace Ustaz.Application.Common.Models;

/// <summary>The date windows every analytics endpoint understands.</summary>
public enum AnalyticsPeriod
{
    /// <summary>Since midnight UTC today.</summary>
    Today = 1,

    Last7Days = 2,

    Last30Days = 3,

    Last90Days = 4,

    Last12Months = 5,

    /// <summary>Everything on record.</summary>
    AllTime = 6,

    /// <summary>Uses the supplied <c>From</c> and <c>To</c>, which then become required.</summary>
    Custom = 7
}

/// <summary>
/// A resolved, half-open window <c>[FromUtc, ToUtc)</c> used by every analytics query, so the
/// same <c>?period=</c> value means the same thing everywhere and the frontend never has to
/// compute dates itself.
/// </summary>
public sealed record AnalyticsRange
{
    /// <summary>Longest custom window accepted, to keep an unbounded scan off the database.</summary>
    public const int MaxCustomRangeDays = 1095;

    private AnalyticsRange(AnalyticsPeriod period, DateTime fromUtc, DateTime toUtc)
    {
        Period = period;
        FromUtc = fromUtc;
        ToUtc = toUtc;
    }

    public AnalyticsPeriod Period { get; }

    /// <summary>Inclusive start. <see cref="DateTime.MinValue"/> for <c>AllTime</c>.</summary>
    public DateTime FromUtc { get; }

    /// <summary>Exclusive end.</summary>
    public DateTime ToUtc { get; }

    public string PeriodName => Period.ToString();

    public bool IsAllTime => Period == AnalyticsPeriod.AllTime;

    /// <summary>
    /// The window immediately before this one and the same length, for period-over-period
    /// comparison. Null for <c>AllTime</c>, which has nothing before it.
    /// </summary>
    public AnalyticsRange? PreviousPeriod()
    {
        if (IsAllTime)
        {
            return null;
        }

        var span = ToUtc - FromUtc;

        return new AnalyticsRange(Period, FromUtc - span, FromUtc);
    }

    /// <summary>
    /// Turns a requested period into concrete UTC bounds. A custom range is validated by
    /// <see cref="Validation.AnalyticsRangeRules"/> before it reaches here; the clamping below
    /// is the defence in depth for a handler invoked directly.
    /// </summary>
    public static AnalyticsRange Resolve(
        AnalyticsPeriod period,
        DateTime? from,
        DateTime? to,
        DateTime utcNow)
    {
        var midnight = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);

        // The end is exclusive and always the start of tomorrow, so "today" includes everything
        // recorded so far today rather than stopping at the current instant.
        var endOfToday = midnight.AddDays(1);

        return period switch
        {
            AnalyticsPeriod.Today => new AnalyticsRange(period, midnight, endOfToday),
            AnalyticsPeriod.Last7Days => new AnalyticsRange(period, midnight.AddDays(-6), endOfToday),
            AnalyticsPeriod.Last30Days => new AnalyticsRange(period, midnight.AddDays(-29), endOfToday),
            AnalyticsPeriod.Last90Days => new AnalyticsRange(period, midnight.AddDays(-89), endOfToday),
            AnalyticsPeriod.Last12Months => new AnalyticsRange(period, midnight.AddMonths(-12), endOfToday),
            AnalyticsPeriod.AllTime => new AnalyticsRange(period, DateTime.MinValue, endOfToday),
            AnalyticsPeriod.Custom => ResolveCustom(from, to, midnight, endOfToday),
            _ => new AnalyticsRange(AnalyticsPeriod.Last30Days, midnight.AddDays(-29), endOfToday)
        };
    }

    private static AnalyticsRange ResolveCustom(
        DateTime? from,
        DateTime? to,
        DateTime midnight,
        DateTime endOfToday)
    {
        var start = from.HasValue
            ? DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc)
            : midnight.AddDays(-29);

        // The caller names an inclusive last day; internally the window stays half-open.
        var end = to.HasValue
            ? DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc).AddDays(1)
            : endOfToday;

        if (end <= start)
        {
            end = start.AddDays(1);
        }

        if ((end - start).TotalDays > MaxCustomRangeDays)
        {
            end = start.AddDays(MaxCustomRangeDays);
        }

        return new AnalyticsRange(AnalyticsPeriod.Custom, start, end);
    }
}
