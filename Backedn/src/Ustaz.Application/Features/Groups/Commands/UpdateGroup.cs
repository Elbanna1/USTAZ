using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Groups.Commands;

/// <summary>Partial update of a group the caller owns. Only supplied fields change.</summary>
public sealed record UpdateGroupCommand : IRequest<GroupDetailsDto>
{
    public Guid GroupId { get; init; }

    public Guid? SubjectId { get; init; }

    public Guid? GradeId { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public GroupType? Type { get; init; }

    public TeachingMode? TeachingMode { get; init; }

    public string? Location { get; init; }

    public decimal? MonthlyPrice { get; init; }

    public decimal? OriginalPrice { get; init; }

    public int? DiscountPercentage { get; init; }

    public int? Capacity { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public string? WhatsAppLink { get; init; }

    public bool? IsActive { get; init; }
}

public sealed class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
{
    public UpdateGroupCommandValidator()
    {
        RuleFor(command => command.GroupId).NotEmpty();
        RuleFor(command => command.Name).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Description).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.Location).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Type).IsInEnum().When(command => command.Type.HasValue);
        RuleFor(command => command.TeachingMode).IsInEnum().When(command => command.TeachingMode.HasValue);
        RuleFor(command => command.MonthlyPrice!.Value).Money().When(command => command.MonthlyPrice.HasValue);
        RuleFor(command => command.OriginalPrice!.Value).Money().When(command => command.OriginalPrice.HasValue);

        RuleFor(command => command.DiscountPercentage!.Value)
            .InclusiveBetween(0, DomainRules.MaxPercentageDiscount)
            .When(command => command.DiscountPercentage.HasValue);

        RuleFor(command => command.Capacity!.Value)
            .InclusiveBetween(DomainRules.MinGroupCapacity, DomainRules.MaxGroupCapacity)
            .When(command => command.Capacity.HasValue);

        RuleFor(command => command)
            .Must(command => !command.StartDate.HasValue || !command.EndDate.HasValue || command.EndDate > command.StartDate)
            .WithMessage("The end date must be after the start date.")
            .WithName("EndDate");
    }
}

public sealed class UpdateGroupCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IWhatsAppLinkService whatsAppLinkService,
    ICacheService cache,
    IMediator mediator) : IRequestHandler<UpdateGroupCommand, GroupDetailsDto>
{
    public async Task<GroupDetailsDto> Handle(UpdateGroupCommand command, CancellationToken cancellationToken)
    {
        var group = await context.Groups
            .FirstOrDefaultAsync(entity => entity.Id == command.GroupId, cancellationToken)
            ?? throw new NotFoundException("Group", command.GroupId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, group.TeacherId);

        var subjectId = command.SubjectId ?? group.SubjectId;
        var gradeId = command.GradeId ?? group.GradeId;

        if (subjectId != group.SubjectId || gradeId != group.GradeId)
        {
            await GroupRules.EnsureTeacherTeachesAsync(context, group.TeacherId, subjectId, gradeId, cancellationToken);
            group.SubjectId = subjectId;
            group.GradeId = gradeId;
        }

        if (command.Capacity.HasValue && command.Capacity.Value < group.EnrolledStudentsCount)
        {
            throw new BusinessRuleException(
                "CAPACITY_BELOW_ENROLLED",
                "Capacity cannot be lower than the " + group.EnrolledStudentsCount + " students already enrolled.");
        }

        if (command.Name is not null)
        {
            group.Name = command.Name.Trim();
        }

        if (command.Description is not null)
        {
            group.Description = command.Description.Trim();
        }

        if (command.Type.HasValue)
        {
            group.Type = command.Type.Value;
        }

        if (command.TeachingMode.HasValue)
        {
            group.TeachingMode = command.TeachingMode.Value;
        }

        if (command.Location is not null)
        {
            group.Location = command.Location.Trim();
        }

        if (command.MonthlyPrice.HasValue)
        {
            group.MonthlyPrice = command.MonthlyPrice.Value;
        }

        if (command.OriginalPrice.HasValue)
        {
            group.OriginalPrice = command.OriginalPrice.Value;
        }

        if (command.DiscountPercentage.HasValue)
        {
            group.DiscountPercentage = command.DiscountPercentage.Value;
        }

        if (command.Capacity.HasValue)
        {
            group.Capacity = command.Capacity.Value;
        }

        if (command.StartDate.HasValue)
        {
            group.StartDate = DateTime.SpecifyKind(command.StartDate.Value, DateTimeKind.Utc);
        }

        if (command.EndDate.HasValue)
        {
            group.EndDate = DateTime.SpecifyKind(command.EndDate.Value, DateTimeKind.Utc);
        }

        if (command.WhatsAppLink is not null)
        {
            group.WhatsAppLink = string.IsNullOrWhiteSpace(command.WhatsAppLink)
                ? null
                : whatsAppLinkService.NormalizeGroupInviteLink(command.WhatsAppLink);
        }

        if (command.IsActive.HasValue)
        {
            group.IsActive = command.IsActive.Value;

            if (!group.IsActive)
            {
                group.IsPublished = false;
            }
        }

        if (group.TeachingMode != TeachingMode.Online && string.IsNullOrWhiteSpace(group.Location))
        {
            throw new BusinessRuleException(
                "LOCATION_REQUIRED",
                "Offline and hybrid groups require a location.");
        }

        group.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.GroupDetails(group.Id), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.GroupsTag, cancellationToken);

        return await mediator.Send(new GetGroupByIdQuery(group.Id), cancellationToken);
    }
}
