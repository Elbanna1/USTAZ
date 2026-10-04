using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Authentication.Commands;

/// <summary>
/// Sets the language the caller reads the platform in. One field drives both channels: the
/// notification API returns the matching text and every email is rendered in it.
/// </summary>
public sealed record UpdateLanguagePreferenceCommand : IRequest<Unit>
{
    /// <summary><c>ar-EG</c> or <c>en</c>.</summary>
    public required string Language { get; init; }
}

public sealed class UpdateLanguagePreferenceCommandValidator : AbstractValidator<UpdateLanguagePreferenceCommand>
{
    public UpdateLanguagePreferenceCommandValidator() =>
        RuleFor(command => command.Language)
            .NotEmpty()
            .Must(TransactionalEmailLanguage.IsSupported)
            .WithMessage("Supported languages are 'ar-EG' and 'en'.");
}

public sealed class UpdateLanguagePreferenceCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateLanguagePreferenceCommand, Unit>
{
    public async Task<Unit> Handle(UpdateLanguagePreferenceCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        user.PreferredLanguage = command.Language.Equals(
            TransactionalEmailLanguage.English, StringComparison.OrdinalIgnoreCase)
            ? TransactionalEmailLanguage.English
            : TransactionalEmailLanguage.Arabic;

        user.UpdatedAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
