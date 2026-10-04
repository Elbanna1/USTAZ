namespace Ustaz.Infrastructure.Options;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string PolicyName { get; set; } = "UstazClientPolicy";

    public string[] AllowedOrigins { get; set; } = [];
}
