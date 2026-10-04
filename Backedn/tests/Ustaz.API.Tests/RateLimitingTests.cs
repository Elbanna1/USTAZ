using System.Net;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// The credential endpoints carry their own, much tighter rate-limit budget than the rest of
/// the API. These tests exist because that budget was once silently replaced by the global
/// one: <c>MapControllers().RequireRateLimiting(global)</c> appends its metadata after the
/// controller's own <c>[EnableRateLimiting]</c>, and the rate-limiting middleware reads the
/// last entry — so the attribute lost and login was throttled at 300/minute instead of 10.
/// </summary>
public sealed class RateLimitingTests : IClassFixture<RateLimitingTests.ThrottledApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A host that keeps a budget small enough to reach inside a test: five credential
    /// requests per minute against a generous global ceiling, so a 429 in these tests can
    /// only have come from the auth policy and never from the global limiter.
    /// </summary>
    public sealed class ThrottledApiFactory : UstazApiFactory
    {
        public const int AuthPermitLimit = 5;

        protected override IReadOnlyDictionary<string, string> RateLimitSettings =>
            new Dictionary<string, string>
            {
                ["RateLimiting:GlobalPermitLimit"] = "100000",
                ["RateLimiting:AuthPermitLimit"] = AuthPermitLimit.ToString(),
                ["RateLimiting:UploadPermitLimit"] = "100000"
            };
    }

    private readonly ThrottledApiFactory _factory;

    public RateLimitingTests(ThrottledApiFactory factory) => _factory = factory;

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Repeated_login_attempts_are_throttled_by_the_auth_budget()
    {
        var client = _factory.CreateClient();

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < ThrottledApiFactory.AuthPermitLimit + 4; attempt++)
        {
            var response = await client.PostAsync("/api/v1/auth/login", Body(new
            {
                email = "rate.limit.probe@ustaz.test",
                password = "WrongPassword@1"
            }));

            statuses.Add(response.StatusCode);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        statuses.ShouldContain(HttpStatusCode.TooManyRequests);
        statuses.Count(status => status != HttpStatusCode.TooManyRequests)
            .ShouldBeLessThanOrEqualTo(ThrottledApiFactory.AuthPermitLimit);
    }

    [Fact]
    public async Task A_throttled_response_is_a_problem_document_and_names_no_internals()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage? throttled = null;

        for (var attempt = 0; attempt < ThrottledApiFactory.AuthPermitLimit + 4 && throttled is null; attempt++)
        {
            var response = await client.PostAsync("/api/v1/auth/forgot-password", Body(new
            {
                email = "throttle.shape@ustaz.test"
            }));

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throttled = response;
            }
        }

        throttled.ShouldNotBeNull();
        throttled!.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        var payload = await throttled.Content.ReadAsStringAsync();
        payload.ShouldNotContain("Exception", Case.Insensitive);
        payload.ShouldNotContain("Server=", Case.Insensitive);
    }

    [Fact]
    public async Task Ordinary_browsing_is_not_throttled_at_the_auth_budget()
    {
        var client = _factory.CreateClient();

        // Reads of a public catalogue endpoint, well past the auth budget: the tight policy
        // must not leak onto endpoints that never asked for it.
        for (var attempt = 0; attempt < ThrottledApiFactory.AuthPermitLimit * 4; attempt++)
        {
            var response = await client.GetAsync("/api/v1/subjects");

            response.StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }
    }
}
