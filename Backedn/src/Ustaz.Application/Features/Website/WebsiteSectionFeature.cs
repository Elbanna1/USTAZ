using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Website;

internal static class SectionHelper
{
    /// <summary>Resolves the caller's website id after checking the manage permission.</summary>
    public static async Task<(TenantAccess Access, Guid WebsiteId)> ResolveAsync(
        IApplicationDbContext context,
        ITenantAuthorizationService tenantAuthorization,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.WebsiteManage, cancellationToken: cancellationToken);

        var websiteId = await context.TenantWebsites
            .AsNoTracking()
            .Where(website => website.TenantId == access.TenantId)
            .Select(website => (Guid?)website.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("TenantWebsite", access.TenantId);

        return (access, websiteId);
    }

    public static WebsiteSectionDto ToDto(WebsiteSection section) => new()
    {
        Id = section.Id,
        Type = section.Type,
        Title = section.Title,
        Subtitle = section.Subtitle,
        Body = section.Body,
        ImageUrl = section.ImageUrl,
        SettingsJson = section.SettingsJson,
        IsVisible = section.IsVisible,
        SortOrder = section.SortOrder
    };
}

public sealed record GetWebsiteSectionsQuery : IRequest<IReadOnlyList<WebsiteSectionDto>>;

public sealed class GetWebsiteSectionsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetWebsiteSectionsQuery, IReadOnlyList<WebsiteSectionDto>>
{
    public async Task<IReadOnlyList<WebsiteSectionDto>> Handle(
        GetWebsiteSectionsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.WebsiteView, cancellationToken: cancellationToken);

        return await context.WebsiteSections
            .AsNoTracking()
            .Where(section => section.TenantId == access.TenantId)
            .OrderBy(section => section.SortOrder)
            .Select(section => new WebsiteSectionDto
            {
                Id = section.Id,
                Type = section.Type,
                Title = section.Title,
                Subtitle = section.Subtitle,
                Body = section.Body,
                ImageUrl = section.ImageUrl,
                SettingsJson = section.SettingsJson,
                IsVisible = section.IsVisible,
                SortOrder = section.SortOrder
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed record CreateWebsiteSectionCommand : IRequest<WebsiteSectionDto>
{
    public WebsiteSectionType Type { get; init; } = WebsiteSectionType.Custom;

    public string? Title { get; init; }

    public string? Subtitle { get; init; }

    public string? Body { get; init; }

    public string? ImageUrl { get; init; }

    public string? SettingsJson { get; init; }

    public bool IsVisible { get; init; } = true;

    public int? SortOrder { get; init; }
}

public sealed class CreateWebsiteSectionCommandValidator : AbstractValidator<CreateWebsiteSectionCommand>
{
    public CreateWebsiteSectionCommandValidator()
    {
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.Title).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.Subtitle).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.Body).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.ImageUrl).OptionalHttpUrl();
        RuleFor(command => command.SettingsJson).MaximumLength(4000);
    }
}

public sealed class CreateWebsiteSectionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<CreateWebsiteSectionCommand, WebsiteSectionDto>
{
    public async Task<WebsiteSectionDto> Handle(
        CreateWebsiteSectionCommand command,
        CancellationToken cancellationToken)
    {
        var (access, websiteId) = await SectionHelper.ResolveAsync(context, tenantAuthorization, cancellationToken);

        await planLimits.EnsureFeatureAsync(
            access.TenantId, PlanFeatures.WebsiteCustomization, cancellationToken);

        var nextOrder = command.SortOrder ?? await context.WebsiteSections
            .Where(section => section.TenantId == access.TenantId)
            .CountAsync(cancellationToken) + 1;

        var section = new WebsiteSection
        {
            Id = Guid.NewGuid(),
            TenantWebsiteId = websiteId,
            TenantId = access.TenantId,
            Type = command.Type,
            Title = command.Title?.Trim(),
            Subtitle = command.Subtitle?.Trim(),
            Body = command.Body?.Trim(),
            ImageUrl = command.ImageUrl?.Trim(),
            SettingsJson = command.SettingsJson,
            IsVisible = command.IsVisible,
            SortOrder = nextOrder,
            CreatedAt = dateTimeProvider.UtcNow
        };

        await context.WebsiteSections.AddAsync(section, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return SectionHelper.ToDto(section);
    }
}

public sealed record UpdateWebsiteSectionCommand : IRequest<WebsiteSectionDto>
{
    public Guid SectionId { get; init; }

