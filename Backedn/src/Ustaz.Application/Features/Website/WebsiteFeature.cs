using System.Linq.Expressions;
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

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------
public sealed record TenantWebsiteDto
{
    public Guid TenantId { get; init; }

    public bool IsPublished { get; init; }

    public string? HeroTitle { get; init; }

    public string? HeroSubtitle { get; init; }

    public string? HeroImageUrl { get; init; }

    public string? HeroCtaLabel { get; init; }

    public string? HeroCtaUrl { get; init; }

    public string? AboutTitle { get; init; }

    public string? AboutBody { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? ContactAddress { get; init; }

    public string? WhatsAppNumber { get; init; }

    public string? WhatsAppLink { get; init; }

    public string? MapEmbedUrl { get; init; }

    public WebsiteSeoDto Seo { get; init; } = new();

    public IReadOnlyList<WebsiteSectionDto> Sections { get; init; } = [];

    public IReadOnlyList<TestimonialDto> Testimonials { get; init; } = [];
}

public sealed record WebsiteSeoDto
{
    public string? Title { get; init; }

    public string? Description { get; init; }

    public string? Keywords { get; init; }

    public string? ImageUrl { get; init; }

    public string? GoogleAnalyticsId { get; init; }

    public string? FacebookPixelId { get; init; }

    public string? CanonicalUrl { get; init; }
}

public sealed record WebsiteSectionDto
{
    public Guid Id { get; init; }

    public WebsiteSectionType Type { get; init; }

    public string TypeName => Type.ToString();

    public string? Title { get; init; }

    public string? Subtitle { get; init; }

    public string? Body { get; init; }

    public string? ImageUrl { get; init; }

    public string? SettingsJson { get; init; }

    public bool IsVisible { get; init; }

    public int SortOrder { get; init; }
}

public sealed record TestimonialDto
{
    public Guid Id { get; init; }

    public string AuthorName { get; init; } = string.Empty;

    public string? AuthorTitle { get; init; }

    public string? AuthorImageUrl { get; init; }

    public string Quote { get; init; } = string.Empty;

    public int Rating { get; init; }

    public bool IsVisible { get; init; }

    public int SortOrder { get; init; }
}

internal static class WebsiteProjections
{
    public static Expression<Func<TenantWebsite, TenantWebsiteDto>> ToDto =>
        website => new TenantWebsiteDto
        {
            TenantId = website.TenantId,
            IsPublished = website.IsPublished,
            HeroTitle = website.HeroTitle,
            HeroSubtitle = website.HeroSubtitle,
            HeroImageUrl = website.HeroImageUrl,
            HeroCtaLabel = website.HeroCtaLabel,
            HeroCtaUrl = website.HeroCtaUrl,
            AboutTitle = website.AboutTitle,
            AboutBody = website.AboutBody,
            ContactEmail = website.ContactEmail,
            ContactPhone = website.ContactPhone,
            ContactAddress = website.ContactAddress,
            WhatsAppNumber = website.WhatsAppNumber,
            MapEmbedUrl = website.MapEmbedUrl,
            Seo = new WebsiteSeoDto
            {
                Title = website.SeoTitle,
                Description = website.SeoDescription,
                Keywords = website.SeoKeywords,
                ImageUrl = website.SeoImageUrl,
                GoogleAnalyticsId = website.GoogleAnalyticsId,
                FacebookPixelId = website.FacebookPixelId
            },
            Sections = website.Sections
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
                .ToList(),
            Testimonials = website.Testimonials
                .OrderBy(testimonial => testimonial.SortOrder)
                .Select(testimonial => new TestimonialDto
                {
                    Id = testimonial.Id,
                    AuthorName = testimonial.AuthorName,
                    AuthorTitle = testimonial.AuthorTitle,
                    AuthorImageUrl = testimonial.AuthorImageUrl,
                    Quote = testimonial.Quote,
                    Rating = testimonial.Rating,
                    IsVisible = testimonial.IsVisible,
                    SortOrder = testimonial.SortOrder
                })
                .ToList()
        };
}

// ---------------------------------------------------------------------------
// Read / update the website
// ---------------------------------------------------------------------------
public sealed record GetTenantWebsiteQuery : IRequest<TenantWebsiteDto>;

public sealed class GetTenantWebsiteQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetTenantWebsiteQuery, TenantWebsiteDto>
{
    public async Task<TenantWebsiteDto> Handle(GetTenantWebsiteQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.WebsiteView, cancellationToken: cancellationToken);

