using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Ustaz.API.Extensions;

public static class SwaggerExtensions
{
    /// <summary>Tag order shown in the Swagger UI.</summary>
    private static readonly string[] TagOrder =
    [
        "Authentication",
        "Teachers",
        "Students",
        "Subjects",
        "Grades",
        "Groups",
        "Courses",
        "Schedule",
        "Enrollments",
        "Discounts",
        "Reviews",
        "Favorites",
        "Inquiries",
        "Notifications",
        "Search",
        "Dashboard",
        "Admin",
        "Health"
    ];

    public static IServiceCollection AddUstazSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "USTAZ API",
                Version = "v1",
                Description =
                    "USTAZ - أستاذ. Backend API for the Egyptian teacher discovery and learning platform.\n\n"
                    + "All endpoints are versioned under /api/v1. Successful responses use "
                    + "{ success, message, data }; failures use RFC 7807 ProblemDetails with a "
                    + "validation `errors` dictionary where applicable.",
                Contact = new OpenApiContact { Name = "USTAZ Engineering" },
                License = new OpenApiLicense { Name = "Proprietary" }
            });

            var jwtScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Paste the JWT access token here. The 'Bearer ' prefix is added automatically.",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            };

            options.AddSecurityDefinition("Bearer", jwtScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { [jwtScheme] = [] });

            // Every controller carries [ApiExplorerSettings(GroupName = "Exams")] and the like
            // to drive the tag grouping in the UI. That also replaces the API-version group
            // name, so the default "does this action belong to document v1" test matched
            // nothing and the document came out with zero paths. There is exactly one
            // document, so include every version-1 action regardless of its display group.
            options.DocInclusionPredicate((_, description) =>
            {
                var version = description.ActionDescriptor.EndpointMetadata
                    .OfType<Asp.Versioning.ApiVersionAttribute>()
                    .SelectMany(attribute => attribute.Versions)
                    .Select(apiVersion => apiVersion.ToString())
                    .FirstOrDefault();

                return version is null || version.StartsWith("1", StringComparison.Ordinal);
            });

            options.EnableAnnotations();
            options.SupportNonNullableReferenceTypes();
            options.UseInlineDefinitionsForEnums();

            options.OrderActionsBy(description =>
            {
                var tag = description.ActionDescriptor.EndpointMetadata
                    .OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
                    .Select(_ => description.GroupName)
                    .FirstOrDefault() ?? description.GroupName ?? string.Empty;

                var index = Array.IndexOf(TagOrder, tag);
                return (index < 0 ? TagOrder.Length : index).ToString("D3") + description.RelativePath;
            });

            // Taking the last dot-separated segment of FullName works for a plain class but
            // produces garbage for a generic — ApiResponse<CurrentUserDto> ends up as
            // "0, Culture=neutral, PublicKeyToken=null]]", and the duplicates collide, which
            // aborted generation for the whole document.
            options.CustomSchemaIds(BuildSchemaId);

            var xmlFile = Path.Combine(AppContext.BaseDirectory, "Ustaz.API.xml");
            if (File.Exists(xmlFile))
            {
                options.IncludeXmlComments(xmlFile, includeControllerXmlComments: true);
            }
        });

        return services;
    }

    /// <summary>
    /// A readable, collision-free schema name. Nested types keep their outer name and a
    /// generic is spelled out from its arguments: <c>ApiResponseOfCurrentUserDto</c>.
    /// </summary>
    private static string BuildSchemaId(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }

        var name = type.Name;
        var tick = name.IndexOf('`');

        if (tick > 0)
        {
            name = name[..tick];
        }

        var arguments = string.Join("And", type.GetGenericArguments().Select(BuildSchemaId));

        return name + "Of" + arguments;
    }

    public static WebApplication UseUstazSwagger(this WebApplication app)
    {
        app.UseSwagger(options => options.RouteTemplate = "swagger/{documentName}/swagger.json");

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "USTAZ API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "USTAZ API";
            options.DisplayRequestDuration();
            options.EnablePersistAuthorization();
            options.DefaultModelsExpandDepth(1);
        });

        return app;
    }
}
