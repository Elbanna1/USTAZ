namespace Ustaz.Persistence.Seeding;

/// <summary>Raised when a seeding run is refused. The message names the reason, never a secret.</summary>
public sealed class SeedSafetyException(string message) : InvalidOperationException(message);

/// <summary>
/// The gate every seeding run passes through.
///
/// Two separate mistakes are being prevented here, and they fail differently. Writing demo
/// people and demo money into a live database is unrecoverable by any amount of care
/// afterwards, so it is refused outright. Creating the first administrator is legitimate but
/// privileged, so it is refused unless somebody asked for it in so many words.
/// </summary>
public static class SeedSafety
{
    /// <summary>
    /// Throws <see cref="SeedSafetyException"/> when <paramref name="request"/> must not run.
    /// Returns normally when the run is permitted.
    /// </summary>
    public static void Validate(SeedRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.EnvironmentName))
        {
            throw new SeedSafetyException(
                "Seeding refused: the hosting environment name is unknown, so no safety decision can be made.");
        }

        switch (request.Mode)
        {
            case SeedMode.ReferenceData:
                // Lookup rows only. Safe everywhere by construction.
                return;

            case SeedMode.ProductionBootstrap:
                if (!request.ConfirmProductionBootstrap)
                {
                    throw new SeedSafetyException(
                        "Seeding refused: ProductionBootstrap creates the first platform administrator and needs "
                        + "an explicit confirmation. Set Seed__ConfirmProductionBootstrap=true to authorise it.");
                }

                return;

            case SeedMode.TestFixtures:
                if (!request.IsDevelopmentLike)
                {
                    throw new SeedSafetyException(
                        "Seeding refused: TestFixtures writes fictional accounts and may only run in a "
                        + "Development or Testing environment. Current environment: " + request.EnvironmentName + ".");
                }

                return;

            case SeedMode.DemoData:
                if (request.IsProduction)
                {
                    throw new SeedSafetyException(
                        "Seeding refused: DemoData creates fictional users, enrollments, reviews and payments and "
                        + "is never permitted in Production. There is no override for this.");
                }

                if (!request.IsDevelopmentLike && !request.AllowDemoDataOutsideDevelopment)
                {
                    throw new SeedSafetyException(
                        "Seeding refused: DemoData in the '" + request.EnvironmentName + "' environment needs an "
                        + "explicit opt-in. Set Seed__AllowDemoDataOutsideDevelopment=true if this database is "
                        + "genuinely disposable.");
                }

                if (string.IsNullOrWhiteSpace(request.DemoPassword))
                {
                    throw new SeedSafetyException(
                        "Seeding refused: DemoData needs a password for the fictional accounts. "
                        + "Set Seed__DemoPassword (development only).");
                }

                return;

            default:
                throw new SeedSafetyException("Seeding refused: unknown seed mode '" + request.Mode + "'.");
        }
    }

    /// <summary>
    /// A description of the run that is safe to log: the mode, the environment and the
    /// database name. Never the server, the user id or the password.
    /// </summary>
    public static string Describe(SeedRequest request, string? databaseName) =>
        "mode=" + request.Mode
        + " environment=" + request.EnvironmentName
        + " database=" + (string.IsNullOrWhiteSpace(databaseName) ? "(in-memory)" : databaseName);
}
