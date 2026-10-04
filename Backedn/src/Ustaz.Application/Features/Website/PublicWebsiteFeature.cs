using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Courses.Dtos;
using Ustaz.Application.Features.Courses.Queries;
using Ustaz.Application.Features.Groups.Dtos;
using Ustaz.Application.Features.Groups.Queries;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Application.Features.Tenants.Dtos;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Website;

/// <summary>The anonymous view of a tenant's public site.</summary>
public sealed record PublicTenantDto
{
    public Guid Id { get; init; }

    public string Slug { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? DisplayName { get; init; }

    public string? City { get; init; }

    public string? WhatsAppLink { get; init; }

    public TenantBrandingDto? Branding { get; init; }

    public TenantWebsiteDto? Website { get; init; }

    public int GroupsCount { get; init; }

    public int CoursesCount { get; init; }

    public int StudentsCount { get; init; }

    public decimal Rating { get; init; }

    public int ReviewCount { get; init; }
}

/// <summary>
/// Resolves a public slug to a tenant, refusing anything that is not an active workspace
/// with a published website. Used by every public endpoint below.
/// </summary>
internal static class PublicTenantResolver
{
    public static async Task<Guid> ResolveAsync(
        IApplicationDbContext context,
        string slug,
        CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant();

        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(entity => entity.Slug == normalized && entity.Status == TenantStatus.Active)
            .Select(entity => new { entity.Id })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("No workspace was found at this address.");

        return tenant.Id;
    }
}

public sealed record GetPublicTenantQuery(string Slug) : IRequest<PublicTenantDto>;

public sealed class GetPublicTenantQueryHandler(
    IApplicationDbContext context,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetPublicTenantQuery, PublicTenantDto>
{
    public async Task<PublicTenantDto> Handle(GetPublicTenantQuery query, CancellationToken cancellationToken)
    {
        var tenantId = await PublicTenantResolver.ResolveAsync(context, query.Slug, cancellationToken);

        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(entity => entity.Id == tenantId)
            .Select(entity => new PublicTenantDto
            {
                Id = entity.Id,
                Slug = entity.Slug,
                Name = entity.Name,
                DisplayName = entity.DisplayName,
                City = entity.City,
                GroupsCount = context.Groups.Count(group =>
                    group.TenantId == entity.Id && group.IsPublished && group.IsActive),
                CoursesCount = context.Courses.Count(course =>
                    course.TenantId == entity.Id && course.IsPublished),
                StudentsCount = context.TenantStudents.Count(link =>
                    link.TenantId == entity.Id && link.IsActive),
                Rating = context.TeacherProfiles
                    .Where(profile => profile.TenantId == entity.Id)
                    .Select(profile => profile.Rating)
                    .FirstOrDefault(),
                ReviewCount = context.TeacherProfiles
                    .Where(profile => profile.TenantId == entity.Id)
                    .Select(profile => profile.ReviewCount)
                    .FirstOrDefault(),
                Branding = entity.Branding == null
                    ? null
                    : new TenantBrandingDto
                    {
                        TenantId = entity.Id,
                        LogoUrl = entity.Branding.LogoUrl,
                        CoverImageUrl = entity.Branding.CoverImageUrl,
                        FaviconUrl = entity.Branding.FaviconUrl,
                        PrimaryColorHex = entity.Branding.PrimaryColorHex,
                        SecondaryColorHex = entity.Branding.SecondaryColorHex,
                        AccentColorHex = entity.Branding.AccentColorHex,
                        FontFamily = entity.Branding.FontFamily,
                        Tagline = entity.Branding.Tagline
                    },
                WhatsAppLink = entity.WhatsAppNumber
            })
            .FirstAsync(cancellationToken);

        var website = await context.TenantWebsites
            .AsNoTracking()
            .Where(entity => entity.TenantId == tenantId && entity.IsPublished)
            .Select(WebsiteProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken);

        // Only visible sections and testimonials reach the public site.
        if (website is not null)
        {
            website = website with
            {
                Sections = [.. website.Sections.Where(section => section.IsVisible)],
                Testimonials = [.. website.Testimonials.Where(testimonial => testimonial.IsVisible)],
                WhatsAppLink = whatsAppLinkService.BuildChatLink(website.WhatsAppNumber)
            };
        }

        return tenant with
        {
            Website = website,
            WhatsAppLink = whatsAppLinkService.BuildChatLink(tenant.WhatsAppLink)
        };
    }
}

public sealed record GetPublicTenantBrandingQuery(string Slug) : IRequest<TenantBrandingDto>;

public sealed class GetPublicTenantBrandingQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicTenantBrandingQuery, TenantBrandingDto>
{
    public async Task<TenantBrandingDto> Handle(
        GetPublicTenantBrandingQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = await PublicTenantResolver.ResolveAsync(context, query.Slug, cancellationToken);

        return await context.TenantBrandings
            .AsNoTracking()
            .Where(branding => branding.TenantId == tenantId)
            .Select(branding => new TenantBrandingDto
            {
                TenantId = branding.TenantId,
                LogoUrl = branding.LogoUrl,
                CoverImageUrl = branding.CoverImageUrl,
                FaviconUrl = branding.FaviconUrl,
                PrimaryColorHex = branding.PrimaryColorHex,
                SecondaryColorHex = branding.SecondaryColorHex,
                AccentColorHex = branding.AccentColorHex,
                FontFamily = branding.FontFamily,
                Tagline = branding.Tagline
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("TenantBranding", tenantId);
    }
}

public sealed record GetPublicTenantSeoQuery(string Slug) : IRequest<WebsiteSeoDto>;

public sealed class GetPublicTenantSeoQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicTenantSeoQuery, WebsiteSeoDto>
{
    public async Task<WebsiteSeoDto> Handle(GetPublicTenantSeoQuery query, CancellationToken cancellationToken)
    {
        var tenantId = await PublicTenantResolver.ResolveAsync(context, query.Slug, cancellationToken);

        var seo = await context.TenantWebsites
            .AsNoTracking()
            .Where(website => website.TenantId == tenantId)
            .Select(website => new WebsiteSeoDto
            {
                Title = website.SeoTitle,
                Description = website.SeoDescription,
                Keywords = website.SeoKeywords,
                ImageUrl = website.SeoImageUrl,
                GoogleAnalyticsId = website.GoogleAnalyticsId,
                FacebookPixelId = website.FacebookPixelId
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("TenantWebsite", tenantId);

        // The canonical URL prefers a verified primary custom domain.
        var primaryHost = await context.TenantDomains
            .AsNoTracking()
            .Where(domain => domain.TenantId == tenantId
                && domain.IsPrimary
                && domain.VerificationStatus == DomainVerificationStatus.Verified)
            .Select(domain => domain.Host)
            .FirstOrDefaultAsync(cancellationToken);

        return seo with
        {
            CanonicalUrl = primaryHost is null ? null : "https://" + primaryHost
        };
    }
}

public sealed record GetPublicTenantGroupsQuery : PagedRequest, IRequest<PaginatedList<GroupListItemDto>>
{
    public string Slug { get; init; } = string.Empty;
}

public sealed class GetPublicTenantGroupsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicTenantGroupsQuery, PaginatedList<GroupListItemDto>>
{
    public async Task<PaginatedList<GroupListItemDto>> Handle(
        GetPublicTenantGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = await PublicTenantResolver.ResolveAsync(context, query.Slug, cancellationToken);

        return await context.Groups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId && group.IsPublished && group.IsActive)
            .OrderByDescending(group => group.CreatedAt)
            .Select(GroupProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetPublicTenantCoursesQuery : PagedRequest, IRequest<PaginatedList<CourseListItemDto>>
{
    public string Slug { get; init; } = string.Empty;
}

public sealed class GetPublicTenantCoursesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicTenantCoursesQuery, PaginatedList<CourseListItemDto>>
{
    public async Task<PaginatedList<CourseListItemDto>> Handle(
        GetPublicTenantCoursesQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = await PublicTenantResolver.ResolveAsync(context, query.Slug, cancellationToken);

        return await context.Courses
            .AsNoTracking()
            .Where(course => course.TenantId == tenantId && course.IsPublished)
            .OrderByDescending(course => course.IsFeatured)
            .ThenByDescending(course => course.CreatedAt)
            .Select(CourseProjections.ToListItem)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

public sealed record GetPublicTenantTeachersQuery(string Slug) : IRequest<IReadOnlyList<TeacherListItemDto>>;

public sealed class GetPublicTenantTeachersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPublicTenantTeachersQuery, IReadOnlyList<TeacherListItemDto>>
{
    public async Task<IReadOnlyList<TeacherListItemDto>> Handle(
        GetPublicTenantTeachersQuery query,
        CancellationToken cancellationToken)
    {
        var tenantId = await PublicTenantResolver.ResolveAsync(context, query.Slug, cancellationToken);

        return await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.User.IsActive)
            .OrderByDescending(profile => profile.Rating)
            .Select(TeacherProjections.ToListItem)
            .ToListAsync(cancellationToken);
    }
}
