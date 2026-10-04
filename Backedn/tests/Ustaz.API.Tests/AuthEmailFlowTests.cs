using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Ustaz.Domain.Enums;

namespace Ustaz.API.Tests;

/// <summary>
/// The account flows that send mail, end to end through the HTTP pipeline: registration,
/// verification, password recovery and the language switch. SMTP is switched off in the test
/// host, so what is asserted is the delivery record the platform writes — which is also what
/// support would read in production.
/// </summary>
public sealed class AuthEmailFlowTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEmailFlowTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private HttpClient Authenticated(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> RegisterAsync(string email, string password = "Test@1234")
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "سلمى",
            lastName = "إبراهيم",
            email,
            password,
            phoneNumber = "01012345678",
            city = "القاهرة"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await ReadAsync(response)).GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    /// <summary>Every delivery recorded for one address, newest last.</summary>
    private async Task<List<(string Event, string Subject, EmailDeliveryStatus Status, string? Payload)>>
        DeliveriesAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Ustaz.Persistence.Contexts.UstazDbContext>();

        return await context.Set<Ustaz.Domain.Entities.EmailDelivery>()
            .AsNoTracking()
            .Where(delivery => delivery.Recipient == email)
            .OrderBy(delivery => delivery.CreatedAt)
            .Select(delivery => new ValueTuple<string, string, EmailDeliveryStatus, string?>(
                delivery.Event, delivery.Subject, delivery.Status, delivery.PayloadJson))
            .ToListAsync();
    }

    // -----------------------------------------------------------------------
    // Registration
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Registering_sends_a_welcome_and_a_verification_email()
    {
        const string email = "welcome.flow@ustaz.test";
        await RegisterAsync(email);

        var deliveries = await DeliveriesAsync(email);

        deliveries.Select(entry => entry.Event).ShouldContain("account.welcome");
        deliveries.Select(entry => entry.Event).ShouldContain("account.email_verification");

        deliveries.First(entry => entry.Event == "account.welcome").Subject
            .ShouldBe("أهلًا بيك في Ustaz 🎉");
    }

    [Fact]
    public async Task The_verification_email_never_stores_its_token()
    {
        const string email = "verify.token@ustaz.test";
        await RegisterAsync(email);

        var verification = (await DeliveriesAsync(email))
            .First(entry => entry.Event == "account.email_verification");

        // A security email keeps no payload, so the one-time link cannot be replayed from
        // the database or leaked by a support query.
        verification.Payload.ShouldBeNull();
    }

    // -----------------------------------------------------------------------
    // Password recovery
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Forgot_password_sends_a_reset_email_to_a_real_account()
    {
        const string email = "forgot.real@ustaz.test";
        await RegisterAsync(email);

        var response = await _client.PostAsync("/api/v1/auth/forgot-password", Body(new { email }));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reset = (await DeliveriesAsync(email))
            .FirstOrDefault(entry => entry.Event == "security.password_reset_requested");

        reset.Subject.ShouldBe("تقدر تغيّر الباسورد بتاعك 🔐");
        reset.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task Forgot_password_for_an_unknown_address_succeeds_and_sends_nothing()
    {
        const string email = "nobody.here@ustaz.test";

        var response = await _client.PostAsync("/api/v1/auth/forgot-password", Body(new { email }));

        // The response must be indistinguishable from the real-account case.
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DeliveriesAsync(email)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Changing_a_password_sends_the_security_confirmation()
    {
        const string email = "changed.mail@ustaz.test";
        var token = await RegisterAsync(email);

        var response = await Authenticated(token).PostAsync("/api/v1/auth/change-password", Body(new
        {
            currentPassword = "Test@1234",
            newPassword = "Changed@5678",
            confirmPassword = "Changed@5678"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var confirmation = (await DeliveriesAsync(email))
            .FirstOrDefault(entry => entry.Event == "security.password_changed");

        confirmation.Subject.ShouldBe("الباسورد بتاعك اتغير 🔐");
    }

    // -----------------------------------------------------------------------
    // Verification resend
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Resending_verification_is_available_and_does_not_reveal_the_account()
    {
        const string email = "resend.flow@ustaz.test";
        await RegisterAsync(email);

        var known = await _client.PostAsync("/api/v1/auth/resend-verification", Body(new { email }));
        known.StatusCode.ShouldBe(HttpStatusCode.OK);

        var unknown = await _client.PostAsync(
            "/api/v1/auth/resend-verification", Body(new { email = "ghost@ustaz.test" }));

        unknown.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await DeliveriesAsync("ghost@ustaz.test")).ShouldBeEmpty();

        // The known account got a second verification link, each one freshly minted.
        (await DeliveriesAsync(email))
            .Count(entry => entry.Event == "account.email_verification")
            .ShouldBeGreaterThanOrEqualTo(2);
    }

    // -----------------------------------------------------------------------
    // Language preference
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_new_account_reads_egyptian_arabic_by_default()
    {
        var token = await RegisterAsync("language.default@ustaz.test");

        var response = await Authenticated(token).GetAsync("/api/v1/auth/me");
        var data = (await ReadAsync(response)).GetProperty("data");

        data.GetProperty("preferredLanguage").GetString().ShouldBe("ar-EG");
    }

    [Fact]
    public async Task The_language_can_be_switched_to_english_and_back()
    {
        var token = await RegisterAsync("language.switch@ustaz.test");
        var client = Authenticated(token);

        (await client.PutAsync("/api/v1/auth/me/language", Body(new { language = "en" })))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterEnglish = (await ReadAsync(await client.GetAsync("/api/v1/auth/me")))
            .GetProperty("data").GetProperty("preferredLanguage").GetString();

        afterEnglish.ShouldBe("en");

        (await client.PutAsync("/api/v1/auth/me/language", Body(new { language = "ar-EG" })))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterArabic = (await ReadAsync(await client.GetAsync("/api/v1/auth/me")))
            .GetProperty("data").GetProperty("preferredLanguage").GetString();

        afterArabic.ShouldBe("ar-EG");
    }

    [Fact]
    public async Task An_unsupported_language_is_rejected()
    {
        var token = await RegisterAsync("language.invalid@ustaz.test");

        var response = await Authenticated(token)
            .PutAsync("/api/v1/auth/me/language", Body(new { language = "fr" }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Switching_language_anonymously_is_unauthorized()
    {
        var response = await _client.PutAsync("/api/v1/auth/me/language", Body(new { language = "en" }));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // Secrets
    // -----------------------------------------------------------------------

    [Fact]
    public async Task No_endpoint_echoes_the_smtp_password()
    {
        var token = await RegisterAsync("secrets.check@ustaz.test");

        foreach (var url in new[] { "/api/v1/auth/me", "/health", "/api/v1/notifications/preferences" })
        {
            var body = await (await Authenticated(token).GetAsync(url)).Content.ReadAsStringAsync();

            body.ShouldNotContain("Password", Case.Insensitive);
            body.ShouldNotContain("smtp", Case.Insensitive);
        }
    }
}
