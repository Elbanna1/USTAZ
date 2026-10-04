using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities.Billing;

/// <summary>Append-only history of everything that happened to a subscription.</summary>
public class SubscriptionEvent : BaseEntity
{
    public Guid TenantSubscriptionId { get; set; }

    public TenantSubscription Subscription { get; set; } = null!;

    public Guid TenantId { get; set; }

    public SubscriptionEventType Type { get; set; }

    public string? Description { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string? MetadataJson { get; set; }
}
