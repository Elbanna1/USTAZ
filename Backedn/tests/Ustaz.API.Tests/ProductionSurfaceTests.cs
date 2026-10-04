using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// The surface an operator and a browser meet: health probes, the notification inbox, public
/// certificate verification, and the authorization on the new private-file routes.
/// </summary>
public sealed class ProductionSurfaceTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public ProductionSurfaceTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private async Task<HttpClient> StudentClientAsync(string email)
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "سلمى",
            lastName = "إبراهيم",
            email,
            password = "Test@1234",
            phoneNumber = "01012345678",
            city = "القاهرة"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var token = (await ReadAsync(response)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // -----------------------------------------------------------------------
    // Health
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_probes_are_anonymous_and_answer(string url)
    {
        var response = await _client.GetAsync(url);

        ((int)response.StatusCode).ShouldBeOneOf(200, 503);
    }

    [Fact]
    public async Task Health_output_names_the_checks_but_leaks_no_internal_detail()
    {
        var response = await _client.GetAsync("/health");
        var payload = await response.Content.ReadAsStringAsync();

        payload.ShouldContain("status");

        // No exception text, no connection strings, no durations to fingerprint.
        payload.ShouldNotContain("Exception", Case.Insensitive);
        payload.ShouldNotContain("Server=", Case.Insensitive);
        payload.ShouldNotContain("duration", Case.Insensitive);
    }

    [Fact]
    public async Task Liveness_does_not_depend_on_the_database()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var payload = await ReadAsync(response);
        payload.GetProperty("checks").GetArrayLength().ShouldBe(1);
        payload.GetProperty("checks")[0].GetProperty("name").GetString().ShouldBe("api");
    }

    // -----------------------------------------------------------------------
    // Observability
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Every_response_carries_a_correlation_id()
    {
        var response = await _client.GetAsync("/api/v1/subjects");

        response.Headers.TryGetValues("X-Correlation-ID", out var values).ShouldBeTrue();
        values!.First().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task A_supplied_correlation_id_is_echoed_back()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/subjects");
        request.Headers.Add("X-Correlation-ID", "trace-me-123");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-ID").First().ShouldBe("trace-me-123");
    }

    // -----------------------------------------------------------------------
    // Notifications
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_notification_inbox_is_empty_and_well_shaped_for_a_new_account()
    {
        var client = await StudentClientAsync("inbox.api@ustaz.test");

        var response = await client.GetAsync("/api/v1/notifications?page=1&pageSize=10");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var data = (await ReadAsync(response)).GetProperty("data");
        data.GetProperty("items").GetArrayLength().ShouldBe(0);
        data.GetProperty("pageSize").GetInt32().ShouldBe(10);

        var unread = await client.GetAsync("/api/v1/notifications/unread-count");
        (await ReadAsync(unread)).GetProperty("data").GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Notification_preferences_list_every_category()
    {
        var client = await StudentClientAsync("prefs.api@ustaz.test");

        var response = await client.GetAsync("/api/v1/notifications/preferences");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var categories = (await ReadAsync(response)).GetProperty("data")
            .EnumerateArray()
            .Select(entry => entry.GetProperty("category").GetString())
            .ToList();

        // The new categories are surfaced too, so a client can render the whole switchboard.
        categories.ShouldContain("Security");
        categories.ShouldContain("Course");
        categories.ShouldContain("Certificate");
        categories.ShouldContain("Enrollment");
    }

    [Fact]
    public async Task Clearing_read_notifications_is_authenticated()
    {
        var response = await _client.DeleteAsync("/api/v1/notifications/read");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // Certificates
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Certificate_verification_is_public_and_answers_for_an_unknown_code()
    {
        var response = await _client.GetAsync("/api/v1/public/certificates/verify/not-a-real-code");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(response)).GetProperty("data").GetProperty("isValid").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_students_certificate_list_needs_a_student()
    {
        var response = await _client.GetAsync("/api/v1/student/certificates");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_workspace_certificate_list_is_closed_to_a_plain_student()
    {
        var client = await StudentClientAsync("cert.student@ustaz.test");

        var response = await client.GetAsync("/api/v1/teacher/certificates");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Private files
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Downloading_an_attachment_anonymously_is_unauthorized()
    {
        var response = await _client.GetAsync(
            "/api/v1/messages/attachments/00000000-0000-0000-0000-000000000001");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_attachment_the_caller_has_nothing_to_do_with_is_a_404()
    {
        var client = await StudentClientAsync("files.api@ustaz.test");

        var response = await client.GetAsync(
            "/api/v1/messages/attachments/00000000-0000-0000-0000-000000000002");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Posting_a_file_into_someone_elses_conversation_is_a_404()
    {
        var client = await StudentClientAsync("files.post@ustaz.test");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("%PDF-1.7 test"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "notes.pdf");

        var response = await client.PostAsync(
            "/api/v1/messages/conversations/00000000-0000-0000-0000-000000000003/attachments", content);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -----------------------------------------------------------------------
    // At-risk analytics
    // -----------------------------------------------------------------------

    [Fact]
    public async Task At_risk_analytics_are_closed_to_a_student()
    {
        var client = await StudentClientAsync("risk.api@ustaz.test");

        var response = await client.GetAsync("/api/v1/teacher/analytics/at-risk");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task At_risk_analytics_reject_anonymous_callers()
    {
        var response = await _client.GetAsync("/api/v1/teacher/analytics/at-risk");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
