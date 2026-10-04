using System.Net;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Exceptions;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Infrastructure.Services.Common;

/// <summary>
/// Builds wa.me deep links from validated Egyptian numbers, and only accepts
/// genuine chat.whatsapp.com invite links for groups.
/// </summary>
public sealed class WhatsAppLinkService : IWhatsAppLinkService
{
    private const string InviteHost = "chat.whatsapp.com";

    public string? BuildChatLink(string? phoneNumber, string? prefilledMessage = null)
    {
        if (!EgyptianPhoneNumber.TryCreate(phoneNumber, out var phone))
        {
            return null;
        }

        var link = "https://wa.me/" + phone.Value.TrimStart('+');

        if (!string.IsNullOrWhiteSpace(prefilledMessage))
        {
            link += "?text=" + WebUtility.UrlEncode(prefilledMessage.Trim());
        }

        return link;
    }

    public bool IsValidGroupInviteLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (!uri.Host.Equals(InviteHost, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // A valid invite looks like https://chat.whatsapp.com/{22 char code}
        var code = uri.AbsolutePath.Trim('/');
        return code.Length is >= 6 and <= 64 && code.All(char.IsLetterOrDigit);
    }

    public string NormalizeGroupInviteLink(string url)
    {
        if (!IsValidGroupInviteLink(url))
        {
            throw new BusinessRuleException(
                "INVALID_WHATSAPP_LINK",
                "The WhatsApp group link must look like https://chat.whatsapp.com/<invite-code>.");
        }

        var uri = new Uri(url.Trim());
        return "https://" + InviteHost + "/" + uri.AbsolutePath.Trim('/');
    }
}
