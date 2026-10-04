namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Records sensitive actions. Implementations redact anything secret before persisting:
/// passwords, tokens, refresh tokens and provider secrets never reach the log.
/// </summary>
public interface IAuditLogger
{
    Task LogAsync(
        string action,
        string? entityName = null,
        object? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        Guid? tenantId = null,
        bool succeeded = true,
        string? failureReason = null,
        CancellationToken cancellationToken = default);
}
