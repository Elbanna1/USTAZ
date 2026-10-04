using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Domain.Entities;

/// <summary>Role specific data for a learner account.</summary>
public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public Guid? GradeId { get; set; }

    public EducationalGrade? Grade { get; set; }

    public string? SchoolName { get; set; }

    public string? City { get; set; }

    public string? PhoneNumber { get; set; }

    public string? ParentPhoneNumber { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public ICollection<GroupEnrollment> GroupEnrollments { get; set; } = [];

    public ICollection<CourseEnrollment> CourseEnrollments { get; set; } = [];

    public ICollection<Review> Reviews { get; set; } = [];

    public ICollection<TeacherFavorite> FavoriteTeachers { get; set; } = [];

    public ICollection<CourseFavorite> FavoriteCourses { get; set; } = [];

    public ICollection<Inquiry> Inquiries { get; set; } = [];

    /// <summary>Guardians following this student.</summary>
    public ICollection<Ustaz.Domain.Entities.Education.ParentStudentLink> Guardians { get; set; } = [];

    /// <summary>Workspaces this student belongs to.</summary>
    public ICollection<Ustaz.Domain.Entities.Tenancy.TenantStudent> TenantMemberships { get; set; } = [];
}
