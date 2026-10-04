using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Homework;

/// <summary>
/// Attaches a real file to the caller's homework submission. Before this existed a student
/// could only hand in a URL they had hosted themselves; the file now goes through the
/// platform's own media service, which is what makes size, type and ownership enforceable.
/// </summary>
public sealed record UploadHomeworkSubmissionFileCommand : IRequest<HomeworkSubmissionDto>
{
    public Guid HomeworkId { get; init; }

    public required FileUploadRequest File { get; init; }
}

public sealed class UploadHomeworkSubmissionFileCommandValidator
    : AbstractValidator<UploadHomeworkSubmissionFileCommand>
{
    public UploadHomeworkSubmissionFileCommandValidator()
    {
        RuleFor(command => command.HomeworkId).NotEmpty();
        RuleFor(command => command.File).ValidDocumentUpload();
    }
}

public sealed class UploadHomeworkSubmissionFileCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IMediaService mediaService,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : IRequestHandler<UploadHomeworkSubmissionFileCommand, HomeworkSubmissionDto>
{
    public async Task<HomeworkSubmissionDto> Handle(
        UploadHomeworkSubmissionFileCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();
        var utcNow = dateTimeProvider.UtcNow;

        var homework = await context.Homeworks
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == command.HomeworkId, cancellationToken)
            ?? throw new NotFoundException("Homework", command.HomeworkId);

        // A student who is not in the group must not learn that the homework exists.
        var enrolled = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.GroupId == homework.GroupId
                && enrollment.StudentId == studentId
                && enrollment.Status != EnrollmentStatus.Cancelled,
            cancellationToken);

        if (!enrolled)
        {
            throw new NotFoundException("Homework", command.HomeworkId);
        }

        // The same window rules as a text submission: closed means closed.
        homework.EnsureAcceptsSubmission(utcNow);

        var submission = await context.HomeworkSubmissions
            .FirstOrDefaultAsync(
                entity => entity.HomeworkId == homework.Id && entity.StudentId == studentId,
                cancellationToken);

        if (submission is { Status: HomeworkSubmissionStatus.Graded })
        {
            throw new ConflictException(
                "This submission has already been graded and can no longer be changed.");
        }

        var previousPublicId = submission?.FilePublicId;

        var uploaded = await mediaService.UploadAsync(
            command.File.Content,
            command.File.FileName,
            "homework/submissions",
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

        submission.FileUrl = uploaded.SecureUrl ?? uploaded.Url;
        submission.FilePublicId = uploaded.PublicId;
        submission.UpdatedAt = utcNow;

        // The file is the submission: record it as handed in, late or on time.
        submission.Submit(homework, utcNow, submission.Content, submission.FileUrl, uploaded.PublicId);

        await context.SaveChangesAsync(cancellationToken);

        // Only drop the old asset once the new one is safely persisted.
        if (!string.IsNullOrWhiteSpace(previousPublicId) && previousPublicId != uploaded.PublicId)
        {
            await mediaService.DeleteAsync(previousPublicId, cancellationToken);
        }

        await HomeworkSubmissionNotifier.AnnounceAsync(
            context, notifications, homework, submission, studentId, cancellationToken);

        return await context.HomeworkSubmissions
            .AsNoTracking()
            .Where(entity => entity.Id == submission.Id)
            .Select(HomeworkProjections.ToSubmissionDto)
            .FirstAsync(cancellationToken);
    }
}
