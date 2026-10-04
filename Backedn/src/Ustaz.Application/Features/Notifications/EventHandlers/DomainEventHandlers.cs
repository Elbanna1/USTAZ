using MediatR;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Behaviours;
using Ustaz.Domain.Events;

namespace Ustaz.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Audit trail for the business events that matter operationally. Notifications themselves are
/// created inside the originating handlers so they share that unit of work; these handlers
/// exist so the event pipeline has real subscribers and important operations are logged.
/// </summary>
public sealed class GroupPublishedEventHandler(ILogger<GroupPublishedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<GroupPublishedEvent>>
{
    public Task Handle(DomainEventNotification<GroupPublishedEvent> notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Group {GroupId} ({GroupName}) was published by teacher {TeacherId}.",
            notification.DomainEvent.GroupId,
            notification.DomainEvent.GroupName,
            notification.DomainEvent.TeacherId);

        return Task.CompletedTask;
    }
}

public sealed class CoursePublishedEventHandler(ILogger<CoursePublishedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<CoursePublishedEvent>>
{
    public Task Handle(DomainEventNotification<CoursePublishedEvent> notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Course {CourseId} ({Title}) was published by teacher {TeacherId}.",
            notification.DomainEvent.CourseId,
            notification.DomainEvent.Title,
            notification.DomainEvent.TeacherId);

        return Task.CompletedTask;
    }
}

public sealed class GroupReachedCapacityEventHandler(ILogger<GroupReachedCapacityEventHandler> logger)
    : INotificationHandler<DomainEventNotification<GroupReachedCapacityEvent>>
{
    public Task Handle(
        DomainEventNotification<GroupReachedCapacityEvent> notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Group {GroupId} reached its capacity of {Capacity} students.",
            notification.DomainEvent.GroupId,
            notification.DomainEvent.Capacity);

        return Task.CompletedTask;
    }
}

public sealed class StudentEnrolledInGroupEventHandler(ILogger<StudentEnrolledInGroupEventHandler> logger)
    : INotificationHandler<DomainEventNotification<StudentEnrolledInGroupEvent>>
{
    public Task Handle(
        DomainEventNotification<StudentEnrolledInGroupEvent> notification,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Enrollment {EnrollmentId}: a student joined group {GroupId} of teacher {TeacherId}.",
            notification.DomainEvent.EnrollmentId,
            notification.DomainEvent.GroupId,
            notification.DomainEvent.TeacherId);

        return Task.CompletedTask;
    }
}

public sealed class TeacherVerifiedEventHandler(ILogger<TeacherVerifiedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<TeacherVerifiedEvent>>
{
    public Task Handle(DomainEventNotification<TeacherVerifiedEvent> notification, CancellationToken cancellationToken)
    {
        logger.LogInformation("Teacher {TeacherId} was verified.", notification.DomainEvent.TeacherId);
        return Task.CompletedTask;
    }
}

public sealed class ReviewCreatedEventHandler(ILogger<ReviewCreatedEventHandler> logger)
    : INotificationHandler<DomainEventNotification<ReviewCreatedEvent>>
{
    public Task Handle(DomainEventNotification<ReviewCreatedEvent> notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Review {ReviewId} ({Rating} stars) was created for teacher {TeacherId}.",
            notification.DomainEvent.ReviewId,
            notification.DomainEvent.Rating,
            notification.DomainEvent.TeacherId);

        return Task.CompletedTask;
    }
}
