using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Infrastructure.Services.Common;

/// <inheritdoc />
public sealed class AuditLogger(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<AuditLogger> logger) : IAuditLogger
{
    private const int MaxValueLength = 4000;

    /// <summary>
    /// Property names that are stripped before anything is written. Matching is
    /// case-insensitive and substring based, so "NewPasswordHash" is caught too.
    /// </summary>
    private static readonly string[] RedactedKeys =
    [
        "password", "token", "secret", "apikey", "api_key", "refresh",
        "authorization", "signature", "cvv", "cardnumber", "card_number", "pin"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public async Task LogAsync(
        string action,
        string? entityName = null,
        object? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        Guid? tenantId = null,
        bool succeeded = true,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId ?? tenantContext.TenantId,
                UserId = currentUser.UserId,
                UserEmail = Truncate(currentUser.Email, 256),
                Action = action,
                EntityName = Truncate(entityName, 100),
                EntityId = Truncate(entityId?.ToString(), 100),
                OldValues = Serialize(oldValues),
                NewValues = Serialize(newValues),
                IpAddress = Truncate(currentUser.IpAddress, 64),
                UserAgent = Truncate(currentUser.UserAgent, 512),
                Succeeded = succeeded,
                FailureReason = Truncate(failureReason, 500),
                CreatedAt = dateTimeProvider.UtcNow
            };

            await context.AuditLogs.AddAsync(entry, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Auditing must never take down the operation it is recording.
            logger.LogError(exception, "Failed to write the audit entry for action {Action}.", action);
        }
    }

    /// <summary>Serialises to JSON with every sensitive property removed.</summary>
    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var element = JsonSerializer.SerializeToElement(value, SerializerOptions);
            var redacted = Redact(element);
            var json = JsonSerializer.Serialize(redacted, SerializerOptions);

            return json.Length > MaxValueLength ? json[..MaxValueLength] : json;
        }
        catch
        {
            return null;
        }
    }

    private static object? Redact(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var result = new Dictionary<string, object?>();

                foreach (var property in element.EnumerateObject())
                {
                    result[property.Name] = IsSensitive(property.Name)
                        ? "[redacted]"
                        : Redact(property.Value);
                }

                return result;

            case JsonValueKind.Array:
                return element.EnumerateArray().Select(Redact).ToList();

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                return element.TryGetInt64(out var integer) ? integer : element.GetDouble();

            case JsonValueKind.True:
            case JsonValueKind.False:
                return element.GetBoolean();

            default:
                return null;
        }
    }

    private static bool IsSensitive(string propertyName) =>
        RedactedKeys.Any(key => propertyName.Contains(key, StringComparison.OrdinalIgnoreCase));

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
