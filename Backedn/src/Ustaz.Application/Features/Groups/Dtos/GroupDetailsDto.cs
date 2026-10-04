using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Groups.Dtos;

/// <summary>Full group page payload, including everything the detail screen renders.</summary>
public sealed record GroupDetailsDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public GroupType Type { get; init; }

    public string TypeName => Type.ToString();

    public TeachingMode TeachingMode { get; init; }

    public string TeachingModeName => TeachingMode.ToString();

    public string? Location { get; init; }

    public PricingDto Pricing { get; init; } = new();

    public int Capacity { get; init; }

    public int EnrolledStudentsCount { get; init; }

    public int AvailableSeats { get; init; }

    public bool IsFull { get; init; }

    public DateTime StartDate { get; init; }

    public DateTime? EndDate { get; init; }

    public bool IsPublished { get; init; }

    public bool IsActive { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public string? WhatsAppLink { get; init; }

    public TeacherSummaryDto Teacher { get; init; } = new();

    public SubjectDto Subject { get; init; } = new();

    public GradeDto Grade { get; init; } = new();

    public IReadOnlyList<GroupScheduleDto> Schedules { get; init; } = [];

    public IReadOnlyList<GroupCurriculumDto> Curriculum { get; init; } = [];

    public IReadOnlyList<GroupMaterialDto> Materials { get; init; } = [];

    /// <summary>True when the caller already holds an active enrollment in this group.</summary>
    public bool IsEnrolled { get; init; }

    /// <summary>True when the caller is the owning teacher.</summary>
    public bool IsOwner { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }
}
