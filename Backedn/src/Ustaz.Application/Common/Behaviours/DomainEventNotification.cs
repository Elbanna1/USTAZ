using MediatR;
using Ustaz.Domain.Common;

namespace Ustaz.Application.Common.Behaviours;

/// <summary>
/// Adapts a pure domain event into a MediatR notification so the Domain layer
/// never has to reference MediatR.
/// </summary>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : DomainEvent;
