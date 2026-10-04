using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Features.Parents;

// ---------------------------------------------------------------------------
// DTOs
// ---------------------------------------------------------------------------
public sealed record ParentProfileDto
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string? PhoneNumber { get; init; }

    public string? AlternatePhoneNumber { get; init; }

    public string? City { get; init; }

    public string? Occupation { get; init; }

    public int ChildrenCount { get; init; }

    public DateTime CreatedAt { get; init; }
}

public sealed record ChildDto
{
    public Guid StudentId { get; init; }

    public Guid LinkId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? SchoolName { get; init; }

    public string? GradeName { get; init; }

    public string? Relationship { get; init; }

    public bool IsConfirmed { get; init; }

    public bool IsPrimaryContact { get; init; }

    public int ActiveGroupsCount { get; init; }

    public int ActiveCoursesCount { get; init; }

    public DateTime LinkedAt { get; init; }
}

/// <summary>
/// The mirror of <see cref="ChildDto"/>: one guardian as seen by the student they follow.
/// </summary>
public sealed record GuardianDto
{
    public Guid LinkId { get; init; }

    public Guid ParentId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? ProfileImageUrl { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Relationship { get; init; }

    /// <summary>False while the guardian is still only *requesting* access.</summary>
    public bool IsConfirmed { get; init; }

    public bool IsPrimaryContact { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? ConfirmedAt { get; init; }
}

// ---------------------------------------------------------------------------
// Profile
// ---------------------------------------------------------------------------
public sealed record GetParentProfileQuery : IRequest<ParentProfileDto>;

public sealed class GetParentProfileQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetParentProfileQuery, ParentProfileDto>
{
    public async Task<ParentProfileDto> Handle(GetParentProfileQuery query, CancellationToken cancellationToken)
    {
        var parentId = currentUser.RequireParentId();

        return await context.ParentProfiles
            .AsNoTracking()
            .Where(parent => parent.Id == parentId)
            .Select(parent => new ParentProfileDto
            {
                Id = parent.Id,
                UserId = parent.UserId,
                DisplayName = parent.User.DisplayName,
                Email = parent.User.Email ?? string.Empty,
                PhoneNumber = parent.PhoneNumber ?? parent.User.PhoneNumber,
                AlternatePhoneNumber = parent.AlternatePhoneNumber,
                City = parent.City,
                Occupation = parent.Occupation,
                ChildrenCount = parent.Children.Count(link => link.IsConfirmed),
                CreatedAt = parent.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("ParentProfile", parentId);
    }
}

public sealed record UpdateParentProfileCommand : IRequest<ParentProfileDto>
{
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? PhoneNumber { get; init; }

    public string? AlternatePhoneNumber { get; init; }

    public string? City { get; init; }

    public string? Occupation { get; init; }
}

public sealed class UpdateParentProfileCommandValidator : AbstractValidator<UpdateParentProfileCommand>
{
    public UpdateParentProfileCommandValidator()
    {
        RuleFor(command => command.FirstName!).PersonName().When(command => command.FirstName is not null);
        RuleFor(command => command.LastName!).PersonName().When(command => command.LastName is not null);
        RuleFor(command => command.PhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.AlternatePhoneNumber).OptionalEgyptianPhone();
        RuleFor(command => command.City).MaximumLength(100);
        RuleFor(command => command.Occupation).MaximumLength(150);
    }
}

public sealed class UpdateParentProfileCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    IMediator mediator) : IRequestHandler<UpdateParentProfileCommand, ParentProfileDto>
{
    public async Task<ParentProfileDto> Handle(
        UpdateParentProfileCommand command,
        CancellationToken cancellationToken)
    {
        var parentId = currentUser.RequireParentId();

        var parent = await context.ParentProfiles
            .Include(entity => entity.User)
            .FirstOrDefaultAsync(entity => entity.Id == parentId, cancellationToken)
            ?? throw new NotFoundException("ParentProfile", parentId);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.FirstName is not null)
        {
            parent.User.FirstName = command.FirstName.Trim();
        }

        if (command.LastName is not null)
        {
            parent.User.LastName = command.LastName.Trim();
        }

        if (command.FirstName is not null || command.LastName is not null)
        {
            parent.User.DisplayName = (parent.User.FirstName + " " + parent.User.LastName).Trim();
        }

        if (command.PhoneNumber is not null)
        {
            parent.PhoneNumber = command.PhoneNumber.Trim();
            parent.User.PhoneNumber = parent.PhoneNumber;
        }

        if (command.AlternatePhoneNumber is not null)
        {
            parent.AlternatePhoneNumber = command.AlternatePhoneNumber.Trim();
        }

        if (command.City is not null)
        {
            parent.City = command.City.Trim();
            parent.User.City = parent.City;
        }

        if (command.Occupation is not null)
        {
            parent.Occupation = command.Occupation.Trim();
        }

        parent.UpdatedAt = utcNow;
        parent.User.UpdatedAt = utcNow;

        await context.SaveChangesAsync(cancellationToken);

        return await mediator.Send(new GetParentProfileQuery(), cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Children
// ---------------------------------------------------------------------------
public sealed record GetMyChildrenQuery : IRequest<IReadOnlyList<ChildDto>>;

public sealed class GetMyChildrenQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyChildrenQuery, IReadOnlyList<ChildDto>>
{
    public async Task<IReadOnlyList<ChildDto>> Handle(
        GetMyChildrenQuery query,
        CancellationToken cancellationToken)
    {
        var parentId = currentUser.RequireParentId();

        return await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.ParentId == parentId)
            .OrderBy(link => link.Student.User.DisplayName)
            .Select(link => new ChildDto
            {
                StudentId = link.StudentId,
                LinkId = link.Id,
                DisplayName = link.Student.User.DisplayName,
                ProfileImageUrl = link.Student.User.ProfileImageUrl,
                Email = link.Student.User.Email,
                PhoneNumber = link.Student.PhoneNumber,
                SchoolName = link.Student.SchoolName,
                GradeName = link.Student.Grade != null ? link.Student.Grade.NameAr : null,
                Relationship = link.Relationship,
                IsConfirmed = link.IsConfirmed,
                IsPrimaryContact = link.IsPrimaryContact,
                LinkedAt = link.CreatedAt,
                ActiveGroupsCount = link.Student.GroupEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active),
                ActiveCoursesCount = link.Student.CourseEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active)
            })
            .ToListAsync(cancellationToken);
    }
}

public sealed record GetChildQuery(Guid StudentId) : IRequest<ChildDto>;

public sealed class GetChildQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IParentAccessGuard parentAccess) : IRequestHandler<GetChildQuery, ChildDto>
{
    public async Task<ChildDto> Handle(GetChildQuery query, CancellationToken cancellationToken)
    {
        await parentAccess.EnsureLinkedAsync(query.StudentId, cancellationToken);

        var parentId = currentUser.RequireParentId();

        return await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.ParentId == parentId && link.StudentId == query.StudentId)
            .Select(link => new ChildDto
            {
                StudentId = link.StudentId,
                LinkId = link.Id,
                DisplayName = link.Student.User.DisplayName,
                ProfileImageUrl = link.Student.User.ProfileImageUrl,
                Email = link.Student.User.Email,
                PhoneNumber = link.Student.PhoneNumber,
                SchoolName = link.Student.SchoolName,
                GradeName = link.Student.Grade != null ? link.Student.Grade.NameAr : null,
                Relationship = link.Relationship,
                IsConfirmed = link.IsConfirmed,
                IsPrimaryContact = link.IsPrimaryContact,
                LinkedAt = link.CreatedAt,
                ActiveGroupsCount = link.Student.GroupEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active),
                ActiveCoursesCount = link.Student.CourseEnrollments.Count(enrollment =>
                    enrollment.Status == EnrollmentStatus.Active)
            })
            .FirstAsync(cancellationToken);
    }
}

