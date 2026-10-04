using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Billing.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Billing;

/// <summary>Issues the next sequential invoice/reference numbers for USTAZ SaaS billing.</summary>
internal static class BillingNumbering
{
    public static async Task<string> NextInvoiceNumberAsync(
        IApplicationDbContext context,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var prefix = "USTAZ-" + utcNow.Year + "-";

        var lastNumber = await context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Number.StartsWith(prefix))
            .OrderByDescending(invoice => invoice.Number)
            .Select(invoice => invoice.Number)
            .FirstOrDefaultAsync(cancellationToken);

        var sequence = 1;

        if (lastNumber is not null && int.TryParse(lastNumber[prefix.Length..], out var parsed))
        {
            sequence = parsed + 1;
        }

        return prefix + sequence.ToString("D6");
    }
}

/// <summary>
/// Starts a payment for the requested plan: issues an invoice and a pending transaction, and
/// returns the reference the provider will echo back.
///
/// Deliberately does NOT activate the subscription. Activation only happens when a verified
/// provider webhook arrives, so a client cannot grant itself a paid plan by calling this.
/// </summary>
public sealed record CreateCheckoutCommand : IRequest<CheckoutSessionDto>
{
    public Guid PlanId { get; init; }

    public BillingCycle BillingCycle { get; init; } = BillingCycle.Monthly;

    public string Provider { get; init; } = "manual";
}

public sealed class CreateCheckoutCommandValidator : AbstractValidator<CreateCheckoutCommand>
{
    public CreateCheckoutCommandValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.BillingCycle).IsInEnum();
        RuleFor(command => command.Provider).NotEmpty().MaximumLength(50);
    }
}

public sealed class CreateCheckoutCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<CreateCheckoutCommand, CheckoutSessionDto>
{
    private static readonly TimeSpan CheckoutValidity = TimeSpan.FromHours(2);

    public async Task<CheckoutSessionDto> Handle(CreateCheckoutCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingManage, cancellationToken: cancellationToken);

        var plan = await context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == command.PlanId && entity.IsActive, cancellationToken)
            ?? throw new NotFoundException("SubscriptionPlan", command.PlanId);

        var utcNow = dateTimeProvider.UtcNow;
        var amount = plan.PriceFor(command.BillingCycle);

        var subscription = await context.TenantSubscriptions
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            TenantSubscriptionId = subscription?.Id,
            Number = await BillingNumbering.NextInvoiceNumberAsync(context, utcNow, cancellationToken),
            Status = InvoiceStatus.Issued,
            Subtotal = amount,
            TaxAmount = 0m,
            Total = amount,
            Currency = plan.Currency,
            IssuedAt = utcNow,
            DueAt = utcNow.Add(CheckoutValidity),
            Description = plan.NameEn + " (" + command.BillingCycle + ")",
            PeriodStart = utcNow,
            PeriodEnd = command.BillingCycle == BillingCycle.Yearly ? utcNow.AddYears(1) : utcNow.AddMonths(1),
            CreatedAt = utcNow
        };

        // The reference travels to the provider and comes back on the webhook, which is how
        // an incoming payment is matched to this tenant, plan and cycle.
        var reference = "ustaz-" + invoice.Number + "-" + plan.Code + "-" + command.BillingCycle;

        var transaction = new BillingTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            TenantSubscriptionId = subscription?.Id,
            InvoiceId = invoice.Id,
            Amount = amount,
            Currency = plan.Currency,
            Status = PaymentStatus.Pending,
            Provider = command.Provider.Trim().ToLowerInvariant(),
            ProviderReference = reference,
            CreatedAt = utcNow
        };

        await context.Invoices.AddAsync(invoice, cancellationToken);
        await context.BillingTransactions.AddAsync(transaction, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.SubscriptionChanged,
            nameof(Invoice),
            invoice.Id,
            newValues: new { invoice.Number, invoice.Total, Plan = plan.Code },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return new CheckoutSessionDto
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number,
            TransactionId = transaction.Id,
            Amount = amount,
            Currency = plan.Currency,
            Provider = transaction.Provider,
            ProviderReference = reference,
            CheckoutUrl = null,
            ExpiresAt = invoice.DueAt!.Value
        };
    }
}

