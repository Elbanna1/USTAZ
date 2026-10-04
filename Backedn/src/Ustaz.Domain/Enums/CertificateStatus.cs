namespace Ustaz.Domain.Enums;

public enum CertificateStatus
{
    /// <summary>Live and verifiable.</summary>
    Issued = 1,

    /// <summary>Withdrawn by the workspace; verification reports it as invalid.</summary>
    Revoked = 2
}
