using Ustaz.Domain.Common;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>Publishes domain events raised by aggregates after they are persisted.</summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<DomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
