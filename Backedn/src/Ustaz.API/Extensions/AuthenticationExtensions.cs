using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ustaz.Application.Common.Localization;
using Ustaz.Domain.Constants;
using Ustaz.Infrastructure.Options;

namespace Ustaz.API.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddUstazAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("The Jwt configuration section is missing.");

        // Configuration wins. When Jwt__Key was not supplied — a deployment that never set
        // the environment variable — fall back to a key persisted outside source control
        // rather than refusing to start.
        jwt.Key = Services.JwtSigningKeyProvider.Resolve(
            jwt.Key,
            configuration["Storage:RootPath"] ?? "storage",
            environment.ContentRootPath,
            logger);

        // Keep the resolved key visible to anything that binds JwtOptions later.
        services.PostConfigure<JwtOptions>(options => options.Key = jwt.Key);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = false;
                options.RequireHttpsMetadata = !string.Equals(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                    "Development",
                    StringComparison.OrdinalIgnoreCase);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role,
                    NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier
                };

                options.Events = new JwtBearerEvents
                {
                    // A browser WebSocket cannot set an Authorization header, so SignalR
                    // passes the token in the query string. It is accepted only for the hub
                    // path, never for the REST surface.
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"].ToString();

                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments(Hubs.NotificationsHub.Path))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },

                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception is SecurityTokenExpiredException)
                        {
                            context.Response.Headers.Append("x-token-expired", "true");
                            context.HttpContext.Items[TokenExpiredMarker] = true;
                        }

                        return Task.CompletedTask;
                    },

                    // Without these two the authentication middleware answers a missing or
                    // expired token with an empty 401 and a bare 403: no body, no code,
                    // nothing for a client to show a user or switch on. Every other failure
                    // in the API returns a problem document, and a session that has simply
                    // expired is the single most common failure a signed-in user meets.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();

                        var expired = context.HttpContext.Items.ContainsKey(TokenExpiredMarker);

                        await WriteProblemAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            expired ? ErrorCodes.TokenExpired : ErrorCodes.Unauthorized,
                            expired ? "Your session has expired. Please sign in again." : null);
                    },

                    OnForbidden = context => WriteProblemAsync(
                        context.HttpContext,
                        StatusCodes.Status403Forbidden,
                        ErrorCodes.Forbidden,
                        authoredMessage: null)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(PolicyNames.AdminOnly, policy => policy.RequireRole(RoleNames.Admin))
            .AddPolicy(PolicyNames.TeacherOnly, policy => policy.RequireRole(RoleNames.Teacher))
            .AddPolicy(PolicyNames.StudentOnly, policy => policy.RequireRole(RoleNames.Student))
            .AddPolicy(PolicyNames.TeacherOrAdmin, policy => policy.RequireRole(RoleNames.Teacher, RoleNames.Admin))
            .AddPolicy(PolicyNames.AuthenticatedUser, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(PolicyNames.VerifiedTeacherOnly, policy => policy
                .RequireRole(RoleNames.Teacher)
                .RequireClaim(CustomClaimTypes.IsVerified, "true"))
            .AddPolicy(PolicyNames.ParentOnly, policy => policy.RequireRole(RoleNames.Parent))
            // Workspace membership is proved by the tenantId claim; the actual role and
            // permissions are always re-resolved from the database by the handlers.
            .AddPolicy(PolicyNames.TenantMember, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context =>
                    context.User.HasClaim(claim => claim.Type == CustomClaimTypes.TenantId)
                    || context.User.IsInRole(RoleNames.Admin)));

        return services;
    }

    /// <summary>
    /// Marks a request whose token was rejected specifically because it had expired, so the
    /// challenge that follows can say so. OnAuthenticationFailed knows why; OnChallenge, which
    /// writes the response, does not.
    /// </summary>
    private const string TokenExpiredMarker = "ustaz.token-expired";

    /// <summary>
    /// Writes the same problem document shape the global exception handler produces, in the
    /// caller's language. Kept here rather than shared with the handler because an
    /// authentication failure never becomes an exception: the middleware short-circuits.
    /// </summary>
    private static async Task WriteProblemAsync(
        HttpContext httpContext,
        int status,
        string code,
        string? authoredMessage)
    {
        if (httpContext.Response.HasStarted)
        {
            return;
        }

        var language = UstazLanguages.FromCurrentCulture();

        var detail = language == UstazLanguage.Arabic
            ? ErrorCatalog.ArabicFor(code) ?? ErrorCatalog.ArabicForStatus(status)
            : authoredMessage ?? ErrorCatalog.EnglishForStatus(status);

        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = status,
            Title = status == StatusCodes.Status401Unauthorized ? "Unauthorized." : "Forbidden.",
            Detail = detail,
            Instance = httpContext.Request.Path,
            Type = "https://datatracker.ietf.org/doc/html/rfc7807"
        };

        problem.Extensions["code"] = code;
        problem.Extensions["success"] = false;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        problem.Extensions["language"] = UstazLanguages.TagFor(language);

        httpContext.Response.StatusCode = status;

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            httpContext.RequestAborted);
    }
}
