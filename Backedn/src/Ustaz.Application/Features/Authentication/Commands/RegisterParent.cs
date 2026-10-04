using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Events;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

/// <summary>
/// Creates a guardian account.
///
/// The Parent role, <see cref="ParentProfile"/>, the guardian/student link and the entire
/// /parent API already existed, but nothing could create the account they all depend on — so
/// the role was unreachable in production and only reachable in a seeded database. This is
/// that missing entry point.
///
/// A guardian may name one child at sign-up. Naming one does not grant access: the link is
/// created through the same rules as <c>LinkChildCommand</c>, so it is only auto-confirmed
/// when the supplied phone number matches the parent number on the student's own profile, and
/// the student is always notified either way.
/// </summary>
public sealed record RegisterParentCommand : IRequest<AuthenticationResponse>
{
    public required string FirstName { get; init; }

    public required string LastName { get; init; }

    public required string Email { get; init; }

    public required string Password { get; init; }

    public required string PhoneNumber { get; init; }

    public string? AlternatePhoneNumber { get; init; }

    public string? City { get; init; }

    public string? Occupation { get; init; }

    /// <summary>Optional: the email of a child to request access to during sign-up.</summary>
    public string? ChildEmail { get; init; }

    /// <summary>Father, Mother, Guardian, ...</summary>
    public string? Relationship { get; init; }
}

public sealed class RegisterParentCommandValidator : AbstractValidator<RegisterParentCommand>
{
    public RegisterParentCommandValidator()
    {
        RuleFor(command => command.FirstName).PersonName();
        RuleFor(command => command.LastName).PersonName();
        RuleFor(command => command.Email).ValidEmail();
        RuleFor(command => command.Password).StrongPassword();
        RuleFor(command => command.PhoneNumber).EgyptianPhone();
        RuleFor(command => command.AlternatePhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.City).MaximumLength(100);
        RuleFor(command => command.Occupation).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Relationship).MaximumLength(50);

        RuleFor(command => command.ChildEmail).OptionalEmail();
    }
}

public sealed class RegisterParentCommandHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IAuthenticationTokenIssuer tokenIssuer,
    IDateTimeProvider dateTimeProvider,
    ITransactionalEmailService emails,
    INotificationDispatcher notifications,
    IConfiguration configuration) : IRequestHandler<RegisterParentCommand, AuthenticationResponse>
{
    public async Task<AuthenticationResponse> Handle(
        RegisterParentCommand command,
        CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email address already exists.");
        }

        var utcNow = dateTimeProvider.UtcNow;

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
            CreatedAt = utcNow
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            throw new Common.Exceptions.ValidationException(
                nameof(command.Password),
                string.Join(" ", result.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, RoleNames.Parent);

        var profile = new ParentProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PhoneNumber = command.PhoneNumber.Trim(),
            AlternatePhoneNumber = command.AlternatePhoneNumber?.Trim(),
            City = command.City?.Trim(),
            Occupation = command.Occupation?.Trim(),
            CreatedAt = utcNow
        };

        profile.RaiseDomainEvent(new ParentRegisteredEvent(user.Id, profile.Id, email, user.DisplayName));

        await context.ParentProfiles.AddAsync(profile, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(command.ChildEmail))
        {
            await RequestChildLinkAsync(profile, command, utcNow, cancellationToken);
        }

        await AccountEmails.SendWelcomeAndVerificationAsync(
            emails, userManager, configuration, user, RoleNames.Parent, cancellationToken);

        return await tokenIssuer.IssueAsync(user, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Requests access to the named child. A sign-up that names an unknown email still
    /// succeeds — refusing the whole registration would turn this field into an account
    /// oracle for student emails, and the guardian can link the child later from their
    /// dashboard.
    /// </summary>
    private async Task RequestChildLinkAsync(
        ParentProfile parent,
        RegisterParentCommand command,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var childEmail = command.ChildEmail!.Trim().ToLowerInvariant();

        var student = await context.StudentProfiles
            .AsNoTracking()
            .Where(entity => entity.User.Email == childEmail)
            .Select(entity => new { entity.Id, entity.UserId, entity.ParentPhoneNumber })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return;
        }

        // Identical rule to LinkChildCommand: a link is only confirmed when the guardian can
        // prove the relationship with the number already on the student's profile.
        var autoConfirm =
            !string.IsNullOrWhiteSpace(student.ParentPhoneNumber)
            && Domain.ValueObjects.EgyptianPhoneNumber.TryCreate(command.PhoneNumber, out var supplied)
            && Domain.ValueObjects.EgyptianPhoneNumber.TryCreate(student.ParentPhoneNumber, out var onFile)
            && supplied.Value == onFile.Value;

        var link = new ParentStudentLink
        {
            Id = Guid.NewGuid(),
            ParentId = parent.Id,
            StudentId = student.Id,
            Relationship = command.Relationship?.Trim(),
            IsPrimaryContact = true,
            IsConfirmed = autoConfirm,
            ConfirmedAt = autoConfirm ? utcNow : null,
            CreatedAt = utcNow
        };

        await context.ParentStudentLinks.AddAsync(link, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await notifications.NotifyAsync(
            student.UserId,
            autoConfirm ? "ولي أمر اتربط بحسابك" : "في ولي أمر عايز يتابع حسابك",
            autoConfirm
                ? "ولي أمرك بقى يقدر يتابع حضورك ودرجاتك."
                : "في ولي أمر طلب يتابع حسابك. تقدر توافق أو ترفض من إعدادات حسابك.",
            Domain.Enums.NotificationType.Security,
            Domain.Enums.NotificationCategory.Security,
            null,
            "/student/guardians",
            // The link, not the parent: this is the id the student answers the request with.
            link.Id,
            cancellationToken);
    }
}
