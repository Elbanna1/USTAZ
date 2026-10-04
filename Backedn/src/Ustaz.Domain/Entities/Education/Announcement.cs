using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Education;

/// <summary>A broadcast from the workspace to students, parents, or a single group.</summary>
public class Announcement : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid CreatedByUserId { get; set; }

    /// <summary>Only set when the audience is <see cref="AnnouncementAudience.Group"/>.</summary>
    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public AnnouncementAudience Audience { get; set; } = AnnouncementAudience.TenantWide;

    public bool IsPinned { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? ImageUrl { get; set; }

    public string? AttachmentUrl { get; set; }

    public bool IsVisibleAt(DateTime utcNow) =>
        IsPublished && (ExpiresAt is null || ExpiresAt > utcNow);

    public void Publish(DateTime utcNow)
    {
        IsPublished = true;
        PublishedAt ??= utcNow;
        UpdatedAt = utcNow;
    }
}
