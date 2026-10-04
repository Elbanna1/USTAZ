using Ustaz.Domain.Common;

namespace Ustaz.Domain.Entities;

/// <summary>One item of the syllabus a group covers.</summary>
public class GroupCurriculum : BaseEntity
{
    public Guid GroupId { get; set; }

    public Group Group { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int SortOrder { get; set; }
}
