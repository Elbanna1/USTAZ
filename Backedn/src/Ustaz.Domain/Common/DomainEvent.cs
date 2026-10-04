namespace Ustaz.Domain.Common;

/// <summary>
/// Base type for all domain events. The domain deliberately has no knowledge of the
/// dispatching mechanism; the Application layer adapts these to MediatR notifications.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();

    public DateTime OccurredAtUtc { get; init; } = DateTime.UtcNow;
}
