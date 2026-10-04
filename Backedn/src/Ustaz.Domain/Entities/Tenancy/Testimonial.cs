using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>A curated quote shown on the public website, written by the tenant.</summary>
public class Testimonial : BaseEntity
{
    public Guid TenantWebsiteId { get; set; }

    public TenantWebsite TenantWebsite { get; set; } = null!;

    public Guid TenantId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string? AuthorTitle { get; set; }

    public string? AuthorImageUrl { get; set; }

    public string Quote { get; set; } = string.Empty;

    public int Rating { get; set; } = 5;

    public bool IsVisible { get; set; } = true;

    public int SortOrder { get; set; }
}
