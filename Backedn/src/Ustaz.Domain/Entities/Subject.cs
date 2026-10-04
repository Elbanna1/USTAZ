using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>A taught subject, e.g. Physics / الفيزياء.</summary>
public class Subject : BaseEntity
{
    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Icon identifier or URL used by the frontend (e.g. a lucide icon name).</summary>
    public string? Icon { get; set; }

    public string? ColorHex { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<TeacherSubject> TeacherSubjects { get; set; } = [];

    public ICollection<Group> Groups { get; set; } = [];

    public ICollection<Course> Courses { get; set; } = [];
}
