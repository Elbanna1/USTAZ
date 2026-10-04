namespace Ustaz.Domain.Exceptions;

/// <summary>Root of every exception intentionally thrown by USTAZ business logic.</summary>
public abstract class UstazDomainException : Exception
{
    protected UstazDomainException(string message) : base(message) { }

    protected UstazDomainException(string message, Exception innerException) : base(message, innerException) { }
}
