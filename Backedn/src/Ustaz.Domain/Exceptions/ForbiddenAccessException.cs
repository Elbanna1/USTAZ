namespace Ustaz.Domain.Exceptions;

/// <summary>Raised when an authenticated principal is not allowed to touch a resource.</summary>
public sealed class ForbiddenAccessException : UstazDomainException
{
    public ForbiddenAccessException()
        : base("You do not have permission to perform this action.") { }

    public ForbiddenAccessException(string message) : base(message) { }

    /// <summary>With a stable code, for the cases a client needs to react to specifically.</summary>
    public ForbiddenAccessException(string code, string message) : base(message) => Code = code;

    public string? Code { get; }
}
