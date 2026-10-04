using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;
using HomeworkEntity = Ustaz.Domain.Entities.Education.Homework;

namespace Ustaz.Application.Features.Homework;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------
public sealed record HomeworkDto
{
    public Guid Id { get; init; }

    public Guid TenantId { get; init; }

    public Guid GroupId { get; init; }

    public string GroupName { get; init; } = string.Empty;

    public Guid? SubjectId { get; init; }

    public string? SubjectNameAr { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Instructions { get; init; }

    public DateTime AssignedAt { get; init; }

    public DateTime DueAt { get; init; }

    public decimal MaximumScore { get; init; }

    public bool AllowLateSubmission { get; init; }

    public bool AllowResubmission { get; init; }

    /// <summary>Null means no cap on attempts.</summary>
    public int? MaxSubmissions { get; init; }

    public bool IsPublished { get; init; }

    /// <summary>Set once the teacher has closed the assignment.</summary>
    public DateTime? ClosedAt { get; init; }

    public bool IsClosed { get; init; }

    public int SubmissionsCount { get; init; }

    public int GradedCount { get; init; }

    /// <summary>Submissions handed in and waiting to be marked.</summary>
    public int PendingGradingCount { get; init; }

    /// <summary>Submissions sent back for revision.</summary>
    public int ReturnedCount { get; init; }

    public IReadOnlyList<HomeworkAttachmentDto> Attachments { get; init; } = [];

    /// <summary>The calling student's own submission, when there is one.</summary>
    public HomeworkSubmissionDto? MySubmission { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record HomeworkAttachmentDto
{
    public Guid Id { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string FileUrl { get; init; } = string.Empty;

    public MaterialFileType FileType { get; init; }

    public long? FileSizeInBytes { get; init; }
}

public sealed record HomeworkSubmissionDto
{
    public Guid Id { get; init; }

    public Guid HomeworkId { get; init; }

    public string HomeworkTitle { get; init; } = string.Empty;

    public DateTime DueAt { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public HomeworkSubmissionStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public string? Content { get; init; }

    public string? FileUrl { get; init; }

    public DateTime? SubmittedAt { get; init; }

    public bool IsLate { get; init; }

    public decimal? Score { get; init; }

    public decimal? MaximumScore { get; init; }

    public decimal? Percentage { get; init; }

    public string? TeacherFeedback { get; init; }

    public DateTime? GradedAt { get; init; }

    /// <summary>How many times this student has handed work in.</summary>
    public int SubmissionCount { get; init; }

    public DateTime? ReturnedAt { get; init; }

    /// <summary>What the teacher asked the student to fix.</summary>
    public string? ReturnReason { get; init; }
}

internal static class HomeworkProjections
{
    public static Expression<Func<HomeworkEntity, HomeworkDto>> ToDto =>
        homework => new HomeworkDto
        {
            Id = homework.Id,
            TenantId = homework.TenantId,
            GroupId = homework.GroupId,
            GroupName = homework.Group.Name,
            SubjectId = homework.SubjectId,
            SubjectNameAr = homework.Subject != null ? homework.Subject.NameAr : null,
            Title = homework.Title,
            Description = homework.Description,
            Instructions = homework.Instructions,
            AssignedAt = homework.AssignedAt,
            DueAt = homework.DueAt,
            MaximumScore = homework.MaximumScore,
            AllowLateSubmission = homework.AllowLateSubmission,
            AllowResubmission = homework.AllowResubmission,
            MaxSubmissions = homework.MaxSubmissions,
            IsPublished = homework.IsPublished,
            ClosedAt = homework.ClosedAt,
            IsClosed = homework.ClosedAt != null,
            CreatedAt = homework.CreatedAt,
            SubmissionsCount = homework.Submissions.Count(submission =>
                submission.Status != HomeworkSubmissionStatus.NotSubmitted),
            GradedCount = homework.Submissions.Count(submission =>
                submission.Status == HomeworkSubmissionStatus.Graded),
            PendingGradingCount = homework.Submissions.Count(submission =>
                submission.Status == HomeworkSubmissionStatus.Submitted
                || submission.Status == HomeworkSubmissionStatus.Late),
            ReturnedCount = homework.Submissions.Count(submission =>
                submission.Status == HomeworkSubmissionStatus.Returned),
            Attachments = homework.Attachments
                .Select(attachment => new HomeworkAttachmentDto
                {
                    Id = attachment.Id,
                    FileName = attachment.FileName,
                    FileUrl = attachment.FileUrl,
                    FileType = attachment.FileType,
                    FileSizeInBytes = attachment.FileSizeInBytes
                })
                .ToList()
        };

    public static Expression<Func<Domain.Entities.Education.HomeworkSubmission, HomeworkSubmissionDto>> ToSubmissionDto =>
        submission => new HomeworkSubmissionDto
        {
            Id = submission.Id,
            HomeworkId = submission.HomeworkId,
            HomeworkTitle = submission.Homework.Title,
            DueAt = submission.Homework.DueAt,
            StudentId = submission.StudentId,
            StudentName = submission.Student.User.DisplayName,
            Status = submission.Status,
            Content = submission.Content,
            FileUrl = submission.FileUrl,
            SubmittedAt = submission.SubmittedAt,
            IsLate = submission.IsLate,
            Score = submission.Score,
            MaximumScore = submission.MaximumScore,
            Percentage = submission.Percentage,
            TeacherFeedback = submission.TeacherFeedback,
            GradedAt = submission.GradedAt,
            SubmissionCount = submission.SubmissionCount,
            ReturnedAt = submission.ReturnedAt,
            ReturnReason = submission.ReturnReason
        };
}

internal static class HomeworkHelper
{
    public static async Task<HomeworkEntity> LoadOwnedAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        TenantAccess access,
        Guid homeworkId,
        CancellationToken cancellationToken)
    {
        var homework = await context.Homeworks
            .FirstOrDefaultAsync(entity => entity.Id == homeworkId, cancellationToken)
            ?? throw new NotFoundException("Homework", homeworkId);

        tenantAuthorization.EnsureSameTenant(access, homework.TenantId);

        return homework;
    }
}

// ---------------------------------------------------------------------------
// Teacher: create / update / publish / delete
// ---------------------------------------------------------------------------
public sealed record CreateHomeworkCommand : IRequest<HomeworkDto>
{
    public Guid GroupId { get; init; }

    public Guid? SubjectId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Instructions { get; init; }

    public DateTime DueAt { get; init; }

    public decimal MaximumScore { get; init; } = 10m;

    public bool AllowLateSubmission { get; init; } = true;

    /// <summary>When false, the first submission is final.</summary>
    public bool AllowResubmission { get; init; } = true;

    /// <summary>Hard cap on attempts per student. Null means no cap.</summary>
    public int? MaxSubmissions { get; init; }

    public IReadOnlyList<HomeworkAttachmentInput> Attachments { get; init; } = [];
}

public sealed record HomeworkAttachmentInput
{
    public string FileName { get; init; } = string.Empty;

    public string FileUrl { get; init; } = string.Empty;

    public MaterialFileType FileType { get; init; } = MaterialFileType.Pdf;

    public long? FileSizeInBytes { get; init; }
}

public sealed class CreateHomeworkCommandValidator : AbstractValidator<CreateHomeworkCommand>
{
    public CreateHomeworkCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Instructions).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.DueAt).NotEmpty();
        RuleFor(command => command.MaximumScore).GreaterThan(0).LessThanOrEqualTo(1000);
        RuleFor(command => command.MaxSubmissions).InclusiveBetween(1, 20)
            .When(command => command.MaxSubmissions.HasValue);

        RuleForEach(command => command.Attachments).ChildRules(attachment =>
        {
            attachment.RuleFor(item => item.FileName).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
            attachment.RuleFor(item => item.FileUrl).HttpUrl();
            attachment.RuleFor(item => item.FileType).IsInEnum();
        });
    }
}

public sealed class CreateHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    IMediator mediator) : IRequestHandler<CreateHomeworkCommand, HomeworkDto>
{
    public async Task<HomeworkDto> Handle(CreateHomeworkCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.Homework, cancellationToken);

