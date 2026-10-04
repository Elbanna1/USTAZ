using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Tests.Infrastructure;

/// <summary>
/// Stands in for SMTP and keeps every message, so tests can assert on the real rendered
/// subject and HTML — including the RTL metadata — without a mail server.
/// </summary>
public sealed class RecordingEmailService : IEmailService
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Set to make delivery fail, proving a failed email never breaks a workflow.</summary>
    public bool ThrowOnSend { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("The mail server is unreachable.");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationLink, CancellationToken cancellationToken = default) =>
        SendAsync(new EmailMessage
        {
            To = toEmail,
            ToDisplayName = displayName,
            Subject = "verification",
            HtmlBody = verificationLink
        }, cancellationToken);

    public Task SendPasswordResetAsync(string toEmail, string displayName, string resetLink, CancellationToken cancellationToken = default) =>
        SendAsync(new EmailMessage
        {
            To = toEmail,
            ToDisplayName = displayName,
            Subject = "reset",
            HtmlBody = resetLink
        }, cancellationToken);

    public Task SendWelcomeAsync(string toEmail, string displayName, string role, CancellationToken cancellationToken = default) =>
        SendAsync(new EmailMessage
        {
            To = toEmail,
            ToDisplayName = displayName,
            Subject = "welcome",
            HtmlBody = role
        }, cancellationToken);

    public Task SendEnrollmentConfirmationAsync(string toEmail, string displayName, string itemName, CancellationToken cancellationToken = default) =>
        SendAsync(new EmailMessage
        {
            To = toEmail,
            ToDisplayName = displayName,
            Subject = "enrollment",
            HtmlBody = itemName
        }, cancellationToken);
}