        var website = await context.TenantWebsites
            .AsNoTracking()
            .Where(entity => entity.TenantId == access.TenantId)
            .Select(WebsiteProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("TenantWebsite", access.TenantId);

        return website with { WhatsAppLink = whatsAppLinkService.BuildChatLink(website.WhatsAppNumber) };
    }
}

public sealed record UpdateTenantWebsiteCommand : IRequest<TenantWebsiteDto>
{
    public bool? IsPublished { get; init; }

    public string? HeroTitle { get; init; }

    public string? HeroSubtitle { get; init; }

    public string? HeroImageUrl { get; init; }

    public string? HeroCtaLabel { get; init; }

    public string? HeroCtaUrl { get; init; }

    public string? AboutTitle { get; init; }

    public string? AboutBody { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? ContactAddress { get; init; }

    public string? WhatsAppNumber { get; init; }

    public string? MapEmbedUrl { get; init; }

    public string? SeoTitle { get; init; }

    public string? SeoDescription { get; init; }

    public string? SeoKeywords { get; init; }

    public string? SeoImageUrl { get; init; }

    public string? GoogleAnalyticsId { get; init; }

    public string? FacebookPixelId { get; init; }
}

public sealed class UpdateTenantWebsiteCommandValidator : AbstractValidator<UpdateTenantWebsiteCommand>
{
    public UpdateTenantWebsiteCommandValidator()
    {
        RuleFor(command => command.HeroTitle).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.HeroSubtitle).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.HeroImageUrl).OptionalHttpUrl();
        RuleFor(command => command.HeroCtaUrl).OptionalHttpUrl();
        RuleFor(command => command.HeroCtaLabel).MaximumLength(100);
        RuleFor(command => command.AboutTitle).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.AboutBody).MaximumLength(DomainRules.DescriptionMaxLength);
        RuleFor(command => command.ContactAddress).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.WhatsAppNumber).OptionalEgyptianPhone();
        RuleFor(command => command.ContactPhone).OptionalEgyptianPhone();
        RuleFor(command => command.MapEmbedUrl).OptionalHttpUrl();
        RuleFor(command => command.SeoImageUrl).OptionalHttpUrl();
        RuleFor(command => command.SeoTitle).MaximumLength(200);
        RuleFor(command => command.SeoDescription).MaximumLength(500);
        RuleFor(command => command.SeoKeywords).MaximumLength(500);
        RuleFor(command => command.GoogleAnalyticsId).MaximumLength(50);
        RuleFor(command => command.FacebookPixelId).MaximumLength(50);

        RuleFor(command => command.ContactEmail)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.ContactEmail));
    }
}

public sealed class UpdateTenantWebsiteCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<UpdateTenantWebsiteCommand, TenantWebsiteDto>
{
    public async Task<TenantWebsiteDto> Handle(
        UpdateTenantWebsiteCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.WebsiteManage, cancellationToken: cancellationToken);

        await planLimits.EnsureFeatureAsync(
            access.TenantId, PlanFeatures.WebsiteCustomization, cancellationToken);

        var website = await context.TenantWebsites
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("TenantWebsite", access.TenantId);

        if (command.IsPublished.HasValue)
        {
            website.IsPublished = command.IsPublished.Value;
        }

        Assign(command.HeroTitle, value => website.HeroTitle = value);
        Assign(command.HeroSubtitle, value => website.HeroSubtitle = value);
        Assign(command.HeroImageUrl, value => website.HeroImageUrl = value);
        Assign(command.HeroCtaLabel, value => website.HeroCtaLabel = value);
        Assign(command.HeroCtaUrl, value => website.HeroCtaUrl = value);
        Assign(command.AboutTitle, value => website.AboutTitle = value);
        Assign(command.AboutBody, value => website.AboutBody = value);
        Assign(command.ContactEmail, value => website.ContactEmail = value);
        Assign(command.ContactPhone, value => website.ContactPhone = value);
        Assign(command.ContactAddress, value => website.ContactAddress = value);
        Assign(command.WhatsAppNumber, value => website.WhatsAppNumber = value);
        Assign(command.MapEmbedUrl, value => website.MapEmbedUrl = value);
        Assign(command.SeoTitle, value => website.SeoTitle = value);
        Assign(command.SeoDescription, value => website.SeoDescription = value);
        Assign(command.SeoKeywords, value => website.SeoKeywords = value);
        Assign(command.SeoImageUrl, value => website.SeoImageUrl = value);
        Assign(command.GoogleAnalyticsId, value => website.GoogleAnalyticsId = value);
        Assign(command.FacebookPixelId, value => website.FacebookPixelId = value);

        website.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.WebsiteChanged,
            nameof(TenantWebsite),
            website.Id,
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetTenantWebsiteQuery(), cancellationToken);

        static void Assign(string? value, Action<string?> setter)
        {
            if (value is not null)
            {
                setter(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
            }
        }
    }
}
