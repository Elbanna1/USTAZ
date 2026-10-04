using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Commands;

/// <summary>Updates the calling teacher's own profile. Ownership comes from the JWT, never the body.</summary>
public sealed record UpdateTeacherProfileCommand : IRequest<TeacherDetailsDto>
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? DisplayName { get; init; }

    public string? Headline { get; init; }

    public string? Bio { get; init; }

    public int? YearsOfExperience { get; init; }

    public string? City { get; init; }

    public string? PhoneNumber { get; init; }

    public string? WhatsAppNumber { get; init; }

    public TeachingMode? PreferredTeachingMode { get; init; }
}

public sealed class UpdateTeacherProfileCommandValidator : AbstractValidator<UpdateTeacherProfileCommand>
{
    public UpdateTeacherProfileCommandValidator()
    {
        RuleFor(command => command.FirstName!).PersonName().When(command => command.FirstName is not null);
        RuleFor(command => command.LastName!).PersonName().When(command => command.LastName is not null);
        RuleFor(command => command.DisplayName).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Headline).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Bio).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.City).MaximumLength(100);
        RuleFor(command => command.PhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.WhatsAppNumber).OptionalEgyptianPhone();
        RuleFor(command => command.PreferredTeachingMode).IsInEnum().When(command => command.PreferredTeachingMode.HasValue);

        RuleFor(command => command.YearsOfExperience)
            .InclusiveBetween(0, DomainRules.MaxYearsOfExperience)
            .When(command => command.YearsOfExperience.HasValue);
    }
}

public sealed class UpdateTeacherProfileCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache,
    IMediator mediator) : IRequestHandler<UpdateTeacherProfileCommand, TeacherDetailsDto>
{
    public async Task<TeacherDetailsDto> Handle(UpdateTeacherProfileCommand command, CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();

        var teacher = await context.TeacherProfiles
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.Id == teacherId, cancellationToken)
            ?? throw new NotFoundException("Teacher", teacherId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.FirstName is not null)
        {
            teacher.User.FirstName = command.FirstName.Trim();
        }

        if (command.LastName is not null)
        {
            teacher.User.LastName = command.LastName.Trim();
        }

        if (command.DisplayName is not null)
        {
            teacher.User.DisplayName = command.DisplayName.Trim();
        }
        else if (command.FirstName is not null || command.LastName is not null)
        {
            teacher.User.DisplayName = (teacher.User.FirstName + " " + teacher.User.LastName).Trim();
        }

        if (command.PhoneNumber is not null)
        {
            teacher.User.PhoneNumber = command.PhoneNumber.Trim();
        }

        if (command.City is not null)
        {
            teacher.City = command.City.Trim();
            teacher.User.City = teacher.City;
        }

        if (command.Headline is not null)
        {
            teacher.Headline = command.Headline.Trim();
        }

        if (command.Bio is not null)
        {
            teacher.Bio = command.Bio.Trim();
        }

        if (command.YearsOfExperience.HasValue)
        {
            teacher.YearsOfExperience = command.YearsOfExperience.Value;
        }

        if (command.WhatsAppNumber is not null)
        {
            teacher.WhatsAppNumber = command.WhatsAppNumber.Trim();
        }

        if (command.PreferredTeachingMode.HasValue)
        {
            teacher.PreferredTeachingMode = command.PreferredTeachingMode.Value;
        }

        teacher.UpdatedAt = utcNow;
        teacher.User.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacherId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return await mediator.Send(new GetTeacherByIdQuery(teacherId), cancellationToken);
    }
}
