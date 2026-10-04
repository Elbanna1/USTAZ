namespace Ustaz.Application.Features.Shared.Dtos;

public sealed record SubjectDto
{
    public Guid Id { get; init; }

    public string NameAr { get; init; } = string.Empty;

    public string NameEn { get; init; } = string.Empty;

    public string Slug { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Icon { get; init; }

    public string? ColorHex { get; init; }

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }

    public int TeachersCount { get; init; }

    public int GroupsCount { get; init; }

    public int CoursesCount { get; init; }
}
