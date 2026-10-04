using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Discounts.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Discounts.Commands;

internal static class DiscountMapper
{
    public static DiscountDto ToDto(Discount discount, DateTime utcNow) => new()
    {
        Id = discount.Id,
        TeacherId = discount.TeacherId,
        Code = discount.Code,
        Description = discount.Description,
        Type = discount.Type,
        Value = discount.Value,
        MinimumAmount = discount.MinimumAmount,
        MaximumDiscount = discount.MaximumDiscount,
        StartDate = discount.StartDate,
        EndDate = discount.EndDate,
        UsageLimit = discount.UsageLimit,
        UsedCount = discount.UsedCount,
        IsActive = discount.IsActive,
        IsCurrentlyValid = discount.IsActive
            && discount.HasStarted(utcNow)
            && !discount.IsExpired(utcNow)
            && !discount.HasReachedUsageLimit,
        CreatedAt = discount.CreatedAt
    };
}

public sealed record CreateDiscountCommand : IRequest<DiscountDto>
{
    public required string Code { get; init; }

    public string? Description { get; init; }

    public DiscountType Type { get; init; } = DiscountType.Percentage;

    public decimal Value { get; init; }

    public decimal? MinimumAmount { get; init; }

    public decimal? MaximumDiscount { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public int? UsageLimit { get; init; }
}

public sealed class CreateDiscountCommandValidator : AbstractValidator<CreateDiscountCommand>
{
    public CreateDiscountCommandValidator()
    {
        RuleFor(command => command.Code)
            .NotEmpty().WithMessage("A discount code is required.")
            .MinimumLength(3).WithMessage("A discount code must be at least 3 characters long.")
            .MaximumLength(50)
            .Matches("^[A-Za-z0-9_-]+$")
            .WithMessage("A discount code may only contain letters, digits, hyphens and underscores.");

        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Type).IsInEnum();

        RuleFor(command => command.Value)
            .GreaterThan(0).WithMessage("The discount value must be greater than zero.");

        RuleFor(command => command.Value)
            .LessThanOrEqualTo(DomainRules.MaxPercentageDiscount)
            .When(command => command.Type == DiscountType.Percentage)
            .WithMessage("A percentage discount cannot exceed 100%.");

        RuleFor(command => command.Value)
            .LessThanOrEqualTo(DomainRules.MaxPrice)
            .When(command => command.Type == DiscountType.FixedAmount);

        RuleFor(command => command.MinimumAmount!.Value)
            .GreaterThanOrEqualTo(0)
            .When(command => command.MinimumAmount.HasValue);

        RuleFor(command => command.MaximumDiscount!.Value)
            .GreaterThan(0)
            .When(command => command.MaximumDiscount.HasValue);

        RuleFor(command => command.UsageLimit!.Value)
            .GreaterThan(0)
            .When(command => command.UsageLimit.HasValue);

        RuleFor(command => command.StartDate).NotEmpty();

        RuleFor(command => command.EndDate!.Value)
            .GreaterThan(command => command.StartDate)
            .When(command => command.EndDate.HasValue)
            .WithMessage("The end date must be after the start date.");
    }
}

public sealed class CreateDiscountCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CreateDiscountCommand, DiscountDto>
{
    public async Task<DiscountDto> Handle(CreateDiscountCommand command, CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var code = command.Code.Trim().ToUpperInvariant();

        var exists = await context.Discounts.AnyAsync(
            discount => discount.TeacherId == teacherId && discount.Code == code, cancellationToken);

        if (exists)
        {
            throw new ConflictException("You already have a discount with the code " + code + ".");
        }

        var utcNow = dateTimeProvider.UtcNow;

        var discount = new Discount
        {
            Id = Guid.NewGuid(),
            TenantId = await Groups.Commands.GroupRules.ResolveTenantIdAsync(context, teacherId, cancellationToken),
            TeacherId = teacherId,
            Code = code,
            Description = command.Description?.Trim(),
            Type = command.Type,
            Value = command.Value,
            MinimumAmount = command.MinimumAmount,
            MaximumDiscount = command.MaximumDiscount,
            StartDate = DateTime.SpecifyKind(command.StartDate, DateTimeKind.Utc),
            EndDate = command.EndDate.HasValue
                ? DateTime.SpecifyKind(command.EndDate.Value, DateTimeKind.Utc)
                : null,
            UsageLimit = command.UsageLimit,
            UsedCount = 0,
            IsActive = true,
            CreatedAt = utcNow
        };

        await context.Discounts.AddAsync(discount, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return DiscountMapper.ToDto(discount, utcNow);
    }
}

public sealed record UpdateDiscountCommand : IRequest<DiscountDto>
{
    public Guid DiscountId { get; init; }

