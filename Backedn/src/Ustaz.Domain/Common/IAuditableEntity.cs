namespace Ustaz.Domain.Common;

/// <summary>Marks an entity whose creation/modification timestamps are maintained automatically.</summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }

    DateTime? UpdatedAt { get; set; }
}
