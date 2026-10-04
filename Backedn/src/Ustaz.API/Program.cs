using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;
using Ustaz.API.Extensions;
using Ustaz.API.Hubs;
using Ustaz.API.Middleware;
using Ustaz.API.Services;
using Ustaz.Application;
using Ustaz.Application.Common.Interfaces;
using Microsoft.Extensions.FileProviders;
using Ustaz.Infrastructure;
using Ustaz.Infrastructure.Services.Media;
using Ustaz.Infrastructure.Options;
using Ustaz.Persistence;
using Ustaz.Persistence.Contexts;
using Ustaz.Persistence.Seeding;
using CorsOptions = Ustaz.Infrastructure.Options.CorsOptions;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Startup failure reporting, installed before anything that can fail.
//
// Service registration and builder.Build() both run before any try/catch further down this
// file could catch them, and either can throw: a storage root that cannot be created, a JWT
// signing key that cannot be persisted, an options validator tripping on start. Every one of
// those escapes as an unhandled exception and the ASP.NET Core Module answers all of them with
// the same contentless HTTP 500.30 page.
//
// A process-wide hook catches the lot, wherever in startup it happened, and writes a named
// cause to logs/startup-error.txt — the one sink a shared-hosting operator can actually read.
// ---------------------------------------------------------------------------
var startupContentRoot = builder.Environment.ContentRootPath;
var startupEnvironment = builder.Environment.EnvironmentName;

AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
{
    if (eventArgs.ExceptionObject is not Exception exception)
    {
        return;
    }

    // A missing-configuration failure has already written a far more useful report naming the
    // exact keys. Overwriting it with a generic stack trace would lose that.
    if (exception is StartupConfigurationException)
    {
        return;
    }

    StartupDiagnostics.Report(
        StartupDiagnostics.DescribeUnexpectedFailure(exception, startupEnvironment, startupContentRoot),
        startupContentRoot);
};

// ---------------------------------------------------------------------------
// Logging: Serilog is configured entirely from appsettings so development and
// production can differ without a code change. Secrets are never logged.
// ---------------------------------------------------------------------------
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "Ustaz.API"));

// ---------------------------------------------------------------------------
// Configuration gate. Everything the application cannot run without is checked here, in one
// place, before a single service is registered — so a deployment that forgot an environment
// variable gets a named list of what is missing rather than a bare HTTP 500.30 page.
// ---------------------------------------------------------------------------
var startupWarnings = StartupConfigurationCheck.Validate(builder.Configuration, builder.Environment);

// ---------------------------------------------------------------------------
// Composition root
// ---------------------------------------------------------------------------
// Storage resolves against the host content root, so a deployed instance keeps its files
// beside the application rather than wherever the process was launched from.
builder.Services.AddSingleton<IStorageRootProvider>(
    new HostStorageRootProvider(builder.Environment));

builder.Services.AddApplicationLayer();
builder.Services.AddPersistenceLayer(builder.Configuration);
builder.Services.AddInfrastructureLayer(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<ICorrelationContext, HttpCorrelationContext>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddUstazLocalization();
builder.Services.AddUstazModelValidationResponse();
builder.Services.AddUstazApiVersioning();
builder.Services.AddUstazSwagger();
// A bootstrap logger: startup decisions happen before the host (and Serilog) are built.
using var bootstrapLoggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
var bootstrapLogger = bootstrapLoggerFactory.CreateLogger("Ustaz.Startup");

builder.Services.AddUstazAuthentication(builder.Configuration, builder.Environment, bootstrapLogger);
builder.Services.AddUstazCors(builder.Configuration);
builder.Services.AddUstazRateLimiting(builder.Configuration);

// ---------------------------------------------------------------------------
// Real-time delivery. SignalR ships with ASP.NET Core, so this adds no external
// dependency; when it is switched off the no-op publisher from the Infrastructure
// layer stays in place and notifications are simply fetched rather than pushed.
// ---------------------------------------------------------------------------
var realtimeEnabled = builder.Configuration.GetValue("Realtime:Enabled", true);

if (realtimeEnabled)
{
    builder.Services.AddSignalR();
    builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

    // Presence is what keeps email an offline channel rather than a duplicate of the bell.
    builder.Services.AddSingleton<IUserPresenceTracker, InMemoryUserPresenceTracker>();
}

// Background work. Off by default in the test host, which drives time by hand.
if (builder.Configuration.GetValue("Exams:ExpirySweepEnabled", true))
{
    builder.Services.AddHostedService<ExamAttemptExpiryService>();
}

if (builder.Configuration.GetValue("EarlyWarning:Enabled", true))
{
    builder.Services.AddHostedService<ScheduledNotificationsService>();
}

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Multipart uploads are additionally capped per-file by Storage:MaxFileSizeInMegabytes.
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 64L * 1024 * 1024;
});

// ---------------------------------------------------------------------------
// Health checks: the API itself, SQL Server, and Redis when it is configured.
// ---------------------------------------------------------------------------
var healthChecks = builder.Services
    .AddHealthChecks()
    .AddCheck("api", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API is running."))
    .AddDbContextCheck<UstazDbContext>("database");

var redisConnection = builder.Configuration["Redis:ConnectionString"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    healthChecks.AddRedis(redisConnection, name: "redis");
}

var app = builder.Build();

// Configuration gaps that are survivable but worth an operator's attention. Logged through
// Serilog so they land in the same file as everything else.
foreach (var warning in startupWarnings)
{
    app.Logger.LogWarning("Startup configuration warning: {Warning}", warning);
}

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
// First in the pipeline, ahead of the exception handler: the culture it sets is what decides
// the language of every error document the rest of the pipeline can produce.
app.UseRequestLocalization();

app.UseExceptionHandler();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        // Enough to trace a request end to end; never a header, a token or a body.
        diagnosticContext.Set("CorrelationId", httpContext.Response.Headers[Ustaz.API.Middleware.CorrelationIdMiddleware.HeaderName].ToString());
        diagnosticContext.Set("Endpoint", httpContext.GetEndpoint()?.DisplayName);
        diagnosticContext.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

// Conservative security headers; the API serves JSON, never HTML pages.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["X-Permitted-Cross-Domain-Policies"] = "none";
    headers.Remove("X-Powered-By");

    await next();
});

