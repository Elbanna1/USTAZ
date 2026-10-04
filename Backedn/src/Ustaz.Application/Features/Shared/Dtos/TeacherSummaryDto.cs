namespace Ustaz.Application.Features.Shared.Dtos;

/// <summary>Compact teacher payload embedded in group, course and review responses.</summary>
public sealed record TeacherSummaryDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Headline { get; init; }

    public string? ProfileImageUrl { get; init; }

    public string? City { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }

    public bool IsVerified { get; init; }

    public string? WhatsAppLink { get; init; }
}
