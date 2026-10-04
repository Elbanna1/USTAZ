namespace Ustaz.Application.Features.Teachers.Dtos;

public sealed record TeacherAchievementDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int? Year { get; init; }

    public string? ImageUrl { get; init; }

    public int SortOrder { get; init; }
}
