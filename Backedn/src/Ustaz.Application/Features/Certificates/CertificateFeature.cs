using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Notifications;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Certificates;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------

/// <summary>A certificate as its owner and the issuing workspace see it.</summary>
public sealed record CertificateDto
{
    public Guid Id { get; init; }

    public string CertificateNumber { get; init; } = string.Empty;

    /// <summary>The code to hand to a verifier. Only ever returned to the owner.</summary>
    public string VerificationCode { get; init; } = string.Empty;

    public Guid CourseId { get; init; }

    public string CourseTitle { get; init; } = string.Empty;

    public Guid StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string TeacherName { get; init; } = string.Empty;

    public string WorkspaceName { get; init; } = string.Empty;

    public int LessonsCompleted { get; init; }

    public CertificateStatus Status { get; init; }

    public string StatusName => Status.ToString();

    public bool IsValid => Status == CertificateStatus.Issued;

    public DateTime CompletedAt { get; init; }

    public DateTime IssuedAt { get; init; }

    public DateTime? RevokedAt { get; init; }
}

/// <summary>
/// What a public verifier is shown. Deliberately minimal: names and dates, no identifiers,
/// no contact details, nothing that turns verification into a data source.
/// </summary>
public sealed record CertificateVerificationDto
{
    public required bool IsValid { get; init; }

    public string? CertificateNumber { get; init; }

    public string? StudentName { get; init; }

    public string? CourseTitle { get; init; }

    public string? TeacherName { get; init; }