        var group = await context.Groups
            .AsNoTracking()
            .Where(entity => entity.Id == command.GroupId)
            .Select(entity => new { entity.TenantId, entity.SubjectId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        tenantAuthorization.EnsureSameTenant(access, group.TenantId);

        var teacherId = await Exams.ExamHelper.ResolveTeacherIdAsync(context, access, cancellationToken);
        var utcNow = dateTimeProvider.UtcNow;

        var homework = new HomeworkEntity
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            TeacherId = teacherId,
            GroupId = command.GroupId,
            SubjectId = command.SubjectId ?? group.SubjectId,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            Instructions = command.Instructions?.Trim(),
            AssignedAt = utcNow,
            DueAt = DateTime.SpecifyKind(command.DueAt, DateTimeKind.Utc),
            MaximumScore = command.MaximumScore,
            AllowLateSubmission = command.AllowLateSubmission,
            AllowResubmission = command.AllowResubmission,
            MaxSubmissions = command.MaxSubmissions,
            CreatedAt = utcNow
        };

        if (homework.DueAt <= homework.AssignedAt)
        {
            throw new BusinessRuleException(
                "HOMEWORK_INVALID_DEADLINE",
                "The deadline must be in the future.");
        }

        foreach (var attachment in command.Attachments)
        {
            urlGuard.EnsureSafeHttpUrl(attachment.FileUrl, "Attachment URL");

            homework.Attachments.Add(new Domain.Entities.Education.HomeworkAttachment
            {
                Id = Guid.NewGuid(),
                TenantId = access.TenantId,
                HomeworkId = homework.Id,
                FileName = attachment.FileName.Trim(),
                FileUrl = attachment.FileUrl.Trim(),
                FileType = attachment.FileType,
                FileSizeInBytes = attachment.FileSizeInBytes,
                CreatedAt = utcNow
            });
        }

        await context.Homeworks.AddAsync(homework, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetHomeworkByIdQuery(homework.Id), cancellationToken);
    }
}

