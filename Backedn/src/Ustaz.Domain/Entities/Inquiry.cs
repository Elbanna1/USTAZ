using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities;

/// <summary>
/// A lead sent to a teacher. Guests may submit one too, which is why
/// <see cref="StudentId"/> is optional and the contact details are captured inline.
/// </summary>
public class Inquiry : BaseEntity
{
    public Guid? StudentId { get; set; }

    public StudentProfile? Student { get; set; }

    /// <summary>Denormalised from the receiving teacher so inquiries can be tenant-filtered.</summary>
    public Guid TenantId { get; set; }

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid? GroupId { get; set; }

    public Group? Group { get; set; }

    public Guid? CourseId { get; set; }

    public Course? Course { get; set; }

    public string Name { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string Message { get; set; } = string.Empty;

    public InquiryStatus Status { get; set; } = InquiryStatus.New;

    public string? TeacherNote { get; set; }

    public DateTime? RespondedAt { get; set; }

    public void ChangeStatus(InquiryStatus status, DateTime utcNow, string? note = null)
    {
        Status = status;
        TeacherNote = note ?? TeacherNote;
        UpdatedAt = utcNow;

        if (status is InquiryStatus.Contacted or InquiryStatus.Converted)
        {
            RespondedAt ??= utcNow;
        }
    }
}
