using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Email;

/// <summary>
/// The one path every transactional email takes.
///
/// It resolves the recipient and the language they read, renders the catalog template,
/// records the attempt in <see cref="EmailDelivery"/>, and hands the message to the SMTP
/// service. Three properties matter:
///
/// 1. <b>It never throws at the caller.</b> Enrolling a student must not fail because Gmail
///    was briefly unreachable — the row is marked failed and the retry pass picks it up.
/// 2. <b>It deduplicates.</b> A deduplication key is unique per recipient in the database, so
///    a replayed handler or a second nightly pass cannot send the same reminder twice.
/// 3. <b>It logs nothing sensitive.</b> Addresses are logged as a short hash, tokens and
///    bodies never, and the SMTP password exists only inside the options object.
/// </summary>
public sealed class TransactionalEmailService(
    IApplicationDbContext context,
    IEmailService emailService,
    IDateTimeProvider dateTimeProvider,
    IOptions<EmailOptions> options,
    ICorrelationContext correlation,
    IUserPresenceTracker presence,
    ILogger<TransactionalEmailService> logger) : ITransactionalEmailService
{
    private readonly EmailOptions _options = options.Value;

    public async Task<bool> SendToUserAsync(
        Guid userId,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var recipient = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.PreferredLanguage,
                user.IsActive
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (recipient?.Email is null)
        {
            return false;
        }

        return await DeliverAsync(
            recipient.Id,
            recipient.Email,
            recipient.DisplayName,
            request.Language ?? ParseLanguage(recipient.PreferredLanguage),
            request,
            cancellationToken);
    }

    public async Task<int> SendToUsersAsync(
        IEnumerable<Guid> userIds,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return 0;
        }

        // One lookup for the whole fan-out rather than one per recipient.
        var recipients = await context.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id) && user.Email != null)
            .Select(user => new { user.Id, user.Email, user.DisplayName, user.PreferredLanguage })
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var recipient in recipients)
        {
            if (await DeliverAsync(
                    recipient.Id,
                    recipient.Email!,
                    recipient.DisplayName,
                    request.Language ?? ParseLanguage(recipient.PreferredLanguage),
                    request,
                    cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    public Task<bool> SendToAddressAsync(
        string email,
        string? displayName,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default) =>
        DeliverAsync(
            userId: null,
            email,
            displayName,
            request.Language ?? EmailLanguage.Arabic,
            request,
            cancellationToken);

    private async Task<bool> DeliverAsync(
        Guid? userId,
        string email,
        string? displayName,
        EmailLanguage language,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken)
    {
        var template = EmailCatalog.Get(request.Event);
        var utcNow = dateTimeProvider.UtcNow;

        // An offline-only email to someone with the app open would just be noise.
        if (template.SkipWhenRecipientOnline && userId is not null && presence.IsOnline(userId.Value))
        {
            return false;
        }

        // The name is what the greeting uses; fall back to something neutral rather than
        // shipping an empty "أهلًا ،".
        var values = WithRecipientName(request.Values, displayName);

        var message = EmailRenderer.Render(
            template,
            language,
            email,
            displayName,
            values,
            new EmailBranding
            {
                ClientBaseUrl = _options.ClientBaseUrl,
                SupportEmail = _options.SupportEmail,
                ProductName = _options.FromName
            });

        if (!string.IsNullOrWhiteSpace(request.DeduplicationKey)
            && await context.EmailDeliveries.AnyAsync(
                existing => existing.Recipient == email
                    && existing.DeduplicationKey == request.DeduplicationKey,
                cancellationToken))
        {
            return false;
        }

        var delivery = new EmailDelivery
        {
            Id = Guid.NewGuid(),
            Event = template.Event,
            UserId = userId,
            TenantId = request.TenantId,
            Recipient = email,
            Subject = message.Subject,
            Language = language == EmailLanguage.English ? "en" : "ar-EG",
            Status = EmailDeliveryStatus.Pending,
            CorrelationId = correlation.CorrelationId,
            DeduplicationKey = request.DeduplicationKey,
            RelatedEntityId = request.RelatedEntityId,
            RelatedEntityType = template.RelatedEntityType,
            // A security email's values can embed a one-time link, so nothing is stored and
            // the message is never re-rendered for a retry.
            PayloadJson = template.IsSecurity || values is null || values.Count == 0
                ? null
                : Truncate(JsonSerializer.Serialize(values), 4000),
            CreatedAt = utcNow
        };

        if (!_options.Enabled)
        {
            delivery.MarkSkipped(utcNow, "EmailDisabled");
            await PersistAsync(delivery, cancellationToken);
            return false;
        }

        await context.EmailDeliveries.AddAsync(delivery, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return await AttemptAsync(delivery, message, cancellationToken);
    }

    /// <summary>
    /// Sends one prepared message and records the outcome. Shared with the retry pass, which
    /// is why it takes an existing delivery row.
    /// </summary>
    public async Task<bool> AttemptAsync(
        EmailDelivery delivery,
        Application.Common.Models.EmailMessage message,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;

        try
        {
            await emailService.SendAsync(message, cancellationToken);
            delivery.MarkSent(utcNow);

            logger.LogInformation(
                "Email {Event} delivered to {RecipientHash} ({Language}).",
                delivery.Event,
                Hash(delivery.Recipient),
                delivery.Language);

            await PersistAsync(delivery, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            delivery.MarkFailed(utcNow, Categorise(exception), exception.Message, _options.MaxDeliveryAttempts);

            // The recipient is hashed and the exception message is the provider's, never ours:
            // no addresses, no tokens, no credentials reach the log.
            logger.LogWarning(
                exception,
                "Email {Event} to {RecipientHash} failed ({Category}), attempt {Attempts}.",
                delivery.Event,
                Hash(delivery.Recipient),
                delivery.ErrorCategory,
                delivery.Attempts);

            await PersistAsync(delivery, cancellationToken);
            return false;
        }
    }

    private async Task PersistAsync(EmailDelivery delivery, CancellationToken cancellationToken)
    {
        try
        {
            if (context.EmailDeliveries.Local.All(entry => entry.Id != delivery.Id))
            {
                await context.EmailDeliveries.AddAsync(delivery, cancellationToken);
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Losing the audit row must not take the caller down with it.
            logger.LogWarning(exception, "Could not record the delivery of email {Event}.", delivery.Event);
        }
    }

    /// <summary>Adds the recipient's name so every template can greet them.</summary>
    private static IReadOnlyDictionary<string, string>? WithRecipientName(
        IReadOnlyDictionary<string, string>? values,
        string? displayName)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? "صديقنا" : displayName;

        if (values is null)
        {
            return new Dictionary<string, string> { ["userName"] = name };
        }

        if (values.ContainsKey("userName"))
        {
            return values;
        }

        return new Dictionary<string, string>(values) { ["userName"] = name };
    }

    /// <summary>Coarse buckets, so failures can be counted without parsing prose.</summary>
    private static string Categorise(Exception exception) => exception switch
    {
        TimeoutException => "Timeout",
        MailKit.Security.AuthenticationException => "AuthenticationFailed",
        MailKit.Net.Smtp.SmtpCommandException => "SmtpRejected",
        MailKit.Net.Smtp.SmtpProtocolException => "SmtpProtocol",
        IOException or System.Net.Sockets.SocketException => "NetworkUnavailable",
        InvalidOperationException => "NotConfigured",
        _ => "Unknown"
    };

    /// <summary>Short, stable, non-reversible identifier for an address in logs.</summary>
    internal static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    internal static EmailLanguage ParseLanguage(string? preferred) =>
        preferred is not null && preferred.StartsWith("en", StringComparison.OrdinalIgnoreCase)
            ? EmailLanguage.English
            : EmailLanguage.Arabic;

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
