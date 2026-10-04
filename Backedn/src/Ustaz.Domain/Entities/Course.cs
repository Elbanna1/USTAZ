using Ustaz.Domain.Common;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>A self paced recorded course owned by a teacher.</summary>
public class Course : BaseEntity
{
    /// <summary>Denormalised from the owning teacher so every query can filter by tenant directly.</summary>
    public Guid TenantId { get; set; }

    public Guid TeacherId { get; set; }

    public TeacherProfile Teacher { get; set; } = null!;

    public Guid SubjectId { get; set; }

    public Subject Subject { get; set; } = null!;

    public Guid GradeId { get; set; }

    public EducationalGrade Grade { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? ThumbnailPublicId { get; set; }

    public decimal Price { get; set; }

    public decimal? OriginalPrice { get; set; }

    public int DiscountPercentage { get; set; }

    public int DurationInMinutes { get; set; }

    public int LessonsCount { get; set; }

    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public int StudentCount { get; set; }

    public bool IsPublished { get; set; }

    public bool IsFeatured { get; set; }

    public ICollection<CourseSection> Sections { get; set; } = [];

    public ICollection<CourseEnrollment> Enrollments { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public ICollection<CourseFavorite> FavouritedBy { get; set; } = [];

    public bool HasDiscount => DiscountPercentage > 0
        || (OriginalPrice.HasValue && OriginalPrice.Value > Price);

    public bool IsFree => Price <= 0m;

    /// <summary>Recomputes duration and lesson counters from the published lessons.</summary>
    public void RecalculateContentTotals(DateTime utcNow)
    {
        var lessons = Sections.SelectMany(s => s.Lessons).Where(l => l.IsPublished).ToList();
        LessonsCount = lessons.Count;
        DurationInMinutes = lessons.Sum(l => l.DurationInMinutes);
        UpdatedAt = utcNow;
    }

    /// <summary>A course may only go public once it actually contains playable content.</summary>
    public void Publish(DateTime utcNow)
    {
        Teacher.EnsureCanPublish();

        var publishedLessons = Sections
            .SelectMany(s => s.Lessons)
            .Count(l => l.IsPublished && !string.IsNullOrWhiteSpace(l.VideoUrl));

        if (publishedLessons < DomainRules.MinPublishableLessonCount)
        {
            throw new BusinessRuleException(
                "COURSE_WITHOUT_CONTENT",
                "Add at least one published lesson with a video before publishing this course.");
        }

        if (string.IsNullOrWhiteSpace(Description))
        {
            throw new BusinessRuleException(
                "COURSE_WITHOUT_DESCRIPTION",
                "Add a course description before publishing.");
        }

        IsPublished = true;
        RecalculateContentTotals(utcNow);
        RaiseDomainEvent(new CoursePublishedEvent(Id, TeacherId, Title));
    }

    public void Unpublish(DateTime utcNow)
    {
        IsPublished = false;
        UpdatedAt = utcNow;
    }

    public void ApplyRatingSnapshot(decimal averageRating, int reviewCount, DateTime utcNow)
    {
        Rating = Math.Round(Math.Clamp(averageRating, 0m, DomainRules.MaxRating), 2, MidpointRounding.AwayFromZero);
        ReviewCount = Math.Max(0, reviewCount);
        UpdatedAt = utcNow;
    }
}
