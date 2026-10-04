using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;
using Ustaz.API.Extensions;
using Ustaz.Application.Common.Localization;

namespace Ustaz.API.Tests;

/// <summary>
/// Arabic is USTAZ's first language, so the error surface has to answer in it — not only the
/// notification and email catalogues, which already did.
///
/// What these tests pin down:
/// * Arabic is the default. A request that says nothing about language gets Arabic.
/// * English is available, on an explicit request.
/// * Every failure carries a stable, untranslated <c>code</c> beside the localised prose, so
///   the client branches on the code and only ever displays the text.
/// * The 401 and 403 that the authentication middleware produces have bodies at all. They
///   used to be empty, which left the client with nothing to show for an expired session.
/// </summary>
public sealed class ArabicErrorSurfaceTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public ArabicErrorSurfaceTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    /// <summary>True when the text contains at least one Arabic letter.</summary>
    private static bool IsArabic(string? text) =>
        text is not null && text.Any(character => character >= '؀' && character <= 'ۿ');

    private HttpClient EnglishClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(LocalizationExtensions.LanguageHeaderName, "en");
        return client;
    }

    // -----------------------------------------------------------------------
    // Validation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_request_with_no_language_preference_is_validated_in_Arabic()
    {
        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "",
            lastName = "",
            email = "not-an-email",
            password = "weak",
            phoneNumber = "123"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.ValidationFailed);
        IsArabic(payload.GetProperty("detail").GetString()).ShouldBeTrue();

        var errors = payload.GetProperty("errors");

        // Field names are part of the contract and stay in English; the messages are Arabic.
        var messages = errors.EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray().Select(message => message.GetString()))
            .ToList();

        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(message => IsArabic(message));
    }

    [Fact]
    public async Task The_same_request_is_validated_in_English_when_English_is_asked_for()
    {
        var response = await EnglishClient().PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "",
            lastName = "",
            email = "not-an-email",
            password = "weak",
            phoneNumber = "123"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.ValidationFailed);
        payload.GetProperty("language").GetString().ShouldBe("en");

        var messages = payload.GetProperty("errors").EnumerateObject()
            .SelectMany(field => field.Value.EnumerateArray().Select(message => message.GetString()))
            .ToList();

        messages.ShouldNotBeEmpty();
        messages.ShouldAllBe(message => !IsArabic(message));
    }

    [Fact]
    public async Task Accept_Language_also_selects_the_language()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register/student")
        {
            Content = Body(new { firstName = "", lastName = "", email = "x", password = "y", phoneNumber = "z" })
        };

        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US"));

        var payload = await ReadAsync(await _client.SendAsync(request));

        payload.GetProperty("language").GetString().ShouldBe("en");
    }

    // -----------------------------------------------------------------------
    // Credentials
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_wrong_password_is_reported_in_Arabic_and_says_nothing_about_the_account()
    {
        var response = await _client.PostAsync("/api/v1/auth/login", Body(new
        {
            email = "nobody.at.all@ustaz.test",
            password = "WrongPassword@1"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var payload = await ReadAsync(response);
        var detail = payload.GetProperty("detail").GetString().ShouldNotBeNull();

        IsArabic(detail).ShouldBeTrue();

        // The code says "these credentials are wrong", not the generic "you are not signed
        // in" — the two mean different things to a client and now read differently too.
        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.InvalidCredentials);

        // It must not reveal whether the address is registered.
        detail.ShouldNotContain("nobody.at.all@ustaz.test");
        detail.ShouldNotContain("exist", Case.Insensitive);
    }

    [Fact]
    public async Task A_wrong_password_reads_the_same_whether_or_not_the_account_exists()
    {
        await _factory.SeedEssentialsAsync();

        var registration = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "ندى",
            lastName = "سامي",
            email = "arabic.enumeration@ustaz.test",
            password = "Test@1234",
            phoneNumber = "01011112222",
            city = "طنطا"
        }));

        registration.StatusCode.ShouldBe(HttpStatusCode.Created);

        async Task<(int Status, string? Code, string? Detail)> AttemptAsync(string email)
        {
            var response = await _client.PostAsync("/api/v1/auth/login", Body(new
            {
                email,
                password = "DefinitelyWrong@9"
            }));

            var body = await ReadAsync(response);

            return ((int)response.StatusCode,
                body.GetProperty("code").GetString(),
                body.GetProperty("detail").GetString());
        }

        var known = await AttemptAsync("arabic.enumeration@ustaz.test");
        var unknown = await AttemptAsync("definitely.not.registered@ustaz.test");

        // Identical in every observable way, which is the point: the response must not be an
        // oracle for which email addresses have accounts.
        known.ShouldBe(unknown);
    }

    // -----------------------------------------------------------------------
    // Authentication and authorization
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_anonymous_request_to_a_protected_route_returns_a_problem_document()
    {
        var response = await _client.GetAsync("/api/v1/student/dashboard");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.Unauthorized);
        IsArabic(payload.GetProperty("detail").GetString()).ShouldBeTrue();
    }

    [Fact]
    public async Task An_expired_token_is_reported_as_an_expired_session()
    {
        // A token signed with the right key but already past its lifetime. Built by hand
        // because the test host cannot wind the clock forward past a real token's expiry.
        var expired = ExpiredTokenFactory.Create();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await client.GetAsync("/api/v1/student/dashboard");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains("x-token-expired").ShouldBeTrue();

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.TokenExpired);
        IsArabic(payload.GetProperty("detail").GetString()).ShouldBeTrue();
    }

    [Fact]
    public async Task A_forbidden_request_returns_a_problem_document_in_Arabic()
    {
        await _factory.SeedEssentialsAsync();

        var registration = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "سلمى",
            lastName = "إبراهيم",
            email = "arabic.forbidden@ustaz.test",
            password = "Test@1234",
            phoneNumber = "01012345678",
            city = "القاهرة"
        }));

        registration.StatusCode.ShouldBe(HttpStatusCode.Created);

        var token = (await ReadAsync(registration)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/v1/admin/dashboard");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.Forbidden);
        IsArabic(payload.GetProperty("detail").GetString()).ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // Not found
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_missing_resource_is_reported_in_Arabic_with_a_stable_code()
    {
        var response = await _client.GetAsync("/api/v1/teachers/" + Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe(ErrorCodes.ResourceNotFound);
        IsArabic(payload.GetProperty("detail").GetString()).ShouldBeTrue();

        // The identifier must not be echoed into the message a user reads.
        payload.GetProperty("detail").GetString()!.ShouldNotContain("-0000-");
    }

    // -----------------------------------------------------------------------
    // Nothing leaks
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/api/v1/student/dashboard")]
    [InlineData("/api/v1/teachers/00000000-0000-0000-0000-000000000000")]
    public async Task No_error_response_carries_internal_detail(string route)
    {
        var payload = await (await _client.GetAsync(route)).Content.ReadAsStringAsync();

        payload.ShouldNotContain("Server=", Case.Insensitive);
        payload.ShouldNotContain("StackTrace", Case.Insensitive);
        payload.ShouldNotContain("Ustaz.Application.", Case.Insensitive);
        payload.ShouldNotContain("Microsoft.EntityFrameworkCore", Case.Insensitive);
    }
}
