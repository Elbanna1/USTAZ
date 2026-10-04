using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Email;

/// <summary>
/// Used when SMTP is not configured: writes each message to disk so verification and reset
/// links stay reachable while developing, without a mail server or a secret.
///
/// Files land under the configured storage root rather than inside the published application,
/// for the same reason every other local artefact does — a redeploy should not eat them.
/// </summary>
public sealed class DevelopmentEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<DevelopmentEmailService> _logger;
    private readonly string _outputDirectory;

    public DevelopmentEmailService(
        IOptions<EmailOptions> options,
        IOptions<StorageOptions> storageOptions,
        ILogger<DevelopmentEmailService> logger,
        string contentRootPath)
    {
        _options = options.Value;
        _logger = logger;

        var storageRoot = Path.IsPathRooted(storageOptions.Value.RootPath)
            ? Path.GetFullPath(storageOptions.Value.RootPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, storageOptions.Value.RootPath));

        _outputDirectory = Path.Combine(storageRoot, "sent-emails");
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_outputDirectory);

        var fileName = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")
            + "-" + Guid.NewGuid().ToString("N")[..6] + ".html";

        var path = Path.Combine(_outputDirectory, fileName);

        await File.WriteAllTextAsync(path, message.HtmlBody, cancellationToken);

        // The path is developer-facing; the recipient and body still stay out of the log.
        _logger.LogInformation(
            "SMTP is not configured; email '{Subject}' was written to {Path} instead of being sent.",
            message.Subject,
            path);
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
