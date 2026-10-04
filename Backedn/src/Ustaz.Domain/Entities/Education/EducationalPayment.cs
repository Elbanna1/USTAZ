using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities.Education;

/// <summary>
/// Money a student or parent pays a teacher/centre for tuition.
///
/// This is deliberately a separate aggregate from <c>BillingTransaction</c>: that one records
/// a tenant paying USTAZ for the SaaS subscription, this one records a family paying the
/// tenant for teaching. The two never share a table, a sequence or a provider record.
/// </summary>
public class EducationalPayment : BaseEntity
{
    public Guid TenantId { get; set; }

    public Tenant Tenant { get; set; } = null!;

    public Guid StudentId { get; set; }

    public StudentProfile Student { get; set; } = null!;

    /// <summary>Set when a guardian paid on the student's behalf.</summary>
    public Guid? ParentId { get; set; }

    public ParentProfile? Parent { get; set; }

    public EducationalPaymentType Type { get; set; } = EducationalPaymentType.GroupEnrollment;

    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }

    public Guid? CourseId { get; set; }

    public Course? Course { get; set; }

    public Guid? GroupEnrollmentId { get; set; }

    public GroupEnrollment? GroupEnrollment { get; set; }

    public Guid? CourseEnrollmentId { get; set; }

    public CourseEnrollment? CourseEnrollment { get; set; }

    /// <summary>Tuition invoice number, issued from the tenant's own sequence.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal NetAmount { get; set; }

    public string Currency { get; set; } = "EGP";

    public Guid? DiscountId { get; set; }

    public Discount? Discount { get; set; }

    public string? DiscountCode { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>"cash", "instapay", "fawry", ... Cash is the common case in Egyptian centres.</summary>
    public string? Provider { get; set; }

    public string? ProviderTransactionId { get; set; }

    /// <summary>Month the payment covers, for recurring monthly tuition.</summary>
    public DateOnly? PeriodMonth { get; set; }

    public DateTime? DueAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public string? Notes { get; set; }

    public Guid? RecordedByUserId { get; set; }

    public bool IsSettled => Status == PaymentStatus.Paid;

    public void MarkPaid(DateTime utcNow, string? provider = null, string? providerTransactionId = null)
    {
        if (Status == PaymentStatus.Refunded)
        {
            throw new BusinessRuleException(
                "PAYMENT_REFUNDED",
                "A refunded payment cannot be marked as paid again.");
        }

        Status = PaymentStatus.Paid;
        PaidAt ??= utcNow;
        Provider = provider ?? Provider;
        ProviderTransactionId = providerTransactionId ?? ProviderTransactionId;
        UpdatedAt = utcNow;
    }

    public void Refund(DateTime utcNow, string? reason)
    {
        if (Status != PaymentStatus.Paid)
        {
            throw new BusinessRuleException("PAYMENT_NOT_PAID", "Only a settled payment can be refunded.");
        }

        Status = PaymentStatus.Refunded;
        Notes = string.IsNullOrWhiteSpace(reason) ? Notes : reason;
        UpdatedAt = utcNow;
    }

    /// <summary>Recomputes the net amount from the gross amount and discount.</summary>
    public void RecalculateNet()
    {
        var discount = Math.Clamp(DiscountAmount, 0m, Amount);
        DiscountAmount = Math.Round(discount, 2, MidpointRounding.AwayFromZero);
        NetAmount = Math.Round(Amount - DiscountAmount, 2, MidpointRounding.AwayFromZero);
    }
}
