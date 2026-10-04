using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Enrollments.Dtos;

/// <summary>A student's subscription to a teaching group.</summary>
public sealed record GroupEnrollmentDto
{
    public Guid Id { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string? StudentPhoneNumber { get; init; }

    public Guid GroupId { get; init; }

    public string GroupName { get; init; } = string.Empty;

    public string GroupSlug { get; init; } = string.Empty;

    public EnrollmentStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public DateTime EnrolledAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public decimal PricePaid { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal FinalPrice { get; init; }

    public string? DiscountCode { get; init; }

    public TeacherSummaryDto Teacher { get; init; } = new();

    public SubjectDto Subject { get; init; } = new();

    public GradeDto Grade { get; init; } = new();

    public string? WhatsAppLink { get; init; }

    public IReadOnlyList<ScheduleSlotDto> Schedule { get; init; } = [];
}

/// <summary>A student's enrollment in a recorded course.</summary>
public sealed record CourseEnrollmentDto
{
    public Guid Id { get; init; }

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public Guid CourseId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public string CourseSlug { get; init; } = string.Empty;

    public string? CourseThumbnailUrl { get; init; }

    public EnrollmentStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public DateTime EnrolledAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? LastAccessedAt { get; init; }

    public decimal PricePaid { get; init; }

    public decimal DiscountAmount { get; init; }

    public decimal FinalPrice { get; init; }

    public string? DiscountCode { get; init; }

    public int ProgressPercentage { get; init; }

    public int LessonsCount { get; init; }

    public int DurationInMinutes { get; init; }

    public TeacherSummaryDto Teacher { get; init; } = new();

    public SubjectDto Subject { get; init; } = new();

    public GradeDto Grade { get; init; } = new();
}
