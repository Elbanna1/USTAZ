using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Students;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

/// <summary>The learner's own profile, as the student app renders it.</summary>
public sealed record StudentProfileDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public string? PhoneNumber { get; init; }

    public string? ParentPhoneNumber { get; init; }

    public string? SchoolName { get; init; }

    public string? City { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Guid? GradeId { get; init; }

    public string? GradeNameAr { get; init; }

    public string? GradeNameEn { get; init; }

    public bool EmailConfirmed { get; init; }

    public bool IsActive { get; init; }

    public int ActiveGroupsCount { get; init; }

    public int ActiveCoursesCount { get; init; }

    public int GuardiansCount { get; init; }

    public DateTime CreatedAt { get; init; }
}

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/// <summary>The signed-in student's own profile.</summary>
public sealed record GetStudentProfileQuery : IRequest<StudentProfileDto>;

public sealed class GetStudentProfileQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetStudentProfileQuery, StudentProfileDto>
{
    public async Task<StudentProfileDto> Handle(
        GetStudentProfileQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return await StudentProfileReader.ProjectAsync(context, studentId, cancellationToken);
    }
}

internal static class StudentProfileReader
{
    /// <summary>
    /// Single projection shared by the query and by the update command, so the shape the
    /// client sees never drifts between a read and a write.
    /// </summary>
    public static async Task<StudentProfileDto> ProjectAsync(
        IApplicationDbContext context,
        Guid studentId,
        CancellationToken cancellationToken) =>
        await context.StudentProfiles
            .AsNoTracking()
            .Where(student => student.Id == studentId)
            .Select(student => new StudentProfileDto
            {
                Id = student.Id,
                UserId = student.UserId,
                FirstName = student.User.FirstName,
                LastName = student.User.LastName,
                DisplayName = student.User.DisplayName,
                Email = student.User.Email ?? string.Empty,
                ProfileImageUrl = student.User.ProfileImageUrl,
                PhoneNumber = student.PhoneNumber ?? student.User.PhoneNumber,
                ParentPhoneNumber = student.ParentPhoneNumber,
                SchoolName = student.SchoolName,
                City = student.City ?? student.User.City,
                DateOfBirth = student.DateOfBirth,
                GradeId = student.GradeId,
                GradeNameAr = student.Grade != null ? student.Grade.NameAr : null,
                GradeNameEn = student.Grade != null ? student.Grade.NameEn : null,
                EmailConfirmed = student.User.EmailConfirmed,
                IsActive = student.User.IsActive,
                ActiveGroupsCount = student.GroupEnrollments
                    .Count(enrollment => enrollment.Status == Domain.Enums.EnrollmentStatus.Active),
                ActiveCoursesCount = student.CourseEnrollments
                    .Count(enrollment => enrollment.Status == Domain.Enums.EnrollmentStatus.Active),
                GuardiansCount = student.Guardians.Count(link => link.IsConfirmed),
                CreatedAt = student.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException("StudentProfile", studentId);
}

// ---------------------------------------------------------------------------
// Commands
// ---------------------------------------------------------------------------

/// <summary>
/// Updates the signed-in student's own profile. Every field is optional: only the properties
/// the client actually sends are written, which keeps PUT usable as a partial update the way
/// the teacher and parent profile endpoints already behave.
/// </summary>
public sealed record UpdateStudentProfileCommand : IRequest<StudentProfileDto>
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? ParentPhoneNumber { get; init; }

    public string? SchoolName { get; init; }

    public string? City { get; init; }

    public DateOnly? DateOfBirth { get; init; }

    public Guid? GradeId { get; init; }
}

public sealed class UpdateStudentProfileCommandValidator : AbstractValidator<UpdateStudentProfileCommand>
{
    public UpdateStudentProfileCommandValidator()
    {
        RuleFor(command => command.FirstName!).PersonName().When(command => command.FirstName is not null);
        RuleFor(command => command.LastName!).PersonName().When(command => command.LastName is not null);
        RuleFor(command => command.PhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.ParentPhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.SchoolName).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.City).MaximumLength(100);

        RuleFor(command => command.DateOfBirth!.Value)
            .LessThan(_ => DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("The date of birth must be in the past.")
            .When(command => command.DateOfBirth.HasValue);
    }
}

public sealed class UpdateStudentProfileCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateStudentProfileCommand, StudentProfileDto>
{
    public async Task<StudentProfileDto> Handle(
        UpdateStudentProfileCommand command,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var student = await context.StudentProfiles
            .Include(entity => entity.User)
            .FirstOrDefaultAsync(entity => entity.Id == studentId, cancellationToken)
            ?? throw new NotFoundException("StudentProfile", studentId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.FirstName is not null)
        {
            student.User.FirstName = command.FirstName.Trim();
        }

        if (command.LastName is not null)
        {
            student.User.LastName = command.LastName.Trim();
        }

        if (command.FirstName is not null || command.LastName is not null)
        {
            student.User.DisplayName = (student.User.FirstName + " " + student.User.LastName).Trim();
        }

        if (command.PhoneNumber is not null)
        {
            student.PhoneNumber = command.PhoneNumber.Trim();
            student.User.PhoneNumber = student.PhoneNumber;
        }

        if (command.ParentPhoneNumber is not null)
        {
            student.ParentPhoneNumber = command.ParentPhoneNumber.Trim();
        }

        if (command.SchoolName is not null)
        {
            student.SchoolName = command.SchoolName.Trim();
        }

        if (command.City is not null)
        {
            student.City = command.City.Trim();
            student.User.City = student.City;
        }

        if (command.DateOfBirth.HasValue)
        {
            student.DateOfBirth = command.DateOfBirth.Value;
        }

        if (command.GradeId.HasValue)
        {
            var gradeExists = await context.EducationalGrades
                .AnyAsync(grade => grade.Id == command.GradeId.Value && grade.IsActive, cancellationToken);

            if (!gradeExists)
            {
                throw new NotFoundException("EducationalGrade", command.GradeId.Value);
            }

            student.GradeId = command.GradeId.Value;
        }

        student.UpdatedAt = utcNow;
        student.User.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await StudentProfileReader.ProjectAsync(context, studentId, cancellationToken);
    }
}
