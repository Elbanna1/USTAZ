using FluentValidation;
using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Validation;

/// <summary>Anything an analytics query carrying a date window has in common.</summary>
public interface IHasAnalyticsRange
{
    AnalyticsPeriod Period { get; }

    /// <summary>Inclusive first day. Required only when the period is <c>Custom</c>.</summary>
    DateTime? From { get; }

    /// <summary>Inclusive last day. Required only when the period is <c>Custom</c>.</summary>
    DateTime? To { get; }
}

/// <summary>
/// Shared validation for a requested date window, so a bad custom range is refused with a
/// 422 and a clear message instead of being silently clamped.
/// </summary>
public static class AnalyticsRangeRules
{
    public static void ApplyAnalyticsRangeRules<T>(this AbstractValidator<T> validator)
        where T : IHasAnalyticsRange
    {
        validator.RuleFor(query => query.Period)
            .IsInEnum()
            .WithMessage("Unknown period. Use Today, Last7Days, Last30Days, Last90Days, "
                + "Last12Months, AllTime or Custom.");

        validator.RuleFor(query => query.From)
            .NotNull()
            .When(query => query.Period == AnalyticsPeriod.Custom)
            .WithMessage("A custom period needs a 'from' date.");

        validator.RuleFor(query => query.To)
            .NotNull()
            .When(query => query.Period == AnalyticsPeriod.Custom)
            .WithMessage("A custom period needs a 'to' date.");

        validator.RuleFor(query => query)
            .Must(query => query.From!.Value.Date <= query.To!.Value.Date)
            .When(query => query.Period == AnalyticsPeriod.Custom
                && query.From.HasValue
                && query.To.HasValue)
            .WithMessage("The 'from' date must not be after the 'to' date.")
            .WithName(nameof(IHasAnalyticsRange.From));

        validator.RuleFor(query => query)
            .Must(query => (query.To!.Value.Date - query.From!.Value.Date).TotalDays
                <= AnalyticsRange.MaxCustomRangeDays)
            .When(query => query.Period == AnalyticsPeriod.Custom
                && query.From.HasValue
                && query.To.HasValue)
            .WithMessage("A custom period cannot be longer than "
                + AnalyticsRange.MaxCustomRangeDays + " days.")
            .WithName(nameof(IHasAnalyticsRange.To));
    }
}
