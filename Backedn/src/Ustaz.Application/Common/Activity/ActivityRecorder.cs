using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;

namespace Ustaz.Application.Common.Activity;

/// <inheritdoc />
public sealed class ActivityRecorder(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    ILogger<ActivityRecorder> logger) : IActivityRecorder
{
    /// <summary>Caps the stored JSON so one oversized payload cannot bloat the feed table.</summary>
    private const int MaxMetadataLength = 1000;

    private static readonly JsonSerializerOptions MetadataJson = new() { WriteIndented = false };

    public async Task<bool> RecordAsync(
        ActivityRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entry = await BuildAsync(request, cancellationToken);

            if (entry is null)
            {
                return false;
            }

            await context.ActivityEvents.AddAsync(entry, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return true;
        }
        catch (DbUpdateException exception)
        {
            // Two requests racing on the same deduplication key: the unique index rejects the
            // loser, which is exactly the intended outcome, so this is not an error.
            logger.LogDebug(
                exception,
                "Activity entry {Type} was not written, most likely a duplicate.",
                request.Type);

            return false;
        }
        catch (Exception exception)
        {
            // A timeline entry is never worth failing the action it describes.
            logger.LogError(exception, "Failed to record activity {Type}.", request.Type);

            return false;
        }
    }

    public async Task<int> RecordManyAsync(
        IEnumerable<ActivityRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<ActivityEvent>();

        try
        {
            foreach (var request in requests)
            {
                var entry = await BuildAsync(request, cancellationToken);

                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }

            if (entries.Count == 0)
            {
                return 0;
            }

            await context.ActivityEvents.AddRangeAsync(entries, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return entries.Count;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to record {Count} activity entries.", entries.Count);

            return 0;
        }
    }

    /// <summary>
    /// Validates and materialises one entry, or returns null when it must not be written.
    /// The pre-check on the deduplication key is the common path; the unique index behind it is
    /// what actually guarantees uniqueness under concurrency.
    /// </summary>
    private async Task<ActivityEvent?> BuildAsync(
        ActivityRequest request,
        CancellationToken cancellationToken)
    {
        if (!ActivityTypes.IsKnown(request.Type))
        {
            // A typo in a key would produce rows no client can render, so it is dropped loudly
            // in the log rather than stored.
            logger.LogWarning("Refusing to record unknown activity type {Type}.", request.Type);

            return null;
        }

        if (request.TenantId == Guid.Empty)
        {
            logger.LogWarning("Refusing to record activity {Type} with no workspace.", request.Type);

            return null;
        }

        if (!string.IsNullOrWhiteSpace(request.DeduplicationKey))
        {
            var exists = await context.ActivityEvents
                .AsNoTracking()
                .AnyAsync(
                    entry => entry.TenantId == request.TenantId
                        && entry.DeduplicationKey == request.DeduplicationKey,
                    cancellationToken);

            if (exists)
            {
                return null;
            }
        }

        return new ActivityEvent
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            Type = request.Type,
            ActorUserId = request.ActorUserId,
            ActorName = await ResolveActorNameAsync(request, cancellationToken),
            StudentId = request.StudentId,
            RelatedEntityType = request.RelatedEntityType,
            RelatedEntityId = request.RelatedEntityId,
            Metadata = Serialise(request.Metadata),
            Visibility = request.Visibility,
            OccurredAt = request.OccurredAt ?? dateTimeProvider.UtcNow,
            DeduplicationKey = request.DeduplicationKey,
            CreatedAt = dateTimeProvider.UtcNow
        };
    }

    private async Task<string> ResolveActorNameAsync(
        ActivityRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ActorName))
        {
            return Truncate(request.ActorName.Trim(), DomainRules.NameMaxLength);
        }

        if (request.ActorUserId is not { } userId)
        {
            return "النظام";
        }

        var name = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(name)
            ? "مستخدم"
            : Truncate(name, DomainRules.NameMaxLength);
    }

    private string? Serialise(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(metadata, MetadataJson);

        if (json.Length <= MaxMetadataLength)
        {
            return json;
        }

        // Rather than store truncated JSON no client could parse, the payload is dropped and
        // the entry is kept: the type and the related entity are the parts that matter.
        logger.LogWarning(
            "Activity metadata of {Length} characters exceeded the {Max} character cap and was dropped.",
            json.Length,
            MaxMetadataLength);

        return null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
