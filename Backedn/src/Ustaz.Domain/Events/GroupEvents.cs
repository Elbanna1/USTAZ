using Ustaz.Domain.Common;

namespace Ustaz.Domain.Events;

public sealed record GroupPublishedEvent(Guid GroupId, Guid TeacherId, string GroupName) : DomainEvent;

public sealed record GroupReachedCapacityEvent(Guid GroupId, Guid TeacherId, int Capacity) : DomainEvent;

public sealed record StudentEnrolledInGroupEvent(
    Guid EnrollmentId,
    Guid GroupId,
    Guid StudentId,
    Guid TeacherId,
    string GroupName) : DomainEvent;

public sealed record GroupEnrollmentCancelledEvent(
    Guid EnrollmentId,
    Guid GroupId,
    Guid StudentId,
    Guid TeacherId) : DomainEvent;
