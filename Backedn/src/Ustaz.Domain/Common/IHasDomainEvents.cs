namespace Ustaz.Domain.Common;

public interface IHasDomainEvents
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }

    void RaiseDomainEvent(DomainEvent domainEvent);

    void ClearDomainEvents();
}
