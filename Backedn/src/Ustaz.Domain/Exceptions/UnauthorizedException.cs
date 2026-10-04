namespace Ustaz.Domain.Exceptions;

public sealed class UnauthorizedException : UstazDomainException
{
    public UnauthorizedException() : base("Authentication is required to perform this action.") { }

    public UnauthorizedException(string message) : base(message) { }

    /// <summary>
    /// With a stable code, so the client can tell "wrong password" from "no token" and from
    /// "session expired" without reading the prose, and so the error catalogue can give each
    /// of them its own Arabic.
    /// </summary>
    public UnauthorizedException(string code, string message) : base(message) => Code = code;

    public string? Code { get; }
}
