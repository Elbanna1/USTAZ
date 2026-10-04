using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Authentication;

/// <summary>
/// Issues HS256 access tokens and 256-bit opaque refresh tokens. Only the SHA-256 hash
/// of a refresh token is ever persisted.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider dateTimeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessTokenResult CreateAccessToken(
        ApplicationUser user,
        IEnumerable<string> roles,
        Guid? teacherId,
        Guid? studentId,
        Guid? parentId = null,
        Guid? tenantId = null,
        string? tenantSlug = null,
        string? tenantRole = null)
    {
        var issuedAt = dateTimeProvider.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt).ToString(),
                ClaimValueTypes.Integer64),
            new(CustomClaimTypes.UserId, user.Id.ToString()),
            new(CustomClaimTypes.DisplayName, user.DisplayName),
            new(CustomClaimTypes.IsVerified, user.IsVerified.ToString().ToLowerInvariant())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        if (teacherId.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.TeacherId, teacherId.Value.ToString()));
        }

        if (studentId.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.StudentId, studentId.Value.ToString()));
        }

        if (parentId.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.ParentId, parentId.Value.ToString()));
        }

        if (tenantId.HasValue)
        {
            claims.Add(new Claim(CustomClaimTypes.TenantId, tenantId.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(tenantSlug))
        {
            claims.Add(new Claim(CustomClaimTypes.TenantSlug, tenantSlug));
        }

        if (!string.IsNullOrWhiteSpace(tenantRole))
        {
            claims.Add(new Claim(CustomClaimTypes.TenantRole, tenantRole));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessTokenResult(encoded, expiresAt, _options.AccessTokenMinutes * 60);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes);

        return new RefreshTokenResult(
            token,
            HashRefreshToken(token),
            dateTimeProvider.UtcNow.Add(RefreshTokenLifetime));
    }

    public string HashRefreshToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hash);
    }
}
