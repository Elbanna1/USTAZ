using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Groups.Commands;

internal static class GroupCurriculumHelper
{
    public static async Task<Group> LoadOwnedGroupAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .FirstOrDefaultAsync(entity => entity.Id == groupId, cancellationToken)
            ?? throw new NotFoundException("Group", groupId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, group.TeacherId);
        return group;
    }

    public static GroupCurriculumDto ToDto(GroupCurriculum item) => new()
    {
        Id = item.Id,
        GroupId = item.GroupId,
        Title = item.Title,
        Description = item.Description,
        SortOrder = item.SortOrder
    };
}

public sealed record AddGroupCurriculumCommand : IRequest<GroupCurriculumDto>
{
    public Guid GroupId { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public int SortOrder { get; init; }
}

public sealed class AddGroupCurriculumCommandValidator : AbstractValidator<AddGroupCurriculumCommand>
{
    public AddGroupCurriculumCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddGroupCurriculumCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<AddGroupCurriculumCommand, GroupCurriculumDto>
{
    public async Task<GroupCurriculumDto> Handle(AddGroupCurriculumCommand command, CancellationToken cancellationToken)
    {
        var group = await GroupCurriculumHelper.LoadOwnedGroupAsync(context, currentUser, command.GroupId, cancellationToken);

        var nextOrder = command.SortOrder > 0
            ? command.SortOrder
            : await context.GroupCurriculums
                .Where(item => item.GroupId == group.Id)
                .CountAsync(cancellationToken) + 1;

        var item = new GroupCurriculum
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            Title = command.Title.Trim(),
            Description = command.Description?.Trim(),
            SortOrder = nextOrder,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await context.GroupCurriculums.AddAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);

        return GroupCurriculumHelper.ToDto(item);
    }
}

public sealed record UpdateGroupCurriculumCommand : IRequest<GroupCurriculumDto>
{
    public Guid GroupId { get; init; }

    public Guid CurriculumId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public int? SortOrder { get; init; }
}

public sealed class UpdateGroupCurriculumCommandValidator : AbstractValidator<UpdateGroupCurriculumCommand>
{
    public UpdateGroupCurriculumCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.CurriculumId).NotEmpty();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.SortOrder!.Value).GreaterThanOrEqualTo(0).When(command => command.SortOrder.HasValue);
    }
}

public sealed class UpdateGroupCurriculumCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<UpdateGroupCurriculumCommand, GroupCurriculumDto>
{
    public async Task<GroupCurriculumDto> Handle(UpdateGroupCurriculumCommand command, CancellationToken cancellationToken)
    {
        var group = await GroupCurriculumHelper.LoadOwnedGroupAsync(context, currentUser, command.GroupId, cancellationToken);

        var item = await context.GroupCurriculums
            .FirstOrDefaultAsync(
                entity => entity.Id == command.CurriculumId && entity.GroupId == group.Id,
                cancellationToken)
            ?? throw new NotFoundException("GroupCurriculum", command.CurriculumId);

        if (command.Title is not null)
        {
            item.Title = command.Title.Trim();
        }

        if (command.Description is not null)
        {
            item.Description = command.Description.Trim();
        }

        if (command.SortOrder.HasValue)
        {
            item.SortOrder = command.SortOrder.Value;
        }

        item.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);

        return GroupCurriculumHelper.ToDto(item);
    }
}

public sealed record DeleteGroupCurriculumCommand(Guid GroupId, Guid CurriculumId) : IRequest<Unit>;

public sealed class DeleteGroupCurriculumCommandValidator : AbstractValidator<DeleteGroupCurriculumCommand>
{
    public DeleteGroupCurriculumCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.CurriculumId).NotEmpty();
    }
}

public sealed class DeleteGroupCurriculumCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ICacheService cache) : IRequestHandler<DeleteGroupCurriculumCommand, Unit>
{
    public async Task<Unit> Handle(DeleteGroupCurriculumCommand command, CancellationToken cancellationToken)
    {
        var group = await GroupCurriculumHelper.LoadOwnedGroupAsync(context, currentUser, command.GroupId, cancellationToken);

        var item = await context.GroupCurriculums
            .FirstOrDefaultAsync(
                entity => entity.Id == command.CurriculumId && entity.GroupId == group.Id,
                cancellationToken)
            ?? throw new NotFoundException("GroupCurriculum", command.CurriculumId);

        context.GroupCurriculums.Remove(item);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);

        return Unit.Value;
    }
}
