using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Persistence.Contexts;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// The rows the application cannot start without: platform roles, the subject and grade
/// catalogues, and the SaaS price list.
///
/// Everything here is keyed by a fixed id and inserted only when absent, so an operator who
/// renamed a subject or repriced a plan keeps their edit. The one exception is plan
/// <em>features</em>, which are reconciled: a capability added to a plan in a later release
/// has to reach installations whose plan rows already exist, or the feature would be
/// permanently off for every existing customer.
/// </summary>
internal sealed class ReferenceDataSeeder(
    UstazDbContext context,
    RoleManager<ApplicationRole> roleManager,
    ILogger logger)
{
    public async Task SeedAsync(SeedReport report, CancellationToken cancellationToken)
    {
        await SeedRolesAsync(report);
        await SeedSubjectsAsync(report, cancellationToken);
        await SeedGradesAsync(report, cancellationToken);
        await SeedSubscriptionPlansAsync(report, cancellationToken);
    }

    private async Task SeedRolesAsync(SeedReport report)
    {
        var inserted = 0;
        var skipped = 0;

        foreach (var roleName in RoleNames.All)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                skipped++;
                continue;
            }

            var result = await roleManager.CreateAsync(new ApplicationRole(roleName)
            {
                Description = $"USTAZ {roleName} role."
            });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Failed to create role '{roleName}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
            }

            inserted++;
            logger.LogInformation("Seeded role {RoleName}.", roleName);
        }

        report.Record("Roles", inserted, skipped: skipped);
    }

    private async Task SeedSubjectsAsync(SeedReport report, CancellationToken cancellationToken)
    {
        // One projected id read, not a full-table load: the catalogue is small but this runs on
        // every start-up and there is no reason to materialise the rows.
        var existingIds = await context.Subjects
            .AsNoTracking()
            .Select(subject => subject.Id)
            .ToListAsync(cancellationToken);

        var existing = existingIds.ToHashSet();

        var missing = SeedCatalog.Subjects
            .Where(subject => !existing.Contains(subject.Id))
            .ToList();

        if (missing.Count > 0)
        {
            await context.Subjects.AddRangeAsync(missing, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} subjects.", missing.Count);
        }

        report.Record("Subjects", missing.Count, skipped: existing.Count);
    }

    private async Task SeedGradesAsync(SeedReport report, CancellationToken cancellationToken)
    {
        var existingIds = await context.EducationalGrades
            .AsNoTracking()
            .Select(grade => grade.Id)
            .ToListAsync(cancellationToken);

        var existing = existingIds.ToHashSet();

        var missing = SeedCatalog.Grades
            .Where(grade => !existing.Contains(grade.Id))
            .ToList();

        if (missing.Count > 0)
        {
            await context.EducationalGrades.AddRangeAsync(missing, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} educational grades.", missing.Count);
        }

        report.Record("EducationalGrades", missing.Count, skipped: existing.Count);
    }

    private async Task SeedSubscriptionPlansAsync(SeedReport report, CancellationToken cancellationToken)
    {
        var existingPlanIds = (await context.SubscriptionPlans
                .AsNoTracking()
                .Select(plan => plan.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var missingPlans = PlanCatalog.Plans
            .Where(plan => !existingPlanIds.Contains(plan.Id))
            .ToList();

        if (missingPlans.Count > 0)
        {
            await context.SubscriptionPlans.AddRangeAsync(missingPlans, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} subscription plans.", missingPlans.Count);
        }

        report.Record("SubscriptionPlans", missingPlans.Count, skipped: existingPlanIds.Count);

        // Reconcile the feature rows of plans that were already present. A plan inserted just
        // above brought its own features with it, so only the pre-existing ones need checking.
        var catalogFeatures = PlanCatalog.Plans
            .Where(plan => existingPlanIds.Contains(plan.Id))
            .SelectMany(plan => plan.Features)
            .ToList();

        var featuresInserted = 0;
        var featuresSkipped = 0;

        if (catalogFeatures.Count > 0)
        {
            var existingFeatureIds = (await context.PlanFeatures
                    .AsNoTracking()
                    .Where(feature => existingPlanIds.Contains(feature.SubscriptionPlanId))
                    .Select(feature => feature.Id)
                    .ToListAsync(cancellationToken))
                .ToHashSet();

            var missingFeatures = catalogFeatures
                .Where(feature => !existingFeatureIds.Contains(feature.Id))
                .Select(feature => new PlanFeature
                {
                    Id = feature.Id,
                    SubscriptionPlanId = feature.SubscriptionPlanId,
                    FeatureKey = feature.FeatureKey,
                    IsEnabled = feature.IsEnabled,
                    DisplayValue = feature.DisplayValue
                })
                .ToList();

            if (missingFeatures.Count > 0)
            {
                await context.PlanFeatures.AddRangeAsync(missingFeatures, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Reconciled {Count} plan features.", missingFeatures.Count);
            }

            featuresInserted = missingFeatures.Count;
            featuresSkipped = existingFeatureIds.Count;
        }

        var newPlanFeatureCount = missingPlans.Sum(plan => plan.Features.Count);
        report.Record("PlanFeatures", newPlanFeatureCount + featuresInserted, skipped: featuresSkipped);
    }
}
