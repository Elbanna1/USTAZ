using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Discounts.Commands;
using Ustaz.Application.Features.Discounts.Dtos;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Discounts.Queries;

/// <summary>The calling teacher's promo codes.</summary>
/// <summary>
/// One teacher's discount codes, or every code on the platform when an administrator asks.
/// </summary>
public sealed record GetTeacherDiscountsQuery : PagedRequest, IRequest<PaginatedList<DiscountDto>>
{
    public bool? IsActive { get; init; }

    public bool? CurrentlyValidOnly { get; init; }

    /// <summary>Narrows a platform-wide listing to one teacher. Ignored for a teacher caller.</summary>
    public Guid? TeacherId { get; init; }
}

public sealed class GetTeacherDiscountsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetTeacherDiscountsQuery, PaginatedList<DiscountDto>>
{
    public async Task<PaginatedList<DiscountDto>> Handle(
        GetTeacherDiscountsQuery query,
        CancellationToken cancellationToken)
    {
        var scopedTeacherId = currentUser.IsAdmin && currentUser.TeacherId is null
            ? query.TeacherId
            : currentUser.RequireTeacherId();

        var utcNow = dateTimeProvider.UtcNow;
        var search = query.Search?.Trim().ToUpperInvariant();

        var discounts = context.Discounts
            .AsNoTracking()
            .WhereIf(scopedTeacherId.HasValue, discount => discount.TeacherId == scopedTeacherId!.Value)
            .WhereIf(query.IsActive.HasValue, discount => discount.IsActive == query.IsActive!.Value)
            .WhereIf(query.CurrentlyValidOnly == true, discount =>
                discount.IsActive
                && discount.StartDate <= utcNow
                && (discount.EndDate == null || discount.EndDate >= utcNow)
                && (discount.UsageLimit == null || discount.UsedCount < discount.UsageLimit))
            .WhereIf(!string.IsNullOrWhiteSpace(search), discount => discount.Code.Contains(search!))
            .OrderByDescending(discount => discount.CreatedAt);

        var page = await discounts.ToPaginatedListAsync(query, cancellationToken);

        return page.Map(discount => DiscountMapper.ToDto(discount, utcNow));
    }
}

public sealed record GetDiscountByIdQuery(Guid DiscountId) : IRequest<DiscountDto>;

public sealed class GetDiscountByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetDiscountByIdQuery, DiscountDto>
{
    public async Task<DiscountDto> Handle(GetDiscountByIdQuery query, CancellationToken cancellationToken)
    {
        var discount = await context.Discounts
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == query.DiscountId, cancellationToken)
            ?? throw new NotFoundException("Discount", query.DiscountId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, discount.TeacherId);

        return DiscountMapper.ToDto(discount, dateTimeProvider.UtcNow);
    }
}

/// <summary>
/// Validates a promo code against a specific group/course basket before checkout.
/// Never mutates the usage counter.
/// </summary>
public sealed record ValidateDiscountQuery : IRequest<DiscountValidationResultDto>
{
    public required string Code { get; init; }

    public Guid? GroupId { get; init; }

    public Guid? CourseId { get; init; }

    public Guid? TeacherId { get; init; }

    public decimal? Amount { get; init; }
}

public sealed class ValidateDiscountQueryValidator : AbstractValidator<ValidateDiscountQuery>
{
    public ValidateDiscountQueryValidator()
    {
        RuleFor(query => query.Code).NotEmpty().MaximumLength(50);
        RuleFor(query => query.Amount!.Value).GreaterThanOrEqualTo(0).When(query => query.Amount.HasValue);

        RuleFor(query => query)
            .Must(query => query.GroupId.HasValue || query.CourseId.HasValue || (query.TeacherId.HasValue && query.Amount.HasValue))
            .WithMessage("Provide a groupId, a courseId, or both a teacherId and an amount.")
            .WithName("GroupId");
    }
}

public sealed class ValidateDiscountQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ValidateDiscountQuery, DiscountValidationResultDto>
{
    public async Task<DiscountValidationResultDto> Handle(
        ValidateDiscountQuery query,
        CancellationToken cancellationToken)
    {
        var code = query.Code.Trim().ToUpperInvariant();
        var utcNow = dateTimeProvider.UtcNow;

        var (teacherId, amount) = await ResolveBasketAsync(query, cancellationToken);

        var discount = await context.Discounts
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TeacherId == teacherId && entity.Code == code, cancellationToken);

        if (discount is null)
        {
            return new DiscountValidationResultDto
            {
                IsValid = false,
                FailureReason = "This discount code does not exist for this teacher.",
                Code = code,
                OriginalAmount = amount,
                FinalAmount = amount
            };
        }

        if (!discount.CanBeRedeemed(utcNow, amount, out var failureReason))
        {
            return new DiscountValidationResultDto
            {
                IsValid = false,
                FailureReason = failureReason,
                Code = code,
                Type = discount.Type,
                OriginalAmount = amount,
                FinalAmount = amount
            };
        }

        var discountAmount = discount.CalculateDiscountAmount(amount);

        return new DiscountValidationResultDto
        {
            IsValid = true,
            Code = code,
            Type = discount.Type,
            OriginalAmount = amount,
            DiscountAmount = discountAmount,
            FinalAmount = Math.Round(amount - discountAmount, 2, MidpointRounding.AwayFromZero)
        };
    }

    private async Task<(Guid TeacherId, decimal Amount)> ResolveBasketAsync(
        ValidateDiscountQuery query,
        CancellationToken cancellationToken)
    {
        if (query.GroupId.HasValue)
        {
            var group = await context.Groups
                .AsNoTracking()
                .Where(entity => entity.Id == query.GroupId.Value)
                .Select(entity => new { entity.TeacherId, entity.MonthlyPrice })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Group", query.GroupId.Value);

            return (group.TeacherId, query.Amount ?? group.MonthlyPrice);
        }

        if (query.CourseId.HasValue)
        {
            var course = await context.Courses
                .AsNoTracking()
                .Where(entity => entity.Id == query.CourseId.Value)
                .Select(entity => new { entity.TeacherId, entity.Price })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Course", query.CourseId.Value);

            return (course.TeacherId, query.Amount ?? course.Price);
        }

        return (query.TeacherId!.Value, query.Amount!.Value);
    }
}
