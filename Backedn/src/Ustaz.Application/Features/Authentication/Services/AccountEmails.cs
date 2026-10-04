using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Authentication.Commands;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.Application.Features.Authentication.Services;

/// <summary>
/// The two emails a brand-new account gets: a welcome, and the link that confirms the address.
///
/// Both registration paths call this so a student and a teacher receive exactly the same
/// onboarding, and neither handler has to know how a confirmation token is built.
/// </summary>
internal static class AccountEmails
{
    public static async Task SendWelcomeAndVerificationAsync(
        ITransactionalEmailService emails,
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ApplicationUser user,
        string role,
        CancellationToken cancellationToken)
    {
        var email = user.Email!;

        await emails.SendToUserAsync(
            user.Id,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.Welcome,
                // One welcome per account, however the registration is retried.
                DeduplicationKey = "welcome:" + user.Id,
                Values = new Dictionary<string, string>
                {
                    ["userName"] = user.DisplayName,
                    ["role"] = role
                }
            },
            cancellationToken);

        if (user.EmailConfirmed)
        {
            return;
        }

        var rawToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = VerifyEmailCommandHandler.EncodeToken(rawToken);

        var clientBaseUrl = configuration["Email:ClientBaseUrl"] ?? "http://localhost:5173";

        var link = clientBaseUrl.TrimEnd('/')
            + "/verify-email?email=" + Uri.EscapeDataString(email)
            + "&token=" + encoded;

        // No deduplication key: the token is one-time, so every send has to be a fresh link.
        await emails.SendToUserAsync(
            user.Id,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.EmailVerification,
                Values = new Dictionary<string, string>
                {
                    ["userName"] = user.DisplayName,
                    ["verificationUrl"] = link
                }
            },
            cancellationToken);
    }
}
