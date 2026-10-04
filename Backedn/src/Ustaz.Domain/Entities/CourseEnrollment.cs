using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>A student's enrollment in a recorded course.</summary>
public class CourseEnrollment : BaseEntity
{
    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    public Guid CourseId { get; set; }

    public Course Course { get; set; } = null!;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Active;

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

    public int ProgressPercentage { get; set; }

    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Per-lesson completion, the source of truth behind <see cref="ProgressPercentage"/>.</summary>
    public ICollection<CourseLessonProgress> LessonProgress { get; set; } = [];

    public bool IsActive => Status is EnrollmentStatus.Active or EnrollmentStatus.Pending;

    public void UpdateProgress(int percentage, DateTime utcNow)
    {
        ProgressPercentage = Math.Clamp(percentage, 0, 100);
        LastAccessedAt = utcNow;
        UpdatedAt = utcNow;

        if (ProgressPercentage == 100 && Status == EnrollmentStatus.Active)
        {
            Status = EnrollmentStatus.Completed;
            CompletedAt = utcNow;
        }
    }

    public void Cancel(DateTime utcNow, string? reason = null)
    {
        if (Status == EnrollmentStatus.Cancelled)
        {
            throw new BusinessRuleException("ENROLLMENT_ALREADY_CANCELLED", "This enrollment is already cancelled.");
        }

        Status = EnrollmentStatus.Cancelled;
        CancelledAt = utcNow;
        CancellationReason = reason;
        UpdatedAt = utcNow;
    }
}
