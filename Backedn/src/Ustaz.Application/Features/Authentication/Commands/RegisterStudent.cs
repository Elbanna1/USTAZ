using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

public sealed record RegisterStudentCommand : IRequest<AuthenticationResponse>
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public string? PhoneNumber { get; init; }

    public Guid? GradeId { get; init; }

    public string? SchoolName { get; init; }

    public string? City { get; init; }

    public string? ParentPhoneNumber { get; init; }
}

public sealed class RegisterStudentCommandValidator : AbstractValidator<RegisterStudentCommand>
{
    public RegisterStudentCommandValidator()
    {
        RuleFor(command => command.FirstName).PersonName();
        RuleFor(command => command.LastName).PersonName();
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).StrongPassword();
        RuleFor(command => command.PhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.ParentPhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.SchoolName).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.City).MaximumLength(100);
    }
}

public sealed class RegisterStudentCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IAuthenticationTokenIssuer tokenIssuer,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailService emails,
    IConfiguration configuration) : IRequestHandler<RegisterStudentCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(RegisterStudentCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        if (command.GradeId.HasValue
            && !await context.EducationalGrades.AnyAsync(grade => grade.Id == command.GradeId.Value && grade.IsActive, cancellationToken))
        {
            throw new NotFoundException("EducationalGrade", command.GradeId.Value);
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            DisplayName = (command.FirstName.Trim() + " " + command.LastName.Trim()).Trim(),
            PhoneNumber = command.PhoneNumber?.Trim(),
            City = command.City?.Trim(),
            IsActive = true,
            CreatedAt = dateTimeProvider.UtcNow
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new Common.Exceptions.ValidationException(
                nameof(command.Password),
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, RoleNames.Student);

        var profile = new StudentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            GradeId = command.GradeId,
            SchoolName = command.SchoolName?.Trim(),
            City = command.City?.Trim(),
            PhoneNumber = command.PhoneNumber?.Trim(),
            ParentPhoneNumber = command.ParentPhoneNumber?.Trim(),
            CreatedAt = dateTimeProvider.UtcNow
        };

        profile.RaiseDomainEvent(new StudentRegisteredEvent(user.Id, profile.Id, email, user.DisplayName));

        await context.StudentProfiles.AddAsync(profile, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await AccountEmails.SendWelcomeAndVerificationAsync(
            emails, userManager, configuration, user, RoleNames.Student, cancellationToken);

        return await tokenIssuer.IssueAsync(user, cancellationToken: cancellationToken);
    }
}
