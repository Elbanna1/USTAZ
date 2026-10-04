using Ustaz.Domain.Common;

namespace Ustaz.Domain.Events;

public sealed record CoursePublishedEvent(Guid CourseId, Guid TeacherId, string Title) : DomainEvent;

public sealed record StudentEnrolledInCourseEvent(
    Guid EnrollmentId,
    Guid CourseId,
    Guid StudentId,
    Guid TeacherId,
    string CourseTitle) : DomainEvent;