    public string? WorkspaceName { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? IssuedAt { get; init; }

    /// <summary>Set when the certificate exists but is no longer valid.</summary>
    public string? Status { get; init; }
}

internal static class CertificateProjections
{
    public static System.Linq.Expressions.Expression<Func<Certificate, CertificateDto>> ToDto =>
        certificate => new CertificateDto
        {
            Id = certificate.Id,
            CertificateNumber = certificate.CertificateNumber,
            VerificationCode = certificate.VerificationCode,
            CourseId = certificate.CourseId,
            CourseTitle = certificate.CourseTitle,
            StudentId = certificate.StudentId,
            StudentName = certificate.StudentName,
            TeacherName = certificate.TeacherName,
            WorkspaceName = certificate.WorkspaceName,
            LessonsCompleted = certificate.LessonsCompleted,
            Status = certificate.Status,
            CompletedAt = certificate.CompletedAt,
            IssuedAt = certificate.IssuedAt,
            RevokedAt = certificate.RevokedAt
        };
}

// ---------------------------------------------------------------------------
// Issuing
// ---------------------------------------------------------------------------

/// <summary>
/// Issues the certificate for a completed course enrollment.
///
/// Called by the completion path rather than by a client, and safe to call again: the unique
/// index on the enrollment means a second attempt returns the certificate already issued
/// instead of minting a duplicate.
/// </summary>
public sealed class CertificateIssuer(
    IApplicationDbContext context,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications) : ICertificateIssuer
{
    public async Task<Guid?> IssueForCompletedCourseAsync(
        Guid courseEnrollmentId,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.Certificates
            .AsNoTracking()
            .Where(certificate => certificate.CourseEnrollmentId == courseEnrollmentId)
            .Select(certificate => (Guid?)certificate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var enrollment = await context.CourseEnrollments
            .AsNoTracking()
            .Where(entity => entity.Id == courseEnrollmentId)
            .Select(entity => new
            {
                entity.Id,
                entity.StudentId,
                entity.CourseId,
                entity.Status,
                entity.CompletedAt,
                entity.ProgressPercentage,
                StudentName = entity.Student.User.DisplayName,
                CourseTitle = entity.Course.Title,
                entity.Course.TenantId,
                entity.Course.TeacherId,
                TeacherName = entity.Course.Teacher.User.DisplayName,
                TenantName = context.Tenants
                    .Where(tenant => tenant.Id == entity.Course.TenantId)
                    .Select(tenant => tenant.Name)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Only a genuinely completed enrollment earns a certificate.
        if (enrollment is null || enrollment.Status != EnrollmentStatus.Completed)
        {
            return null;
        }

        var utcNow = dateTimeProvider.UtcNow;

        var lessonsCompleted = await context.CourseLessonProgress
            .CountAsync(
                progress => progress.CourseEnrollmentId == enrollment.Id && progress.IsCompleted,
                cancellationToken);

        var certificate = new Certificate
        {
            Id = Guid.NewGuid(),
            TenantId = enrollment.TenantId,
            StudentId = enrollment.StudentId,
            CourseId = enrollment.CourseId,
            CourseEnrollmentId = enrollment.Id,
            TeacherId = enrollment.TeacherId,
            CertificateNumber = BuildNumber(utcNow),
            VerificationCode = BuildVerificationCode(),
            Status = CertificateStatus.Issued,
            IssuedAt = utcNow,
            CompletedAt = enrollment.CompletedAt ?? utcNow,
            StudentName = enrollment.StudentName,
            CourseTitle = enrollment.CourseTitle,
            TeacherName = enrollment.TeacherName,
            WorkspaceName = enrollment.TenantName ?? string.Empty,
            LessonsCompleted = lessonsCompleted,
            CreatedAt = utcNow
        };

        await context.Certificates.AddAsync(certificate, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        var studentUserId = await NotificationAudience.StudentUserIdAsync(
            context, enrollment.StudentId, cancellationToken);

        if (studentUserId != Guid.Empty)
        {
            await notifications.SendAsync(
                studentUserId,
                new NotificationRequest
                {
                    Event = NotificationEvents.CertificateIssued,
                    TenantId = certificate.TenantId,
                    ReferenceId = certificate.Id,
                    DeduplicationKey = "certificate:" + certificate.Id,
                    // The email template needs the printed facts and the verification link;
                    // the in-app card needs the course and the number. One value set serves
                    // both, and neither carries an internal id into the email body.
                    Values = new Dictionary<string, string>
                    {
                        ["course"] = certificate.CourseTitle,
                        ["number"] = certificate.CertificateNumber,
                        ["certificateId"] = certificate.Id.ToString(),
                        ["studentName"] = certificate.StudentName,
                        ["completedAt"] = certificate.CompletedAt.ToString("yyyy-MM-dd"),
                        ["verificationUrl"] = "/verify-certificate/" + certificate.VerificationCode
                    }
                },
                cancellationToken);
        }

        return certificate.Id;
    }

    /// <summary>Readable and year-scoped, but never the thing that grants access.</summary>
    private static string BuildNumber(DateTime utcNow) =>
        "USTAZ-" + utcNow.Year + "-" + RandomNumberGenerator.GetHexString(8, lowercase: false);

    /// <summary>128 bits of randomness: not guessable, not enumerable.</summary>
    private static string BuildVerificationCode() =>
        RandomNumberGenerator.GetHexString(32, lowercase: true);
}

// ---------------------------------------------------------------------------
// Queries
// ---------------------------------------------------------------------------

/// <summary>The calling student's own certificates.</summary>
public sealed record GetMyCertificatesQuery : PagedRequest, IRequest<PaginatedList<CertificateDto>>;

public sealed class GetMyCertificatesQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyCertificatesQuery, PaginatedList<CertificateDto>>
{
    public Task<PaginatedList<CertificateDto>> Handle(
        GetMyCertificatesQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return context.Certificates
            .AsNoTracking()
            .Where(certificate => certificate.StudentId == studentId)
            .OrderByDescending(certificate => certificate.IssuedAt)
            .ThenByDescending(certificate => certificate.Id)
            .Select(CertificateProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>One certificate, readable by its owner or by the issuing workspace.</summary>
public sealed record GetCertificateByIdQuery(Guid CertificateId) : IRequest<CertificateDto>;

public sealed class GetCertificateByIdQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    ITenantAuthorizationService tenantAuthorization) : IRequestHandler<GetCertificateByIdQuery, CertificateDto>
{
    public async Task<CertificateDto> Handle(GetCertificateByIdQuery query, CancellationToken cancellationToken)
    {
        var certificate = await context.Certificates
            .AsNoTracking()
            .Where(entity => entity.Id == query.CertificateId)
            .Select(entity => new { entity.StudentId, entity.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Certificate", query.CertificateId);

        if (currentUser.StudentId != certificate.StudentId)
        {
            var access = await tenantAuthorization.RequirePermissionAsync(
                TenantPermissions.CoursesView, cancellationToken: cancellationToken);

            tenantAuthorization.EnsureSameTenant(access, certificate.TenantId);
        }

        return await context.Certificates
            .AsNoTracking()
            .Where(entity => entity.Id == query.CertificateId)
            .Select(CertificateProjections.ToDto)
            .FirstAsync(cancellationToken);
    }
}

/// <summary>Certificates issued by the caller's workspace.</summary>
public sealed record GetWorkspaceCertificatesQuery : PagedRequest, IRequest<PaginatedList<CertificateDto>>
{
    public Guid? CourseId { get; init; }

    public CertificateStatus? Status { get; init; }
}

public sealed class GetWorkspaceCertificatesQueryHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization)
    : IRequestHandler<GetWorkspaceCertificatesQuery, PaginatedList<CertificateDto>>
{
    public async Task<PaginatedList<CertificateDto>> Handle(
        GetWorkspaceCertificatesQuery query,
        CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.CoursesView, cancellationToken: cancellationToken);

        var search = query.Search?.Trim();

        return await context.Certificates
            .AsNoTracking()
            .Where(certificate => certificate.TenantId == access.TenantId)
            .WhereIf(query.CourseId.HasValue, certificate => certificate.CourseId == query.CourseId!.Value)
            .WhereIf(query.Status.HasValue, certificate => certificate.Status == query.Status!.Value)
            .WhereIf(!string.IsNullOrWhiteSpace(search), certificate =>
                certificate.StudentName.Contains(search!)
                || certificate.CertificateNumber.Contains(search!))
            .OrderByDescending(certificate => certificate.IssuedAt)
            .ThenByDescending(certificate => certificate.Id)
            .Select(CertificateProjections.ToDto)
            .ToPaginatedListAsync(query, cancellationToken);
    }
}

/// <summary>
/// Public verification. Anonymous by design — that is the point of a certificate — but it
/// answers only for an exact verification code and returns nothing but the printed facts.
/// </summary>
public sealed record VerifyCertificateQuery(string VerificationCode) : IRequest<CertificateVerificationDto>;

public sealed class VerifyCertificateQueryValidator : AbstractValidator<VerifyCertificateQuery>
{
    public VerifyCertificateQueryValidator() =>
        RuleFor(query => query.VerificationCode)
            .NotEmpty()
            .MaximumLength(64);
}

public sealed class VerifyCertificateQueryHandler(IApplicationDbContext context)
    : IRequestHandler<VerifyCertificateQuery, CertificateVerificationDto>
{
    public async Task<CertificateVerificationDto> Handle(
        VerifyCertificateQuery query,
        CancellationToken cancellationToken)
    {
        var code = query.VerificationCode.Trim().ToLowerInvariant();

        var certificate = await context.Certificates
            .AsNoTracking()
            .Where(entity => entity.VerificationCode == code)
            .Select(entity => new
            {
                entity.CertificateNumber,
                entity.StudentName,
                entity.CourseTitle,
                entity.TeacherName,
                entity.WorkspaceName,
                entity.CompletedAt,
                entity.IssuedAt,
                entity.Status
            })
            .FirstOrDefaultAsync(cancellationToken);

        // An unknown code is simply "not valid": no 404, nothing to enumerate against.
        if (certificate is null)
        {
            return new CertificateVerificationDto { IsValid = false };
        }

        if (certificate.Status != CertificateStatus.Issued)
        {
            return new CertificateVerificationDto
            {
                IsValid = false,
                Status = certificate.Status.ToString(),
                CertificateNumber = certificate.CertificateNumber
            };
        }

        return new CertificateVerificationDto
        {
            IsValid = true,
            CertificateNumber = certificate.CertificateNumber,
            StudentName = certificate.StudentName,
            CourseTitle = certificate.CourseTitle,
            TeacherName = certificate.TeacherName,
            WorkspaceName = certificate.WorkspaceName,
            CompletedAt = certificate.CompletedAt,
            IssuedAt = certificate.IssuedAt,
            Status = certificate.Status.ToString()
        };
    }
}

// ---------------------------------------------------------------------------
// Revocation
// ---------------------------------------------------------------------------

/// <summary>Withdraws a certificate the workspace issued.</summary>
public sealed record RevokeCertificateCommand(Guid CertificateId, string? Reason) : IRequest<CertificateDto>;

public sealed class RevokeCertificateCommandValidator : AbstractValidator<RevokeCertificateCommand>
{
    public RevokeCertificateCommandValidator()
    {
        RuleFor(command => command.CertificateId).NotEmpty();
        RuleFor(command => command.Reason).MaximumLength(DomainRules.ShortTextMaxLength);
    }
}

public sealed class RevokeCertificateCommandHandler(
    IApplicationDbContext context,
    ITenantAuthorizationService tenantAuthorization,
    IDateTimeProvider dateTimeProvider,
    IAuditLogger auditLogger) : IRequestHandler<RevokeCertificateCommand, CertificateDto>
{
    public async Task<CertificateDto> Handle(RevokeCertificateCommand command, CancellationToken cancellationToken)
    {
        var access = await tenantAuthorization.RequirePermissionAsync(
            TenantPermissions.CoursesManage, cancellationToken: cancellationToken);

        var certificate = await context.Certificates
            .FirstOrDefaultAsync(entity => entity.Id == command.CertificateId, cancellationToken)
            ?? throw new NotFoundException("Certificate", command.CertificateId);

        tenantAuthorization.EnsureSameTenant(access, certificate.TenantId);

        certificate.Revoke(dateTimeProvider.UtcNow, command.Reason?.Trim());
        await context.SaveChangesAsync(cancellationToken);

        await auditLogger.LogAsync(
            "certificate.revoked",
            nameof(Certificate),
            certificate.Id,
            newValues: new { certificate.Status, command.Reason },
            tenantId: access.TenantId,
            cancellationToken: cancellationToken);

        return await context.Certificates
            .AsNoTracking()
            .Where(entity => entity.Id == certificate.Id)
            .Select(CertificateProjections.ToDto)
            .FirstAsync(cancellationToken);
    }
}
