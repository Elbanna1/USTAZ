using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Infrastructure.Options;
using Ustaz.Infrastructure.Services.Authentication;
using Ustaz.Infrastructure.Services.Caching;
using Ustaz.Infrastructure.Services.Common;
using Ustaz.Infrastructure.Services.Email;
using Ustaz.Infrastructure.Services.Media;
using Ustaz.Infrastructure.Services.Messaging;
using Ustaz.Infrastructure.Services.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Features.Billing;
using Ustaz.Application.Features.Tenants;

namespace Ustaz.Infrastructure;

/// <summary>
/// Composition root for the Infrastructure layer. Every external dependency degrades
/// gracefully so the API still runs locally without Redis or SMTP. File and image storage has
/// no external dependency at all: it is always this server's own filesystem.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<CorsOptions>(configuration.GetSection(CorsOptions.SectionName));
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IUrlGuard, UrlGuard>();
        services.AddSingleton<IWhatsAppLinkService, WhatsAppLinkService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        // No live transport by default. The API layer swaps in the SignalR publisher when
        // Realtime:Enabled is set, so nothing here depends on a web host being present.
        services.TryAddSingleton<IRealtimeNotifier, NoOpRealtimeNotifier>();
        services.TryAddSingleton<IUserPresenceTracker, UnknownPresenceTracker>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ITransactionalEmailService, TransactionalEmailService>();

        // The API replaces this with the request-scoped one; background work keeps the null.
        services.TryAddSingleton<ICorrelationContext, NullCorrelationContext>();
        services.AddSingleton<IDomainVerificationService, DnsDomainVerificationService>();
        services.AddSingleton<IPaymentWebhookVerifier, PaymentWebhookVerifier>();

        // Private file storage. The content root is resolved by the host so the storage
        // folder sits beside the deployed application, never at a hard-coded path.
        services.AddSingleton<IFileStorage>(provider => new LocalFileStorage(
            provider.GetRequiredService<IOptions<StorageOptions>>(),
            provider.GetRequiredService<ILogger<LocalFileStorage>>(),
            provider.GetService<IStorageRootProvider>()?.ContentRootPath ?? AppContext.BaseDirectory));

        // A couple of maintenance handlers live here because they need SMTP settings and the
        // concrete delivery service. Registering this assembly with MediatR keeps them
        // reachable through the same dispatch as every other command.
        services.AddMediatR(mediator => mediator.RegisterServicesFromAssembly(
            typeof(DependencyInjection).Assembly));

        AddCaching(services, configuration);
        AddMedia(services, configuration);
        AddEmail(services, configuration);

        return services;
    }

    private static void AddCaching(IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>() ?? new RedisOptions();

        if (redis.Enabled)
        {
            services.AddStackExchangeRedisCache(cacheOptions =>
            {
                cacheOptions.Configuration = redis.ConnectionString;
                cacheOptions.InstanceName = redis.InstanceName;
            });
        }
        else
        {
            // Keeps ICacheService usable (and the app bootable) without a Redis server.
            services.AddDistributedMemoryCache();
        }

        services.TryAddSingleton<IDistributedCache, MemoryDistributedCache>();
        services.AddSingleton<ICacheService, DistributedCacheService>();
    }

    /// <summary>
    /// Public media — avatars, cover images, school logos, course thumbnails.
    ///
    /// There is one implementation and it writes to this server's own storage root. USTAZ
    /// deliberately has no third-party image host: a school's files stay on infrastructure the
    /// operator controls, and there is no external account to configure, pay for or have
    /// expire. The root is a mounted volume in Docker and a configured directory on IIS, so
    /// the files outlive any single deployment.
    /// </summary>
    private static void AddMedia(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<MediaValidator>();

        // Same content root as private storage, so local media survives a redeploy.
        services.AddSingleton<IMediaService>(provider => new LocalMediaService(
            provider.GetRequiredService<MediaValidator>(),
            provider.GetRequiredService<IOptions<StorageOptions>>(),
            provider.GetRequiredService<ILogger<LocalMediaService>>(),
            provider.GetService<IStorageRootProvider>()?.ContentRootPath ?? AppContext.BaseDirectory));
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        var email = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();

        if (email.IsConfigured)
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }
        else
        {
            // No SMTP host, or the password secret is absent: messages are written to disk
            // so a developer can still click a verification link, and nothing throws.
            services.AddScoped<IEmailService>(provider => new DevelopmentEmailService(
                provider.GetRequiredService<IOptions<EmailOptions>>(),
                provider.GetRequiredService<IOptions<StorageOptions>>(),
                provider.GetRequiredService<ILogger<DevelopmentEmailService>>(),
                provider.GetService<IStorageRootProvider>()?.ContentRootPath ?? AppContext.BaseDirectory));
        }
    }
}