public sealed record UpdateHomeworkCommand : IRequest<HomeworkDto>
{
    public Guid HomeworkId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Instructions { get; init; }

    public DateTime? DueAt { get; init; }

    public decimal? MaximumScore { get; init; }

    public bool? AllowLateSubmission { get; init; }

    public bool? AllowResubmission { get; init; }

    public int? MaxSubmissions { get; init; }
}

public sealed class UpdateHomeworkCommandValidator : AbstractValidator<UpdateHomeworkCommand>
{
    public UpdateHomeworkCommandValidator()
    {
        RuleFor(command => command.HomeworkId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength)
            .When(command => command.Title is not null);
        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Instructions).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.MaximumScore).GreaterThan(0).LessThanOrEqualTo(1000)
            .When(command => command.MaximumScore.HasValue);
        RuleFor(command => command.MaxSubmissions).InclusiveBetween(1, 20)
            .When(command => command.MaxSubmissions.HasValue);
    }
}

public sealed class UpdateHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<UpdateHomeworkCommand, HomeworkDto>
{
    public async Task<HomeworkDto> Handle(UpdateHomeworkCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var homework = await HomeworkHelper.LoadOwnedAsync(
            context, tenantAuthorization, access, command.HomeworkId, cancellationToken);

        if (command.Title is not null)
        {
            homework.Title = command.Title.Trim();
        }

        if (command.Description is not null)
        {
            homework.Description = command.Description.Trim();
        }

        if (command.Instructions is not null)
        {
            homework.Instructions = command.Instructions.Trim();
        }

        if (command.DueAt.HasValue)
        {
            var dueAt = DateTime.SpecifyKind(command.DueAt.Value, DateTimeKind.Utc);

            // The same invariant the entity enforces on publish: a deadline that precedes the
            // assignment would make every submission retroactively late.
            if (dueAt <= homework.AssignedAt)
            {
                throw new BusinessRuleException(
                    "HOMEWORK_INVALID_DEADLINE",
                    "The deadline must be after the assignment date.");
            }

            homework.DueAt = dueAt;
        }

        if (command.MaximumScore.HasValue)
        {
            // Rescaling after marks are in would leave stored percentages describing a
            // different paper, so the scale is frozen once anything has been graded.
            var alreadyGraded = await context.HomeworkSubmissions.AnyAsync(
                submission => submission.HomeworkId == homework.Id
                    && submission.Status == HomeworkSubmissionStatus.Graded,
                cancellationToken);

            if (alreadyGraded && homework.MaximumScore != command.MaximumScore.Value)
            {
                throw new ConflictException(
                    "Some submissions are already graded, so the maximum score can no longer change.");
            }

            homework.MaximumScore = command.MaximumScore.Value;
        }

        if (command.AllowLateSubmission.HasValue)
        {
            homework.AllowLateSubmission = command.AllowLateSubmission.Value;
        }

        if (command.AllowResubmission.HasValue)
        {
            homework.AllowResubmission = command.AllowResubmission.Value;
        }

        if (command.MaxSubmissions.HasValue)
        {
            homework.MaxSubmissions = command.MaxSubmissions.Value;
        }

        homework.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetHomeworkByIdQuery(homework.Id), cancellationToken);
    }
}

