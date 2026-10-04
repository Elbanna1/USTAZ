namespace Ustaz.Persistence.Seeding;

/// <summary>Applies migrations and idempotently seeds reference, demo and test data.</summary>
public interface IDatabaseSeeder
{
    Task MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs one seeding mode, after <see cref="SeedSafety"/> has approved it for the
    /// environment in <paramref name="request"/>. Returns what was written.
    /// </summary>
    /// <exception cref="SeedSafetyException">
    /// The requested mode must not run against this environment.
    /// </exception>
    Task<SeedReport> SeedAsync(SeedRequest request, CancellationToken cancellationToken = default);

    /// <summary>Seeds roles, subjects, grades, plans and the configured administrator.</summary>
    Task SeedEssentialsAsync(CancellationToken cancellationToken = default);

    /// <summary>Seeds fictional demo workspaces, people, content, assessment and payments.</summary>
    Task SeedDemoDataAsync(CancellationToken cancellationToken = default);
}
