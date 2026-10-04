using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>Content and SEO configuration for the tenant's public website.</summary>
public class TenantWebsite : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public bool IsPublished { get; set; }

    public string? HeroTitle { get; set; }

    public string? HeroSubtitle { get; set; }

    public string? HeroImageUrl { get; set; }

    public string? HeroImagePublicId { get; set; }

    public string? HeroCtaLabel { get; set; }

    public string? HeroCtaUrl { get; set; }

    public string? AboutTitle { get; set; }

    public string? AboutBody { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactAddress { get; set; }

    public string? WhatsAppNumber { get; set; }

    public string? MapEmbedUrl { get; set; }

    // --- SEO -------------------------------------------------------------
    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? SeoKeywords { get; set; }

    public string? SeoImageUrl { get; set; }

    public string? GoogleAnalyticsId { get; set; }

    public string? FacebookPixelId { get; set; }

    public ICollection<WebsiteSection> Sections { get; set; } = [];

    public ICollection<Testimonial> Testimonials { get; set; } = [];
}
