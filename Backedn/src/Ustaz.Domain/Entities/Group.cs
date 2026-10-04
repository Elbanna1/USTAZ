using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>
/// A recurring offline/online teaching group ("مجموعة") owned by a teacher.
/// Capacity and enrollment counters are guarded by the aggregate itself.
/// </summary>
public class Group : BaseEntity
{
    /// <summary>Denormalised from the owning teacher so every query can filter by tenant directly.</summary>
    public Guid TenantId { get; set; }

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    public Guid GradeId { get; set; }

    public EducationalGrade Grade { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public GroupType Type { get; set; } = GroupType.Regular;

    public TeachingMode TeachingMode { get; set; } = TeachingMode.Offline;

    public string? Location { get; set; }

    public decimal MonthlyPrice { get; set; }

    public decimal? OriginalPrice { get; set; }

    public int DiscountPercentage { get; set; }

    public int Capacity { get; set; }

    public int EnrolledStudentsCount { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsPublished { get; set; }

    public bool IsActive { get; set; } = true;

    public string? WhatsAppLink { get; set; }

    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public ICollection<GroupSchedule> Schedules { get; set; } = [];

    public ICollection<GroupCurriculum> Curriculum { get; set; } = [];

    public ICollection<GroupMaterial> Materials { get; set; } = [];

    public ICollection<GroupEnrollment> Enrollments { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public int AvailableSeats => Math.Max(0, Capacity - EnrolledStudentsCount);

    public bool IsFull => EnrolledStudentsCount >= Capacity;

    public bool HasDiscount => DiscountPercentage > 0
        || (OriginalPrice.HasValue && OriginalPrice.Value > MonthlyPrice);

    /// <summary>Throws when the group cannot accept one more student.</summary>
    /// <summary>
    /// Capacity and availability check. <paramref name="requirePublished"/> is false only when
    /// a member of the workspace enrolls a student by hand: staff may fill a draft group
    /// before it is opened to the public, but the seat count still has to hold.
    /// </summary>
    public void EnsureHasCapacity(bool requirePublished = true)
    {
        if (!IsActive)
        {
            throw new BusinessRuleException("GROUP_INACTIVE", "This group is no longer active.");
        }

        if (requirePublished && !IsPublished)
        {
            throw new BusinessRuleException("GROUP_NOT_PUBLISHED", "This group is not open for enrollment yet.");
        }

        if (IsFull)
        {
            throw new BusinessRuleException(
                "GROUP_FULL",
                $"This group has reached its capacity of {Capacity} students.");
        }
    }

    public void IncrementEnrollment(DateTime utcNow, bool requirePublished = true)
    {
        EnsureHasCapacity(requirePublished);
        EnrolledStudentsCount++;
        UpdatedAt = utcNow;

        if (IsFull)
        {
            RaiseDomainEvent(new GroupReachedCapacityEvent(Id, TeacherId, Capacity));
        }
    }

    public void DecrementEnrollment(DateTime utcNow)
    {
        EnrolledStudentsCount = Math.Max(0, EnrolledStudentsCount - 1);
        UpdatedAt = utcNow;
    }

    /// <summary>A group may only go public once it has at least one weekly session.</summary>
    public void Publish(DateTime utcNow)
    {
        Teacher.EnsureCanPublish();

        if (Schedules.Count == 0 || Schedules.All(s => !s.IsActive))
        {
            throw new BusinessRuleException(
                "GROUP_WITHOUT_SCHEDULE",
                "Add at least one active weekly session before publishing this group.");
        }

        if (Capacity <= 0)
        {
            throw new BusinessRuleException("GROUP_INVALID_CAPACITY", "Set a capacity greater than zero before publishing.");
        }

        IsPublished = true;
        IsActive = true;
        UpdatedAt = utcNow;
        RaiseDomainEvent(new GroupPublishedEvent(Id, TeacherId, Name));
    }

    public void Unpublish(DateTime utcNow)
    {
        IsPublished = false;
        UpdatedAt = utcNow;
    }

    public void ApplyRatingSnapshot(decimal averageRating, int reviewCount, DateTime utcNow)
    {
        Rating = Math.Round(Math.Clamp(averageRating, 0m, 5m), 2, MidpointRounding.AwayFromZero);
        ReviewCount = Math.Max(0, reviewCount);
        UpdatedAt = utcNow;
    }
}
