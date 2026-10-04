using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Features.Groups.Commands;

public sealed record GroupScheduleInput
{
    public DayOfWeek DayOfWeek { get; init; }

    public TimeOnly StartTime { get; init; }

    public TimeOnly EndTime { get; init; }

    public string? Location { get; init; }

    public string? MeetingUrl { get; init; }
}

public sealed record CreateGroupCommand : IRequest<GroupDetailsDto>
{
    public required Guid SubjectId { get; init; }

    public required Guid GradeId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public GroupType Type { get; init; } = GroupType.Regular;

    public TeachingMode TeachingMode { get; init; } = TeachingMode.Offline;

    public string? Location { get; init; }

    public decimal MonthlyPrice { get; init; }

    public decimal? OriginalPrice { get; init; }

    public int DiscountPercentage { get; init; }

    public int Capacity { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string? WhatsAppLink { get; init; }

    public IReadOnlyList<GroupScheduleInput> Schedules { get; init; } = [];
}

public sealed class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
{
    public CreateGroupCommandValidator()
    {
        RuleFor(command => command.SubjectId).NotEmpty();
        RuleFor(command => command.GradeId).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("A group name is required.")
            .MaximumLength(DomainRules.NameMaxLength);

        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Location).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.TeachingMode).IsInEnum();
        RuleFor(command => command.MonthlyPrice).Money();

        RuleFor(command => command.OriginalPrice!.Value)
            .Money()
            .GreaterThanOrEqualTo(command => command.MonthlyPrice)
            .WithMessage("The original price must be greater than or equal to the current price.")
            .When(command => command.OriginalPrice.HasValue);

        RuleFor(command => command.DiscountPercentage)
            .InclusiveBetween(0, DomainRules.MaxPercentageDiscount);

        RuleFor(command => command.Capacity)
            .InclusiveBetween(DomainRules.MinGroupCapacity, DomainRules.MaxGroupCapacity)
            .WithMessage("Capacity must be between " + DomainRules.MinGroupCapacity + " and " + DomainRules.MaxGroupCapacity + ".");

        RuleFor(command => command.StartDate).NotEmpty();

        RuleFor(command => command.EndDate!.Value)
            .GreaterThan(command => command.StartDate)
            .WithMessage("The end date must be after the start date.")
            .When(command => command.EndDate.HasValue);

        RuleFor(command => command.Location)
            .NotEmpty()
            .When(command => command.TeachingMode != TeachingMode.Online)
            .WithMessage("Offline and hybrid groups require a location.");

        RuleForEach(command => command.Schedules).ChildRules(schedule =>
        {
            schedule.RuleFor(item => item.DayOfWeek).IsInEnum();
            schedule.RuleFor(item => item.MeetingUrl).OptionalHttpUrl();
            schedule.RuleFor(item => item.EndTime)
                .GreaterThan(item => item.StartTime)
                .WithMessage("A session end time must be after its start time.");
        });
    }
}

