using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    Task SendEmailVerificationAsync(string toEmail, string displayName, string verificationLink, CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(string toEmail, string displayName, string resetLink, CancellationToken cancellationToken = default);

    Task SendWelcomeAsync(string toEmail, string displayName, string role, CancellationToken cancellationToken = default);

    Task SendEnrollmentConfirmationAsync(string toEmail, string displayName, string itemName, CancellationToken cancellationToken = default);
}
