using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>A student's subscription to a teaching group.</summary>
public class GroupEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;

    public DateTime EnrolledAt { get; set; } = DateTime.UtcNow;

    public DateTime? CancelledAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CancellationReason { get; set; }

    public decimal PricePaid { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalPrice { get; set; }

    public Guid? DiscountId { get; set; }

    public Discount? Discount { get; set; }

    public string? DiscountCode { get; set; }

    public bool IsOccupyingSeat => Status is EnrollmentStatus.Pending or EnrollmentStatus.Active;

    public void Activate(DateTime utcNow)
    {
        if (Status == EnrollmentStatus.Cancelled)
        {
            throw new BusinessRuleException("ENROLLMENT_CANCELLED", "A cancelled enrollment cannot be re-activated.");
        }

        Status = EnrollmentStatus.Active;
        UpdatedAt = utcNow;
    }

    public void Cancel(DateTime utcNow, string? reason = null)
    {
        if (Status == EnrollmentStatus.Cancelled)
        {
            throw new BusinessRuleException("ENROLLMENT_ALREADY_CANCELLED", "This enrollment is already cancelled.");
        }

        if (Status == EnrollmentStatus.Completed)
        {
            throw new BusinessRuleException("ENROLLMENT_COMPLETED", "A completed enrollment cannot be cancelled.");
        }

        Status = EnrollmentStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason;
        UpdatedAt = utcNow;
    }

    public void Complete(DateTime utcNow)
    {
        Status = EnrollmentStatus.Completed;
        CompletedAt = utcNow;
        UpdatedAt = utcNow;
    }
}
