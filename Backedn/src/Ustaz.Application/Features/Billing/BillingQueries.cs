using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Billing.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Billing;

internal static class BillingProjections
{
    public static Expression<Func<SubscriptionPlan, SubscriptionPlanDto>> ToPlanDto =>
        plan => new SubscriptionPlanDto
        {
            Id = plan.Id,
            Code = plan.Code,
            NameAr = plan.NameAr,
            NameEn = plan.NameEn,
            Description = plan.Description,
            MonthlyPrice = plan.MonthlyPrice,
            YearlyPrice = plan.YearlyPrice,
            Currency = plan.Currency,
            TrialDays = plan.TrialDays,
            SortOrder = plan.SortOrder,
            IsActive = plan.IsActive,
            MaxStudents = plan.MaxStudents,
            MaxGroups = plan.MaxGroups,
            MaxCourses = plan.MaxCourses,
            MaxAssistants = plan.MaxAssistants,
            MaxTeamMembers = plan.MaxTeamMembers,
            MaxStorageMegabytes = plan.MaxStorageMegabytes,
            Features = plan.Features
                .Where(feature => feature.IsEnabled)
                .Select(feature => feature.FeatureKey)
                .ToList()
        };

    public static Expression<Func<Invoice, InvoiceDto>> ToInvoiceDto =>
        invoice => new InvoiceDto
        {
            Id = invoice.Id,
            TenantId = invoice.TenantId,
            Number = invoice.Number,
            Status = invoice.Status,
            Subtotal = invoice.Subtotal,
            TaxAmount = invoice.TaxAmount,
            Total = invoice.Total,
            Currency = invoice.Currency,
            IssuedAt = invoice.IssuedAt,
            DueAt = invoice.DueAt,
            PaidAt = invoice.PaidAt,
            Description = invoice.Description,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd
        };

    public static Expression<Func<BillingTransaction, BillingTransactionDto>> ToTransactionDto =>
        transaction => new BillingTransactionDto
        {
            Id = transaction.Id,
            TenantId = transaction.TenantId,
            InvoiceId = transaction.InvoiceId,
            InvoiceNumber = transaction.Invoice != null ? transaction.Invoice.Number : null,
            Amount = transaction.Amount,
            Currency = transaction.Currency,
            Status = transaction.Status,
            Provider = transaction.Provider,
            ProviderTransactionId = transaction.ProviderTransactionId,
            FailureReason = transaction.FailureReason,
            PaidAt = transaction.PaidAt,
            CreatedAt = transaction.CreatedAt
        };
}

/// <summary>The public price list. Anonymous: it drives the marketing pricing page.</summary>
public sealed record GetSubscriptionPlansQuery : IRequest<IReadOnlyList<SubscriptionPlanDto>>
{
    public bool IncludeInactive { get; init; }
}

public sealed class GetSubscriptionPlansQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSubscriptionPlansQuery, IReadOnlyList<SubscriptionPlanDto>>
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> Handle(
        GetSubscriptionPlansQuery query,
        CancellationToken cancellationToken) =>
        await context.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => query.IncludeInactive || (plan.IsActive && plan.IsPublic))
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.MonthlyPrice)
            .Select(BillingProjections.ToPlanDto)
            .ToListAsync(cancellationToken);
}

public sealed record GetSubscriptionPlanQuery(Guid PlanId) : IRequest<SubscriptionPlanDto>;

public sealed class GetSubscriptionPlanQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSubscriptionPlanQuery, SubscriptionPlanDto>
{
    public async Task<SubscriptionPlanDto> Handle(GetSubscriptionPlanQuery query, CancellationToken cancellationToken) =>
        await context.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.Id == query.PlanId)
            .Select(BillingProjections.ToPlanDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("SubscriptionPlan", query.PlanId);
}

/// <summary>The calling workspace's subscription, with live quota usage.</summary>
public sealed record GetMySubscriptionQuery : IRequest<TenantSubscriptionDto>;

public sealed class GetMySubscriptionQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetMySubscriptionQuery, TenantSubscriptionDto>
{
    public async Task<TenantSubscriptionDto> Handle(
        GetMySubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingView, cancellationToken: cancellationToken);

        var subscription = await context.TenantSubscriptions
            .AsNoTracking()
            .Include(entity => entity.Plan)
                .ThenInclude(plan => plan.Features)
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("This workspace has no subscription yet.");

        var usage = await planLimits.GetAllUsageAsync(access.TenantId, cancellationToken);

        return BillingMapper.ToDto(subscription, usage, dateTimeProvider.UtcNow);
    }
}

