using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// Exercises the account endpoints a signed-in user relies on: changing their own password
/// and maintaining their own profile, through the real HTTP pipeline.
/// </summary>
public sealed class PasswordAndProfileTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public PasswordAndProfileTests(UstazApiFactory factory)
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

    private async Task<string> RegisterStudentAsync(string email, string password = "Test@1234")
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

    // -----------------------------------------------------------------------
    // Change password
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Changing_the_password_lets_the_new_one_sign_in_and_retires_the_old_one()
    {
        const string email = "change.password@ustaz.test";
        var token = await RegisterStudentAsync(email);

        var response = await Authenticated(token).PostAsync("/api/v1/auth/change-password", Body(new
        {
            currentPassword = "Test@1234",
            newPassword = "Changed@5678",
            confirmPassword = "Changed@5678"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var withNew = await _client.PostAsync("/api/v1/auth/login", Body(new
        {
            email,
            password = "Changed@5678"
        }));

        withNew.StatusCode.ShouldBe(HttpStatusCode.OK);

        var withOld = await _client.PostAsync("/api/v1/auth/login", Body(new
        {
            email,
            password = "Test@1234"
        }));

        withOld.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_the_password_with_the_wrong_current_one_is_rejected()
    {
        var token = await RegisterStudentAsync("wrong.current@ustaz.test");

        var response = await Authenticated(token).PostAsync("/api/v1/auth/change-password", Body(new
        {
            currentPassword = "NotMyPassword@1",
            newPassword = "Changed@5678",
            confirmPassword = "Changed@5678"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_mismatched_confirmation_is_rejected_before_anything_is_written()
    {
        var token = await RegisterStudentAsync("mismatch.confirm@ustaz.test");

        var response = await Authenticated(token).PostAsync("/api/v1/auth/change-password", Body(new
        {
            currentPassword = "Test@1234",
            newPassword = "Changed@5678",
            confirmPassword = "Different@5678"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Changing_a_password_anonymously_is_unauthorized()
    {
        var response = await _client.PostAsync("/api/v1/auth/change-password", Body(new
        {
            currentPassword = "Test@1234",
            newPassword = "Changed@5678",
            confirmPassword = "Changed@5678"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // Student profile
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_student_can_read_and_update_their_own_profile()
    {
        var token = await RegisterStudentAsync("profile.student@ustaz.test");
        var client = Authenticated(token);

        var read = await client.GetAsync("/api/v1/student/profile");
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadAsync(read)).GetProperty("data").GetProperty("email").GetString()
            .ShouldBe("profile.student@ustaz.test");

        var update = await client.PutAsync("/api/v1/student/profile", Body(new
        {
            schoolName = "مدرسة النيل",
            city = "الجيزة"
        }));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var updated = (await ReadAsync(update)).GetProperty("data");
        updated.GetProperty("schoolName").GetString().ShouldBe("مدرسة النيل");
        updated.GetProperty("city").GetString().ShouldBe("الجيزة");
    }

    [Fact]
    public async Task The_student_profile_is_closed_to_anonymous_callers()
    {
        var response = await _client.GetAsync("/api/v1/student/profile");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_invalid_phone_number_is_rejected_by_validation()
    {
        var token = await RegisterStudentAsync("profile.validation@ustaz.test");

        var response = await Authenticated(token).PutAsync("/api/v1/student/profile", Body(new
        {
            phoneNumber = "12345"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------------
    // Messages badge
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_unread_message_count_starts_at_zero_for_a_new_account()
    {
        var token = await RegisterStudentAsync("unread.count@ustaz.test");

        var response = await Authenticated(token).GetAsync("/api/v1/messages/unread-count");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var data = (await ReadAsync(response)).GetProperty("data");
        data.GetProperty("totalUnread").GetInt32().ShouldBe(0);
        data.GetProperty("conversationsWithUnread").GetInt32().ShouldBe(0);
    }
}
