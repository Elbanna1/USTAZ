using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Ustaz.Application.Common.Localization;

namespace Ustaz.API.Extensions;

/// <summary>
/// Decides what language a request is answered in, once, for the whole pipeline.
///
/// Everything downstream — FluentValidation's built-in messages, the shared validation rules,
/// the error catalogue, the challenge responses — reads
/// <see cref="CultureInfo.CurrentUICulture"/>. Setting it here is what makes a single
/// <c>Accept-Language</c> header turn the entire error surface Arabic.
///
/// Resolution order:
/// 1. <c>X-Ustaz-Language</c>, an explicit override the client sets from the signed-in user's
///    stored preference (which it already has from <c>GET /auth/me</c>).
/// 2. <c>?lang=</c>, for a link that has to open in a particular language.
/// 3. <c>Accept-Language</c>, what the browser asks for.
/// 4. Arabic.
///
/// The user's stored <c>PreferredLanguage</c> is deliberately not read here: doing so would
/// put a database query in front of every request, including the anonymous ones, to decide the
/// wording of an error. The client knows the preference and sends it.
/// </summary>
public static class LocalizationExtensions
{
    public const string LanguageHeaderName = "X-Ustaz-Language";

    public const string LanguageQueryName = "lang";

    public static IServiceCollection AddUstazLocalization(this IServiceCollection services)
    {
        services.Configure<RequestLocalizationOptions>(options =>
        {
            var supported = UstazLanguages.SupportedTags
                .Select(tag => new CultureInfo(tag))
                .ToList();

            // Arabic is the product's default language, so it is the default culture too: a
            // request that expresses no preference is answered in Arabic.
            options.DefaultRequestCulture = new RequestCulture(UstazLanguages.ArabicTag);
            options.SupportedCultures = supported;
            options.SupportedUICultures = supported;
            options.ApplyCurrentCultureToResponseHeaders = true;

            options.RequestCultureProviders =
            [
                new CustomRequestCultureProvider(context =>
                {
                    var explicitChoice =
                        context.Request.Headers[LanguageHeaderName].ToString() is { Length: > 0 } header
                            ? header
                            : context.Request.Query[LanguageQueryName].ToString();

                    if (string.IsNullOrWhiteSpace(explicitChoice))
                    {
                        return Task.FromResult<ProviderCultureResult?>(null);
                    }

                    var tag = UstazLanguages.TagFor(UstazLanguages.From(explicitChoice));

                    return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(tag));
                }),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        return services;
    }
}
