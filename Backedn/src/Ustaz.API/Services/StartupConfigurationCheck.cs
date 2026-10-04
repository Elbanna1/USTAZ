using System.Text;

namespace Ustaz.API.Services;

/// <summary>
/// Validates the configuration the application cannot run without, before any service is
/// registered, and reports every problem at once.
///
/// Why this exists: a missing <c>ConnectionStrings__DefaultConnection</c> used to surface as
/// <c>InvalidOperationException</c> thrown out of the Persistence composition root. Under IIS
/// the ASP.NET Core Module turns any unhandled startup exception into the generic
/// <c>HTTP Error 500.30 - ASP.NET Core app failed to start</c> page, and with
/// <c>stdoutLogEnabled="false"</c> — the publish default — the exception is written nowhere an
/// operator will look. The symptom carried no information about the cause.
///
/// So the check does three things the bare throw did not:
/// 1. Reports <em>all</em> missing keys together, instead of whichever one fails first.
/// 2. Hands the report to <see cref="StartupDiagnostics"/>, which writes it to stderr, the
///    Windows event log, and a file under the content root — so it is visible even when the
///    stdout log is off and Serilog's file sink was never reached.
/// 3. Names only configuration <em>keys</em>, never values. A diagnostic must never be the
///    thing that leaks the connection string it is complaining about.
/// </summary>
public static class StartupConfigurationCheck
{
    /// <summary>
    /// Throws <see cref="StartupConfigurationException"/> when a required setting is absent.
    /// Non-fatal gaps are returned as warnings for the caller to log once the host is up.
    /// </summary>
    public static IReadOnlyList<string> Validate(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var missing = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.GetConnectionString("DefaultConnection")))
        {
            missing.Add(
                "ConnectionStrings__DefaultConnection — the SQL Server connection string. "
                + "Note that appsettings.Production.json ships this key as an empty placeholder, "
                + "which overrides the value in appsettings.json: on a production host the "
                + "environment variable is the only source.");
        }

        var isProduction = environment.IsProduction();

        if (isProduction)
        {
            if (string.IsNullOrWhiteSpace(configuration["Jwt:Issuer"])
                || string.IsNullOrWhiteSpace(configuration["Jwt:Audience"]))
            {
                missing.Add("Jwt__Issuer and Jwt__Audience — the token issuer and audience.");
            }

            if (string.IsNullOrWhiteSpace(configuration["Jwt:Key"]))
            {
                warnings.Add(
                    "Jwt__Key is not set. A signing key will be generated and persisted under the "
                    + "storage root instead. Every access token is invalidated if that file is "
                    + "lost, so set Jwt__Key explicitly in production.");
            }

            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

            if (origins.Length == 0)
            {
                warnings.Add(
                    "Cors__AllowedOrigins__0 is not set, so no browser origin is allowed and the "
                    + "web client will be blocked by CORS. Set it to the client's origin.");
            }
        }

        if (missing.Count == 0)
        {
            return warnings;
        }

        var report = BuildReport(missing, environment);

        // Every sink StartupDiagnostics can reach, including a file under the content root —
        // which on shared Windows hosting is the only one the operator can actually read.
        StartupDiagnostics.Report(report, environment.ContentRootPath);

        throw new StartupConfigurationException(report);
    }

    private static string BuildReport(IReadOnlyList<string> missing, IHostEnvironment environment)
    {
        var builder = new StringBuilder();

        builder.AppendLine("USTAZ cannot start: required configuration is missing.");
        builder.AppendLine();
        builder.AppendLine("Environment: " + environment.EnvironmentName);
        builder.AppendLine("Content root: " + environment.ContentRootPath);
        builder.AppendLine();
        builder.AppendLine("Missing settings:");

        foreach (var entry in missing)
        {
            builder.AppendLine("  - " + entry);
        }

        builder.AppendLine();
        builder.AppendLine(
            "Set these as environment variables on the host (double underscore separates the "
            + "section from the key). No value is printed here by design.");

        return builder.ToString();
    }

}

/// <summary>
/// Raised when the host is missing configuration the application cannot run without. Carries
/// only key names, so the message is safe to log and safe to read out of an event viewer.
/// </summary>
public sealed class StartupConfigurationException(string message) : Exception(message);
