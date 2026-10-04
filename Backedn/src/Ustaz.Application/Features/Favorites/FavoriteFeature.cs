using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Favorites;

/// <summary>The calling student's saved teachers.</summary>
public sealed record GetFavoriteTeachersQuery : PagedRequest, IRequest<PaginatedList<TeacherListItemDto>>;

public sealed class GetFavoriteTeachersQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetFavoriteTeachersQuery, PaginatedList<TeacherListItemDto>>
{
    public Task<PaginatedList<TeacherListItemDto>> Handle(
        GetFavoriteTeachersQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.TeacherFavorites
            .AsNoTracking()
            .Where(favorite => favorite.StudentId == studentId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Select(favorite => favorite.Teacher)
            .Select(TeacherProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record AddFavoriteTeacherCommand(Guid TeacherId) : IRequest<Unit>;

public sealed class AddFavoriteTeacherCommandValidator : AbstractValidator<AddFavoriteTeacherCommand>
{
    public AddFavoriteTeacherCommandValidator() => RuleFor(command => command.TeacherId).NotEmpty();
}

public sealed class AddFavoriteTeacherCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AddFavoriteTeacherCommand, Unit>
{
    public async Task<Unit> Handle(AddFavoriteTeacherCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        if (!await context.TeacherProfiles.AnyAsync(teacher => teacher.Id == command.TeacherId, cancellationToken))
        {
            throw new NotFoundException("Teacher", command.TeacherId);
        }

        var alreadySaved = await context.TeacherFavorites.AnyAsync(
            favorite => favorite.StudentId == studentId && favorite.TeacherId == command.TeacherId,
            cancellationToken);

        // Saving twice is a no-op so the UI can fire the request optimistically.
        if (alreadySaved)
        {
            return Unit.Value;
        }

        await context.TeacherFavorites.AddAsync(
            new TeacherFavorite
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                TeacherId = command.TeacherId,
                CreatedAt = dateTimeProvider.UtcNow
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed record RemoveFavoriteTeacherCommand(Guid TeacherId) : IRequest<Unit>;

public sealed class RemoveFavoriteTeacherCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<RemoveFavoriteTeacherCommand, Unit>
{
    public async Task<Unit> Handle(RemoveFavoriteTeacherCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var favorite = await context.TeacherFavorites.FirstOrDefaultAsync(
            entity => entity.StudentId == studentId && entity.TeacherId == command.TeacherId,
            cancellationToken);

        if (favorite is null)
        {
            return Unit.Value;
        }

        context.TeacherFavorites.Remove(favorite);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>The calling student's saved courses.</summary>
public sealed record GetFavoriteCoursesQuery : PagedRequest, IRequest<PaginatedList<CourseListItemDto>>;

public sealed class GetFavoriteCoursesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetFavoriteCoursesQuery, PaginatedList<CourseListItemDto>>
{
    public Task<PaginatedList<CourseListItemDto>> Handle(
        GetFavoriteCoursesQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.CourseFavorites
            .AsNoTracking()
            .Where(favorite => favorite.StudentId == studentId)
            .OrderByDescending(favorite => favorite.CreatedAt)
            .Select(favorite => favorite.Course)
            .Select(CourseProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record AddFavoriteCourseCommand(Guid CourseId) : IRequest<Unit>;

public sealed class AddFavoriteCourseCommandValidator : AbstractValidator<AddFavoriteCourseCommand>
{
    public AddFavoriteCourseCommandValidator() => RuleFor(command => command.CourseId).NotEmpty();
}

public sealed class AddFavoriteCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<AddFavoriteCourseCommand, Unit>
{
    public async Task<Unit> Handle(AddFavoriteCourseCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        if (!await context.Courses.AnyAsync(course => course.Id == command.CourseId, cancellationToken))
        {
            throw new NotFoundException("Course", command.CourseId);
        }

        var alreadySaved = await context.CourseFavorites.AnyAsync(
            favorite => favorite.StudentId == studentId && favorite.CourseId == command.CourseId,
            cancellationToken);

        if (alreadySaved)
        {
            return Unit.Value;
        }

        await context.CourseFavorites.AddAsync(
            new CourseFavorite
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                CourseId = command.CourseId,
                CreatedAt = dateTimeProvider.UtcNow
            },
            cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

public sealed record RemoveFavoriteCourseCommand(Guid CourseId) : IRequest<Unit>;

public sealed class RemoveFavoriteCourseCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<RemoveFavoriteCourseCommand, Unit>
{
    public async Task<Unit> Handle(RemoveFavoriteCourseCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var favorite = await context.CourseFavorites.FirstOrDefaultAsync(
            entity => entity.StudentId == studentId && entity.CourseId == command.CourseId,
            cancellationToken);

        if (favorite is null)
        {
            return Unit.Value;
        }

        context.CourseFavorites.Remove(favorite);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
