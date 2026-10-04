namespace Ustaz.Infrastructure.Options;

/// <summary>
/// Transactional email settings.
///
/// The password is never a literal in this file, in <c>appsettings.json</c>, or anywhere in
/// source: it comes from User Secrets in development and from the <c>Email__Password</c>
/// environment variable in production. <see cref="IsConfigured"/> is what decides whether the
/// platform talks to a real SMTP server or writes messages to disk, so a missing secret
/// degrades to the development sink instead of throwing at startup.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>Informational: which SMTP service these settings describe, e.g. "Gmail".</summary>
    public string Provider { get; set; } = "Smtp";

    public string? Host { get; set; }

    public int Port { get; set; } = 587;

    /// <summary>
    /// STARTTLS on port 587, which is what Gmail expects. Implicit TLS (port 465) is used
    /// when this is false and <see cref="EnableSsl"/> is true.
    /// </summary>
    public bool UseStartTls { get; set; } = true;

    /// <summary>Requires an encrypted connection. Never turn this off against a real server.</summary>
    public bool EnableSsl { get; set; } = true;

    public string? UserName { get; set; }

    /// <summary>
    /// Gmail App Password. Supplied by User Secrets or <c>Email__Password</c> — never by a
    /// committed file. It is never logged, never returned by an endpoint, and never included
    /// in an exception message.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>The address recipients see. Gmail requires this to match the account.</summary>
    public string FromEmail { get; set; } = "no-reply@ustaz.app";

    public string FromName { get; set; } = "Ustaz";

    /// <summary>Where a recipient should write back. Shown in every email footer.</summary>
    public string SupportEmail { get; set; } = "support.webcraft.eg@gmail.com";

    /// <summary>Base URL of the React client, used to build verification / reset links.</summary>
    public string ClientBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>How long to wait on the SMTP conversation before giving up.</summary>
    public int TimeoutInSeconds { get; set; } = 30;

    /// <summary>How many times the retry pass may re-attempt a failed, retryable email.</summary>
    public int MaxDeliveryAttempts { get; set; } = 3;

    /// <summary>
    /// Master switch. Off means nothing is sent and every message is recorded as skipped,
    /// which is what the integration test host uses.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// True when a real SMTP server is configured and usable. A host with a username but no
    /// password is deliberately "not configured": the secret is missing, and failing every
    /// send with an authentication error is worse than writing the message to disk.
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && Port > 0
        && (string.IsNullOrWhiteSpace(UserName) || !string.IsNullOrWhiteSpace(Password));
}
