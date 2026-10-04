namespace Ustaz.Persistence.Seeding;

/// <summary>
/// One seeding run: what to write, and the facts <see cref="SeedSafety"/> needs to decide
/// whether writing it is allowed.
///
/// <see cref="EnvironmentName"/> is supplied by the caller rather than read from
/// configuration here. The host is the only component that actually knows which environment
/// it is running as, and a safety decision must never depend on a value a misconfigured
/// settings file could flip.
/// </summary>
public sealed record SeedRequest
{
    public required SeedMode Mode { get; init; }

    /// <summary>The hosting environment name, e.g. <c>Development</c> or <c>Production</c>.</summary>
    public required string EnvironmentName { get; init; }

    /// <summary>
    /// Lets an operator run <see cref="SeedMode.DemoData"/> against a non-development
    /// environment — a staging host, typically. Off by default and deliberately noisy.
    /// Never permits demo data in Production.
    /// </summary>
    public bool AllowDemoDataOutsideDevelopment { get; init; }

    /// <summary>
    /// Required for <see cref="SeedMode.ProductionBootstrap"/>. Creating the first platform
    /// administrator is the one privileged write the seeder can make, so it is never implied.
    /// </summary>
    public bool ConfirmProductionBootstrap { get; init; }

    /// <summary>
    /// Password for the fictional demo accounts. Supplied from configuration or generated;
    /// never written to a log, a report, or a source file.
    /// </summary>
    public string? DemoPassword { get; init; }

    public bool IsDevelopmentLike =>
        EnvironmentName.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || EnvironmentName.Equals("Testing", StringComparison.OrdinalIgnoreCase)
        || EnvironmentName.Equals("Test", StringComparison.OrdinalIgnoreCase)
        || EnvironmentName.Equals("Local", StringComparison.OrdinalIgnoreCase);

    public bool IsProduction =>
        EnvironmentName.Equals("Production", StringComparison.OrdinalIgnoreCase);
}