    public string? Description { get; init; }

    public decimal? Value { get; init; }

    public decimal? MinimumAmount { get; init; }

    public decimal? MaximumDiscount { get; init; }

    public DateTime? StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public int? UsageLimit { get; init; }
}

public sealed class UpdateDiscountCommandValidator : AbstractValidator<UpdateDiscountCommand>
{
    public UpdateDiscountCommandValidator()
    {
        RuleFor(command => command.DiscountId).NotEmpty();
        RuleFor(command => command.Description).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Value!.Value).GreaterThan(0).When(command => command.Value.HasValue);
        RuleFor(command => command.MinimumAmount!.Value).GreaterThanOrEqualTo(0).When(command => command.MinimumAmount.HasValue);
        RuleFor(command => command.MaximumDiscount!.Value).GreaterThan(0).When(command => command.MaximumDiscount.HasValue);
        RuleFor(command => command.UsageLimit!.Value).GreaterThan(0).When(command => command.UsageLimit.HasValue);
    }
}

public sealed class UpdateDiscountCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateDiscountCommand, DiscountDto>
{
    public async Task<DiscountDto> Handle(UpdateDiscountCommand command, CancellationToken cancellationToken)
    {
        var discount = await context.Discounts
            .FirstOrDefaultAsync(entity => entity.Id == command.DiscountId, cancellationToken)
            ?? throw new NotFoundException("Discount", command.DiscountId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, discount.TeacherId);

        if (command.Description is not null)
        {
            discount.Description = command.Description.Trim();
        }

        if (command.Value.HasValue)
        {
            if (discount.Type == DiscountType.Percentage && command.Value.Value > DomainRules.MaxPercentageDiscount)
            {
                throw new BusinessRuleException("INVALID_DISCOUNT_VALUE", "A percentage discount cannot exceed 100%.");
            }

            discount.Value = command.Value.Value;
        }

        if (command.MinimumAmount.HasValue)
        {
            discount.MinimumAmount = command.MinimumAmount.Value;
        }

        if (command.MaximumDiscount.HasValue)
        {
            discount.MaximumDiscount = command.MaximumDiscount.Value;
        }

        if (command.StartDate.HasValue)
        {
            discount.StartDate = DateTime.SpecifyKind(command.StartDate.Value, DateTimeKind.Utc);
        }

        if (command.EndDate.HasValue)
        {
            discount.EndDate = DateTime.SpecifyKind(command.EndDate.Value, DateTimeKind.Utc);
        }

        if (command.UsageLimit.HasValue)
        {
            if (command.UsageLimit.Value < discount.UsedCount)
            {
                throw new BusinessRuleException(
                    "USAGE_LIMIT_BELOW_USED",
                    "The usage limit cannot be lower than the " + discount.UsedCount + " redemptions already made.");
            }

            discount.UsageLimit = command.UsageLimit.Value;
        }

        if (discount.EndDate.HasValue && discount.EndDate.Value <= discount.StartDate)
        {
            throw new BusinessRuleException("INVALID_DATE_RANGE", "The end date must be after the start date.");
        }

        var utcNow = dateTimeProvider.UtcNow;
        discount.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        return DiscountMapper.ToDto(discount, utcNow);
    }
}
