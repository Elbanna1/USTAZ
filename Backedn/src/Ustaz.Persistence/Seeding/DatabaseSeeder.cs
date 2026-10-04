using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Persistence.Contexts;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// The one entry point for writing seed data.
///
/// Every run states its <see cref="SeedMode"/> and the environment it believes it is in, and
/// <see cref="SeedSafety"/> decides whether that combination is allowed before a single row is
/// written. Reference data is keyed by fixed ids, so running any mode repeatedly neither
/// duplicates rows nor overwrites an operator's edits.
/// </summary>
public sealed class DatabaseSeeder(
    UstazDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        if (context.Database.IsRelational())
        {
            logger.LogInformation("Applying pending USTAZ database migrations...");
            await context.Database.MigrateAsync(cancellationToken);
        }
    }

    public async Task<SeedReport> SeedAsync(SeedRequest request, CancellationToken cancellationToken = default)
    {
        SeedSafety.Validate(request);

        var databaseName = context.Database.IsRelational()
            ? context.Database.GetDbConnection().Database
            : null;

        // Mode, environment and database name only. The connection string, the credential and
        // the demo password are never written to a log.
        logger.LogInformation("USTAZ seeding starting: {Description}", SeedSafety.Describe(request, databaseName));

        var report = new SeedReport(request.Mode);

        try
        {
            switch (request.Mode)
            {
                case SeedMode.ReferenceData:
                    await new ReferenceDataSeeder(context, roleManager, logger).SeedAsync(report, cancellationToken);
                    break;

                case SeedMode.ProductionBootstrap:
                    await new ReferenceDataSeeder(context, roleManager, logger).SeedAsync(report, cancellationToken);
                    await SeedAdministratorAsync(report);
                    break;

                case SeedMode.DemoData:
                    await new ReferenceDataSeeder(context, roleManager, logger).SeedAsync(report, cancellationToken);
                    await new DemoDataSeeder(context, userManager, report, logger)
                        .SeedAsync(request.DemoPassword!, cancellationToken);
                    break;

                case SeedMode.TestFixtures:
                    await new ReferenceDataSeeder(context, roleManager, logger).SeedAsync(report, cancellationToken);
                    await new TestFixtureSeeder(context, userManager, report)
                        .SeedAsync(request.DemoPassword ?? TestFixtureSeeder.DefaultPassword, cancellationToken);
                    break;
            }
        }
        catch (Exception exception)
        {
            // A partial run leaves whatever it had already committed, which is safe because
            // every module is idempotent: the next run inserts only what is still missing.
            // What the operator needs is the report of how far it got, not just the stack.
            report.RecordFailure(exception.GetType().Name + ": " + exception.Message);
            logger.LogError(exception, "USTAZ seeding failed part-way through.\n{Summary}", report.Summarize());
            throw;
        }

        logger.LogInformation("{Summary}", report.Summarize());
        return report;
    }

    /// <summary>
    /// Reference data plus the configured administrator, for callers that have not been
    /// updated to <see cref="SeedAsync"/>. Keeps the previous behaviour exactly.
    /// </summary>
    public async Task SeedEssentialsAsync(CancellationToken cancellationToken = default) =>
        await SeedAsync(
            new SeedRequest
            {
                Mode = SeedMode.ProductionBootstrap,
                EnvironmentName = ResolveEnvironmentName(),
                // The administrator is only created when both email and password are
                // configured, so the confirmation adds nothing a caller has not already done
                // by setting those two keys.
                ConfirmProductionBootstrap = true
            },
            cancellationToken);

    public async Task SeedDemoDataAsync(CancellationToken cancellationToken = default) =>
        await SeedAsync(
            new SeedRequest
            {
                Mode = SeedMode.DemoData,
                EnvironmentName = ResolveEnvironmentName(),
                AllowDemoDataOutsideDevelopment =
                    configuration.GetValue("Seed:AllowDemoDataOutsideDevelopment", false),
                DemoPassword = configuration["Seed:DemoPassword"]
            },
            cancellationToken);

    /// <summary>
    /// Best-effort environment name for the legacy entry points. The host knows this for
    /// certain and passes it explicitly through <see cref="SeedRequest"/>; this fallback reads
    /// the same environment variable the host itself was configured from, and refuses to guess
    /// "Development" when it finds nothing — an unknown environment must fail closed.
    /// </summary>
    private string ResolveEnvironmentName() =>
        configuration["ASPNETCORE_ENVIRONMENT"]
        ?? configuration["DOTNET_ENVIRONMENT"]
        ?? "Unknown";

    /// <summary>
    /// The administrator is only created when both email and password come from configuration
    /// (environment variables / user-secrets). Nothing is ever hardcoded.
    /// </summary>
    private async Task SeedAdministratorAsync(SeedReport report)
    {
        var email = configuration["Seed:AdminEmail"];
        var password = configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "Administrator seeding skipped: set Seed__AdminEmail and Seed__AdminPassword to provision the first admin.");
            report.Record("PlatformAdministrator", 0, skipped: 1, note: "Seed__AdminEmail/Password not configured");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            report.Record("PlatformAdministrator", 0, skipped: 1, note: "already exists");
            return;
        }

        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = configuration["Seed:AdminFirstName"] ?? "USTAZ",
            LastName = configuration["Seed:AdminLastName"] ?? "Administrator",
            DisplayName = configuration["Seed:AdminDisplayName"] ?? "USTAZ Administrator",
            IsActive = true,
            IsVerified = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to create the administrator account: {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(admin, RoleNames.Admin);
        logger.LogInformation("Seeded the administrator account from configuration.");
        report.Record("PlatformAdministrator", 1);
    }
}