/// <summary>
/// Links a child by their account email. The link starts unconfirmed unless the student's
/// registered parent phone number matches, which keeps a stranger from attaching themselves
/// to someone else's child by guessing an email.
/// </summary>
public sealed record LinkChildCommand : IRequest<ChildDto>
{
    public required string StudentEmail { get; init; }

    /// <summary>Must match the parent phone number on the student's profile to auto-confirm.</summary>
    public string? VerificationPhoneNumber { get; init; }

    public string? Relationship { get; init; }

    public bool IsPrimaryContact { get; init; }
}

public sealed class LinkChildCommandValidator : AbstractValidator<LinkChildCommand>
{
    public LinkChildCommandValidator()
    {
        RuleFor(command => command.StudentEmail).ValidEmail();
        RuleFor(command => command.Relationship).MaximumLength(50);
        RuleFor(command => command.VerificationPhoneNumber).OptionalEgyptianPhone();
    }
}

public sealed class LinkChildCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider,
    INotificationDispatcher notifications,
    IMediator mediator) : IRequestHandler<LinkChildCommand, ChildDto>
{
    public async Task<ChildDto> Handle(LinkChildCommand command, CancellationToken cancellationToken)
    {
        var parentId = currentUser.RequireParentId();
        var email = command.StudentEmail.Trim().ToLowerInvariant();

        var student = await context.StudentProfiles
            .AsNoTracking()
            .Where(entity => entity.User.Email == email)
            .Select(entity => new { entity.Id, entity.UserId, entity.ParentPhoneNumber, entity.User.DisplayName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("No student account was found with that email address.");

        var existing = await context.ParentStudentLinks
            .FirstOrDefaultAsync(
                link => link.ParentId == parentId && link.StudentId == student.Id,
                cancellationToken);

        if (existing is not null)
        {
            throw new ConflictException("This child is already linked to your account.");
        }

        var utcNow = dateTimeProvider.UtcNow;

        // Auto-confirm only when the guardian can prove the relationship.
        var autoConfirm = !string.IsNullOrWhiteSpace(command.VerificationPhoneNumber)
            && !string.IsNullOrWhiteSpace(student.ParentPhoneNumber)
            && EgyptianPhoneNumber.TryCreate(command.VerificationPhoneNumber, out var supplied)
            && EgyptianPhoneNumber.TryCreate(student.ParentPhoneNumber, out var onFile)
            && supplied.Value == onFile.Value;

        var link = new ParentStudentLink
        {
            Id = Guid.NewGuid(),
            ParentId = parentId,
            StudentId = student.Id,
            Relationship = command.Relationship?.Trim(),
            IsPrimaryContact = command.IsPrimaryContact,
            IsConfirmed = autoConfirm,
            ConfirmedAt = autoConfirm ? utcNow : null,
            CreatedAt = utcNow
        };

        await context.ParentStudentLinks.AddAsync(link, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // The student is always told, so an unwanted link cannot go unnoticed.
        await notifications.NotifyAsync(
            student.UserId,
            autoConfirm ? "ولي أمر اتربط بحسابك" : "في ولي أمر عايز يتابع حسابك",
            autoConfirm
                ? "ولي أمرك بقى يقدر يتابع حضورك ودرجاتك."
                : "في ولي أمر طلب يتابع حسابك. تقدر توافق أو ترفض من إعدادات حسابك.",
            NotificationType.Security,
            NotificationCategory.Security,
            null,
            "/student/guardians",
            link.Id,
            cancellationToken);

        return await mediator.Send(new GetChildQuery(student.Id), cancellationToken);
    }
}

public sealed record UnlinkChildCommand(Guid StudentId) : IRequest<Unit>;

public sealed class UnlinkChildCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<UnlinkChildCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkChildCommand command, CancellationToken cancellationToken)
    {
        var parentId = currentUser.RequireParentId();

        var link = await context.ParentStudentLinks
            .FirstOrDefaultAsync(
                entity => entity.ParentId == parentId && entity.StudentId == command.StudentId,
                cancellationToken)
            ?? throw new NotFoundException("No child with this identifier is linked to your account.");

        context.ParentStudentLinks.Remove(link);
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}

// ---------------------------------------------------------------------------
// Guardians, from the student's side
// ---------------------------------------------------------------------------

/// <summary>
/// The guardians following the signed-in student, confirmed and pending alike. This is what
/// backs the /student/guardians screen the link-request notification deep-links to: without
/// it a student has no way to discover the link id that
/// <see cref="RespondToGuardianLinkCommand"/> is keyed by.
/// </summary>
public sealed record GetMyGuardiansQuery : IRequest<IReadOnlyList<GuardianDto>>;

public sealed class GetMyGuardiansQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyGuardiansQuery, IReadOnlyList<GuardianDto>>
{
    public async Task<IReadOnlyList<GuardianDto>> Handle(
        GetMyGuardiansQuery query,
        CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        return await context.ParentStudentLinks
            .AsNoTracking()
            .Where(link => link.StudentId == studentId)
            // Pending requests first: they are the only rows that need an answer.
            .OrderBy(link => link.IsConfirmed)
            .ThenBy(link => link.Parent.User.DisplayName)
            .Select(link => new GuardianDto
            {
                LinkId = link.Id,
                ParentId = link.ParentId,
                DisplayName = link.Parent.User.DisplayName,
                ProfileImageUrl = link.Parent.User.ProfileImageUrl,
                Email = link.Parent.User.Email,
                PhoneNumber = link.Parent.PhoneNumber ?? link.Parent.User.PhoneNumber,
                Relationship = link.Relationship,
                IsConfirmed = link.IsConfirmed,
                IsPrimaryContact = link.IsPrimaryContact,
                RequestedAt = link.CreatedAt,
                ConfirmedAt = link.ConfirmedAt
            })
            .ToListAsync(cancellationToken);
    }
}

/// <summary>A student confirming (or rejecting) a guardian's link request.</summary>
public sealed record RespondToGuardianLinkCommand(Guid LinkId, bool Approve) : IRequest<Unit>;

public sealed class RespondToGuardianLinkCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<RespondToGuardianLinkCommand, Unit>
{
    public async Task<Unit> Handle(RespondToGuardianLinkCommand command, CancellationToken cancellationToken)
    {
        var studentId = currentUser.RequireStudentId();

        var link = await context.ParentStudentLinks
            .FirstOrDefaultAsync(entity => entity.Id == command.LinkId, cancellationToken)
            ?? throw new NotFoundException("ParentStudentLink", command.LinkId);

        // Only the student the request is about may answer it.
        if (link.StudentId != studentId)
        {
            throw new NotFoundException("ParentStudentLink", command.LinkId);
        }

        if (command.Approve)
        {
            link.IsConfirmed = true;
            link.ConfirmedAt = dateTimeProvider.UtcNow;
            link.UpdatedAt = dateTimeProvider.UtcNow;
        }
        else
        {
            context.ParentStudentLinks.Remove(link);
        }

        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
