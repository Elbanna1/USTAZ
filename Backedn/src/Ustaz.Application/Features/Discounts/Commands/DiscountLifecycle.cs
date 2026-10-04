using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Discounts.Commands;

/// <summary>Turns a discount code on so it can be redeemed again.</summary>
public sealed record ActivateDiscountCommand(Guid DiscountId) : IRequest<Unit>;

public sealed class ActivateDiscountCommandValidator : AbstractValidator<ActivateDiscountCommand>
{
    public ActivateDiscountCommandValidator() => RuleFor(command => command.DiscountId).NotEmpty();
}

public sealed class ActivateDiscountCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ActivateDiscountCommand, Unit>
{
    public async Task<Unit> Handle(ActivateDiscountCommand command, CancellationToken cancellationToken)
    {
        var discount = await context.Discounts
            .FirstOrDefaultAsync(entity => entity.Id == command.DiscountId, cancellationToken)
            ?? throw new NotFoundException("Discount", command.DiscountId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, discount.TeacherId);

        discount.Activate(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed record DeactivateDiscountCommand(Guid DiscountId) : IRequest<Unit>;

public sealed class DeactivateDiscountCommandValidator : AbstractValidator<DeactivateDiscountCommand>
{
    public DeactivateDiscountCommandValidator() => RuleFor(command => command.DiscountId).NotEmpty();
}

public sealed class DeactivateDiscountCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<DeactivateDiscountCommand, Unit>
{
    public async Task<Unit> Handle(DeactivateDiscountCommand command, CancellationToken cancellationToken)
    {
        var discount = await context.Discounts
            .FirstOrDefaultAsync(entity => entity.Id == command.DiscountId, cancellationToken)
            ?? throw new NotFoundException("Discount", command.DiscountId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, discount.TeacherId);

        discount.Deactivate(dateTimeProvider.UtcNow);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>
/// Removes a discount. Codes that were already redeemed are deactivated instead, because
/// existing enrollments reference them.
/// </summary>
public sealed record DeleteDiscountCommand(Guid DiscountId) : IRequest<Unit>;

public sealed class DeleteDiscountCommandValidator : AbstractValidator<DeleteDiscountCommand>
{
    public DeleteDiscountCommandValidator() => RuleFor(command => command.DiscountId).NotEmpty();
}

public sealed class DeleteDiscountCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<DeleteDiscountCommand, Unit>
{
    public async Task<Unit> Handle(DeleteDiscountCommand command, CancellationToken cancellationToken)
    {
        var discount = await context.Discounts
            .FirstOrDefaultAsync(entity => entity.Id == command.DiscountId, cancellationToken)
            ?? throw new NotFoundException("Discount", command.DiscountId);

        OwnershipGuard.EnsureTeacherOwns(currentUser, discount.TeacherId);

        if (discount.UsedCount > 0)
        {
            discount.Deactivate(dateTimeProvider.UtcNow);
        }
        else
        {
            context.Discounts.Remove(discount);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