/// <summary>
/// Switches plan. A downgrade is refused while current usage exceeds the target plan's
/// quotas, so a tenant can never end up over their limits.
/// </summary>
public sealed record ChangePlanCommand : IRequest<TenantSubscriptionDto>
{
    public Guid PlanId { get; init; }

    public BillingCycle BillingCycle { get; init; } = BillingCycle.Monthly;
}

public sealed class ChangePlanCommandValidator : AbstractValidator<ChangePlanCommand>
{
    public ChangePlanCommandValidator()
    {
        RuleFor(command => command.PlanId).NotEmpty();
        RuleFor(command => command.BillingCycle).IsInEnum();
    }
}

public sealed class ChangePlanCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<ChangePlanCommand, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(ChangePlanCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingManage, cancellationToken: cancellationToken);

        var plan = await context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == command.PlanId && entity.IsActive, cancellationToken)
            ?? throw new NotFoundException("SubscriptionPlan", command.PlanId);

        var subscription = await context.TenantSubscriptions
            .Include(entity => entity.Plan)
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("This workspace has no subscription to change.");

        await EnsureDowngradeIsSafeAsync(access.TenantId, plan, cancellationToken);

        var previousCode = subscription.Plan.Code;
        subscription.ChangePlan(plan, command.BillingCycle, dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.SubscriptionChanged,
            nameof(TenantSubscription),
            subscription.Id,
            new { Plan = previousCode },
            new { Plan = plan.Code, command.BillingCycle },
            access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetMySubscriptionQuery(), cancellationToken);
    }

    /// <summary>Refuses a plan change that would leave the tenant already over a quota.</summary>
    private async Task EnsureDowngradeIsSafeAsync(
        Guid tenantId,
        SubscriptionPlan target,
        CancellationToken cancellationToken)
    {
        var usage = await planLimits.GetAllUsageAsync(tenantId, cancellationToken);

        foreach (var entry in usage)
        {
            var limit = entry.Quota switch
            {
                PlanQuota.Students => target.MaxStudents,
                PlanQuota.Groups => target.MaxGroups,
                PlanQuota.Courses => target.MaxCourses,
                PlanQuota.Assistants => target.MaxAssistants,
                PlanQuota.TeamMembers => target.MaxTeamMembers,
                _ => null
            };

            if (limit.HasValue && entry.Used > limit.Value)
            {
                throw new BusinessRuleException(
                    "PLAN_LIMIT_REACHED",
                    "You are using " + entry.Used + " " + entry.Quota
                        + " but the " + target.NameEn + " plan allows only " + limit.Value
                        + ". Reduce your usage before downgrading.");
            }
        }
    }
}

public sealed record CancelSubscriptionCommand : IRequest<TenantSubscriptionDto>
{
    /// <summary>When false (the default) access continues until the paid period ends.</summary>
    public bool Immediately { get; init; }

    public string? Reason { get; init; }
}

public sealed class CancelSubscriptionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<CancelSubscriptionCommand, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(
        CancelSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingManage, cancellationToken: cancellationToken);

        var subscription = await context.TenantSubscriptions
            .Include(entity => entity.Plan)
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("This workspace has no subscription to cancel.");

        subscription.Cancel(dateTimeProvider.UtcNow, command.Immediately, command.Reason?.Trim());

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.SubscriptionCanceled,
            nameof(TenantSubscription),
            subscription.Id,
            newValues: new { command.Immediately, command.Reason },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetMySubscriptionQuery(), cancellationToken);
    }
}

public sealed record ReactivateSubscriptionCommand : IRequest<TenantSubscriptionDto>;

public sealed class ReactivateSubscriptionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<ReactivateSubscriptionCommand, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(
        ReactivateSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingManage, cancellationToken: cancellationToken);

        var subscription = await context.TenantSubscriptions
            .Include(entity => entity.Plan)
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("This workspace has no subscription to reactivate.");

        subscription.Reactivate(dateTimeProvider.UtcNow);

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.SubscriptionReactivated,
            nameof(TenantSubscription),
            subscription.Id,
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetMySubscriptionQuery(), cancellationToken);
    }
}
