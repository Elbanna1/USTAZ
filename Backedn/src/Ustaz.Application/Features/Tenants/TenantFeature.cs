using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Tenants.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Tenants;

internal static class TenantProjections
{
    public static Expression<Func<Tenant, TenantDto>> ToDto =>
        tenant => new TenantDto
        {
            Id = tenant.Id,
            Slug = tenant.Slug,
            Name = tenant.Name,
            DisplayName = tenant.DisplayName,
            Status = tenant.Status,
            OwnerUserId = tenant.OwnerUserId,
            ContactEmail = tenant.ContactEmail,
            ContactPhone = tenant.ContactPhone,
            WhatsAppNumber = tenant.WhatsAppNumber,
            City = tenant.City,
            MembersCount = tenant.Members.Count(member => member.Status == Domain.Enums.TenantMemberStatus.Active),
            StudentsCount = tenant.Students.Count(student => student.IsActive),
            CreatedAt = tenant.CreatedAt,
            Branding = tenant.Branding == null
                ? null
                : new TenantBrandingDto
                {
                    TenantId = tenant.Id,
                    LogoUrl = tenant.Branding.LogoUrl,
                    CoverImageUrl = tenant.Branding.CoverImageUrl,
                    FaviconUrl = tenant.Branding.FaviconUrl,
                    PrimaryColorHex = tenant.Branding.PrimaryColorHex,
                    SecondaryColorHex = tenant.Branding.SecondaryColorHex,
                    AccentColorHex = tenant.Branding.AccentColorHex,
                    FontFamily = tenant.Branding.FontFamily,
                    Tagline = tenant.Branding.Tagline
                }
        };
}

// ---------------------------------------------------------------------------
// Read the caller's workspace
// ---------------------------------------------------------------------------
public sealed record GetTenantQuery(Guid? TenantId = null) : IRequest<TenantDto>;

public sealed class GetTenantQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IWhatsAppLinkService whatsAppLinkService) : IRequestHandler<GetTenantQuery, TenantDto>
{
    public async Task<TenantDto> Handle(GetTenantQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequireAccessAsync(query.TenantId, cancellationToken);

        var tenant = await context.Tenants
            .AsNoTracking()
            .Where(entity => entity.Id == access.TenantId)
            .Select(TenantProjections.ToDto)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Tenant", access.TenantId);

        var counters = await context.Tenants
            .AsNoTracking()
            .Where(entity => entity.Id == access.TenantId)
            .Select(entity => new
            {
                Groups = context.Groups.Count(group => group.TenantId == entity.Id && group.IsActive),
                Courses = context.Courses.Count(course => course.TenantId == entity.Id),
                OwnerName = context.Users
                    .Where(user => user.Id == entity.OwnerUserId)
                    .Select(user => user.DisplayName)
                    .FirstOrDefault()
            })
            .FirstAsync(cancellationToken);

        return tenant with
        {
            GroupsCount = counters.Groups,
            CoursesCount = counters.Courses,
            OwnerDisplayName = counters.OwnerName,
            WhatsAppLink = whatsAppLinkService.BuildChatLink(tenant.WhatsAppNumber)
        };
    }
}

// ---------------------------------------------------------------------------
// Update workspace identity
// ---------------------------------------------------------------------------
public sealed record UpdateTenantCommand : IRequest<TenantDto>
{
    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>Changing the slug changes the public website URL, so it must stay unique.</summary>
    public string? Slug { get; init; }

    public string? ContactEmail { get; init; }

    public string? ContactPhone { get; init; }

    public string? WhatsAppNumber { get; init; }

    public string? City { get; init; }
}

public sealed class UpdateTenantCommandValidator : AbstractValidator<UpdateTenantCommand>
{
    public UpdateTenantCommandValidator()
    {
        RuleFor(command => command.Name).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.DisplayName).MaximumLength(DomainRules.NameMaxLength);
        RuleFor(command => command.City).MaximumLength(100);
        RuleFor(command => command.ContactPhone).OptionalEgyptianPhone();
        RuleFor(command => command.WhatsAppNumber).OptionalEgyptianPhone();

        RuleFor(command => command.ContactEmail)
            .EmailAddress()
            .When(command => !string.IsNullOrWhiteSpace(command.ContactEmail));

        RuleFor(command => command.Slug!)
            .MinimumLength(3)
            .MaximumLength(DomainRules.SlugMaxLength)
            .Matches("^[a-z0-9\\-]+$")
            .WithMessage("The slug may only contain lowercase letters, digits and hyphens.")
            .When(command => command.Slug is not null);
    }
}

