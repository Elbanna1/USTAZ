using Ustaz.Domain.Common;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Domain.Entities.Education;

/// <summary>A guardian account. One parent may follow several children.</summary>
public class ParentProfile : BaseEntity
{
    public Guid UserId { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public string? PhoneNumber { get; set; }

    public string? AlternatePhoneNumber { get; set; }

    public string? City { get; set; }

    public string? Occupation { get; set; }

    public ICollection<ParentStudentLink> Children { get; set; } = [];
}
