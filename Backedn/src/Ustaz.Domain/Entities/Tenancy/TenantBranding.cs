using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>Visual identity for the workspace and its public website.</summary>
public class TenantBranding : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public string? LogoPublicId { get; set; }

    public string? CoverImageUrl { get; set; }

    public string? CoverImagePublicId { get; set; }

    public string? FaviconUrl { get; set; }

    public string PrimaryColorHex { get; set; } = "#2563EB";

    public string SecondaryColorHex { get; set; } = "#1E293B";

    public string AccentColorHex { get; set; } = "#F59E0B";

    public string? FontFamily { get; set; }

    public string? Tagline { get; set; }
}
