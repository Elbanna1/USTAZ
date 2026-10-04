using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities.Tenancy;

/// <summary>Operational preferences for one workspace.</summary>
public class TenantSettings : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public string DefaultLanguage { get; set; } = "ar";

    public string TimeZoneId { get; set; } = "Egypt Standard Time";

    public string Currency { get; set; } = "EGP";

    /// <summary>Students below this exam percentage trigger a parent notification.</summary>
    public int GradeNotificationThresholdPercentage { get; set; } = 60;

    public bool NotifyParentsOnAbsence { get; set; } = true;

    public bool NotifyParentsOnLowGrade { get; set; } = true;

    public bool NotifyParentsOnHomeworkMissed { get; set; } = true;

    public bool AllowStudentSelfEnrollment { get; set; } = true;

    public bool AutoApproveReviews { get; set; } = true;

    /// <summary>Minutes after the session start time beyond which a student counts as late.</summary>
    public int LateThresholdMinutes { get; set; } = 10;
}
