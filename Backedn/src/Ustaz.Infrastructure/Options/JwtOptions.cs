using System.ComponentModel.DataAnnotations;

namespace Ustaz.Infrastructure.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>Signing key. Must come from an environment variable / secret store, never source control.</summary>
    [Required]
    [MinLength(32, ErrorMessage = "The JWT signing key must be at least 32 characters long.")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;

    [Range(1, 365)]
    public int RefreshTokenDays { get; set; } = 14;

    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;
}
