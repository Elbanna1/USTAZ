using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Dashboard.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Admin.Queries;

public sealed record AuditLogDto
{
    public Guid Id { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? UserId { get; init; }

    public string? UserEmail { get; init; }

    public string Action { get; init; } = string.Empty;

    public string? EntityName { get; init; }

    public string? EntityId { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }

    public string? IpAddress { get; init; }

    public bool Succeeded { get; init; }

    public string? FailureReason { get; init; }

    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Audit log listing. Available to platform admins for the whole platform, and to a
/// workspace owner for their own workspace only.
/// </summary>
public sealed record GetAuditLogsQuery : PagedRequest, IRequest<PaginatedList<AuditLogDto>>
{
    public Guid? TenantId { get; init; }

    public Guid? UserId { get; init; }

    public string? Action { get; init; }

    public string? EntityName { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}

public sealed class GetAuditLogsQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetAuditLogsQuery, PaginatedList<AuditLogDto>>
{
    public async Task<PaginatedList<AuditLogDto>> Handle(
        GetAuditLogsQuery query,
        CancellationToken cancellationToken)
    {
        // A non-admin may only ever read their own workspace's trail.
        Guid? scopedTenantId = query.TenantId;

        if (!currentUser.IsAdmin)
        {
            var access = await tenantAuthorization.RequireOwnerAsync(query.TenantId, cancellationToken);
            scopedTenantId = access.TenantId;
        }

        return await context.AuditLogs
            .AsNoTracking()
            .WhereIf(scopedTenantId.HasValue, log => log.TenantId == scopedTenantId!.Value)
            .WhereIf(query.UserId.HasValue, log => log.UserId == query.UserId!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(query.Action), log => log.Action == query.Action)
            .WhereIf(!string.IsNullOrWhiteSpace(query.EntityName), log => log.EntityName == query.EntityName)
            .WhereIf(query.From.HasValue, log => log.CreatedAt >= query.From!.Value)
            .WhereIf(query.To.HasValue, log => log.CreatedAt <= query.To!.Value)
            .OrderByDescending(log => log.CreatedAt)
            .Select(log => new AuditLogDto
            {
                Id = log.Id,
                TenantId = log.TenantId,
                UserId = log.UserId,
                UserEmail = log.UserEmail,
                Action = log.Action,
                EntityName = log.EntityName,
                EntityId = log.EntityId,
                OldValues = log.OldValues,
                NewValues = log.NewValues,
                IpAddress = log.IpAddress,
                Succeeded = log.Succeeded,
                FailureReason = log.FailureReason,
                CreatedAt = log.CreatedAt
            })
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Platform admin: tenants and SaaS analytics
// ---------------------------------------------------------------------------
public sealed record AdminTenantDto
{
    public Guid Id { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public TenantStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public string? OwnerDisplayName { get; init; }

    public string? OwnerEmail { get; init; }

    public string? PlanCode { get; init; }

    public SubscriptionStatus? SubscriptionStatus { get; init; }

    public DateTime? CurrentPeriodEnd { get; init; }

    public int MembersCount { get; init; }

    public int StudentsCount { get; init; }

    public int GroupsCount { get; init; }

    public decimal LifetimeRevenue { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record GetAdminTenantsQuery : PagedRequest, IRequest<PaginatedList<AdminTenantDto>>
{
    public TenantStatus? Status { get; init; }

    public SubscriptionStatus? SubscriptionStatus { get; init; }

    public string? PlanCode { get; init; }
}

public sealed class GetAdminTenantsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAdminTenantsQuery, PaginatedList<AdminTenantDto>>
{
    public Task<PaginatedList<AdminTenantDto>> Handle(
        GetAdminTenantsQuery query,
        CancellationToken cancellationToken)
    {
        var search = query.Search?.Trim();

        return context.Tenants
            .AsNoTracking()
            .WhereIf(query.Status.HasValue, tenant => tenant.Status == query.Status!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), tenant =>
                tenant.Name.Contains(search!) || tenant.Slug.Contains(search!))
            .WhereIf(query.SubscriptionStatus.HasValue, tenant =>
                context.TenantSubscriptions.Any(subscription =>
                    subscription.TenantId == tenant.Id
                    && subscription.Status == query.SubscriptionStatus!.Value))
            .WhereIf(!string.IsNullOrWhiteSpace(query.PlanCode), tenant =>
                context.TenantSubscriptions.Any(subscription =>
                    subscription.TenantId == tenant.Id && subscription.Plan.Code == query.PlanCode))
            .OrderByDescending(tenant => tenant.CreatedAt)
            .Select(tenant => new AdminTenantDto
            {
                Id = tenant.Id,
                Slug = tenant.Slug,
                Name = tenant.Name,
                Status = tenant.Status,
                CreatedAt = tenant.CreatedAt,
                OwnerDisplayName = context.Users
                    .Where(user => user.Id == tenant.OwnerUserId)
                    .Select(user => user.DisplayName)
                    .FirstOrDefault(),
                OwnerEmail = context.Users
                    .Where(user => user.Id == tenant.OwnerUserId)
                    .Select(user => user.Email)
                    .FirstOrDefault(),
                PlanCode = context.TenantSubscriptions
                    .Where(subscription => subscription.TenantId == tenant.Id)
                    .Select(subscription => subscription.Plan.Code)
                    .FirstOrDefault(),
                SubscriptionStatus = context.TenantSubscriptions
                    .Where(subscription => subscription.TenantId == tenant.Id)
                    .Select(subscription => (SubscriptionStatus?)subscription.Status)
                    .FirstOrDefault(),
                CurrentPeriodEnd = context.TenantSubscriptions
                    .Where(subscription => subscription.TenantId == tenant.Id)
                    .Select(subscription => (DateTime?)subscription.CurrentPeriodEnd)
                    .FirstOrDefault(),
                MembersCount = tenant.Members.Count(member => member.Status == TenantMemberStatus.Active),
                StudentsCount = tenant.Students.Count(student => student.IsActive),
                GroupsCount = context.Groups.Count(group => group.TenantId == tenant.Id),
                LifetimeRevenue = context.BillingTransactions
                    .Where(transaction => transaction.TenantId == tenant.Id
                        && transaction.Status == PaymentStatus.Paid)
                    .Sum(transaction => (decimal?)transaction.Amount) ?? 0m
            })
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>SaaS-level revenue: what tenants pay USTAZ, not what students pay tenants.</summary>
public sealed record GetPlatformRevenueQuery : IRequest<IReadOnlyList<RevenuePointDto>>
{
    public int Months { get; init; } = 12;
}

public sealed class GetPlatformRevenueQueryHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetPlatformRevenueQuery, IReadOnlyList<RevenuePointDto>>
{
    public async Task<IReadOnlyList<RevenuePointDto>> Handle(
        GetPlatformRevenueQuery query,
        CancellationToken cancellationToken)
    {
        var months = Math.Clamp(query.Months, 1, 36);
        var utcNow = dateTimeProvider.UtcNow;
        var windowStart = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(months - 1));

        var buckets = await context.BillingTransactions
            .AsNoTracking()
            .Where(transaction => transaction.Status == PaymentStatus.Paid
                && transaction.PaidAt != null
                && transaction.PaidAt >= windowStart)
            .GroupBy(transaction => new { transaction.PaidAt!.Value.Year, transaction.PaidAt!.Value.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Revenue = group.Sum(transaction => transaction.Amount),
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var points = new List<RevenuePointDto>(months);

        for (var offset = 0; offset < months; offset++)
        {
            var cursor = windowStart.AddMonths(offset);
            var bucket = buckets.FirstOrDefault(entry => entry.Year == cursor.Year && entry.Month == cursor.Month);

            points.Add(new RevenuePointDto
            {
                Year = cursor.Year,
                Month = cursor.Month,
                Label = cursor.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                GroupRevenue = 0m,
                CourseRevenue = 0m,
                TotalRevenue = bucket?.Revenue ?? 0m,
                EnrollmentsCount = bucket?.Count ?? 0
            });
        }

        return points;
    }
}

/// <summary>Admin action: suspend or reactivate a whole workspace.</summary>
public sealed record SetTenantStatusCommand : IRequest<Unit>
{
    public Guid TenantId { get; init; }

    public bool IsActive { get; init; }

    public string? Reason { get; init; }
}

public sealed class SetTenantStatusCommandHandler(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<SetTenantStatusCommand, Unit>
{
    public async Task<Unit> Handle(SetTenantStatusCommand command, CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .FirstOrDefaultAsync(entity => entity.Id == command.TenantId, cancellationToken)
            ?? throw new Domain.Exceptions.NotFoundException("Tenant", command.TenantId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.IsActive)
        {
            tenant.Activate(utcNow);
        }
        else
        {
            tenant.Suspend(utcNow, command.Reason ?? "Suspended by a platform administrator.");
        }

        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.AdminUserStateChanged,
            nameof(Domain.Entities.Tenancy.Tenant),
            tenant.Id,
            newValues: new { tenant.Status, command.Reason },
            tenantId: tenant.Id,
            cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
