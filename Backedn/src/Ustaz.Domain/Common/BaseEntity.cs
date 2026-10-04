namespace Ustaz.Domain.Common;

/// <summary>
/// Base class for every persisted aggregate/entity in the USTAZ domain.
/// Timestamps are always stored in UTC.
/// </summary>
public abstract class BaseEntity : IAuditableEntity, IHasDomainEvents
{
    private readonly List<DomainEvent> _domainEvents = [];

    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public void RaiseDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}
