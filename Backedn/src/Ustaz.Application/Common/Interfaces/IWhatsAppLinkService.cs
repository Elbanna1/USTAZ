namespace Ustaz.Application.Common.Interfaces;

/// <summary>Builds and validates WhatsApp deep links so arbitrary URLs never reach the frontend.</summary>
public interface IWhatsAppLinkService
{
    /// <summary>Builds a wa.me chat link, optionally prefilled with a message. Returns null for invalid numbers.</summary>
    string? BuildChatLink(string? phoneNumber, string? prefilledMessage = null);

    /// <summary>True when the value is a legitimate chat.whatsapp.com group invite link.</summary>
    bool IsValidGroupInviteLink(string? url);

    /// <summary>Normalises a group invite link or throws when it is not a WhatsApp invite.</summary>
    string NormalizeGroupInviteLink(string url);
}
