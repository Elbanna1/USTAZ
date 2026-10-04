namespace Ustaz.Domain.Common;

/// <summary>Marks an entity that is logically removed rather than physically deleted.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }

    DateTime? DeletedAt { get; set; }
}