internal static class BillingMapper
{
    public static TenantSubscriptionDto ToDto(
        TenantSubscription subscription,
        IReadOnlyList<PlanQuotaUsage> usage,
        DateTime utcNow)
    {
        var reference = subscription.Status == SubscriptionStatus.Trial && subscription.TrialEndsAt.HasValue
            ? subscription.TrialEndsAt.Value
            : subscription.CurrentPeriodEnd;

        return new TenantSubscriptionDto
        {
            Id = subscription.Id,
            TenantId = subscription.TenantId,
            Status = subscription.Status,
            BillingCycle = subscription.BillingCycle,
            Amount = subscription.Amount,
            Currency = subscription.Currency,
            StartedAt = subscription.StartedAt,
            TrialEndsAt = subscription.TrialEndsAt,
            CurrentPeriodStart = subscription.CurrentPeriodStart,
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            GracePeriodEndsAt = subscription.GracePeriodEndsAt,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CanceledAt = subscription.CanceledAt,
            IsUsable = subscription.IsUsable(utcNow),
            IsTrialing = subscription.IsTrialing(utcNow),
            DaysRemaining = reference <= utcNow ? 0 : (int)Math.Ceiling((reference - utcNow).TotalDays),
            Plan = new SubscriptionPlanDto
            {
                Id = subscription.Plan.Id,
                Code = subscription.Plan.Code,
                NameAr = subscription.Plan.NameAr,
                NameEn = subscription.Plan.NameEn,
                Description = subscription.Plan.Description,
                MonthlyPrice = subscription.Plan.MonthlyPrice,
                YearlyPrice = subscription.Plan.YearlyPrice,
                Currency = subscription.Plan.Currency,
                TrialDays = subscription.Plan.TrialDays,
                SortOrder = subscription.Plan.SortOrder,
                IsActive = subscription.Plan.IsActive,
                MaxStudents = subscription.Plan.MaxStudents,
                MaxGroups = subscription.Plan.MaxGroups,
                MaxCourses = subscription.Plan.MaxCourses,
                MaxAssistants = subscription.Plan.MaxAssistants,
                MaxTeamMembers = subscription.Plan.MaxTeamMembers,
                MaxStorageMegabytes = subscription.Plan.MaxStorageMegabytes,
                Features = [.. subscription.Plan.Features
                    .Where(feature => feature.IsEnabled)
                    .Select(feature => feature.FeatureKey)]
            },
            Usage = [.. usage.Select(entry => new PlanQuotaUsageDto
            {
                Quota = entry.Quota.ToString(),
                Limit = entry.Limit,
                Used = entry.Used,
                Remaining = entry.Remaining,
                IsUnlimited = entry.IsUnlimited
            })]
        };
    }
}

public sealed record GetMyInvoicesQuery : PagedRequest, IRequest<PaginatedList<InvoiceDto>>
{
    public InvoiceStatus? Status { get; init; }
}

public sealed class GetMyInvoicesQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetMyInvoicesQuery, PaginatedList<InvoiceDto>>
{
    public async Task<PaginatedList<InvoiceDto>> Handle(GetMyInvoicesQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingView, cancellationToken: cancellationToken);

        return await context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.TenantId == access.TenantId)
            .WhereIf(query.Status.HasValue, invoice => invoice.Status == query.Status!.Value)
            .OrderByDescending(invoice => invoice.IssuedAt)
            .Select(BillingProjections.ToInvoiceDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetInvoiceQuery(Guid InvoiceId) : IRequest<InvoiceDto>;

public sealed class GetInvoiceQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetInvoiceQuery, InvoiceDto>
{
    public async Task<InvoiceDto> Handle(GetInvoiceQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingView, cancellationToken: cancellationToken);

        var invoice = await context.Invoices
            .AsNoTracking()
            .Where(entity => entity.Id == query.InvoiceId)
            .Select(BillingProjections.ToInvoiceDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Invoice", query.InvoiceId);

        tenantAuthorization.EnsureSameTenant(access, invoice.TenantId);

        return invoice;
    }
}

public sealed record GetMyTransactionsQuery : PagedRequest, IRequest<PaginatedList<BillingTransactionDto>>
{
    public PaymentStatus? Status { get; init; }
}

public sealed class GetMyTransactionsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetMyTransactionsQuery, PaginatedList<BillingTransactionDto>>
{
    public async Task<PaginatedList<BillingTransactionDto>> Handle(
        GetMyTransactionsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingView, cancellationToken: cancellationToken);

        return await context.BillingTransactions
            .AsNoTracking()
            .Where(transaction => transaction.TenantId == access.TenantId)
            .WhereIf(query.Status.HasValue, transaction => transaction.Status == query.Status!.Value)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Select(BillingProjections.ToTransactionDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetTransactionQuery(Guid TransactionId) : IRequest<BillingTransactionDto>;

public sealed class GetTransactionQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetTransactionQuery, BillingTransactionDto>
{
    public async Task<BillingTransactionDto> Handle(
        GetTransactionQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.BillingView, cancellationToken: cancellationToken);

        var transaction = await context.BillingTransactions
            .AsNoTracking()
            .Where(entity => entity.Id == query.TransactionId)
            .Select(BillingProjections.ToTransactionDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("BillingTransaction", query.TransactionId);

        tenantAuthorization.EnsureSameTenant(access, transaction.TenantId);

        return transaction;
    }
}
