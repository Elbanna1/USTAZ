using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Billing;

namespace Ustaz.Persistence.Configurations.Billing;

public sealed class SubscriptionPlanConfiguration : BaseEntityConfiguration<SubscriptionPlan>
{
    protected override void ConfigureEntity(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("SubscriptionPlans");

        builder.Property(plan => plan.Code).HasMaxLength(50).IsRequired();
        builder.Property(plan => plan.NameAr).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(plan => plan.NameEn).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(plan => plan.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(plan => plan.Currency).HasMaxLength(3).IsRequired();
        builder.Property(plan => plan.MonthlyPrice).HasPrecision(18, 2);
        builder.Property(plan => plan.YearlyPrice).HasPrecision(18, 2);

        builder.Ignore(plan => plan.IsFree);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_SubscriptionPlans_MonthlyPrice", "[MonthlyPrice] >= 0");
            table.HasCheckConstraint("CK_SubscriptionPlans_TrialDays", "[TrialDays] >= 0");
        });

        builder.HasIndex(plan => plan.Code)
            .IsUnique()
            .HasDatabaseName("UX_SubscriptionPlans_Code");

        builder.HasIndex(plan => new { plan.IsActive, plan.IsPublic, plan.SortOrder })
            .HasDatabaseName("IX_SubscriptionPlans_Active_Public_Sort");

        builder.HasMany(plan => plan.Features)
            .WithOne(feature => feature.SubscriptionPlan)
            .HasForeignKey(feature => feature.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PlanFeatureConfiguration : BaseEntityConfiguration<PlanFeature>
{
    protected override void ConfigureEntity(EntityTypeBuilder<PlanFeature> builder)
    {
        builder.ToTable("PlanFeatures");

        builder.Property(feature => feature.FeatureKey).HasMaxLength(100).IsRequired();
        builder.Property(feature => feature.DisplayValue).HasMaxLength(200);

        builder.HasIndex(feature => new { feature.SubscriptionPlanId, feature.FeatureKey })
            .IsUnique()
            .HasDatabaseName("UX_PlanFeatures_PlanId_FeatureKey");
    }
}

public sealed class TenantSubscriptionConfiguration : BaseEntityConfiguration<TenantSubscription>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantSubscription> builder)
    {
        builder.ToTable("TenantSubscriptions");

        builder.Property(subscription => subscription.Status).HasConversion<int>();
        builder.Property(subscription => subscription.BillingCycle).HasConversion<int>();
        builder.Property(subscription => subscription.Amount).HasPrecision(18, 2);
        builder.Property(subscription => subscription.Currency).HasMaxLength(3).IsRequired();
        builder.Property(subscription => subscription.ProviderName).HasMaxLength(50);
        builder.Property(subscription => subscription.ProviderSubscriptionId).HasMaxLength(200);
        builder.Property(subscription => subscription.ProviderCustomerId).HasMaxLength(200);
        builder.Property(subscription => subscription.CancellationReason).HasMaxLength(DomainRules.ShortTextMaxLength);

        // A tenant has exactly one live subscription row; history lives in SubscriptionEvents.
        builder.HasIndex(subscription => subscription.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TenantSubscriptions_TenantId");

        builder.HasIndex(subscription => new { subscription.Status, subscription.CurrentPeriodEnd })
            .HasDatabaseName("IX_TenantSubscriptions_Status_PeriodEnd");

        builder.HasIndex(subscription => subscription.SubscriptionPlanId)
            .HasDatabaseName("IX_TenantSubscriptions_PlanId");

        builder.HasIndex(subscription => subscription.ProviderSubscriptionId)
            .HasFilter("[ProviderSubscriptionId] IS NOT NULL")
            .HasDatabaseName("IX_TenantSubscriptions_ProviderSubscriptionId");

        builder.HasOne(subscription => subscription.Tenant)
            .WithMany()
            .HasForeignKey(subscription => subscription.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(subscription => subscription.Plan)
            .WithMany(plan => plan.Subscriptions)
            .HasForeignKey(subscription => subscription.SubscriptionPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(subscription => subscription.Events)
            .WithOne(entry => entry.Subscription)
            .HasForeignKey(entry => entry.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(subscription => subscription.Invoices)
            .WithOne(invoice => invoice.Subscription)
            .HasForeignKey(invoice => invoice.TenantSubscriptionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class SubscriptionEventConfiguration : BaseEntityConfiguration<SubscriptionEvent>
{
    protected override void ConfigureEntity(EntityTypeBuilder<SubscriptionEvent> builder)
    {
        builder.ToTable("SubscriptionEvents");

        builder.Property(entry => entry.Type).HasConversion<int>();
        builder.Property(entry => entry.Description).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(entry => entry.MetadataJson).HasMaxLength(4000);

        builder.HasIndex(entry => new { entry.TenantSubscriptionId, entry.OccurredAt })
            .HasDatabaseName("IX_SubscriptionEvents_SubscriptionId_OccurredAt");

        builder.HasIndex(entry => new { entry.TenantId, entry.Type })
            .HasDatabaseName("IX_SubscriptionEvents_TenantId_Type");
    }
}
