namespace Ustaz.Domain.Exceptions;

/// <summary>Raised when an invariant of the USTAZ domain would be violated.</summary>
public sealed class BusinessRuleException : UstazDomainException
{
    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string code, string message) : base(message) => Code = code;

    public string? Code { get; }
}
