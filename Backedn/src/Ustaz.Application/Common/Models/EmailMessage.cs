namespace Ustaz.Application.Common.Models;

public sealed record EmailMessage
{
    public required string To { get; init; }

    public required string Subject { get; init; }

    public required string HtmlBody { get; init; }

    public string? PlainTextBody { get; init; }

    public string? ToDisplayName { get; init; }
}
