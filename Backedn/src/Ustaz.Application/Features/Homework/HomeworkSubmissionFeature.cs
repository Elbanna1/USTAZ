using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Homework;

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------
public sealed record GetHomeworkByIdQuery(Guid HomeworkId) : IRequest<HomeworkDto>;

public sealed class GetHomeworkByIdQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    ICurrentUserService currentUser) : IRequestHandler<GetHomeworkByIdQuery, HomeworkDto>
{
    public async Task<HomeworkDto> Handle(GetHomeworkByIdQuery query, CancellationToken cancellationToken)
    {
        var homework = await context.Homeworks
            .AsNoTracking()
            .Where(entity => entity.Id == query.HomeworkId)
            .Select(HomeworkProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Homework", query.HomeworkId);

        var studentId = currentUser.StudentId;

        // A student may read published homework for a group they are enrolled in; anyone else
        // must hold the workspace permission.
        if (studentId is not null)
        {
            var enrolled = await context.GroupEnrollments.AnyAsync(
                enrollment => enrollment.GroupId == homework.GroupId
                    && enrollment.StudentId == studentId.Value
                    && enrollment.Status != EnrollmentStatus.Cancelled,
                cancellationToken);

            if (enrolled && homework.IsPublished)
            {
                var mine = await context.HomeworkSubmissions
                    .AsNoTracking()
                    .Where(submission => submission.HomeworkId == homework.Id
                        && submission.StudentId == studentId.Value)
                    .Select(HomeworkProjections.ToSubmissionDto)
                    .FirstOrDefaultAsync(cancellationToken);

                return homework with { MySubmission = mine };
            }
        }

        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkView, cancellationToken: cancellationToken);

        tenantAuthorization.EnsureSameTenant(access, homework.TenantId);

        return homework;
    }
}

public sealed record GetHomeworkListQuery : PagedRequest, IRequest<PaginatedList<HomeworkDto>>
{
    public Guid? GroupId { get; init; }

    public bool? IsPublished { get; init; }

    public bool? Overdue { get; init; }
}

public sealed class GetHomeworkListQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetHomeworkListQuery, PaginatedList<HomeworkDto>>
{
    public async Task<PaginatedList<HomeworkDto>> Handle(
        GetHomeworkListQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkView, cancellationToken: cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        return await context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.TenantId == access.TenantId)
            .WhereIf(query.GroupId.HasValue, homework => homework.GroupId == query.GroupId!.Value)
            .WhereIf(query.IsPublished.HasValue, homework => homework.IsPublished == query.IsPublished!.Value)
            .WhereIf(query.Overdue == true, homework => homework.DueAt < utcNow)
            .WhereIf(query.Overdue == false, homework => homework.DueAt >= utcNow)
            .OrderByDescending(homework => homework.DueAt)
            .Select(HomeworkProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>Homework visible to the signed-in student.</summary>
public sealed record GetMyHomeworkQuery : PagedRequest, IRequest<PaginatedList<HomeworkDto>>
{
    public HomeworkSubmissionStatus? Status { get; init; }
}

public sealed class GetMyHomeworkQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyHomeworkQuery, PaginatedList<HomeworkDto>>
{
    public async Task<PaginatedList<HomeworkDto>> Handle(
        GetMyHomeworkQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var source = context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.IsPublished
                && context.GroupEnrollments.Any(enrollment =>
                    enrollment.GroupId == homework.GroupId
                    && enrollment.StudentId == studentId
                    && enrollment.Status != EnrollmentStatus.Cancelled));

        // The status filter runs in the database, before paging: applying it to an already
        // paged slice would report the slice's size as the total and lose later pages.
        // "Not submitted" is the absence of a row as much as a row in that state.
        if (query.Status == HomeworkSubmissionStatus.NotSubmitted)
        {
            source = source.Where(homework => !context.HomeworkSubmissions.Any(submission =>
                submission.HomeworkId == homework.Id
                && submission.StudentId == studentId
                && submission.Status != HomeworkSubmissionStatus.NotSubmitted));
        }
        else if (query.Status is { } status)
        {
            source = source.Where(homework => context.HomeworkSubmissions.Any(submission =>
                submission.HomeworkId == homework.Id
                && submission.StudentId == studentId
                && submission.Status == status));
        }

        var page = await source
            .OrderByDescending(homework => homework.DueAt)
            .Select(HomeworkProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);

        var ids = page.Items.Select(item => item.Id).ToList();

        var submissions = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.StudentId == studentId && ids.Contains(submission.HomeworkId))
            .Select(HomeworkProjections.ToSubmissionDto)
            .ToListAsync(cancellationToken);

        return page.Map(item => item with
        {
            MySubmission = submissions.FirstOrDefault(submission => submission.HomeworkId == item.Id)
        });
    }
}

