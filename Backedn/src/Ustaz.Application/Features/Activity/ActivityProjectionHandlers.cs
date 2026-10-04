using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Behaviours;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;

namespace Ustaz.Application.Features.Activity;

/// <summary>
/// Projects domain events onto the activity timeline.
///
/// These subscribe to events the aggregates already raise rather than being called from the
/// command handlers, which keeps the timeline decoupled: publishing a course knows nothing
/// about feeds, and a new feed entry needs no change to the publishing handler. Recording is
/// best-effort inside the recorder, so a feed write can never fail the action it describes.
///
/// Domain events carry only what the aggregate knew, so the workspace each event belongs to is
/// resolved here from the entity it names.
/// </summary>
internal static class ActivityTenantLookup
{
    public static Task<Guid> ForTeacherAsync(
        IApplicationDbContext context,
        Guid teacherId,
        CancellationToken cancellationToken) =>
        context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == teacherId)
            .Select(profile => profile.TenantId)
            .FirstOrDefaultAsync(cancellationToken);
}

public sealed class GroupPublishedActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity) : INotificationHandler<DomainEventNotification<GroupPublishedEvent>>
{
    public async Task Handle(
        DomainEventNotification<GroupPublishedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.GroupPublished,
                TenantId = tenantId,
                RelatedEntityType = "Group",
                RelatedEntityId = domainEvent.GroupId,
                Visibility = ActivityVisibility.Workspace,
                OccurredAt = domainEvent.OccurredAtUtc,
                // Unpublishing and republishing is a normal edit, not a second announcement.
                DeduplicationKey = ActivityTypes.GroupPublished + ":" + domainEvent.GroupId,
                Metadata = new Dictionary<string, string> { ["group"] = domainEvent.GroupName }
            },
            cancellationToken);
    }
}

public sealed class CoursePublishedActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity) : INotificationHandler<DomainEventNotification<CoursePublishedEvent>>
{
    public async Task Handle(
        DomainEventNotification<CoursePublishedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.CoursePublished,
                TenantId = tenantId,
                RelatedEntityType = "Course",
                RelatedEntityId = domainEvent.CourseId,
                Visibility = ActivityVisibility.Workspace,
                OccurredAt = domainEvent.OccurredAtUtc,
                DeduplicationKey = ActivityTypes.CoursePublished + ":" + domainEvent.CourseId,
                Metadata = new Dictionary<string, string> { ["course"] = domainEvent.Title }
            },
            cancellationToken);
    }
}

public sealed class StudentEnrolledInGroupActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity) : INotificationHandler<DomainEventNotification<StudentEnrolledInGroupEvent>>
{
    public async Task Handle(
        DomainEventNotification<StudentEnrolledInGroupEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        var actorUserId = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == domainEvent.StudentId)
            .Select(profile => (Guid?)profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.GroupJoined,
                TenantId = tenantId,
                ActorUserId = actorUserId,
                StudentId = domainEvent.StudentId,
                RelatedEntityType = "GroupEnrollment",
                RelatedEntityId = domainEvent.EnrollmentId,
                // Both the teacher's feed and the student's own timeline want this.
                Visibility = ActivityVisibility.Shared,
                OccurredAt = domainEvent.OccurredAtUtc,
                DeduplicationKey = ActivityTypes.GroupJoined + ":" + domainEvent.EnrollmentId,
                Metadata = new Dictionary<string, string>
                {
                    ["group"] = domainEvent.GroupName,
                    ["groupId"] = domainEvent.GroupId.ToString()
                }
            },
            cancellationToken);
    }
}

public sealed class StudentEnrolledInCourseActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity) : INotificationHandler<DomainEventNotification<StudentEnrolledInCourseEvent>>
{
    public async Task Handle(
        DomainEventNotification<StudentEnrolledInCourseEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        var actorUserId = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == domainEvent.StudentId)
            .Select(profile => (Guid?)profile.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.CourseJoined,
                TenantId = tenantId,
                ActorUserId = actorUserId,
                StudentId = domainEvent.StudentId,
                RelatedEntityType = "CourseEnrollment",
                RelatedEntityId = domainEvent.EnrollmentId,
                Visibility = ActivityVisibility.Shared,
                OccurredAt = domainEvent.OccurredAtUtc,
                DeduplicationKey = ActivityTypes.CourseJoined + ":" + domainEvent.EnrollmentId,
                Metadata = new Dictionary<string, string>
                {
                    ["course"] = domainEvent.CourseTitle,
                    ["courseId"] = domainEvent.CourseId.ToString()
                }
            },
            cancellationToken);
    }
}

public sealed class GroupEnrollmentCancelledActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity)
    : INotificationHandler<DomainEventNotification<GroupEnrollmentCancelledEvent>>
{
    public async Task Handle(
        DomainEventNotification<GroupEnrollmentCancelledEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.GroupLeft,
                TenantId = tenantId,
                StudentId = domainEvent.StudentId,
                RelatedEntityType = "GroupEnrollment",
                RelatedEntityId = domainEvent.EnrollmentId,
                // A departure belongs on the staff feed, not on the learner's own timeline.
                Visibility = ActivityVisibility.Workspace,
                OccurredAt = domainEvent.OccurredAtUtc,
                DeduplicationKey = ActivityTypes.GroupLeft + ":" + domainEvent.EnrollmentId,
                Metadata = new Dictionary<string, string>
                {
                    ["groupId"] = domainEvent.GroupId.ToString()
                }
            },
            cancellationToken);
    }
}

public sealed class TeacherVerifiedActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity) : INotificationHandler<DomainEventNotification<TeacherVerifiedEvent>>
{
    public async Task Handle(
        DomainEventNotification<TeacherVerifiedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.TeacherVerified,
                TenantId = tenantId,
                ActorName = domainEvent.DisplayName,
                RelatedEntityType = "TeacherProfile",
                RelatedEntityId = domainEvent.TeacherId,
                Visibility = ActivityVisibility.Workspace,
                OccurredAt = domainEvent.OccurredAtUtc,
                DeduplicationKey = ActivityTypes.TeacherVerified + ":" + domainEvent.TeacherId
            },
            cancellationToken);
    }
}

public sealed class ReviewCreatedActivityHandler(
    IApplicationDbContext context,
    IActivityRecorder activity) : INotificationHandler<DomainEventNotification<ReviewCreatedEvent>>
{
    public async Task Handle(
        DomainEventNotification<ReviewCreatedEvent> notification,
        CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;

        var tenantId = await ActivityTenantLookup.ForTeacherAsync(
            context, domainEvent.TeacherId, cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.ReviewReceived,
                TenantId = tenantId,
                StudentId = domainEvent.StudentId,
                RelatedEntityType = "Review",
                RelatedEntityId = domainEvent.ReviewId,
                // The teacher's feed only: a learner does not need their own review echoed back.
                Visibility = ActivityVisibility.Workspace,
                OccurredAt = domainEvent.OccurredAtUtc,
                DeduplicationKey = ActivityTypes.ReviewReceived + ":" + domainEvent.ReviewId,
                Metadata = new Dictionary<string, string>
                {
                    ["rating"] = domainEvent.Rating.ToString()
                }
            },
            cancellationToken);
    }
}
