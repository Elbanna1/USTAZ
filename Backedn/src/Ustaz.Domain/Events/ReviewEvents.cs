using Ustaz.Domain.Common;

namespace Ustaz.Domain.Events;

public sealed record ReviewCreatedEvent(
    Guid ReviewId,
    Guid TeacherId,
    Guid StudentId,
    Guid? GroupId,
    Guid? CourseId,
    int Rating) : DomainEvent;

public sealed record ReviewUpdatedEvent(Guid ReviewId, Guid TeacherId, Guid? GroupId, Guid? CourseId) : DomainEvent;

public sealed record ReviewDeletedEvent(Guid ReviewId, Guid TeacherId, Guid? GroupId, Guid? CourseId) : DomainEvent;
