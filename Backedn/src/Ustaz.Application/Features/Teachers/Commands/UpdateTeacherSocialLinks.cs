using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Commands;

public sealed record SocialLinkInput
{
    public SocialPlatform Platform { get; init; }

    public string Url { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

/// <summary>Replaces the calling teacher's social links. Each URL is validated against its platform.</summary>
public sealed record UpdateTeacherSocialLinksCommand : IRequest<IReadOnlyList<TeacherSocialLinkDto>>
{
    public IReadOnlyList<SocialLinkInput> Links { get; init; } = [];
}

public sealed class UpdateTeacherSocialLinksCommandValidator : AbstractValidator<UpdateTeacherSocialLinksCommand>
{
    public UpdateTeacherSocialLinksCommandValidator()
    {
        RuleFor(command => command.Links)
            .Must(links => links.Select(link => link.Platform).Distinct().Count() == links.Count)
            .WithMessage("Each social platform may only appear once.");

        RuleForEach(command => command.Links).ChildRules(link =>
        {
            link.RuleFor(item => item.Platform).IsInEnum();
            link.RuleFor(item => item.Url).HttpUrl();
        });
    }
}

public sealed class UpdateTeacherSocialLinksCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IUrlGuard urlGuard) : IRequestHandler<UpdateTeacherSocialLinksCommand, IReadOnlyList<TeacherSocialLinkDto>>
{
    public async Task<IReadOnlyList<TeacherSocialLinkDto>> Handle(
        UpdateTeacherSocialLinksCommand command,
        CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();

        foreach (var link in command.Links)
        {
            urlGuard.EnsureSafeHttpUrl(link.Url, link.Platform + " link");

            if (!urlGuard.MatchesPlatform(link.Url, link.Platform))
            {
                throw new BusinessRuleException(
                    "SOCIAL_LINK_PLATFORM_MISMATCH",
                    "The supplied URL does not belong to " + link.Platform + ".");
            }
        }

        var existing = await context.TeacherSocialLinks
            .Where(link => link.TeacherId == teacherId)
            .ToListAsync(cancellationToken);

        context.TeacherSocialLinks.RemoveRange(existing);

        var replacements = command.Links
            .Select((link, index) => new TeacherSocialLink
            {
                Id = Guid.NewGuid(),
                TeacherId = teacherId,
                Platform = link.Platform,
                Url = link.Url.Trim(),
                SortOrder = link.SortOrder == 0 ? index + 1 : link.SortOrder
            })
            .ToList();

        await context.TeacherSocialLinks.AddRangeAsync(replacements, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return [.. replacements
            .OrderBy(link => link.SortOrder)
            .Select(link => new TeacherSocialLinkDto
            {
                Id = link.Id,
                Platform = link.Platform,
                Url = link.Url,
                SortOrder = link.SortOrder
            })];
    }
}
