using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Persistence.Contexts;
using Ustaz.Persistence.Seeding;

namespace Ustaz.API.Tests;

/// <summary>
/// Boots the real API pipeline (routing, auth, validation, exception handling) against an
/// in-memory database so the integration tests need no SQL Server.
/// </summary>
public class UstazApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = "ustaz-api-tests-" + Guid.NewGuid();

    static UstazApiFactory()
    {
        // Program.cs reads configuration while the top-level statements run, which is before
        // any ConfigureAppConfiguration delegate would apply. Environment variables are the
        // one source that is both available that early and ranked above appsettings.json.
        Set("ASPNETCORE_ENVIRONMENT", "Testing");
        Set("ConnectionStrings__DefaultConnection", "Server=(local);Database=UstazTests;Trusted_Connection=True;TrustServerCertificate=True");
        Set("Jwt__Issuer", "https://tests.ustaz.local");
        Set("Jwt__Audience", "https://tests.ustaz.local");
        Set("Jwt__Key", "ustaz-integration-test-signing-key-0123456789");
        Set("Jwt__AccessTokenMinutes", "30");
        Set("Jwt__RefreshTokenDays", "7");
        Set("Cors__AllowedOrigins__0", "http://localhost:5173");
        Set("Seed__ApplyMigrationsOnStartup", "false");
        Set("Seed__SeedEssentials", "false");
        Set("Seed__SeedDemoData", "false");
        Set("Swagger__Enabled", "false");
        Set("Exams__ExpirySweepEnabled", "false");
        Set("EarlyWarning__Enabled", "false");

        // No SMTP in tests: every message is recorded and skipped, so a test run never
        // reaches out to Gmail and never waits on a connection timeout.
        Set("Email__Enabled", "false");
        Set("Email__Host", string.Empty);
        Set("Email__ClientBaseUrl", "https://app.ustaz.test");
        Set("Realtime__Enabled", "false");
        Set("Serilog__MinimumLevel__Default", "Warning");

        static void Set(string key, string value) => Environment.SetEnvironmentVariable(key, value);
    }

    /// <summary>
    /// The request budgets this host runs with. UseSetting lands in host configuration, which
    /// is the one layer WebApplicationBuilder.Configuration already carries while Program.cs's
    /// top-level statements execute — so unlike ConfigureAppConfiguration it is actually in
    /// effect by the time AddUstazRateLimiting reads it. Overridden by the rate-limit tests,
    /// which need a budget they can drive to its limit.
    ///
    /// The functional suite registers and signs in dozens of accounts from one partition (the
    /// test server reports no client IP, so every request shares a key). The real 10-per-minute
    /// auth budget would reject most of them, which says nothing about the code under test.
    /// </summary>
    protected virtual IReadOnlyDictionary<string, string> RateLimitSettings => new Dictionary<string, string>
    {
        ["RateLimiting:GlobalPermitLimit"] = "100000",
        ["RateLimiting:AuthPermitLimit"] = "100000",
        ["RateLimiting:UploadPermitLimit"] = "100000"
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        foreach (var (key, value) in RateLimitSettings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureServices(services =>
        {
            // AddDbContext registers the options, the options configuration and the context
            // itself. All three have to go, or the SQL Server provider stays registered
            // alongside the in-memory one and EF Core refuses to resolve the context.
            var doomed = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(DbContextOptions<UstazDbContext>)
                    || descriptor.ServiceType == typeof(UstazDbContext)
                    || (descriptor.ServiceType.FullName?.Contains("DbContextOptionsConfiguration", StringComparison.Ordinal) ?? false))
                .ToList();

            foreach (var descriptor in doomed)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<UstazDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<UstazDbContext>());
        });
    }

    /// <summary>Seeds roles, subjects and grades into the in-memory store.</summary>
    public async Task SeedEssentialsAsync()
    {
        using var scope = Services.CreateScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();
        await seeder.SeedEssentialsAsync();
    }

    public UstazDbContext CreateContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<UstazDbContext>();
    }
}
