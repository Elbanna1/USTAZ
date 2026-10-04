using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Tenants.Dtos;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Tenants;

/// <summary>Checks that a domain's DNS TXT record carries the expected verification token.</summary>
public interface IDomainVerificationService
{
    /// <summary>The record the tenant must publish, e.g. _ustaz-verify.example.com.</summary>
    string RecordName(string host);

    Task<bool> IsVerifiedAsync(string host, string expectedToken, CancellationToken cancellationToken = default);
}

internal static class DomainMapper
{
    public static TenantDomainDto ToDto(TenantDomain domain, IDomainVerificationService verification) => new()
    {
        Id = domain.Id,
        TenantId = domain.TenantId,
        Host = domain.Host,
        IsPrimary = domain.IsPrimary,
        VerificationStatus = domain.VerificationStatus,
        VerificationRecordName = verification.RecordName(domain.Host),
        VerificationRecordValue = domain.VerificationToken,
        VerifiedAt = domain.VerifiedAt,
        LastVerificationError = domain.LastVerificationError,
        CreatedAt = domain.CreatedAt
    };
}

public sealed record GetTenantDomainsQuery : IRequest<IReadOnlyList<TenantDomainDto>>;

public sealed class GetTenantDomainsQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDomainVerificationService verification) : IRequestHandler<GetTenantDomainsQuery, IReadOnlyList<TenantDomainDto>>
{
    public async Task<IReadOnlyList<TenantDomainDto>> Handle(
        GetTenantDomainsQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        var domains = await context.TenantDomains
            .AsNoTracking()
            .Where(domain => domain.TenantId == access.TenantId)
            .OrderByDescending(domain => domain.IsPrimary)
            .ThenBy(domain => domain.Host)
            .ToListAsync(cancellationToken);

        return [.. domains.Select(domain => DomainMapper.ToDto(domain, verification))];
    }
}

public sealed record AddTenantDomainCommand : IRequest<TenantDomainDto>
{
    public required string Host { get; init; }

    public bool MakePrimary { get; init; }
}

public sealed class AddTenantDomainCommandValidator : AbstractValidator<AddTenantDomainCommand>
{
    public AddTenantDomainCommandValidator() =>
        RuleFor(command => command.Host)
            .NotEmpty().WithMessage("A domain name is required.")
            .MaximumLength(253)
            .Matches("^(?!-)[a-zA-Z0-9-]{1,63}(?<!-)(\\.(?!-)[a-zA-Z0-9-]{1,63}(?<!-))+$")
            .WithMessage("Enter a valid domain such as school.example.com, without http:// or a path.");
}

public sealed class AddTenantDomainCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IPlanLimitService planLimits,
    IDateTimeProvider dateTimeProvider,
    IDomainVerificationService verification,
    IAuditLogger auditLogger) : IRequestHandler<AddTenantDomainCommand, TenantDomainDto>
{
    public async Task<TenantDomainDto> Handle(AddTenantDomainCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        // Custom domains are a paid capability, enforced here rather than in the UI.
        await planLimits.EnsureFeatureAsync(access.TenantId, PlanFeatures.CustomDomain, cancellationToken);

        var host = command.Host.Trim().ToLowerInvariant();

        if (await context.TenantDomains.AnyAsync(domain => domain.Host == host, cancellationToken))
        {
            throw new ConflictException("This domain is already registered on USTAZ.");
        }

        var utcNow = dateTimeProvider.UtcNow;

        var entity = new TenantDomain
        {
            Id = Guid.NewGuid(),
            TenantId = access.TenantId,
            Host = host,
            VerificationStatus = DomainVerificationStatus.Pending,
            VerificationToken = "ustaz-verify-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            CreatedAt = utcNow
        };

        await context.TenantDomains.AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.TenantDomainChanged,
            nameof(TenantDomain),
            entity.Id,
            newValues: new { entity.Host },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return DomainMapper.ToDto(entity, verification);
    }
}

public sealed record UpdateTenantDomainCommand : IRequest<TenantDomainDto>
{
    public Guid DomainId { get; init; }

    /// <summary>Only a verified domain may become the primary one.</summary>
    public bool? IsPrimary { get; init; }
}

public sealed class UpdateTenantDomainCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IDomainVerificationService verification) : IRequestHandler<UpdateTenantDomainCommand, TenantDomainDto>
{
    public async Task<TenantDomainDto> Handle(UpdateTenantDomainCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        var domain = await context.TenantDomains
            .FirstOrDefaultAsync(entity => entity.Id == command.DomainId, cancellationToken)
            ?? throw new NotFoundException("TenantDomain", command.DomainId);

        tenantAuthorization.EnsureSameTenant(access, domain.TenantId);

        if (command.IsPrimary == true)
        {
            domain.EnsureCanBePrimary();

            // Only one primary domain per workspace.
            var others = await context.TenantDomains
                .Where(entity => entity.TenantId == access.TenantId && entity.Id != domain.Id && entity.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var other in others)
            {
                other.IsPrimary = false;
                other.UpdatedAt = dateTimeProvider.UtcNow;
            }

            domain.IsPrimary = true;
        }
        else if (command.IsPrimary == false)
        {
            domain.IsPrimary = false;
        }

        domain.UpdatedAt = dateTimeProvider.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return DomainMapper.ToDto(domain, verification);
    }
}

public sealed record VerifyTenantDomainCommand(Guid DomainId) : IRequest<TenantDomainDto>;

public sealed class VerifyTenantDomainCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IDomainVerificationService verification) : IRequestHandler<VerifyTenantDomainCommand, TenantDomainDto>
{
    public async Task<TenantDomainDto> Handle(VerifyTenantDomainCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        var domain = await context.TenantDomains
            .FirstOrDefaultAsync(entity => entity.Id == command.DomainId, cancellationToken)
            ?? throw new NotFoundException("TenantDomain", command.DomainId);

        tenantAuthorization.EnsureSameTenant(access, domain.TenantId);

        var utcNow = dateTimeProvider.UtcNow;
        var verified = await verification.IsVerifiedAsync(domain.Host, domain.VerificationToken, cancellationToken);

        if (verified)
        {
            domain.MarkVerified(utcNow);
        }
        else
        {
            domain.MarkFailed(
                utcNow,
                "The TXT record at " + verification.RecordName(domain.Host)
                + " did not contain the expected value. DNS changes can take a while to propagate.");
        }

        await context.SaveChangesAsync(cancellationToken);

        return DomainMapper.ToDto(domain, verification);
    }
}

public sealed record DeleteTenantDomainCommand(Guid DomainId) : IRequest<Unit>;

public sealed class DeleteTenantDomainCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IAuditLogger auditLogger) : IRequestHandler<DeleteTenantDomainCommand, Unit>
{
    public async Task<Unit> Handle(DeleteTenantDomainCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.TenantManage, cancellationToken: cancellationToken);

        var domain = await context.TenantDomains
            .FirstOrDefaultAsync(entity => entity.Id == command.DomainId, cancellationToken)
            ?? throw new NotFoundException("TenantDomain", command.DomainId);

        tenantAuthorization.EnsureSameTenant(access, domain.TenantId);

        context.TenantDomains.Remove(domain);
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            AuditActions.TenantDomainChanged,
            nameof(TenantDomain),
            domain.Id,
            oldValues: new { domain.Host },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return Unit.Value;
    }
}
