using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Accounts;

/// <summary>
/// The caller's own profile picture, whoever they are.
///
/// Every role already *displayed* a picture — a student's avatar shows up in their dashboard, a
/// parent's in their profile, both in the teacher's class lists — but only a teacher could
/// actually set one, through <c>POST /teacher/profile/image</c>. A student or a parent had no
/// way to upload anything, so the field was permanently null for them.
///
/// This lives on the identity user rather than on any one role's profile, which is what makes
/// it work for all of them at once. Where a teacher profile also carries the picture (it is
/// part of their public page), the two are kept in step here.
///
/// Authorization is simply "the caller". There is no id in any of these requests, so there is
/// nothing to tamper with: a user can only ever reach their own picture.
/// </summary>
public sealed record ProfilePictureDto
{
    /// <summary>Application-relative URL, or null when the user has no picture set.</summary>
    public string? Url { get; init; }

    /// <summary>True when a picture has been uploaded, so the client can pick its placeholder.</summary>
    public bool HasPicture => !string.IsNullOrWhiteSpace(Url);
}

// ---------------------------------------------------------------------------
// Read
// ---------------------------------------------------------------------------

public sealed record GetMyProfilePictureQuery : IRequest<ProfilePictureDto>;

public sealed class GetMyProfilePictureQueryHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : IRequestHandler<GetMyProfilePictureQuery, ProfilePictureDto>
{
    public async Task<ProfilePictureDto> Handle(
        GetMyProfilePictureQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var url = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.ProfileImageUrl)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProfilePictureDto { Url = url };
    }
}

// ---------------------------------------------------------------------------
// Upload and replace
// ---------------------------------------------------------------------------

/// <summary>Sets or replaces the caller's profile picture.</summary>
public sealed record UploadMyProfilePictureCommand : IRequest<ProfilePictureDto>
{
    public required FileUploadRequest File { get; init; }
}

public sealed class UploadMyProfilePictureCommandValidator : AbstractValidator<UploadMyProfilePictureCommand>
{
    public UploadMyProfilePictureCommandValidator() =>
        // Size, extension, declared type and the file's own signature, from the shared rules.
        RuleFor(command => command.File).ValidImageUpload();
}

public sealed class UploadMyProfilePictureCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IMediaService mediaService,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<UploadMyProfilePictureCommand, ProfilePictureDto>
{
    public async Task<ProfilePictureDto> Handle(
        UploadMyProfilePictureCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        var previousPublicId = user.ProfileImagePublicId;

        var uploaded = await mediaService.UploadAsync(
            command.File.Content,
            command.File.FileName,
            "users/profile",
            cancellationToken);

        var url = uploaded.SecureUrl ?? uploaded.Url;
        var utcNow = dateTimeProvider.UtcNow;

        user.ProfileImageUrl = url;
        user.ProfileImagePublicId = uploaded.PublicId;
        user.UpdatedAt = utcNow;

        // A teacher's public page reads the picture off their own profile row, so the two have
        // to agree. Done in the same transaction as the user row for exactly that reason.
        var teacher = await context.TeacherProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (teacher is not null)
        {
            teacher.ProfileImageUrl = url;
            teacher.ProfileImagePublicId = uploaded.PublicId;
            teacher.UpdatedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Only now that the new picture is committed is the old one safe to remove. The other
        // order would leave a user with no avatar at all if the write failed.
        if (!string.IsNullOrWhiteSpace(previousPublicId) && previousPublicId != uploaded.PublicId)
        {
            await mediaService.DeleteAsync(previousPublicId, cancellationToken);
        }

        await InvalidateAsync(cache, teacher?.Id, userId, cancellationToken);

        return new ProfilePictureDto { Url = url };
    }

    internal static async Task InvalidateAsync(
        ICacheService cache,
        Guid? teacherId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (teacherId is not null)
        {
            await cache.RemoveAsync(CacheKeys.TeacherProfile(teacherId.Value), cancellationToken);
            await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.StudentDashboard(userId), cancellationToken);
    }
}

// ---------------------------------------------------------------------------
// Remove
// ---------------------------------------------------------------------------

/// <summary>
/// Clears the caller's picture and returns them to the default avatar. Idempotent: removing a
/// picture that is not there succeeds rather than 404ing, because the end state is what the
/// caller asked for.
/// </summary>
public sealed record RemoveMyProfilePictureCommand : IRequest<ProfilePictureDto>;

public sealed class RemoveMyProfilePictureCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IMediaService mediaService,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<RemoveMyProfilePictureCommand, ProfilePictureDto>
{
    public async Task<ProfilePictureDto> Handle(
        RemoveMyProfilePictureCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        var user = await context.Users
            .FirstOrDefaultAsync(entity => entity.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        var publicId = user.ProfileImagePublicId;

        if (user.ProfileImageUrl is null && publicId is null)
        {
            return new ProfilePictureDto { Url = null };
        }

        var utcNow = dateTimeProvider.UtcNow;

        user.ProfileImageUrl = null;
        user.ProfileImagePublicId = null;
        user.UpdatedAt = utcNow;

        var teacher = await context.TeacherProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

        if (teacher is not null)
        {
            teacher.ProfileImageUrl = null;
            teacher.ProfileImagePublicId = null;
            teacher.UpdatedAt = utcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        // The row no longer references the file, so deleting it cannot orphan a reference.
        if (!string.IsNullOrWhiteSpace(publicId))
        {
            await mediaService.DeleteAsync(publicId, cancellationToken);
        }

        await UploadMyProfilePictureCommandHandler.InvalidateAsync(
            cache, teacher?.Id, userId, cancellationToken);

        return new ProfilePictureDto { Url = null };
    }
}