public sealed record PublishHomeworkCommand(Guid HomeworkId) : IRequest<Unit>;

public sealed class PublishHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<PublishHomeworkCommand, Unit>
{
    public async Task<Unit> Handle(PublishHomeworkCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var homework = await HomeworkHelper.LoadOwnedAsync(
            context, tenantAuthorization, access, command.HomeworkId, cancellationToken);

        homework.Publish(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        // Tell the group's active students there is new work.
        var recipients = await context.GroupEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.GroupId == homework.GroupId
                && enrollment.Status == EnrollmentStatus.Active)
            .Select(enrollment => enrollment.Student.UserId)
            .ToListAsync(cancellationToken);

        await notifications.SendManyAsync(
            recipients,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkAssigned,
                TenantId = access.TenantId,
                ReferenceId = homework.Id,
                // Re-publishing the same homework must not re-notify the whole group.
                DeduplicationKey = "homework-assigned:" + homework.Id,
                Values = new Dictionary<string, string>
                {
                    ["homework"] = homework.Title,
                    ["homeworkId"] = homework.Id.ToString(),
                    ["due"] = homework.DueAt.ToString("yyyy-MM-dd HH:mm")
                }
            },
            cancellationToken);

        return Unit.Value;
    }
}

public sealed record DeleteHomeworkCommand(Guid HomeworkId) : IRequest<Unit>;

public sealed class DeleteHomeworkCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<DeleteHomeworkCommand, Unit>
{
    public async Task<Unit> Handle(DeleteHomeworkCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.HomeworkManage, cancellationToken: cancellationToken);

        var homework = await HomeworkHelper.LoadOwnedAsync(
            context, tenantAuthorization, access, command.HomeworkId, cancellationToken);

        var hasSubmissions = await context.HomeworkSubmissions.AnyAsync(
            submission => submission.HomeworkId == homework.Id
                && submission.Status != HomeworkSubmissionStatus.NotSubmitted,
            cancellationToken);

        if (hasSubmissions)
        {
            throw new ConflictException(
                "Students have already submitted this homework, so it cannot be deleted.");
        }

        context.Homeworks.Remove(homework);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
