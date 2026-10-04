using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Groups.Commands;

/// <summary>Makes a group publicly discoverable. Only verified teachers may publish.</summary>
public sealed record PublishGroupCommand(Guid GroupId) : IRequest<Unit>;

public sealed class PublishGroupCommandValidator : AbstractValidator<PublishGroupCommand>
{
    public PublishGroupCommandValidator() => RuleFor(command => command.GroupId).NotEmpty();
}

public sealed class PublishGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<PublishGroupCommand, Unit>
{
    public async Task<Unit> Handle(PublishGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Include(entity => entity.Schedules)
            .Include(entity => entity.Teacher)
            .FirstOrDefaultAsync(entity => entity.Id == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, group.TeacherId);

        group.Publish(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.GroupsTag, cancellationToken);

        return Unit.Value;
    }
}

public sealed record UnpublishGroupCommand(Guid GroupId) : IRequest<Unit>;

public sealed class UnpublishGroupCommandValidator : AbstractValidator<UnpublishGroupCommand>
{
    public UnpublishGroupCommandValidator() => RuleFor(command => command.GroupId).NotEmpty();
}

public sealed class UnpublishGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<UnpublishGroupCommand, Unit>
{
    public async Task<Unit> Handle(UnpublishGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .FirstOrDefaultAsync(entity => entity.Id == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, group.TeacherId);

        group.Unpublish(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.GroupsTag, cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Deletes a group. A group that ever had enrollments is archived (deactivated) instead of
/// being removed, so student history is never silently destroyed.
/// </summary>
public sealed record DeleteGroupCommand(Guid GroupId) : IRequest<Unit>;

public sealed class DeleteGroupCommandValidator : AbstractValidator<DeleteGroupCommand>
{
    public DeleteGroupCommandValidator() => RuleFor(command => command.GroupId).NotEmpty();
}

public sealed class DeleteGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<DeleteGroupCommand, Unit>
{
    public async Task<Unit> Handle(DeleteGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .FirstOrDefaultAsync(entity => entity.Id == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, group.TeacherId);

        var hasActiveEnrollments = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.GroupId == group.Id
                && (enrollment.Status == EnrollmentStatus.Active || enrollment.Status == EnrollmentStatus.Pending),
            cancellationToken);

        if (hasActiveEnrollments)
        {
            throw new ConflictException(
                "This group still has active students. Cancel or complete their enrollments before deleting it.");
        }

        var hasHistory = await context.GroupEnrollments.AnyAsync(
            enrollment => enrollment.GroupId == group.Id, cancellationToken);

        if (hasHistory)
        {
            group.IsActive = false;
            group.IsPublished = false;
            group.UpdatedAt = dateTimeProvider.UtcNow;
        }
        else
        {
            // Inquiries survive the group they were about, so detach them first: the
            // foreign key is deliberately non-cascading.
            var relatedInquiries = await context.Inquiries
                .Where(inquiry => inquiry.GroupId == group.Id)
                .ToListAsync(cancellationToken);

            foreach (var inquiry in relatedInquiries)
            {
                inquiry.GroupId = null;
            }

            context.Groups.Remove(group);
        }

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.GroupsTag, cancellationToken);

        return Unit.Value;
    }
}
