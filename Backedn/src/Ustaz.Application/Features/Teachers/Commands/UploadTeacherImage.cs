using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Features.Teachers.Commands;

public enum TeacherImageKind
{
    Profile = 1,
    Cover = 2
}

/// <summary>Uploads a profile or cover image for the calling teacher and swaps out the old asset.</summary>
public sealed record UploadTeacherImageCommand : IRequest<MediaUploadResult>
{
    public required FileUploadRequest File { get; init; }

    public required TeacherImageKind Kind { get; init; }
}

public sealed class UploadTeacherImageCommandValidator : AbstractValidator<UploadTeacherImageCommand>
{
    public UploadTeacherImageCommandValidator()
    {
        RuleFor(command => command.Kind).IsInEnum();

        // Size, extension and declared type all come from the one shared rule set.
        RuleFor(command => command.File).ValidImageUpload();
    }
}

public sealed class UploadTeacherImageCommandHandler(
    IApplicationDbContext context,
    ICurrentUserService currentUser,
    IMediaService mediaService,
    IDateTimeProvider dateTimeProvider,
    ICacheService cache) : IRequestHandler<UploadTeacherImageCommand, MediaUploadResult>
{
    public async Task<MediaUploadResult> Handle(UploadTeacherImageCommand command, CancellationToken cancellationToken)
    {
        var teacherId = currentUser.RequireTeacherId();

        var teacher = await context.TeacherProfiles
            .Include(profile => profile.User)
            .FirstOrDefaultAsync(profile => profile.Id == teacherId, cancellationToken)
            ?? throw new NotFoundException("Teacher", teacherId);

        var folder = command.Kind == TeacherImageKind.Profile ? "teachers/profile" : "teachers/cover";
        var previousPublicId = command.Kind == TeacherImageKind.Profile
            ? teacher.ProfileImagePublicId
            : teacher.CoverImagePublicId;

        var uploaded = await mediaService.UploadAsync(
            command.File.Content,
            command.File.FileName,
            folder,
            cancellationToken);

        var utcNow = dateTimeProvider.UtcNow;

        if (command.Kind == TeacherImageKind.Profile)
        {
            teacher.ProfileImageUrl = uploaded.SecureUrl ?? uploaded.Url;
            teacher.ProfileImagePublicId = uploaded.PublicId;
            teacher.User.ProfileImageUrl = teacher.ProfileImageUrl;
            teacher.User.ProfileImagePublicId = uploaded.PublicId;
            teacher.User.UpdatedAt = utcNow;
        }
        else
        {
            teacher.CoverImageUrl = uploaded.SecureUrl ?? uploaded.Url;
            teacher.CoverImagePublicId = uploaded.PublicId;
        }

        teacher.UpdatedAt = utcNow;
        await context.SaveChangesAsync(cancellationToken);

        // Only remove the previous asset once the new one is safely persisted.
        if (!string.IsNullOrWhiteSpace(previousPublicId))
        {
            await mediaService.DeleteAsync(previousPublicId, cancellationToken);
        }

        await cache.RemoveAsync(CacheKeys.TeacherProfile(teacherId), cancellationToken);
        await cache.RemoveByTagAsync(CacheKeys.TeachersTag, cancellationToken);

        return uploaded;
    }
}
