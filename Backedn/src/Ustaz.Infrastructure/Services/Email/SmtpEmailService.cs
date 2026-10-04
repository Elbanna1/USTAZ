using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Email;

/// <summary>
/// SMTP delivery via MailKit, configured for Gmail's submission endpoint (STARTTLS on 587).
///
/// Credentials come only from configuration — User Secrets locally, <c>Email__Password</c> in
/// production — and are never logged, never echoed into an exception message, and never
/// returned by an endpoint. The subject is logged; the body and the recipient are not.
/// </summary>
public sealed class SmtpEmailService(
    IOptions<EmailOptions> options,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException(
                "SMTP is not configured. Set Email__Host, or leave it empty to use the development email service.");
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mime.To.Add(new MailboxAddress(message.ToDisplayName ?? message.To, message.To));
        mime.Subject = message.Subject;

        if (!string.IsNullOrWhiteSpace(_options.SupportEmail))
        {
            mime.ReplyTo.Add(new MailboxAddress(_options.FromName, _options.SupportEmail));
        }

        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody
        }.ToMessageBody();

        using var client = new SmtpClient
        {
            // A hung SMTP conversation must not hold a request open indefinitely.
            Timeout = (int)TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutInSeconds, 5, 120)).TotalMilliseconds
        };

        // STARTTLS is what Gmail expects on 587; implicit TLS covers 465, and an unencrypted
        // connection is only ever reachable by explicitly turning EnableSsl off for a local
        // test server.
        var security = _options.UseStartTls
            ? SecureSocketOptions.StartTls
            : _options.EnableSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.None;

        try
        {
            await client.ConnectAsync(_options.Host, _options.Port, security, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_options.UserName))
            {
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(quit: true, CancellationToken.None);
            }
        }

        // Subject only: the recipient and the body stay out of the log.
        logger.LogInformation(
            "Transactional email dispatched via {Provider}: {Subject}.",
            _options.Provider,
            message.Subject);
    }

    public Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationLink, CancellationToken cancellationToken = default) =>
        SendAsync(Compose(EmailEvents.EmailVerification, toEmail, displayName, new Dictionary<string, string>
        {
            ["userName"] = displayName,
            ["verificationUrl"] = verificationLink
        }), cancellationToken);

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetLink, CancellationToken cancellationToken = default) =>
        SendAsync(Compose(EmailEvents.PasswordResetRequested, toEmail, displayName, new Dictionary<string, string>
        {
            ["userName"] = displayName,
            ["resetUrl"] = resetLink
        }), cancellationToken);

    public Task SendWelcomeAsync(string toEmail, string displayName, string role, CancellationToken cancellationToken = default) =>
        SendAsync(Compose(EmailEvents.Welcome, toEmail, displayName, new Dictionary<string, string>
        {
            ["userName"] = displayName,
            ["role"] = role
        }), cancellationToken);

    public Task SendEnrollmentConfirmationAsync(string toEmail, string displayName, string itemName, CancellationToken cancellationToken = default) =>
        SendAsync(Compose(EmailEvents.StudentAddedToGroup, toEmail, displayName, new Dictionary<string, string>
        {
            ["userName"] = displayName,
            ["group"] = itemName
        }), cancellationToken);

    /// <summary>
    /// Renders a catalog template for the callers that predate the transactional service.
    /// They have no user id, so the language falls back to the platform default.
    /// </summary>
    private EmailMessage Compose(
        string @event,
        string toEmail,
        string displayName,
        IReadOnlyDictionary<string, string> values) =>
        EmailRenderer.Render(
            EmailCatalog.Get(@event),
            EmailLanguage.Arabic,
            toEmail,
            displayName,
            values,
            new EmailBranding
            {
                ClientBaseUrl = _options.ClientBaseUrl,
                SupportEmail = _options.SupportEmail,
                ProductName = _options.FromName
            });
}
