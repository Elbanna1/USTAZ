using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Ustaz.API.Tests;

/// <summary>
/// Mints a correctly signed access token that has already expired.
///
/// Needed because the test host cannot move the clock past a real token's lifetime, and an
/// expired session is the failure a signed-in user meets most often — so the response to one
/// deserves a test. The key, issuer and audience match what
/// <see cref="UstazApiFactory"/> configures, so the token fails validation on its lifetime
/// alone rather than on its signature.
/// </summary>
internal static class ExpiredTokenFactory
{
    public const string SigningKey = "ustaz-integration-test-signing-key-0123456789";

    public const string Issuer = "https://tests.ustaz.local";

    public const string Audience = "https://tests.ustaz.local";

    public static string Create()
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Student")
            ],
            // Comfortably outside the configured 30-second clock skew.
            notBefore: DateTime.UtcNow.AddHours(-2),
            expires: DateTime.UtcNow.AddHours(-1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
