using Ustaz.Domain.Common;

namespace Ustaz.Domain.Events;

public sealed record TeacherRegisteredEvent(Guid UserId, Guid TeacherId, string Email, string DisplayName) : DomainEvent;

public sealed record StudentRegisteredEvent(Guid UserId, Guid StudentId, string Email, string DisplayName) : DomainEvent;

public sealed record ParentRegisteredEvent(Guid UserId, Guid ParentId, string Email, string DisplayName) : DomainEvent;

public sealed record TeacherVerifiedEvent(Guid TeacherId, Guid UserId, string DisplayName) : DomainEvent;

public sealed record InquiryCreatedEvent(Guid InquiryId, Guid TeacherId, string SenderName) : DomainEvent;