/// <summary>Homework for one of the calling parent's children.</summary>
public sealed record GetChildHomeworkQuery : PagedRequest, IRequest<PaginatedList<HomeworkSubmissionDto>>
{
    public Guid StudentId { get; init; }
}

public sealed class GetChildHomeworkQueryHandler(
    IApplicationDbContext context,
    IParentAccessGuard parentAccess) : IRequestHandler<GetChildHomeworkQuery, PaginatedList<HomeworkSubmissionDto>>
{
    public async Task<PaginatedList<HomeworkSubmissionDto>> Handle(
        GetChildHomeworkQuery query,
        CancellationToken cancellationToken)
    {
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.StudentId == query.StudentId)
            .OrderByDescending(submission => submission.Homework.DueAt)
            .Select(HomeworkProjections.ToSubmissionDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetHomeworkSubmissionsQuery : PagedRequest, IRequest<PaginatedList<HomeworkSubmissionDto>>
{
    public Guid HomeworkId { get; init; }

    public HomeworkSubmissionStatus? Status { get; init; }
}

public sealed class GetHomeworkSubmissionsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetHomeworkSubmissionsQuery, PaginatedList<HomeworkSubmissionDto>>
{
    public async Task<PaginatedList<HomeworkSubmissionDto>> Handle(
        GetHomeworkSubmissionsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkView, cancellationToken: cancellationToken);

        var tenantId = await context.Homeworks
            .AsNoTracking()
            .Where(homework => homework.Id == query.HomeworkId)
            .Select(homework => (Guid?)homework.TenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Homework", query.HomeworkId);

        tenantAuthorization.EnsureSameTenant(access, tenantId);

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(submission => submission.HomeworkId == query.HomeworkId)
            .WhereIf(query.Status.HasValue, submission => submission.Status == query.Status!.Value)
            .OrderBy(submission => submission.Student.User.DisplayName)
            .Select(HomeworkProjections.ToSubmissionDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetHomeworkSubmissionQuery(Guid SubmissionId) : IRequest<HomeworkSubmissionDto>;

public sealed class GetHomeworkSubmissionQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    ICurrentUserService currentUser) : IRequestHandler<GetHomeworkSubmissionQuery, HomeworkSubmissionDto>
{
    public async Task<HomeworkSubmissionDto> Handle(
        GetHomeworkSubmissionQuery query,
        CancellationToken cancellationToken)
    {
        var submission = await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => entity.Id == query.SubmissionId)
            .Select(entity => new { Dto = entity, entity.TenantId, entity.StudentId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("HomeworkSubmission", query.SubmissionId);

        // The owning student may always read their own submission.
        if (currentUser.StudentId is null || currentUser.StudentId != submission.StudentId)
        {
            var access = await tenantAuthorization.RequirePermissionAsync(
                TenantPermissions.HomeworkView, cancellationToken: cancellationToken);

            tenantAuthorization.EnsureSameTenant(access, submission.TenantId);
        }

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => entity.Id == query.SubmissionId)
            .Select(HomeworkProjections.ToSubmissionDto)
            .FirstAsync(cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Student: submit
// ---------------------------------------------------------------------------
public sealed record SubmitHomeworkCommand : IRequest<HomeworkSubmissionDto>
{
    public Guid HomeworkId { get; init; }

    public string? Content { get; init; }

    public string? FileUrl { get; init; }
}

public sealed class SubmitHomeworkCommandValidator : AbstractValidator<SubmitHomeworkCommand>
{
    public SubmitHomeworkCommandValidator()
    {
        RuleFor(command => command.HomeworkId).NotEmpty();
        RuleFor(command => command.Content).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.FileUrl).OptionalHttpUrl();
    }
}

public sealed class SubmitHomeworkCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    INotificationDispatcher notifications,
    IActivityRecorder activity) : IRequestHandler<SubmitHomeworkCommand, HomeworkSubmissionDto>
{
    public async Task<HomeworkSubmissionDto> Handle(
        SubmitHomeworkCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var homework = await context.Homeworks
            .FirstOrDefaultAsync(entity => entity.Id == command.HomeworkId, cancellationToken)
            ?? throw new NotFoundException("Homework", command.HomeworkId);

        var utcNow = dateTimeProvider.UtcNow;
        homework.EnsureAcceptsSubmission(utcNow);

        var enrolled = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.GroupId == homework.GroupId
                && enrollment.StudentId == studentId
                && enrollment.Status != EnrollmentStatus.Cancelled,
            cancellationToken);

        if (!enrolled)
        {
            throw new NotFoundException("Homework", command.HomeworkId);
        }

        if (!string.IsNullOrWhiteSpace(command.FileUrl))
        {
            urlGuard.EnsureSafeHttpUrl(command.FileUrl, "Submission file URL");
        }

        var submission = await context.HomeworkSubmissions
            .FirstOrDefaultAsync(
                entity => entity.HomeworkId == homework.Id && entity.StudentId == studentId,
                cancellationToken);

        if (submission is null)
        {
            submission = new HomeworkSubmission
            {
                Id = Guid.NewGuid(),
                TenantId = homework.TenantId,
                HomeworkId = homework.Id,
                StudentId = studentId,
                CreatedAt = utcNow
            };

            await context.HomeworkSubmissions.AddAsync(submission, cancellationToken);
        }

        submission.Submit(homework, utcNow, command.Content?.Trim(), command.FileUrl?.Trim(), null);

        await context.SaveChangesAsync(cancellationToken);

        await HomeworkSubmissionNotifier.AnnounceAsync(
            context, notifications, homework, submission, studentId, cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.HomeworkSubmitted,
                TenantId = homework.TenantId,
                ActorUserId = currentUser.UserId,
                StudentId = studentId,
                RelatedEntityType = nameof(HomeworkSubmission),
                RelatedEntityId = submission.Id,
                Visibility = ActivityVisibility.Shared,
                // Keyed on the attempt, so a resubmission is a new entry but a retried request
                // is not.
                DeduplicationKey = ActivityTypes.HomeworkSubmitted + ":" + submission.Id
                    + ":" + submission.SubmissionCount,
                Metadata = new Dictionary<string, string>
                {
                    ["homework"] = homework.Title,
                    ["homeworkId"] = homework.Id.ToString(),
                    ["isLate"] = submission.IsLate ? "true" : "false"
                }
            },
            cancellationToken);

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => entity.Id == submission.Id)
            .Select(HomeworkProjections.ToSubmissionDto)
            .FirstAsync(cancellationToken);
    }
}

/// <summary>
/// Tells the teacher a submission landed. Keyed on the submission, so a student who edits
/// and re-submits does not ping the teacher over and over.
/// </summary>
internal static class HomeworkSubmissionNotifier
{
    public static async Task AnnounceAsync(
        IApplicationDbContext context,
        INotificationDispatcher notifications,
        Domain.Entities.Education.Homework homework,
        HomeworkSubmission submission,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var teacherUserId = await NotificationAudience.TeacherUserIdAsync(
            context, homework.TeacherId, cancellationToken);

        if (teacherUserId == Guid.Empty)
        {
            return;
        }

        await notifications.SendAsync(
            teacherUserId,
            new NotificationRequest
            {
                Event = submission.IsLate
                    ? NotificationEvents.HomeworkSubmittedLate
                    : NotificationEvents.HomeworkSubmitted,
                TenantId = homework.TenantId,
                ReferenceId = submission.Id,
                DeduplicationKey = "homework-submitted:" + submission.Id,
                Values = new Dictionary<string, string>
                {
                    ["student"] = await NotificationAudience.StudentNameAsync(
                        context, studentId, cancellationToken),
                    ["homework"] = homework.Title,
                    ["homeworkId"] = homework.Id.ToString()
                }
            },
            cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Teacher: grade
// ---------------------------------------------------------------------------
public sealed record GradeHomeworkSubmissionCommand : IRequest<HomeworkSubmissionDto>
{
    public Guid SubmissionId { get; init; }

    public decimal Score { get; init; }

    public string? Feedback { get; init; }
}

public sealed class GradeHomeworkSubmissionCommandValidator : AbstractValidator<GradeHomeworkSubmissionCommand>
{
    public GradeHomeworkSubmissionCommandValidator()
    {
        RuleFor(command => command.SubmissionId).NotEmpty();
        RuleFor(command => command.Score).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Feedback).MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class GradeHomeworkSubmissionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IActivityRecorder activity,
    IAuditLogger auditLogger) : IRequestHandler<GradeHomeworkSubmissionCommand, HomeworkSubmissionDto>
{
    public async Task<HomeworkSubmissionDto> Handle(
        GradeHomeworkSubmissionCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var submission = await context.HomeworkSubmissions
            .Include(entity => entity.Homework)
            .Include(entity => entity.Student)
            .FirstOrDefaultAsync(entity => entity.Id == command.SubmissionId, cancellationToken)
            ?? throw new NotFoundException("HomeworkSubmission", command.SubmissionId);

        tenantAuthorization.EnsureSameTenant(access, submission.TenantId);

        var utcNow = dateTimeProvider.UtcNow;

        submission.Grade(
            command.Score,
            submission.Homework.MaximumScore,
            command.Feedback?.Trim(),
            access.UserId,
            utcNow);

        await context.SaveChangesAsync(cancellationToken);

        var gradeValues = new Dictionary<string, string>
        {
            ["homework"] = submission.Homework.Title,
            ["homeworkId"] = submission.HomeworkId.ToString(),
            ["score"] = (submission.Score ?? 0m).ToString("0.##"),
            ["max"] = (submission.MaximumScore ?? 0m).ToString("0.##")
        };

        await notifications.SendAsync(
            submission.Student.UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkGraded,
                TenantId = access.TenantId,
                ReferenceId = submission.Id,
                Values = gradeValues
            },
            cancellationToken);

        // Guardians see the mark too, with the child named.
        var guardianUserIds = await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.StudentId == submission.StudentId && link.IsConfirmed)
            .Select(link => link.Parent.UserId)
            .ToListAsync(cancellationToken);

        if (guardianUserIds.Count > 0)
        {
            await notifications.SendManyAsync(
                guardianUserIds,
                new NotificationRequest
                {
                    Event = NotificationEvents.ChildHomeworkGraded,
                    TenantId = access.TenantId,
                    ReferenceId = submission.Id,
                    Values = new Dictionary<string, string>(gradeValues)
                    {
                        ["student"] = submission.Student.User.DisplayName,
                        ["studentId"] = submission.StudentId.ToString()
                    }
                },
                cancellationToken);
        }

        await auditLogger.LogAsync(
            AuditActions.HomeworkGraded,
            nameof(HomeworkSubmission),
            submission.Id,
            newValues: new { submission.Score, submission.Percentage },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        await activity.RecordAsync(
            new ActivityRequest
            {
                Type = ActivityTypes.HomeworkGraded,
                TenantId = access.TenantId,
                ActorUserId = access.UserId,
                StudentId = submission.StudentId,
                RelatedEntityType = nameof(HomeworkSubmission),
                RelatedEntityId = submission.Id,
                Visibility = ActivityVisibility.Shared,
                DeduplicationKey = ActivityTypes.HomeworkGraded + ":" + submission.Id
                    + ":" + submission.SubmissionCount,
                Metadata = new Dictionary<string, string>(gradeValues)
            },
            cancellationToken);

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => entity.Id == submission.Id)
            .Select(HomeworkProjections.ToSubmissionDto)
            .FirstAsync(cancellationToken);
    }
}
