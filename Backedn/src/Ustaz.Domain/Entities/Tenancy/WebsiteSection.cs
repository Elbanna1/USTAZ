using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>One orderable, hideable block on the tenant's public website.</summary>
public class WebsiteSection : BaseEntity
{
    public Guid TenantWebsiteId { get; set; }

    public TenantWebsite TenantWebsite { get; set; } = null!;

    /// <summary>Denormalised so section queries can be tenant-filtered directly.</summary>
    public Guid TenantId { get; set; }

    public WebsiteSectionType Type { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public string? Body { get; set; }

    public string? ImageUrl { get; set; }

    public string? ImagePublicId { get; set; }

    /// <summary>Free-form JSON for section specific settings (item counts, layout, ...).</summary>
    public string? SettingsJson { get; set; }

    public bool IsVisible { get; set; } = true;

    public int SortOrder { get; set; }
}
