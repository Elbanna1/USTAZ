namespace Ustaz.Infrastructure.Options;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Applies pending migrations at startup. Keep false in production deployments.</summary>
    public bool ApplyMigrationsOnStartup { get; set; }

    public bool SeedEssentials { get; set; } = true;

    public bool SeedDemoData { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }
}