public sealed class CreateGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IWhatsAppLinkService whatsAppLinkService,
    IUrlGuard urlGuard,
    ICacheService cache,
    IPlanLimitService planLimits,
    IMediator mediator) : IRequestHandler<CreateGroupCommand, GroupDetailsDto>
{
    public async Task<GroupDetailsDto> Handle(CreateGroupCommand command, CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var utcNow = dateTimeProvider.UtcNow;

        await GroupRules.EnsureTeacherTeachesAsync(context, teacherId, command.SubjectId, command.GradeId, cancellationToken);

        var tenantId = await GroupRules.ResolveTenantIdAsync(context, teacherId, cancellationToken);
        await planLimits.EnsureQuotaAsync(tenantId, PlanQuota.Groups, 1, cancellationToken);

        var group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TeacherId = teacherId,
            SubjectId = command.SubjectId,
            GradeId = command.GradeId,
            Name = command.Name.Trim(),
            Slug = Slug.CreateUnique(command.Name).Value,
            Description = command.Description?.Trim(),
            Type = command.Type,
            TeachingMode = command.TeachingMode,
            Location = command.Location?.Trim(),
            MonthlyPrice = command.MonthlyPrice,
            OriginalPrice = command.OriginalPrice,
            DiscountPercentage = command.DiscountPercentage,
            Capacity = command.Capacity,
            EnrolledStudentsCount = 0,
            StartDate = DateTime.SpecifyKind(command.StartDate, DateTimeKind.Utc),
            EndDate = command.EndDate.HasValue
                ? DateTime.SpecifyKind(command.EndDate.Value, DateTimeKind.Utc)
                : null,
            IsPublished = false,
            IsActive = true,
            CreatedAt = utcNow
        };

        if (!string.IsNullOrWhiteSpace(command.WhatsAppLink))
        {
            group.WhatsAppLink = whatsAppLinkService.NormalizeGroupInviteLink(command.WhatsAppLink);
        }

        foreach (var input in command.Schedules)
        {
            if (!string.IsNullOrWhiteSpace(input.MeetingUrl))
            {
                urlGuard.EnsureSafeHttpUrl(input.MeetingUrl, "Meeting URL");
            }

            group.Schedules.Add(new GroupSchedule
            {
                Id = Guid.NewGuid(),
                DayOfWeek = input.DayOfWeek,
                StartTime = input.StartTime,
                EndTime = input.EndTime,
                Location = input.Location?.Trim() ?? group.Location,
                MeetingUrl = input.MeetingUrl?.Trim(),
                IsActive = true,
                CreatedAt = utcNow
            });
        }

        GroupRules.EnsureNoOverlappingSessions(group.Schedules);

        await context.Groups.AddAsync(group, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(CacheKeys.GroupsTag, cancellationToken);

        return await mediator.Send(new GetGroupByIdQuery(group.Id), cancellationToken);
    }
}

/// <summary>Business rules shared by the group create/update paths.</summary>
public static class GroupRules
{
    /// <summary>A teacher may only open a group for a subject and grade they actually teach.</summary>
    public static async Task EnsureTeacherTeachesAsync(
        IApplicationDbContext context,
        Guid teacherId,
        Guid subjectId,
        Guid gradeId,
        CancellationToken cancellationToken)
    {
        var teachesSubject = await context.TeacherSubjects
            .AsNoTracking()
            .AnyAsync(link => link.TeacherId == teacherId && link.SubjectId == subjectId, cancellationToken);

        if (!teachesSubject)
        {
            throw new BusinessRuleException(
                "SUBJECT_NOT_TAUGHT",
                "You cannot create content for a subject that is not on your profile. Add the subject first.");
        }

        var teachesGrade = await context.TeacherGrades
            .AsNoTracking()
            .AnyAsync(link => link.TeacherId == teacherId && link.GradeId == gradeId, cancellationToken);

        if (!teachesGrade)
        {
            throw new BusinessRuleException(
                "GRADE_NOT_TAUGHT",
                "You cannot create content for an educational grade that is not on your profile. Add the grade first.");
        }
    }

    /// <summary>The workspace that owns a teacher's content.</summary>
    public static async Task<Guid> ResolveTenantIdAsync(
        IApplicationDbContext context,
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        var tenantId = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == teacherId)
            .Select(profile => profile.TenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId == Guid.Empty)
        {
            throw new NotFoundException("Teacher", teacherId);
        }

        return tenantId;
    }

    /// <summary>Two sessions of the same group must not overlap on the same weekday.</summary>
    public static void EnsureNoOverlappingSessions(IEnumerable<GroupSchedule> schedules)
    {
        var slots = schedules.Where(schedule => schedule.IsActive).ToList();

        foreach (var schedule in slots)
        {
            schedule.EnsureValid();
        }

        for (var i = 0; i < slots.Count; i++)
        {
            for (var j = i + 1; j < slots.Count; j++)
            {
                if (slots[i].ToTimeSlot().Overlaps(slots[j].ToTimeSlot()))
                {
                    throw new BusinessRuleException(
                        "SCHEDULE_OVERLAP",
                        "Two sessions of this group overlap on " + slots[i].DayOfWeek + ".");
                }
            }
        }
    }
}
