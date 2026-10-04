using System.Threading.RateLimiting;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ustaz.Application.Common.Exceptions;
using Ustaz.Application.Common.Localization;
using Ustaz.Infrastructure.Options;
using CorsOptions = Ustaz.Infrastructure.Options.CorsOptions;

namespace Ustaz.API.Extensions;

public static class ApiServiceExtensions
{
    public const string GlobalRateLimitPolicy = "ustaz-global";
    public const string AuthRateLimitPolicy = "ustaz-auth";
    public const string UploadRateLimitPolicy = "ustaz-upload";

    public static IServiceCollection AddUstazApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = ApiVersionReader.Combine(
                    new UrlSegmentApiVersionReader(),
                    new HeaderApiVersionReader("x-api-version"));
            })
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    /// <summary>
    /// CORS always comes from configuration. AllowAnyOrigin is never combined with credentials,
    /// so an empty allow-list means the API simply refuses browser cross-origin calls.
    /// </summary>
    public static IServiceCollection AddUstazCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        var origins = corsOptions.AllowedOrigins.Length > 0
            ? corsOptions.AllowedOrigins
            : configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options => options.AddPolicy(corsOptions.PolicyName, policy =>
        {
            if (origins.Length == 0)
            {
                return;
            }

            policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("x-token-expired", "x-pagination");
        }));

        return services;
    }

    public static IServiceCollection AddUstazRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var limits = configuration.GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()
            ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, cancellationToken) =>
            {
                var language = UstazLanguages.FromCurrentCulture();

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Detail = language == UstazLanguage.Arabic
                        ? ErrorCatalog.ArabicFor(ErrorCodes.RateLimited)
                        : ErrorCatalog.EnglishForStatus(StatusCodes.Status429TooManyRequests),
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["code"] = ErrorCodes.RateLimited;
                problem.Extensions["success"] = false;
                problem.Extensions["language"] = UstazLanguages.TagFor(language);

                await context.HttpContext.Response.WriteAsJsonAsync(
                    problem,
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken);
            };

            // The baseline ceiling every request passes through, including the ones whose
            // endpoint carries a tighter named policy.
            //
            // This has to be the *global* limiter rather than a policy attached with
            // MapControllers().RequireRateLimiting(...). That convention appends its
            // EnableRateLimiting metadata after a controller's own [EnableRateLimiting]
            // attribute, and the middleware reads the last entry — so attaching the generous
            // policy that way silently replaced the tight auth budget on every credential
            // endpoint. A global limiter composes with named policies instead of racing them.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.GlobalPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.GlobalWindowSeconds),
                        QueueLimit = 0
                    }));

            // Kept as a named policy as well, so an action inside a tightly limited
            // controller can opt back out to the ordinary budget.
            options.AddPolicy(GlobalRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.GlobalPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.GlobalWindowSeconds),
                        QueueLimit = 0
                    }));

            // Much tighter on the credential endpoints to blunt brute forcing.
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.AuthWindowSeconds),
                        QueueLimit = 0
                    }));

            // Uploads cost disk and bandwidth, so they get their own budget: generous
            // enough for someone handing in a few files, useless as an abuse channel.
            options.AddPolicy(UploadRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.UploadPermitLimit,
                        Window = TimeSpan.FromSeconds(limits.UploadWindowSeconds),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    /// <summary>
    /// Replaces the default model-state 400 with the same ProblemDetails shape the
    /// global exception handler produces.
    /// </summary>
    public static IServiceCollection AddUstazModelValidationResponse(this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());

                // Model binding failures never reach the exception handler, so this factory
                // has to produce the same envelope itself — including the stable code and the
                // localised detail a client gets from every other 400.
                var language = UstazLanguages.FromCurrentCulture();

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Validation failed.",
                    Detail = language == UstazLanguage.Arabic
                        ? ErrorCatalog.ArabicFor(ErrorCodes.ValidationFailed)
                        : ErrorCatalog.EnglishForStatus(StatusCodes.Status400BadRequest),
                    Instance = context.HttpContext.Request.Path
                };

                problem.Extensions["code"] = ErrorCodes.ValidationFailed;
                problem.Extensions["errors"] = errors;
                problem.Extensions["success"] = false;
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
                problem.Extensions["language"] = UstazLanguages.TagFor(language);

                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            };
        });

        return services;
    }

    private static string PartitionKey(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? "user:" + httpContext.User.FindFirst(Domain.Constants.CustomClaimTypes.UserId)?.Value
            : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
