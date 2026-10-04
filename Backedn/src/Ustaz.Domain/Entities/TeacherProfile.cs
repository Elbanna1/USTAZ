using Ustaz.Domain.Common;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.Entities;

/// <summary>
/// The professional identity of a teacher on USTAZ. Rating and counters are denormalised
/// so listing queries never need to aggregate across reviews and enrollments.
/// </summary>
public class TeacherProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    /// <summary>The workspace this teacher owns. Every teacher gets one on registration.</summary>
    public Guid TenantId { get; set; }

    public Ustaz.Domain.Entities.Tenancy.Tenant Tenant { get; set; } = null!;

    public string? Bio { get; set; }

    public string? Headline { get; set; }

    public int YearsOfExperience { get; set; }

    public int StudentsCount { get; set; }

    /// <summary>Average of approved reviews, rounded to two decimals. Zero when never reviewed.</summary>
    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public string? CoverImageUrl { get; set; }

    public string? CoverImagePublicId { get; set; }

    public string? ProfileImageUrl { get; set; }

    public string? ProfileImagePublicId { get; set; }

    public string? City { get; set; }

    public string? WhatsAppNumber { get; set; }

    public TeachingMode PreferredTeachingMode { get; set; } = TeachingMode.Hybrid;

    public bool IsFeatured { get; set; }

    public bool IsVerified { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.NotSubmitted;

    public DateTime? VerifiedAt { get; set; }

    /// <summary>When the teacher last asked to be reviewed. Null until they submit.</summary>
    public DateTime? VerificationSubmittedAt { get; set; }

    /// <summary>
    /// The reviewer's note. On rejection this is the reason the teacher is shown, so it is
    /// written for them to read rather than for internal bookkeeping.
    /// </summary>
    public string? VerificationNotes { get; set; }

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = [];

    public ICollection<TeacherGrade> TeacherGrades { get; set; } = [];

    public ICollection<Group> Groups { get; set; } = [];

    public ICollection<Course> Courses { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public ICollection<TeacherAchievement> Achievements { get; set; } = [];

    public ICollection<TeacherSocialLink> SocialLinks { get; set; } = [];

    public ICollection<Discount> Discounts { get; set; } = [];

    public ICollection<Inquiry> Inquiries { get; set; } = [];

    public ICollection<TeacherFavorite> FavouritedBy { get; set; } = [];

    /// <summary>Only verified teachers are allowed to publish public content.</summary>
    public bool CanPublish => IsVerified && VerificationStatus == VerificationStatus.Verified;

    public void EnsureCanPublish()
    {
        if (!CanPublish)
        {
            throw new BusinessRuleException(
                "TEACHER_NOT_VERIFIED",
                "Only verified teachers can publish groups or courses. Please complete your verification first.");
        }
    }

    /// <summary>
    /// Whether the profile carries enough for a reviewer to judge it. A teacher may only
    /// submit once these hold, which keeps the admin queue free of empty profiles.
    /// </summary>
    public bool IsReadyForVerification =>
        !string.IsNullOrWhiteSpace(Headline)
        && !string.IsNullOrWhiteSpace(Bio)
        && Bio!.Trim().Length >= MinVerifiableBioLength
        && !string.IsNullOrWhiteSpace(WhatsAppNumber)
        && TeacherSubjects.Count > 0
        && TeacherGrades.Count > 0;

    /// <summary>A reviewer needs at least this much of a bio to judge a profile.</summary>
    public const int MinVerifiableBioLength = 80;

    /// <summary>True while the request sits with the review team.</summary>
    public bool HasVerificationInReview =>
        VerificationStatus is VerificationStatus.Pending or VerificationStatus.UnderReview;

    /// <summary>A teacher may submit from a fresh profile, or again after a rejection.</summary>
    public bool CanSubmitForVerification =>
        VerificationStatus is VerificationStatus.NotSubmitted or VerificationStatus.Rejected;

    /// <summary>
    /// Teacher action: ask for review. Resubmitting after a rejection clears the old reason
    /// so the teacher is never shown stale feedback against a new submission.
    /// </summary>
    public void SubmitForVerification(DateTime utcNow)
    {
        if (VerificationStatus == VerificationStatus.Verified)
        {
            throw new BusinessRuleException(
                "TEACHER_ALREADY_VERIFIED",
                "Your account is already verified.");
        }

        if (HasVerificationInReview)
        {
            throw new BusinessRuleException(
                "VERIFICATION_ALREADY_SUBMITTED",
                "Your request is already with the review team. We will let you know as soon as it is decided.");
        }

        if (!IsReadyForVerification)
        {
            throw new BusinessRuleException(
                "VERIFICATION_PROFILE_INCOMPLETE",
                "Complete your profile before asking for verification: a headline, a bio, your WhatsApp number, and at least one subject and grade.");
        }

        VerificationStatus = VerificationStatus.Pending;
        VerificationSubmittedAt = utcNow;
        VerificationNotes = null;
        UpdatedAt = utcNow;
    }

    /// <summary>Admin action: take the request off the queue and onto a reviewer's desk.</summary>
    public void BeginVerificationReview(DateTime utcNow)
    {
        if (VerificationStatus != VerificationStatus.Pending)
        {
            throw new BusinessRuleException(
                "VERIFICATION_NOT_PENDING",
                "Only a pending request can be moved into review.");
        }

        VerificationStatus = VerificationStatus.UnderReview;
        UpdatedAt = utcNow;
    }

    public void MarkVerified(DateTime utcNow, string? notes = null)
    {
        IsVerified = true;
        VerificationStatus = VerificationStatus.Verified;
        VerifiedAt = utcNow;
        VerificationNotes = notes;
        UpdatedAt = utcNow;
    }

    public void RejectVerification(DateTime utcNow, string reason)
    {
        IsVerified = false;
        VerificationStatus = VerificationStatus.Rejected;
        VerifiedAt = null;
        VerificationNotes = reason;
        UpdatedAt = utcNow;
    }

    /// <summary>Recomputes the denormalised rating snapshot from freshly aggregated review data.</summary>
    public void ApplyRatingSnapshot(decimal averageRating, int reviewCount, DateTime utcNow)
    {
        Rating = Math.Round(Math.Clamp(averageRating, 0m, DomainRules.MaxRating), 2, MidpointRounding.AwayFromZero);
        ReviewCount = Math.Max(0, reviewCount);
        UpdatedAt = utcNow;
    }
}
