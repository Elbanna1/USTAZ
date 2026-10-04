namespace Ustaz.Domain.Enums;

public enum SubscriptionEventType
{
    TrialStarted = 1,
    TrialExpired = 2,
    Activated = 3,
    Renewed = 4,
    PaymentFailed = 5,
    EnteredGracePeriod = 6,
    PlanChanged = 7,
    Canceled = 8,
    Reactivated = 9,
    Expired = 10
}