    public string? Title { get; init; }

    public string? Subtitle { get; init; }

    public string? Body { get; init; }

    public string? ImageUrl { get; init; }

    public string? SettingsJson { get; init; }

    public bool? IsVisible { get; init; }

    public int? SortOrder { get; init; }
}

public sealed class UpdateWebsiteSectionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UpdateWebsiteSectionCommand, WebsiteSectionDto>
{
    public async Task<WebsiteSectionDto> Handle(
        UpdateWebsiteSectionCommand command,
        CancellationToken cancellationToken)
    {
        var (access, _) = await SectionHelper.ResolveAsync(context, tenantAuthorization, cancellationToken);

        var section = await context.WebsiteSections
            .FirstOrDefaultAsync(entity => entity.Id == command.SectionId, cancellationToken)
            ?? throw new NotFoundException("WebsiteSection", command.SectionId);

        tenantAuthorization.EnsureSameTenant(access, section.TenantId);

        if (command.Title is not null)
        {
            section.Title = command.Title.Trim();
        }

        if (command.Subtitle is not null)
        {
            section.Subtitle = command.Subtitle.Trim();
        }

        if (command.Body is not null)
        {
            section.Body = command.Body.Trim();
        }

        if (command.ImageUrl is not null)
        {
            section.ImageUrl = string.IsNullOrWhiteSpace(command.ImageUrl) ? null : command.ImageUrl.Trim();
        }

        if (command.SettingsJson is not null)
        {
            section.SettingsJson = command.SettingsJson;
        }

        if (command.IsVisible.HasValue)
        {
            section.IsVisible = command.IsVisible.Value;
        }

        if (command.SortOrder.HasValue)
        {
            section.SortOrder = command.SortOrder.Value;
        }

        section.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return SectionHelper.ToDto(section);
    }
}

public sealed record DeleteWebsiteSectionCommand(Guid SectionId) : IRequest<Unit>;

public sealed class DeleteWebsiteSectionCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<DeleteWebsiteSectionCommand, Unit>
{
    public async Task<Unit> Handle(DeleteWebsiteSectionCommand command, CancellationToken cancellationToken)
    {
        var (access, _) = await SectionHelper.ResolveAsync(context, tenantAuthorization, cancellationToken);

        var section = await context.WebsiteSections
            .FirstOrDefaultAsync(entity => entity.Id == command.SectionId, cancellationToken)
            ?? throw new NotFoundException("WebsiteSection", command.SectionId);

        tenantAuthorization.EnsureSameTenant(access, section.TenantId);

        context.WebsiteSections.Remove(section);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

/// <summary>Re-orders sections in one call; the array position becomes the sort order.</summary>
public sealed record ReorderWebsiteSectionsCommand : IRequest<IReadOnlyList<WebsiteSectionDto>>
{
    public IReadOnlyList<Guid> SectionIds { get; init; } = [];
}

public sealed class ReorderWebsiteSectionsCommandValidator : AbstractValidator<ReorderWebsiteSectionsCommand>
{
    public ReorderWebsiteSectionsCommandValidator() =>
        RuleFor(command => command.SectionIds)
            .NotEmpty().WithMessage("Provide the section ids in their new order.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Each section may appear only once.");
}

public sealed class ReorderWebsiteSectionsCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<ReorderWebsiteSectionsCommand, IReadOnlyList<WebsiteSectionDto>>
{
    public async Task<IReadOnlyList<WebsiteSectionDto>> Handle(
        ReorderWebsiteSectionsCommand command,
        CancellationToken cancellationToken)
    {
        var (access, _) = await SectionHelper.ResolveAsync(context, tenantAuthorization, cancellationToken);

        var sections = await context.WebsiteSections
            .Where(section => section.TenantId == access.TenantId)
            .ToListAsync(cancellationToken);

        // Every id must belong to this workspace, so a foreign section cannot be reordered in.
        var stranger = command.SectionIds.FirstOrDefault(id => sections.All(section => section.Id != id));
        if (stranger != Guid.Empty)
        {
            throw new NotFoundException("WebsiteSection", stranger);
        }

        var utcNow = dateTimeProvider.UtcNow;

        for (var index = 0; index < command.SectionIds.Count; index++)
        {
            var section = sections.First(entity => entity.Id == command.SectionIds[index]);
            section.SortOrder = index + 1;
            section.UpdatedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetWebsiteSectionsQuery(), cancellationToken);
    }
}
