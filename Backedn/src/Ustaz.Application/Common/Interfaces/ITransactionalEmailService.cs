using Ustaz.Application.Common.Emails;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>A request to send one catalog email.</summary>
public sealed record TransactionalEmailRequest
{
    /// <summary>A key from <see cref="EmailEvents"/>.</summary>
    public required string Event { get; init; }

    /// <summary>Values for the <c>{placeholder}</c> tokens in the copy and the CTA.</summary>
    public IReadOnlyDictionary<string, string>? Values { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? RelatedEntityId { get; init; }

    /// <summary>
    /// Identifies "this same email again" so it is sent at most once per recipient, e.g.
    /// <c>homework-due:{homeworkId}</c>. Security emails ignore it by design.
    /// </summary>
    public string? DeduplicationKey { get; init; }

    /// <summary>
    /// Overrides the recipient's stored language. Used by flows that know the language of the
    /// request better than the profile does.
    /// </summary>
    public EmailLanguage? Language { get; init; }
}

/// <summary>
/// Sends transactional email from the catalog: resolves the recipient and their language,
/// applies deduplication, records the attempt, and never lets a delivery failure escape into
/// the business operation that asked for it.
/// </summary>
public interface ITransactionalEmailService
{
    /// <summary>Sends to a known user, using their stored language and address.</summary>
    Task<bool> SendToUserAsync(
        Guid userId,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sends the same email to several users.</summary>
    Task<int> SendToUsersAsync(
        IEnumerable<Guid> userIds,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends to a bare address, for flows where there may be no account yet — or where the
    /// account must not be confirmed to exist, as in password recovery.
    /// </summary>
    Task<bool> SendToAddressAsync(
        string email,
        string? displayName,
        TransactionalEmailRequest request,
        CancellationToken cancellationToken = default);
}
