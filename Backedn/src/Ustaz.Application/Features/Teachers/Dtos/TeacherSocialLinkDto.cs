using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Teachers.Dtos;

public sealed record TeacherSocialLinkDto
{
    public Guid Id { get; init; }

    public SocialPlatform Platform { get; init; }

    public string PlatformName => Platform.ToString();

    public string Url { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}
