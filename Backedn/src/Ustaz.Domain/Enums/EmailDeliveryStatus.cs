namespace Ustaz.Domain.Enums;

public enum EmailDeliveryStatus
{
    /// <summary>Recorded, not yet attempted.</summary>
    Pending = 1,

    Sent = 2,

    /// <summary>Failed, and still eligible for the retry pass.</summary>
    Failed = 3,

    /// <summary>Failed too many times. No further attempts will be made.</summary>
    Abandoned = 4,

    /// <summary>Deliberately not sent: no address, preferences, deduplication, or SMTP off.</summary>
    Skipped = 5
}
