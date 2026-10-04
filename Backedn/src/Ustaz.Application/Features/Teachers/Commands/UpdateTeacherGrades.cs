using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Commands;

/// <summary>Replaces the calling teacher's educational grade list with the supplied set.</summary>
public sealed record UpdateTeacherGradesCommand : IRequest<IReadOnlyList<GradeDto>>
{
    public IReadOnlyList<Guid> GradeIds { get; init; } = [];
}

public sealed class UpdateTeacherGradesCommandValidator : AbstractValidator<UpdateTeacherGradesCommand>
{
    public UpdateTeacherGradesCommandValidator() =>
        RuleFor(command => command.GradeIds)
            .NotEmpty().WithMessage("Select at least one educational grade.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Grades must not be repeated.");
}

public sealed class UpdateTeacherGradesCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ICacheService cache) : IRequestHandler<UpdateTeacherGradesCommand, IReadOnlyList<GradeDto>>
{
    public async Task<IReadOnlyList<GradeDto>> Handle(
        UpdateTeacherGradesCommand command,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var requested = command.GradeIds.Distinct().ToList();

        var known = await context.EducationalGrades
            .AsNoTracking()
            .Where(grade => requested.Contains(grade.Id) && grade.IsActive)
            .Select(grade => grade.Id)
            .ToListAsync(cancellationToken);

        var missing = requested.Except(known).FirstOrDefault();
        if (missing != Guid.Empty)
        {
            throw new NotFoundException("EducationalGrade", missing);
        }

        var existing = await context.TeacherGrades
            .Where(link => link.TeacherId == teacherId)
            .ToListAsync(cancellationToken);

        var removals = existing.Where(link => !requested.Contains(link.GradeId)).ToList();
        foreach (var removal in removals)
        {
            var inUse = await context.Groups.AnyAsync(
                group => group.TeacherId == teacherId && group.GradeId == removal.GradeId, cancellationToken);

            inUse = inUse || await context.Courses.AnyAsync(
                course => course.TeacherId == teacherId && course.GradeId == removal.GradeId, cancellationToken);

            if (inUse)
            {
                throw new ConflictException(
                    "This grade cannot be removed because you still have groups or courses that target it.");
            }
        }

        context.TeacherGrades.RemoveRange(removals);

        var additions = requested
            .Where(gradeId => existing.All(link => link.GradeId != gradeId))
            .Select(gradeId => new TeacherGrade
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                GradeId = gradeId
            });

        await context.TeacherGrades.AddRangeAsync(additions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return await context.TeacherGrades
            .AsNoTracking()
            .Where(link => link.TeacherId == teacherId)
            .OrderBy(link => link.Grade.SortOrder)
            .Select(link => new GradeDto
            {
                Id = link.Grade.Id,
                NameAr = link.Grade.NameAr,
                NameEn = link.Grade.NameEn,
                Slug = link.Grade.Slug,
                Stage = link.Grade.Stage,
                SortOrder = link.Grade.SortOrder,
                IsActive = link.Grade.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
