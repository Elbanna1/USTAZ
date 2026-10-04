namespace Ustaz.Domain.Exceptions;

/// <summary>Raised when the request cannot be completed because it clashes with current state.</summary>
public sealed class ConflictException : UstazDomainException
{
    public ConflictException(string message) : base(message) { }

    /// <summary>With a stable code, so a client can tell one kind of clash from another.</summary>
    public ConflictException(string code, string message) : base(message) => Code = code;

    public string? Code { get; }
}