public sealed class UpdateTenantCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<UpdateTenantCommand, TenantDto>
{
    public async Task<TenantDto> Handle(UpdateTenantCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        var tenant = await context.Tenants
            .FirstOrDefaultAsync(entity => entity.Id == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("Tenant", access.TenantId);

        var before = new { tenant.Name, tenant.Slug, tenant.ContactEmail, tenant.City };

        if (command.Slug is not null)
        {
            var slug = command.Slug.Trim().ToLowerInvariant();

            var taken = await context.Tenants.AnyAsync(
                entity => entity.Slug == slug && entity.Id != tenant.Id, cancellationToken);

            if (taken)
            {
                throw new ConflictException("This workspace address is already taken. Pick another one.");
            }

            tenant.Slug = slug;
        }

        if (command.Name is not null)
        {
            tenant.Name = command.Name.Trim();
        }

        if (command.DisplayName is not null)
        {
            tenant.DisplayName = command.DisplayName.Trim();
        }

        if (command.ContactEmail is not null)
        {
            tenant.ContactEmail = command.ContactEmail.Trim().ToLowerInvariant();
        }

        if (command.ContactPhone is not null)
        {
            tenant.ContactPhone = command.ContactPhone.Trim();
        }

        if (command.WhatsAppNumber is not null)
        {
            tenant.WhatsAppNumber = command.WhatsAppNumber.Trim();
        }

        if (command.City is not null)
        {
            tenant.City = command.City.Trim();
        }

        tenant.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.TenantSettingsChanged,
            nameof(Tenant),
            tenant.Id,
            before,
            new { tenant.Name, tenant.Slug, tenant.ContactEmail, tenant.City },
            access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetTenantQuery(access.TenantId), cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Settings
// ---------------------------------------------------------------------------
public sealed record GetTenantSettingsQuery : IRequest<TenantSettingsDto>;

public sealed class GetTenantSettingsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto>
{
    public async Task<TenantSettingsDto> Handle(GetTenantSettingsQuery query, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequireAccessAsync(cancellationToken: cancellationToken);

        return await context.TenantSettings
            .AsNoTracking()
            .Where(settings => settings.TenantId == access.TenantId)
            .Select(settings => new TenantSettingsDto
            {
                TenantId = settings.TenantId,
                DefaultLanguage = settings.DefaultLanguage,
                TimeZoneId = settings.TimeZoneId,
                Currency = settings.Currency,
                GradeNotificationThresholdPercentage = settings.GradeNotificationThresholdPercentage,
                NotifyParentsOnAbsence = settings.NotifyParentsOnAbsence,
                NotifyParentsOnLowGrade = settings.NotifyParentsOnLowGrade,
                NotifyParentsOnHomeworkMissed = settings.NotifyParentsOnHomeworkMissed,
                AllowStudentSelfEnrollment = settings.AllowStudentSelfEnrollment,
                AutoApproveReviews = settings.AutoApproveReviews,
                LateThresholdMinutes = settings.LateThresholdMinutes
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("TenantSettings", access.TenantId);
    }
}

public sealed record UpdateTenantSettingsCommand : IRequest<TenantSettingsDto>
{
    public string? DefaultLanguage { get; init; }

    public string? TimeZoneId { get; init; }

    public string? Currency { get; init; }

    public int? GradeNotificationThresholdPercentage { get; init; }

    public bool? NotifyParentsOnAbsence { get; init; }

    public bool? NotifyParentsOnLowGrade { get; init; }

    public bool? NotifyParentsOnHomeworkMissed { get; init; }

    public bool? AllowStudentSelfEnrollment { get; init; }

    public bool? AutoApproveReviews { get; init; }

    public int? LateThresholdMinutes { get; init; }
}

public sealed class UpdateTenantSettingsCommandValidator : AbstractValidator<UpdateTenantSettingsCommand>
{
    public UpdateTenantSettingsCommandValidator()
    {
        RuleFor(command => command.DefaultLanguage).MaximumLength(10);
        RuleFor(command => command.TimeZoneId).MaximumLength(100);
        RuleFor(command => command.Currency).Length(3).When(command => command.Currency is not null);

        RuleFor(command => command.GradeNotificationThresholdPercentage!.Value)
            .InclusiveBetween(0, 100)
            .When(command => command.GradeNotificationThresholdPercentage.HasValue)
            .WithMessage("The grade notification threshold must be a percentage between 0 and 100.");

        RuleFor(command => command.LateThresholdMinutes!.Value)
            .InclusiveBetween(0, 240)
            .When(command => command.LateThresholdMinutes.HasValue);
    }
}

public sealed class UpdateTenantSettingsCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger,
    IMediator mediator) : IRequestHandler<UpdateTenantSettingsCommand, TenantSettingsDto>
{
    public async Task<TenantSettingsDto> Handle(
        UpdateTenantSettingsCommand command,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        var settings = await context.TenantSettings
            .FirstOrDefaultAsync(entity => entity.TenantId == access.TenantId, cancellationToken)
            ?? throw new NotFoundException("TenantSettings", access.TenantId);

        if (command.DefaultLanguage is not null)
        {
            settings.DefaultLanguage = command.DefaultLanguage.Trim();
        }

        if (command.TimeZoneId is not null)
        {
            settings.TimeZoneId = command.TimeZoneId.Trim();
        }

        if (command.Currency is not null)
        {
            settings.Currency = command.Currency.Trim().ToUpperInvariant();
        }

        if (command.GradeNotificationThresholdPercentage.HasValue)
        {
            settings.GradeNotificationThresholdPercentage = command.GradeNotificationThresholdPercentage.Value;
        }

        if (command.NotifyParentsOnAbsence.HasValue)
        {
            settings.NotifyParentsOnAbsence = command.NotifyParentsOnAbsence.Value;
        }

        if (command.NotifyParentsOnLowGrade.HasValue)
        {
            settings.NotifyParentsOnLowGrade = command.NotifyParentsOnLowGrade.Value;
        }

        if (command.NotifyParentsOnHomeworkMissed.HasValue)
        {
            settings.NotifyParentsOnHomeworkMissed = command.NotifyParentsOnHomeworkMissed.Value;
        }

        if (command.AllowStudentSelfEnrollment.HasValue)
        {
            settings.AllowStudentSelfEnrollment = command.AllowStudentSelfEnrollment.Value;
        }

        if (command.AutoApproveReviews.HasValue)
        {
            settings.AutoApproveReviews = command.AutoApproveReviews.Value;
        }

        if (command.LateThresholdMinutes.HasValue)
        {
            settings.LateThresholdMinutes = command.LateThresholdMinutes.Value;
        }

        settings.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.TenantSettingsChanged,
            nameof(TenantSettings),
            settings.Id,
            newValues: command,
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await mediator.Send(new GetTenantSettingsQuery(), cancellationToken);
    }
}
