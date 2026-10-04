namespace Ustaz.Domain.Enums;

/// <summary>Lifecycle of a tenant's USTAZ SaaS subscription.</summary>
public enum SubscriptionStatus
{
    Trial = 1,
    Active = 2,
    PastDue = 3,
    GracePeriod = 4,
    Canceled = 5,
    Expired = 6
}
