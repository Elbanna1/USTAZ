using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Notifications;

public sealed record NotificationPreferenceDto
{
    public NotificationCategory Category { get; init; }

    public string CategoryName => Category.ToString();

    public bool IsEnabled { get; init; }

    public bool InApp { get; init; }

    public bool Email { get; init; }

    public bool WhatsApp { get; init; }
}

/// <summary>
/// The caller's delivery preferences. Every category is returned, defaults included, so the
/// client never has to guess what a missing row means.
/// </summary>
public sealed record GetNotificationPreferencesQuery : IRequest<IReadOnlyList<NotificationPreferenceDto>>;

public sealed class GetNotificationPreferencesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(
        GetNotificationPreferencesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var stored = await context.NotificationPreferences
            .AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .ToListAsync(cancellationToken);

        return
        [
            .. Enum.GetValues<NotificationCategory>().Select(category =>
            {
                var preference = stored.FirstOrDefault(entry => entry.Category == category);
                var channels = preference?.Channels ?? NotificationDefaults.ChannelsFor(category);

                return new NotificationPreferenceDto
                {
                    Category = category,
                    IsEnabled = preference?.IsEnabled ?? true,
                    InApp = channels.HasFlag(NotificationChannel.InApp),
                    Email = channels.HasFlag(NotificationChannel.Email),
                    WhatsApp = channels.HasFlag(NotificationChannel.WhatsApp)
                };
            })
        ];
    }
}

public sealed record NotificationPreferenceInput
{
    public NotificationCategory Category { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool InApp { get; init; } = true;

    public bool Email { get; init; }

    public bool WhatsApp { get; init; }
}

public sealed record UpdateNotificationPreferencesCommand : IRequest<IReadOnlyList<NotificationPreferenceDto>>
{
    public IReadOnlyList<NotificationPreferenceInput> Preferences { get; init; } = [];
}

public sealed class UpdateNotificationPreferencesCommandValidator
    : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator()
    {
        RuleFor(command => command.Preferences)
            .NotEmpty().WithMessage("Provide at least one preference.")
            .Must(preferences => preferences.Select(entry => entry.Category).Distinct().Count() == preferences.Count)
            .WithMessage("Each category may appear only once.");

        RuleForEach(command => command.Preferences).ChildRules(preference =>
            preference.RuleFor(entry => entry.Category).IsInEnum());
    }
}

public sealed class UpdateNotificationPreferencesCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator)
    : IRequestHandler<UpdateNotificationPreferencesCommand, IReadOnlyList<NotificationPreferenceDto>>
{
    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(
        UpdateNotificationPreferencesCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        var utcNow = dateTimeProvider.UtcNow;

        var stored = await context.NotificationPreferences
            .Where(preference => preference.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var input in command.Preferences)
        {
            var channels = NotificationChannel.None;

            if (input.InApp)
            {
                channels |= NotificationChannel.InApp;
            }

            if (input.Email)
            {
                channels |= NotificationChannel.Email;
            }

            if (input.WhatsApp)
            {
                channels |= NotificationChannel.WhatsApp;
            }

            var preference = stored.FirstOrDefault(entry => entry.Category == input.Category);

            if (preference is null)
            {
                preference = new NotificationPreference
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Category = input.Category,
                    CreatedAt = utcNow
                };

                await context.NotificationPreferences.AddAsync(preference, cancellationToken);
            }

            preference.Channels = channels;
            preference.IsEnabled = input.IsEnabled;
            preference.UpdatedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetNotificationPreferencesQuery(), cancellationToken);
    }
}
