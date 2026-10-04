using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// Exercises the HTTP surface: response envelopes, ProblemDetails, versioned routes,
/// authentication and role based authorization.
/// </summary>
public sealed class ApiContractTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public ApiContractTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Health_endpoint_is_anonymous()
    {
        var response = await _client.GetAsync("/health");

        // Healthy or Degraded both prove the endpoint is reachable without a token.
        ((int)response.StatusCode).ShouldBeOneOf(200, 503);
    }

    [Fact]
    public async Task Unversioned_routes_are_not_served()
    {
        var response = await _client.GetAsync("/api/subjects");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Public_reference_data_is_anonymous_and_uses_the_success_envelope()
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.GetAsync("/api/v1/subjects");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await ReadAsync(response);
        payload.GetProperty("success").GetBoolean().ShouldBeTrue();
        payload.GetProperty("message").GetString().ShouldNotBeNullOrWhiteSpace();

        var subjects = payload.GetProperty("data");
        subjects.GetArrayLength().ShouldBeGreaterThan(0);

        // Named rather than counted. The subject catalogue covers the Egyptian curriculum and
        // grows whenever a stage or an elective is added, so asserting its exact size only
        // ever tests whether somebody remembered to edit this line. What the endpoint has to
        // guarantee is that the seeded reference data is actually there and addressable by
        // the slug the client filters on.
        var slugs = subjects.EnumerateArray()
            .Select(subject => subject.GetProperty("slug").GetString())
            .ToArray();

        foreach (var expected in new[] { "mathematics", "physics", "chemistry", "biology", "arabic", "english" })
        {
            slugs.ShouldContain(expected);
        }
    }

    [Fact]
    public async Task Paged_endpoints_return_the_full_pagination_envelope()
    {
        var response = await _client.GetAsync("/api/v1/teachers?page=1&pageSize=5");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var data = (await ReadAsync(response)).GetProperty("data");

        foreach (var field in new[] { "items", "pageNumber", "pageSize", "totalCount", "totalPages", "hasPrevious", "hasNext" })
        {
            data.TryGetProperty(field, out _).ShouldBeTrue("missing pagination field: " + field);
        }

        data.GetProperty("pageSize").GetInt32().ShouldBe(5);
    }

    [Fact]
    public async Task Page_size_is_clamped_to_the_maximum()
    {
        var response = await _client.GetAsync("/api/v1/teachers?pageSize=5000");
        var data = (await ReadAsync(response)).GetProperty("data");

        data.GetProperty("pageSize").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task Protected_endpoints_reject_anonymous_callers()
    {
        foreach (var path in new[]
                 {
                     "/api/v1/auth/me",
                     "/api/v1/teacher/profile",
                     "/api/v1/teacher/dashboard",
                     "/api/v1/student/dashboard",
                     "/api/v1/admin/dashboard",
                     "/api/v1/notifications"
                 })
        {
            var response = await _client.GetAsync(path);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, path + " should require authentication");
        }
    }

    [Fact]
    public async Task Validation_failures_return_problem_details_with_an_errors_dictionary()
    {
        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "A",
            lastName = "",
            email = "not-an-email",
            password = "weak"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        var problem = await ReadAsync(response);
        problem.GetProperty("title").GetString().ShouldBe("Validation failed.");
        problem.GetProperty("success").GetBoolean().ShouldBeFalse();
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();

        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("Email", out _).ShouldBeTrue();
        errors.TryGetProperty("Password", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Unknown_resources_return_a_404_problem_details()
    {
        var response = await _client.GetAsync("/api/v1/teachers/" + Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var problem = await ReadAsync(response);
        problem.GetProperty("title").GetString().ShouldBe("Resource not found.");
        problem.GetProperty("status").GetInt32().ShouldBe(404);
    }

    [Fact]
    public async Task Error_responses_never_leak_a_stack_trace()
    {
        var response = await _client.GetAsync("/api/v1/teachers/" + Guid.NewGuid());
        var raw = await response.Content.ReadAsStringAsync();

        raw.ShouldNotContain("at Ustaz.");
        raw.ShouldNotContain("StackTrace");
    }

    [Fact]
    public async Task Security_headers_are_applied()
    {
        var response = await _client.GetAsync("/api/v1/subjects");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.Contains("X-Powered-By").ShouldBeFalse();
    }

    [Fact]
    public async Task Api_version_is_advertised_on_responses()
    {
        var response = await _client.GetAsync("/api/v1/subjects");

        response.Headers.GetValues("api-supported-versions").ShouldContain("1.0");
    }
}
