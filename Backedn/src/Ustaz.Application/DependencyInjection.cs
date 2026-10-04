using System.Reflection;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Ustaz.Application.Common.Behaviours;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Application.Features.Authentication.Services;
using Ustaz.Application.Features.Reviews.Services;

namespace Ustaz.Application;

/// <summary>Composition root for the Application layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(assembly);
            configuration.AddOpenBehavior(typeof(UnhandledExceptionBehaviour<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehaviour<,>));
            configuration.AddOpenBehavior(typeof(CachingBehaviour<,>));
            configuration.AddOpenBehavior(typeof(PerformanceBehaviour<,>));
            configuration.AddOpenRequestPreProcessor(typeof(LoggingBehaviour<>));
        });

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddSingleton(CreateMapperConfiguration(assembly));
        services.AddScoped<IMapper, ServiceMapper>();

        services.AddScoped<IAuthenticationTokenIssuer, AuthenticationTokenIssuer>();
        services.AddScoped<IRatingRecalculator, RatingRecalculator>();
        services.AddScoped<ICertificateIssuer, Features.Certificates.CertificateIssuer>();
        services.AddScoped<Features.Analytics.StudentRiskAnalyzer>();

        // Multi-tenancy, quotas and family access are all resolved server-side, per request.
        services.AddScoped<ITenantAuthorizationService, TenantAuthorizationService>();
        services.AddScoped<IPlanLimitService, PlanLimitService>();
        services.AddScoped<IActivityRecorder, Common.Activity.ActivityRecorder>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<IParentAccessGuard, ParentAccessGuard>();

        return services;
    }

    private static TypeAdapterConfig CreateMapperConfiguration(Assembly assembly)
    {
        var config = TypeAdapterConfig.GlobalSettings;
        config.Default
            .PreserveReference(true)
            .IgnoreNullValues(false);

        config.Scan(assembly);
        return config;
    }
}
