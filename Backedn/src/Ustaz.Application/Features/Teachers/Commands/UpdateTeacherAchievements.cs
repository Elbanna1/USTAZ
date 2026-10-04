using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;

namespace Ustaz.Application.Features.Teachers.Commands;

public sealed record AchievementInput
{
    public Guid? Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int? Year { get; init; }

    public string? ImageUrl { get; init; }

    public int SortOrder { get; init; }
}

/// <summary>Replaces the calling teacher's achievement list in one atomic operation.</summary>
public sealed record UpdateTeacherAchievementsCommand : IRequest<IReadOnlyList<TeacherAchievementDto>>
{
    public IReadOnlyList<AchievementInput> Achievements { get; init; } = [];
}

public sealed class UpdateTeacherAchievementsCommandValidator : AbstractValidator<UpdateTeacherAchievementsCommand>
{
    public UpdateTeacherAchievementsCommandValidator()
    {
        RuleFor(command => command.Achievements)
            .Must(items => items.Count <= 25)
            .WithMessage("You can list at most 25 achievements.");

        RuleForEach(command => command.Achievements).ChildRules(achievement =>
        {
            achievement.RuleFor(item => item.Title)
                .NotEmpty().WithMessage("An achievement title is required.")
                .MaximumLength(DomainRules.NameMaxLength);

            achievement.RuleFor(item => item.Description).MaximumLength(DomainRules.ShortTextMaxLength);
            achievement.RuleFor(item => item.ImageUrl).OptionalHttpUrl();

            achievement.RuleFor(item => item.Year)
                .InclusiveBetween(1950, DateTime.UtcNow.Year + 1)
                .When(item => item.Year.HasValue)
                .WithMessage("The achievement year looks invalid.");
        });
    }
}

public sealed class UpdateTeacherAchievementsCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<UpdateTeacherAchievementsCommand, IReadOnlyList<TeacherAchievementDto>>
{
    public async Task<IReadOnlyList<TeacherAchievementDto>> Handle(
        UpdateTeacherAchievementsCommand command,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var utcNow = dateTimeProvider.UtcNow;

        var existing = await context.TeacherAchievements
            .Where(achievement => achievement.TeacherId == teacherId)
            .ToListAsync(cancellationToken);

        var keptIds = command.Achievements
            .Where(input => input.Id.HasValue)
            .Select(input => input.Id!.Value)
            .ToHashSet();

        context.TeacherAchievements.RemoveRange(
            existing.Where(achievement => !keptIds.Contains(achievement.Id)));

        var order = 1;
        foreach (var input in command.Achievements)
        {
            var achievement = input.Id.HasValue
                ? existing.FirstOrDefault(item => item.Id == input.Id.Value)
                : null;

            if (achievement is null)
            {
                achievement = new TeacherAchievement
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherId,
                    CreatedAt = utcNow
                };

                await context.TeacherAchievements.AddAsync(achievement, cancellationToken);
            }

            achievement.Title = input.Title.Trim();
            achievement.Description = input.Description?.Trim();
            achievement.Year = input.Year;
            achievement.ImageUrl = input.ImageUrl?.Trim();
            achievement.SortOrder = input.SortOrder == 0 ? order : input.SortOrder;
            achievement.UpdatedAt = utcNow;

            order++;
        }

        await context.SaveChangesAsync(cancellationToken);

        return await context.TeacherAchievements
            .AsNoTracking()
            .Where(achievement => achievement.TeacherId == teacherId)
            .OrderBy(achievement => achievement.SortOrder)
            .Select(achievement => new TeacherAchievementDto
            {
                Id = achievement.Id,
                Title = achievement.Title,
                Description = achievement.Description,
                Year = achievement.Year,
                ImageUrl = achievement.ImageUrl,
                SortOrder = achievement.SortOrder
            })
            .ToListAsync(cancellationToken);
    }
}
