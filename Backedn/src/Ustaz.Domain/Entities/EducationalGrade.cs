using Ustaz.Domain.Common;
using Ustaz.Domain.Enums;

namespace Ustaz.Domain.Entities;

/// <summary>An Egyptian educational grade/year, e.g. Third Secondary / الثالث الثانوي.</summary>
public class EducationalGrade : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public EducationalStage Stage { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<TeacherGrade> TeacherGrades { get; set; } = [];

    public ICollection<Group> Groups { get; set; } = [];

    public ICollection<Course> Courses { get; set; } = [];

    public ICollection<StudentProfile> Students { get; set; } = [];
}
