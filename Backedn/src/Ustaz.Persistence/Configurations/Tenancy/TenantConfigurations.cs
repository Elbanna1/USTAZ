using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;

namespace Ustaz.Persistence.Configurations.Tenancy;

public sealed class TenantConfiguration : BaseEntityConfiguration<Tenant>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");

        builder.Property(tenant => tenant.Slug).HasMaxLength(DomainRules.SlugMaxLength).IsRequired();
        builder.Property(tenant => tenant.Name).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(tenant => tenant.DisplayName).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(tenant => tenant.ContactEmail).HasMaxLength(256);
        builder.Property(tenant => tenant.ContactPhone).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(tenant => tenant.WhatsAppNumber).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(tenant => tenant.City).HasMaxLength(100);
        builder.Property(tenant => tenant.SuspendedReason).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(tenant => tenant.Status).HasConversion<int>();

        builder.Ignore(tenant => tenant.IsOperational);

        builder.HasIndex(tenant => tenant.Slug)
            .IsUnique()
            .HasDatabaseName("UX_Tenants_Slug");

        builder.HasIndex(tenant => tenant.OwnerUserId)
            .HasDatabaseName("IX_Tenants_OwnerUserId");

        builder.HasIndex(tenant => tenant.Status)
            .HasDatabaseName("IX_Tenants_Status");

        builder.HasIndex(tenant => tenant.CreatedAt)
            .HasDatabaseName("IX_Tenants_CreatedAt");

        builder.HasOne(tenant => tenant.Settings)
            .WithOne(settings => settings.Tenant)
            .HasForeignKey<TenantSettings>(settings => settings.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tenant => tenant.Branding)
            .WithOne(branding => branding.Tenant)
            .HasForeignKey<TenantBranding>(branding => branding.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tenant => tenant.Website)
            .WithOne(website => website.Tenant)
            .HasForeignKey<TenantWebsite>(website => website.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(tenant => tenant.Domains)
            .WithOne(domain => domain.Tenant)
            .HasForeignKey(domain => domain.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TenantSettingsConfiguration : BaseEntityConfiguration<TenantSettings>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantSettings> builder)
    {
        builder.ToTable("TenantSettings");

        builder.Property(settings => settings.DefaultLanguage).HasMaxLength(10).IsRequired();
        builder.Property(settings => settings.TimeZoneId).HasMaxLength(100).IsRequired();
        builder.Property(settings => settings.Currency).HasMaxLength(3).IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_TenantSettings_GradeThreshold",
            "[GradeNotificationThresholdPercentage] >= 0 AND [GradeNotificationThresholdPercentage] <= 100"));

        builder.HasIndex(settings => settings.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TenantSettings_TenantId");
    }
}

public sealed class TenantBrandingConfiguration : BaseEntityConfiguration<TenantBranding>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("TenantBrandings");

        builder.Property(branding => branding.LogoUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(branding => branding.LogoPublicId).HasMaxLength(300);
        builder.Property(branding => branding.CoverImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(branding => branding.CoverImagePublicId).HasMaxLength(300);
        builder.Property(branding => branding.FaviconUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(branding => branding.PrimaryColorHex).HasMaxLength(9).IsRequired();
        builder.Property(branding => branding.SecondaryColorHex).HasMaxLength(9).IsRequired();
        builder.Property(branding => branding.AccentColorHex).HasMaxLength(9).IsRequired();
        builder.Property(branding => branding.FontFamily).HasMaxLength(100);
        builder.Property(branding => branding.Tagline).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.HasIndex(branding => branding.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TenantBrandings_TenantId");
    }
}

public sealed class TenantDomainConfiguration : BaseEntityConfiguration<TenantDomain>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantDomain> builder)
    {
        builder.ToTable("TenantDomains");

        builder.Property(domain => domain.Host).HasMaxLength(253).IsRequired();
        builder.Property(domain => domain.VerificationToken).HasMaxLength(100).IsRequired();
        builder.Property(domain => domain.LastVerificationError).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(domain => domain.VerificationStatus).HasConversion<int>();

        builder.Ignore(domain => domain.IsVerified);

        // A host may only ever point at one workspace.
        builder.HasIndex(domain => domain.Host)
            .IsUnique()
            .HasDatabaseName("UX_TenantDomains_Host");

        builder.HasIndex(domain => new { domain.TenantId, domain.VerificationStatus })
            .HasDatabaseName("IX_TenantDomains_TenantId_Status");

        // At most one primary domain per tenant.
        builder.HasIndex(domain => domain.TenantId)
            .IsUnique()
            .HasFilter("[IsPrimary] = 1")
            .HasDatabaseName("UX_TenantDomains_TenantId_Primary");
    }
}
