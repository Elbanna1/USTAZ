using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Authentication.Dtos;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Authentication.Services;

/// <inheritdoc />
public sealed class AuthenticationTokenIssuer(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IJwtTokenService jwtTokenService,
    ICurrentUserService currentUser,
    IDateTimeProvider dateTimeProvider) : IAuthenticationTokenIssuer
{
    public async Task<AuthenticationResponse> IssueAsync(
        ApplicationUser user,
        Guid? familyId = null,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileIdsAsync(user.Id, cancellationToken);
        var roles = await userManager.GetRolesAsync(user);

        var accessToken = jwtTokenService.CreateAccessToken(
            user,
            roles,
            profile.TeacherId,
            profile.StudentId,
            profile.ParentId,
            profile.TenantId,
            profile.TenantSlug,
            profile.TenantRole?.ToString());

        var refreshToken = jwtTokenService.CreateRefreshToken();

        await context.RefreshTokens.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = refreshToken.TokenHash,
                FamilyId = familyId ?? Guid.NewGuid(),
                ExpiresAt = refreshToken.ExpiresAtUtc,
                CreatedByIp = currentUser.IpAddress,
                UserAgent = Truncate(currentUser.UserAgent, 512),
                CreatedAt = dateTimeProvider.UtcNow
            },
            cancellationToken);

        user.LastLoginAt = dateTimeProvider.UtcNow;

        await context.SaveChangesAsync(cancellationToken);

        return new AuthenticationResponse
        {
            AccessToken = accessToken.Token,
            RefreshToken = refreshToken.Token,
            AccessTokenExpiresAt = accessToken.ExpiresAtUtc,
            ExpiresInSeconds = accessToken.ExpiresInSeconds,
            User = BuildDto(user, roles, profile)
        };
    }

    public async Task<CurrentUserDto> BuildCurrentUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadProfileIdsAsync(user.Id, cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        return BuildDto(user, roles, profile);
    }

    private async Task<ProfileIds> LoadProfileIdsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var teacherId = await context.TeacherProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var studentId = await context.StudentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var parentId = await context.ParentProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // The active workspace: the one the user owns, otherwise the first they belong to.
        var membership = await context.TenantMembers
            .AsNoTracking()
            .Where(member => member.UserId == userId && member.Status == TenantMemberStatus.Active)
            .OrderBy(member => member.Role == TenantMemberRole.Owner ? 0 : 1)
            .ThenBy(member => member.CreatedAt)
            .Select(member => new
            {
                member.TenantId,
                member.Role,
                member.Tenant.Slug,
                member.Tenant.Name
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new ProfileIds(
            teacherId,
            studentId,
            parentId,
            membership?.TenantId,
            membership?.Slug,
            membership?.Name,
            membership?.Role);
    }

    private static CurrentUserDto BuildDto(ApplicationUser user, IList<string> roles, ProfileIds profile) =>
        new()
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FirstName = user.FirstName,
            LastName = user.LastName,
            DisplayName = user.DisplayName,
            ProfileImageUrl = user.ProfileImageUrl,
            PhoneNumber = user.PhoneNumber,
            City = user.City,
            PreferredLanguage = user.PreferredLanguage,
            EmailConfirmed = user.EmailConfirmed,
            IsActive = user.IsActive,
            IsVerified = user.IsVerified,
            Roles = [.. roles],
            TeacherId = profile.TeacherId,
            StudentId = profile.StudentId,
            ParentId = profile.ParentId,
            TenantId = profile.TenantId,
            TenantSlug = profile.TenantSlug,
            TenantName = profile.TenantName,
            TenantRole = profile.TenantRole,
            CreatedAt = user.CreatedAt
        };

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];

    private readonly record struct ProfileIds(
        Guid? TeacherId,
        Guid? StudentId,
        Guid? ParentId,
        Guid? TenantId,
        string? TenantSlug,
        string? TenantName,
        TenantMemberRole? TenantRole);
}