if (app.Environment.IsDevelopment())
{
    app.UseUstazSwagger();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();

    // Swagger stays available outside development only when explicitly enabled.
    if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseUstazSwagger();
    }
}

app.UseStaticFiles();

// Public media (avatars, covers, school logos, course thumbnails) lives under the storage
// root rather than inside the published application, so it survives a redeploy. Mapping it
// here is what makes the /uploads URLs the media service hands out resolve.
//
// Only public assets are served this way. Private educational documents — homework
// attachments, message attachments, course materials — live under {root}/tenants and
// {root}/shared, which no static file provider is pointed at: they are reachable only through
// an authorised download endpoint that re-checks enrollment, role and tenant.
var storageOptions = app.Configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>()
    ?? new StorageOptions();

var publicMediaRoot = LocalMediaService.ResolvePublicRoot(
    storageOptions,
    app.Environment.ContentRootPath);

// A storage root that cannot be created must not take the whole API down with HTTP 500.30.
// The configured path is a mounted volume in Docker, but on IIS it is frequently a
// Linux-style path that resolves to an unwritable location on the host — the same hazard
// JwtSigningKeyProvider already degrades around. What is lost is the serving of public
// media, which is worth a warning in the log and not an outage.
try
{
    Directory.CreateDirectory(publicMediaRoot);

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(publicMediaRoot),
        RequestPath = LocalMediaService.PublicRequestPath,
        // Uploaded content is never guessed into a type, and never executed by the browser.
        ServeUnknownFileTypes = false,
        OnPrepareResponse = context =>
        {
            var headers = context.Context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["Content-Disposition"] = "inline";

            // Safe to cache hard: the filename is a generated id, so replacing an avatar
            // produces a new URL rather than changing what this one points at.
            headers["Cache-Control"] = "public, max-age=" + storageOptions.PublicCacheSeconds + ", immutable";
        }
    });
}
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    app.Logger.LogWarning(
        exception,
        "The public media directory {Path} could not be created, so locally stored media will "
        + "not be served. Set Storage__RootPath to a path the application can write to.",
        publicMediaRoot);
}

var corsPolicy = app.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()?.PolicyName
    ?? "UstazClientPolicy";
app.UseCors(corsPolicy);

app.UseAuthentication();

// After authentication on purpose: the limiter partitions by user id when there is one and
// falls back to the client IP otherwise, and the principal only exists from here on.
app.UseRateLimiter();

app.UseMiddleware<CorrelationIdMiddleware>();

app.UseAuthorization();

// No RequireRateLimiting here. The baseline ceiling is the global limiter configured in
// AddUstazRateLimiting; attaching it as a convention would override every controller's own
// [EnableRateLimiting] attribute, which is how the credential endpoints lost their budget.
app.MapControllers();

if (realtimeEnabled)
{
    app.MapHub<NotificationsHub>(NotificationsHub.Path);
}

// Health output is deliberately terse: overall state and each check's name and status.
// Descriptions, exception text and durations are internal detail and are not published.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

// Liveness: is the process up at all? Used by the orchestrator to decide on a restart.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Name == "api",
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

// Readiness: can it actually serve traffic? Includes the database.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Name != "api",
    ResponseWriter = WriteHealthResponseAsync
}).AllowAnonymous();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ---------------------------------------------------------------------------
// Startup migration + seeding, all opt-in through configuration.
// ---------------------------------------------------------------------------
// ---------------------------------------------------------------------------
// Run.
//
// Everything from here can still fail in ways that never reach a request: an unwritable
// storage root, a JWT key that cannot be persisted, an options validator tripping on start.
// All of those escape as an unhandled exception, and the ASP.NET Core Module answers every one
// of them with the same contentless HTTP 500.30 page. Catching here is what turns that page
// into a named cause in logs/startup-error.txt.
//
// The exception is rethrown: the process must still fail, so the orchestrator restarts it and
// the host records a failed start. The only thing added is a readable account of why.
// ---------------------------------------------------------------------------
await RunStartupTasksAsync(app);

await app.RunAsync();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var payload = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString()
        })
    };

    return context.Response.WriteAsJsonAsync(payload);
}

static async Task RunStartupTasksAsync(WebApplication app)
{
    var seedOptions = app.Configuration.GetSection(SeedOptions.SectionName).Get<SeedOptions>() ?? new SeedOptions();

    if (!seedOptions.ApplyMigrationsOnStartup && !seedOptions.SeedEssentials && !seedOptions.SeedDemoData)
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDatabaseSeeder>();

    try
    {
        if (seedOptions.ApplyMigrationsOnStartup)
        {
            await seeder.MigrateAsync();
        }

        if (seedOptions.SeedEssentials)
        {
            await seeder.SeedEssentialsAsync();
        }

        if (seedOptions.SeedDemoData)
        {
            await seeder.SeedDemoDataAsync();
        }
    }
    catch (Exception exception)
    {
        logger.LogError(exception, "USTAZ startup migration/seeding failed.");

        if (app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}

/// <summary>Exposed so the integration test host can reference the entry point assembly.</summary>
public partial class Program;
