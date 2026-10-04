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
using Ustaz.Domain.Enums;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

public sealed record RegisterTeacherCommand : IRequest<AuthenticationResponse>
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Headline { get; init; }

    public string? Bio { get; init; }

    public int YearsOfExperience { get; init; }

    public string? City { get; init; }

    public string? WhatsAppNumber { get; init; }

    public TeachingMode PreferredTeachingMode { get; init; } = TeachingMode.Hybrid;

    public IReadOnlyList<Guid> SubjectIds { get; init; } = [];

    public IReadOnlyList<Guid> GradeIds { get; init; } = [];

    /// <summary>
    /// Display name for the teacher's workspace and public website. Defaults to the
    /// teacher's own display name when omitted.
    /// </summary>
    public string? WorkspaceName { get; init; }
}

public sealed class RegisterTeacherCommandValidator : AbstractValidator<RegisterTeacherCommand>
{
    public RegisterTeacherCommandValidator()
    {
        RuleFor(command => command.FirstName).PersonName();
        RuleFor(command => command.LastName).PersonName();
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).StrongPassword();
        RuleFor(command => command.PhoneNumber).EgyptianPhone();
        RuleFor(command => command.WhatsAppNumber).OptionalEgyptianPhone();
        RuleFor(command => command.Headline).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Bio).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.City).MaximumLength(100);
        RuleFor(command => command.PreferredTeachingMode).IsInEnum();

        RuleFor(command => command.YearsOfExperience)
            .InclusiveBetween(0, DomainRules.MaxYearsOfExperience)
            .WithMessage("Years of experience must be between 0 and " + DomainRules.MaxYearsOfExperience + ".");

        RuleFor(command => command.SubjectIds)
            .NotEmpty().WithMessage("Select at least one subject you teach.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Subjects must not be repeated.");

        RuleFor(command => command.GradeIds)
            .NotEmpty().WithMessage("Select at least one educational grade you teach.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Grades must not be repeated.");
    }
}

public sealed class RegisterTeacherCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IAuthenticationTokenIssuer tokenIssuer,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailService emails,
    IConfiguration configuration,
    ICacheService cache,
    ITenantProvisioningService tenantProvisioning) : IRequestHandler<RegisterTeacherCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(RegisterTeacherCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        await EnsureReferenceDataExistsAsync(command, cancellationToken);

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            DisplayName = (command.FirstName.Trim() + " " + command.LastName.Trim()).Trim(),
            PhoneNumber = command.PhoneNumber.Trim(),
            City = command.City?.Trim(),
            IsActive = true,
            IsVerified = false,
            CreatedAt = dateTimeProvider.UtcNow
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new Common.Exceptions.ValidationException(
                nameof(command.Password),
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, RoleNames.Teacher);

        // Every teacher gets a workspace: tenant row, owner membership, default
        // settings/branding/website and a trial subscription, all in this transaction.
        var tenant = await tenantProvisioning.BuildTenantForTeacherAsync(
            user.Id,
            string.IsNullOrWhiteSpace(command.WorkspaceName) ? user.DisplayName : command.WorkspaceName.Trim(),
            command.City?.Trim(),
            email,
            (command.WhatsAppNumber ?? command.PhoneNumber).Trim(),
            cancellationToken);

        await context.Tenants.AddAsync(tenant, cancellationToken);

        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenant.Id,
            Headline = command.Headline?.Trim(),
            Bio = command.Bio?.Trim(),
            YearsOfExperience = command.YearsOfExperience,
            City = command.City?.Trim(),
            WhatsAppNumber = (command.WhatsAppNumber ?? command.PhoneNumber).Trim(),
            PreferredTeachingMode = command.PreferredTeachingMode,
            IsVerified = false,
            // A teacher asks for review once their profile is worth reviewing; an empty
            // profile is not a verification request.
            VerificationStatus = VerificationStatus.NotSubmitted,
            CreatedAt = dateTimeProvider.UtcNow
        };

        foreach (var subjectId in command.SubjectIds.Distinct())
        {
            profile.TeacherSubjects.Add(new TeacherSubject { Id = Guid.NewGuid(), SubjectId = subjectId });
        }

        foreach (var gradeId in command.GradeIds.Distinct())
        {
            profile.TeacherGrades.Add(new TeacherGrade { Id = Guid.NewGuid(), GradeId = gradeId });
        }

        profile.RaiseDomainEvent(new TeacherRegisteredEvent(user.Id, profile.Id, email, user.DisplayName));

        await context.TeacherProfiles.AddAsync(profile, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);
        await AccountEmails.SendWelcomeAndVerificationAsync(
            emails, userManager, configuration, user, RoleNames.Teacher, cancellationToken);

        return await tokenIssuer.IssueAsync(user, cancellationToken: cancellationToken);
    }

    private async Task EnsureReferenceDataExistsAsync(RegisterTeacherCommand command, CancellationToken cancellationToken)
    {
        var subjectIds = command.SubjectIds.Distinct().ToList();
        var knownSubjects = await context.Subjects
            .AsNoTracking()
            .Where(subject => subjectIds.Contains(subject.Id) && subject.IsActive)
            .Select(subject => subject.Id)
            .ToListAsync(cancellationToken);

        var missingSubject = subjectIds.Except(knownSubjects).FirstOrDefault();
        if (missingSubject != Guid.Empty)
        {
            throw new NotFoundException("Subject", missingSubject);
        }

        var gradeIds = command.GradeIds.Distinct().ToList();
        var knownGrades = await context.EducationalGrades
            .AsNoTracking()
            .Where(grade => gradeIds.Contains(grade.Id) && grade.IsActive)
            .Select(grade => grade.Id)
            .ToListAsync(cancellationToken);

        var missingGrade = gradeIds.Except(knownGrades).FirstOrDefault();
        if (missingGrade != Guid.Empty)
        {
            throw new NotFoundException("EducationalGrade", missingGrade);
        }
    }
}
