using Ustaz.Domain.Entities.Tenancy;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Creates the workspace that every teacher gets on registration: the tenant row, its owner
/// membership, default settings/branding/website, and a trial subscription on the default plan.
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>
    /// Builds a fully formed tenant graph for a new teacher. The caller adds it to the context
    /// and saves, so provisioning joins the same transaction as the registration.
    /// </summary>
    Task<Tenant> BuildTenantForTeacherAsync(
        Guid ownerUserId,
        string workspaceName,
        string? city,
        string? contactEmail,
        string? whatsAppNumber,
        CancellationToken cancellationToken = default);

    /// <summary>Ensures a student is registered against a workspace, honouring the plan's student quota.</summary>
    Task EnsureStudentBelongsToTenantAsync(
        Guid tenantId,
        Guid studentId,
        CancellationToken cancellationToken = default);
}
