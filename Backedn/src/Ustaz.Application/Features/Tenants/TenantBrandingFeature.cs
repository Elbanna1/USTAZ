using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Tenants.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Tenants;

public sealed record GetTenantBrandingQuery : IRequest<TenantBrandingDto>;

public sealed class GetTenantBrandingQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetTenantBrandingQuery, TenantBrandingDto>
{
    public async Task<TenantBrandingDto> Handle(GetTenantBrandingQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequireAccessAsync(cancellationToken: cancellationToken);
        return await BrandingReader.LoadAsync(context, access.TenantId, cancellationToken);
    }
}

internal static class BrandingReader
{
    public static async Task<TenantBrandingDto> LoadAsync(
        IApplicationDbContext context,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await context.TenantBrandings
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

public sealed record UpdateTenantBrandingCommand : IRequest<TenantBrandingDto>
{
    public string? PrimaryColorHex { get; init; }

    public string? SecondaryColorHex { get; init; }

    public string? AccentColorHex { get; init; }

    public string? FontFamily { get; init; }

    public string? Tagline { get; init; }

    public string? FaviconUrl { get; init; }
}

public sealed class UpdateTenantBrandingCommandValidator : AbstractValidator<UpdateTenantBrandingCommand>
{
    private const string HexPattern = "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$";

    public UpdateTenantBrandingCommandValidator()
    {
        RuleFor(command => command.PrimaryColorHex!).Matches(HexPattern)
            .When(command => command.PrimaryColorHex is not null)
            .WithMessage("Colours must be hex values such as #2563EB.");

        RuleFor(command => command.SecondaryColorHex!).Matches(HexPattern)
            .When(command => command.SecondaryColorHex is not null)
            .WithMessage("Colours must be hex values such as #1E293B.");

        RuleFor(command => command.AccentColorHex!).Matches(HexPattern)
            .When(command => command.AccentColorHex is not null)
            .WithMessage("Colours must be hex values such as #F59E0B.");

        RuleFor(command => command.FontFamily).MaximumLength(100);
        RuleFor(command => command.Tagline).MaximumLength(DomainRules.ShortTextMaxLength);
        RuleFor(command => command.FaviconUrl).OptionalHttpUrl();
    }
}

public sealed class UpdateTenantBrandingCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<UpdateTenantBrandingCommand, TenantBrandingDto>
{
    public async Task<TenantBrandingDto> Handle(
        UpdateTenantBrandingCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.WebsiteManage, cancellationToken: cancellationToken);

        var branding = await context.TenantBrandings
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("TenantBranding", access.TenantId);

        if (command.PrimaryColorHex is not null)
        {
            branding.PrimaryColorHex = command.PrimaryColorHex;
        }

        if (command.SecondaryColorHex is not null)
        {
            branding.SecondaryColorHex = command.SecondaryColorHex;
        }

        if (command.AccentColorHex is not null)
        {
            branding.AccentColorHex = command.AccentColorHex;
        }

        if (command.FontFamily is not null)
        {
            branding.FontFamily = command.FontFamily.Trim();
        }

        if (command.Tagline is not null)
        {
            branding.Tagline = command.Tagline.Trim();
        }

        if (command.FaviconUrl is not null)
        {
            branding.FaviconUrl = string.IsNullOrWhiteSpace(command.FaviconUrl) ? null : command.FaviconUrl.Trim();
        }

        branding.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.TenantBrandingChanged,
            nameof(TenantBranding),
            branding.Id,
            newValues: command,
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await BrandingReader.LoadAsync(context, access.TenantId, cancellationToken);
    }
}

/// <summary>Which branding image an upload replaces.</summary>
public enum TenantImageKind
{
    Logo = 1,
    Cover = 2
}

public sealed record UploadTenantImageCommand : IRequest<MediaUploadResult>
{
    public required FileUploadRequest File { get; init; }

    public required TenantImageKind Kind { get; init; }
}

public sealed class UploadTenantImageCommandValidator : AbstractValidator<UploadTenantImageCommand>
{
    public UploadTenantImageCommandValidator()
    {
        RuleFor(command => command.Kind).IsInEnum();

        // Size, extension and declared type all come from the one shared rule set.
        RuleFor(command => command.File).ValidImageUpload();
    }
}

public sealed class UploadTenantImageCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IMediaService mediaService,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<UploadTenantImageCommand, MediaUploadResult>
{
    public async Task<MediaUploadResult> Handle(
        UploadTenantImageCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.WebsiteManage, cancellationToken: cancellationToken);

        var branding = await context.TenantBrandings
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("TenantBranding", access.TenantId);

        var folder = command.Kind == TenantImageKind.Logo ? "tenants/logo" : "tenants/cover";
        var previousPublicId = command.Kind == TenantImageKind.Logo
            ? branding.LogoPublicId
            : branding.CoverImagePublicId;

        var uploaded = await mediaService.UploadAsync(
            command.File.Content,
            command.File.FileName,
            folder,
            cancellationToken);

        if (command.Kind == TenantImageKind.Logo)
        {
            branding.LogoUrl = uploaded.SecureUrl ?? uploaded.Url;
            branding.LogoPublicId = uploaded.PublicId;
        }
        else
        {
            branding.CoverImageUrl = uploaded.SecureUrl ?? uploaded.Url;
            branding.CoverImagePublicId = uploaded.PublicId;
        }

        branding.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        // Only drop the old asset once the new one is safely persisted.
        if (!string.IsNullOrWhiteSpace(previousPublicId))
        {
            await mediaService.DeleteAsync(previousPublicId, cancellationToken);
        }

        return uploaded;
    }
}
