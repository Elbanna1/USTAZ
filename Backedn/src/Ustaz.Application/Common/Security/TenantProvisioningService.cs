using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Common.Security;

/// <inheritdoc />
public sealed class TenantProvisioningService(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    IPlanLimitService planLimits) : ITenantProvisioningService
{
    public async Task<Tenant> BuildTenantForTeacherAsync(
        Guid ownerUserId,
        string workspaceName,
        string? city,
        string? contactEmail,
        string? whatsAppNumber,
        CancellationToken cancellationToken = default)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = await BuildUniqueSlugAsync(workspaceName, cancellationToken),
            Name = workspaceName,
            DisplayName = workspaceName,
            Status = TenantStatus.Active,
            OwnerUserId = ownerUserId,
            ContactEmail = contactEmail,
            WhatsAppNumber = whatsAppNumber,
            City = city,
            CreatedAt = utcNow,
            Settings = new TenantSettings { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = utcNow },
            Branding = new TenantBranding { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = utcNow },
            Website = new TenantWebsite
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IsPublished = false,
                HeroTitle = workspaceName,
                ContactEmail = contactEmail,
                WhatsAppNumber = whatsAppNumber,
                SeoTitle = workspaceName,
                CreatedAt = utcNow
            }
        };

        // The owner membership is what every later authorization check resolves against.
        tenant.Members.Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = ownerUserId,
            Role = TenantMemberRole.Owner,
            Status = TenantMemberStatus.Active,
            JoinedAt = utcNow,
            CreatedAt = utcNow
        });

        await AttachTrialSubscriptionAsync(tenant, utcNow, cancellationToken);

        return tenant;
    }

    public async Task EnsureStudentBelongsToTenantAsync(
        Guid tenantId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.TenantStudents
            .FirstOrDefaultAsync(
                link => link.TenantId == tenantId && link.StudentId == studentId,
                cancellationToken);

        if (existing is not null)
        {
            // Re-joining after leaving consumes a seat again, so re-check the quota.
            if (!existing.IsActive)
            {
                await planLimits.EnsureQuotaAsync(tenantId, PlanQuota.Students, 1, cancellationToken);
                existing.IsActive = true;
                existing.LeftAt = null;
                existing.UpdatedAt = dateTimeProvider.UtcNow;
            }

            return;
        }

        await planLimits.EnsureQuotaAsync(tenantId, PlanQuota.Students, 1, cancellationToken);

        await context.TenantStudents.AddAsync(
            new TenantStudent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StudentId = studentId,
                IsActive = true,
                JoinedAt = dateTimeProvider.UtcNow,
                CreatedAt = dateTimeProvider.UtcNow
            },
            cancellationToken);
    }

    /// <summary>
    /// Starts the tenant on the cheapest public plan as a trial. When no plan is seeded yet
    /// the tenant is simply created without a subscription and the billing endpoints will
    /// surface that, rather than blocking registration.
    /// </summary>
    private async Task AttachTrialSubscriptionAsync(Tenant tenant, DateTime utcNow, CancellationToken cancellationToken)
    {
        var plan = await context.SubscriptionPlans
            .AsNoTracking()
            .Where(candidate => candidate.IsActive && candidate.IsPublic)
            .OrderBy(candidate => candidate.SortOrder)
            .ThenBy(candidate => candidate.MonthlyPrice)
            .FirstOrDefaultAsync(cancellationToken);

        if (plan is null)
        {
            return;
        }

        var subscription = new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            SubscriptionPlanId = plan.Id,
            BillingCycle = BillingCycle.Monthly,
            Amount = plan.MonthlyPrice,
            Currency = plan.Currency,
            CreatedAt = utcNow
        };

        subscription.StartTrial(utcNow, plan.TrialDays);

        await context.TenantSubscriptions.AddAsync(subscription, cancellationToken);
    }

    private async Task<string> BuildUniqueSlugAsync(string source, CancellationToken cancellationToken)
    {
        var candidate = Slug.Create(source).Value;

        if (!await context.Tenants.AsNoTracking().AnyAsync(tenant => tenant.Slug == candidate, cancellationToken))
        {
            return candidate;
        }

        // Fall back to a suffixed slug rather than failing registration on a name collision.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var unique = Slug.CreateUnique(source).Value;

            if (!await context.Tenants.AsNoTracking().AnyAsync(tenant => tenant.Slug == unique, cancellationToken))
            {
                return unique;
            }
        }

        return Slug.Create(source, Guid.NewGuid().ToString("N")).Value;
    }
}
