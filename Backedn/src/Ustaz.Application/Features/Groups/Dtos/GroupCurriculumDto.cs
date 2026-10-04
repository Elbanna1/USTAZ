namespace Ustaz.Application.Features.Groups.Dtos;

public sealed record GroupCurriculumDto
{
    public Guid Id { get; init; }

    public Guid GroupId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int SortOrder { get; init; }
}
