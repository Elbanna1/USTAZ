namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// The correlation id of the request currently being handled, so a stored email or audit row
/// can be tied back to the HTTP call that produced it. Background work has none, which is a
/// normal state rather than an error.
/// </summary>
public interface ICorrelationContext
{
    string? CorrelationId { get; }
}
