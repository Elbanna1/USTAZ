using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Groups.Commands;

internal static class GroupScheduleHelper
{
    public static async Task<Group> LoadOwnedGroupAsync(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .Include(entity => entity.Schedules)
            .FirstOrDefaultAsync(entity => entity.Id == groupId, cancellationToken)
            ?? throw new NotFoundException("Group", groupId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, group.TeacherId);
        return group;
    }

    public static GroupScheduleDto ToDto(GroupSchedule schedule) => new()
    {
        Id = schedule.Id,
        GroupId = schedule.GroupId,
        DayOfWeek = schedule.DayOfWeek,
        StartTime = schedule.StartTime,
        EndTime = schedule.EndTime,
        Location = schedule.Location,
        MeetingUrl = schedule.MeetingUrl,
        IsActive = schedule.IsActive
    };
}

public sealed record AddGroupScheduleCommand : IRequest<GroupScheduleDto>
{
    public Guid GroupId { get; init; }

    public DayOfWeek DayOfWeek { get; init; }

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public string? Location { get; init; }

    public string? MeetingUrl { get; init; }
}

public sealed class AddGroupScheduleCommandValidator : AbstractValidator<AddGroupScheduleCommand>
{
    public AddGroupScheduleCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.DayOfWeek).IsInEnum();
        RuleFor(command => command.Location).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.MeetingUrl).OptionalHttpUrl();

        RuleFor(command => command.EndTime)
            .GreaterThan(command => command.StartTime)
            .WithMessage("The session end time must be after its start time.");
    }
}

public sealed class AddGroupScheduleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    ICacheService cache) : IRequestHandler<AddGroupScheduleCommand, GroupScheduleDto>
{
    public async Task<GroupScheduleDto> Handle(AddGroupScheduleCommand command, CancellationToken cancellationToken)
    {
        var group = await GroupScheduleHelper.LoadOwnedGroupAsync(context, currentUser, command.GroupId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(command.MeetingUrl))
        {
            urlGuard.EnsureSafeHttpUrl(command.MeetingUrl, "Meeting URL");
        }

        var schedule = new GroupSchedule
        {
            Id = Guid.NewGuid(),
            GroupId = group.Id,
            DayOfWeek = command.DayOfWeek,
            StartTime = command.StartTime,
            EndTime = command.EndTime,
            Location = command.Location?.Trim() ?? group.Location,
            MeetingUrl = command.MeetingUrl?.Trim(),
            IsActive = true,
            CreatedAt = dateTimeProvider.UtcNow
        };

        group.Schedules.Add(schedule);
        GroupRules.EnsureNoOverlappingSessions(group.Schedules);

        group.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);

        return GroupScheduleHelper.ToDto(schedule);
    }
}

public sealed record UpdateGroupScheduleCommand : IRequest<GroupScheduleDto>
{
    public Guid GroupId { get; init; }

    public Guid ScheduleId { get; init; }

    public DayOfWeek? DayOfWeek { get; init; }

    public TimeOnly? StartTime { get; init; }

    public TimeOnly? EndTime { get; init; }

    public string? Location { get; init; }

    public string? MeetingUrl { get; init; }

    public bool? IsActive { get; init; }
}

public sealed class UpdateGroupScheduleCommandValidator : AbstractValidator<UpdateGroupScheduleCommand>
{
    public UpdateGroupScheduleCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.ScheduleId).NotEmpty();
        RuleFor(command => command.DayOfWeek).IsInEnum().When(command => command.DayOfWeek.HasValue);
        RuleFor(command => command.Location).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.MeetingUrl).OptionalHttpUrl();
    }
}

public sealed class UpdateGroupScheduleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IUrlGuard urlGuard,
    ICacheService cache) : IRequestHandler<UpdateGroupScheduleCommand, GroupScheduleDto>
{
    public async Task<GroupScheduleDto> Handle(UpdateGroupScheduleCommand command, CancellationToken cancellationToken)
    {
        var group = await GroupScheduleHelper.LoadOwnedGroupAsync(context, currentUser, command.GroupId, cancellationToken);

        var schedule = group.Schedules.FirstOrDefault(item => item.Id == command.ScheduleId)
            ?? throw new NotFoundException("GroupSchedule", command.ScheduleId);

        if (!string.IsNullOrWhiteSpace(command.MeetingUrl))
        {
            urlGuard.EnsureSafeHttpUrl(command.MeetingUrl, "Meeting URL");
        }

        if (command.DayOfWeek.HasValue)
        {
            schedule.DayOfWeek = command.DayOfWeek.Value;
        }

        if (command.StartTime.HasValue)
        {
            schedule.StartTime = command.StartTime.Value;
        }

        if (command.EndTime.HasValue)
        {
            schedule.EndTime = command.EndTime.Value;
        }

        if (command.Location is not null)
        {
            schedule.Location = command.Location.Trim();
        }

        if (command.MeetingUrl is not null)
        {
            schedule.MeetingUrl = string.IsNullOrWhiteSpace(command.MeetingUrl) ? null : command.MeetingUrl.Trim();
        }

        if (command.IsActive.HasValue)
        {
            schedule.IsActive = command.IsActive.Value;
        }

        schedule.EnsureValid();
        GroupRules.EnsureNoOverlappingSessions(group.Schedules);

        schedule.UpdatedAt = dateTimeProvider.UtcNow;
        group.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);

        return GroupScheduleHelper.ToDto(schedule);
    }
}

public sealed record DeleteGroupScheduleCommand(Guid GroupId, Guid ScheduleId) : IRequest<Unit>;

public sealed class DeleteGroupScheduleCommandValidator : AbstractValidator<DeleteGroupScheduleCommand>
{
    public DeleteGroupScheduleCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.ScheduleId).NotEmpty();
    }
}

public sealed class DeleteGroupScheduleCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<DeleteGroupScheduleCommand, Unit>
{
    public async Task<Unit> Handle(DeleteGroupScheduleCommand command, CancellationToken cancellationToken)
    {
        var group = await GroupScheduleHelper.LoadOwnedGroupAsync(context, currentUser, command.GroupId, cancellationToken);

        var schedule = group.Schedules.FirstOrDefault(item => item.Id == command.ScheduleId)
            ?? throw new NotFoundException("GroupSchedule", command.ScheduleId);

        if (group.IsPublished && group.Schedules.Count(item => item.IsActive) <= 1 && schedule.IsActive)
        {
            throw new BusinessRuleException(
                "LAST_ACTIVE_SESSION",
                "A published group must keep at least one active weekly session. Unpublish the group first.");
        }

        context.GroupSchedules.Remove(schedule);
        group.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);

        return Unit.Value;
    }
}
