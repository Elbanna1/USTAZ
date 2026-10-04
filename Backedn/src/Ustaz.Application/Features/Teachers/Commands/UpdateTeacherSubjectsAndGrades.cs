using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Shared.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Commands;

/// <summary>Replaces the calling teacher's subject list with the supplied set.</summary>
public sealed record UpdateTeacherSubjectsCommand : IRequest<IReadOnlyList<SubjectDto>>
{
    public IReadOnlyList<Guid> SubjectIds { get; init; } = [];
}

public sealed class UpdateTeacherSubjectsCommandValidator : AbstractValidator<UpdateTeacherSubjectsCommand>
{
    public UpdateTeacherSubjectsCommandValidator() =>
        RuleFor(command => command.SubjectIds)
            .NotEmpty().WithMessage("Select at least one subject.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Subjects must not be repeated.");
}

public sealed class UpdateTeacherSubjectsCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ICacheService cache) : IRequestHandler<UpdateTeacherSubjectsCommand, IReadOnlyList<SubjectDto>>
{
    public async Task<IReadOnlyList<SubjectDto>> Handle(
        UpdateTeacherSubjectsCommand command,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();
        var requested = command.SubjectIds.Distinct().ToList();

        var known = await context.Subjects
            .AsNoTracking()
            .Where(subject => requested.Contains(subject.Id) && subject.IsActive)
            .Select(subject => subject.Id)
            .ToListAsync(cancellationToken);

        var missing = requested.Except(known).FirstOrDefault();
        if (missing != Guid.Empty)
        {
            throw new NotFoundException("Subject", missing);
        }

        var existing = await context.TeacherSubjects
            .Where(link => link.TeacherId == teacherId)
            .ToListAsync(cancellationToken);

        // Groups and courses already reference a subject, so removing one that is still in use
        // would orphan published content.
        var removals = existing.Where(link => !requested.Contains(link.SubjectId)).ToList();
        foreach (var removal in removals)
        {
            var inUse = await context.Groups.AnyAsync(
                group => group.TeacherId == teacherId && group.SubjectId == removal.SubjectId, cancellationToken);

            inUse = inUse || await context.Courses.AnyAsync(
                course => course.TeacherId == teacherId && course.SubjectId == removal.SubjectId, cancellationToken);

            if (inUse)
            {
                throw new ConflictException(
                    "This subject cannot be removed because you still have groups or courses that use it.");
            }
        }

        context.TeacherSubjects.RemoveRange(removals);

        var additions = requested
            .Where(subjectId => existing.All(link => link.SubjectId != subjectId))
            .Select(subjectId => new TeacherSubject
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                SubjectId = subjectId
            });

        await context.TeacherSubjects.AddRangeAsync(additions, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return await context.TeacherSubjects
            .AsNoTracking()
            .Where(link => link.TeacherId == teacherId)
            .OrderBy(link => link.Subject.SortOrder)
            .Select(link => new SubjectDto
            {
                Id = link.Subject.Id,
                NameAr = link.Subject.NameAr,
                NameEn = link.Subject.NameEn,
                Slug = link.Subject.Slug,
                Icon = link.Subject.Icon,
                ColorHex = link.Subject.ColorHex,
                IsActive = link.Subject.IsActive
            })
            .ToListAsync(cancellationToken);
    }
}
