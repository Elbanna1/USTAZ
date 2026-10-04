using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;

namespace Ustaz.Persistence.Configurations.Tenancy;

public sealed class TenantWebsiteConfiguration : BaseEntityConfiguration<TenantWebsite>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantWebsite> builder)
    {
        builder.ToTable("TenantWebsites");

        builder.Property(website => website.HeroTitle).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(website => website.HeroSubtitle).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(website => website.HeroImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(website => website.HeroImagePublicId).HasMaxLength(300);
        builder.Property(website => website.HeroCtaLabel).HasMaxLength(100);
        builder.Property(website => website.HeroCtaUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(website => website.AboutTitle).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(website => website.AboutBody).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(website => website.ContactEmail).HasMaxLength(256);
        builder.Property(website => website.ContactPhone).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(website => website.ContactAddress).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(website => website.WhatsAppNumber).HasMaxLength(DomainRules.PhoneMaxLength);
        builder.Property(website => website.MapEmbedUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(website => website.SeoTitle).HasMaxLength(200);
        builder.Property(website => website.SeoDescription).HasMaxLength(500);
        builder.Property(website => website.SeoKeywords).HasMaxLength(500);
        builder.Property(website => website.SeoImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(website => website.GoogleAnalyticsId).HasMaxLength(50);
        builder.Property(website => website.FacebookPixelId).HasMaxLength(50);

        builder.HasIndex(website => website.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TenantWebsites_TenantId");

        builder.HasMany(website => website.Sections)
            .WithOne(section => section.TenantWebsite)
            .HasForeignKey(section => section.TenantWebsiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(website => website.Testimonials)
            .WithOne(testimonial => testimonial.TenantWebsite)
            .HasForeignKey(testimonial => testimonial.TenantWebsiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WebsiteSectionConfiguration : BaseEntityConfiguration<WebsiteSection>
{
    protected override void ConfigureEntity(EntityTypeBuilder<WebsiteSection> builder)
    {
        builder.ToTable("WebsiteSections");

        builder.Property(section => section.Type).HasConversion<int>();
        builder.Property(section => section.Title).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(section => section.Subtitle).HasMaxLength(DomainRules.ShortTextMaxLength);
        builder.Property(section => section.Body).HasMaxLength(DomainRules.DescriptionMaxLength);
        builder.Property(section => section.ImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(section => section.ImagePublicId).HasMaxLength(300);
        builder.Property(section => section.SettingsJson).HasMaxLength(4000);

        builder.HasIndex(section => new { section.TenantId, section.SortOrder })
            .HasDatabaseName("IX_WebsiteSections_TenantId_SortOrder");

        builder.HasIndex(section => new { section.TenantWebsiteId, section.IsVisible })
            .HasDatabaseName("IX_WebsiteSections_WebsiteId_IsVisible");
    }
}

public sealed class TestimonialConfiguration : BaseEntityConfiguration<Testimonial>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Testimonial> builder)
    {
        builder.ToTable("Testimonials");

        builder.Property(testimonial => testimonial.AuthorName).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(testimonial => testimonial.AuthorTitle).HasMaxLength(150);
        builder.Property(testimonial => testimonial.AuthorImageUrl).HasMaxLength(DomainRules.UrlMaxLength);
        builder.Property(testimonial => testimonial.Quote).HasMaxLength(DomainRules.DescriptionMaxLength).IsRequired();

        builder.ToTable(table => table.HasCheckConstraint(
            "CK_Testimonials_Rating", "[Rating] >= 1 AND [Rating] <= 5"));

        builder.HasIndex(testimonial => new { testimonial.TenantId, testimonial.SortOrder })
            .HasDatabaseName("IX_Testimonials_TenantId_SortOrder");
    }
}
