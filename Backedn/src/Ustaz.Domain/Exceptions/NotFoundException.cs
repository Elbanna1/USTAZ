namespace Ustaz.Domain.Exceptions;

public sealed class NotFoundException : UstazDomainException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} with identifier '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public string? EntityName { get; }

    public object? Key { get; }

    /// <summary>
    /// The message built from an entity name and a key is a developer's sentence, not a
    /// user's: it names an internal type and echoes an identifier back. The exception handler
    /// never shows it to an Arabic caller for that reason, and shows it in English only
    /// because the English surface has always been developer-facing.
    /// </summary>
    public bool NamesAnIdentifier => EntityName is not null;
}
