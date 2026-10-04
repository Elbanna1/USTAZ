using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// Walks the real registration -> sign-in -> refresh -> authorization path through the
/// full HTTP pipeline.
/// </summary>
public sealed class AuthenticationFlowTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationFlowTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private async Task<(string Access, string Refresh, JsonElement User)> RegisterStudentAsync(string email)
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

        var data = (await ReadAsync(response)).GetProperty("data");

        return (
            data.GetProperty("accessToken").GetString()!,
            data.GetProperty("refreshToken").GetString()!,
            data.GetProperty("user"));
    }

    private HttpClient Authenticated(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Registering_a_student_issues_a_usable_token_pair()
    {
        var (access, refresh, user) = await RegisterStudentAsync("flow.student@ustaz.test");

        access.ShouldNotBeNullOrWhiteSpace();
        refresh.ShouldNotBeNullOrWhiteSpace();

        user.GetProperty("displayName").GetString().ShouldBe("سلمى إبراهيم");
        user.GetProperty("studentId").GetString().ShouldNotBeNullOrWhiteSpace();

        // Null fields are omitted from the payload, so a student carries no teacherId at all.
        user.TryGetProperty("teacherId", out _).ShouldBeFalse();

        var roles = user.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToList();
        roles.ShouldBe(["Student"]);
    }

    [Fact]
    public async Task The_issued_token_authenticates_the_me_endpoint()
    {
        var (access, _, _) = await RegisterStudentAsync("flow.me@ustaz.test");

        var response = await Authenticated(access).GetAsync("/api/v1/auth/me");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var data = (await ReadAsync(response)).GetProperty("data");
        data.GetProperty("email").GetString().ShouldBe("flow.me@ustaz.test");
    }

    [Fact]
    public async Task Registering_the_same_email_twice_conflicts()
    {
        await RegisterStudentAsync("flow.duplicate@ustaz.test");

        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "Another",
            lastName = "Person",
            email = "flow.duplicate@ustaz.test",
            password = "Test@1234"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadAsync(response)).GetProperty("title").GetString().ShouldBe("Conflict.");
    }

    [Fact]
    public async Task Signing_in_with_the_wrong_password_is_unauthorized()
    {
        await RegisterStudentAsync("flow.badpass@ustaz.test");

        var response = await _client.PostAsync("/api/v1/auth/login", Body(new
        {
            email = "flow.badpass@ustaz.test",
            password = "Completely@Wrong1"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var payload = await ReadAsync(response);

        // The code is the contract and does not distinguish a wrong password from an unknown
        // account. The prose is localised — Arabic by default — so it is not asserted here;
        // the English wording is pinned in the test below, and the two cases are compared
        // against each other in ArabicErrorSurfaceTests.
        payload.GetProperty("code").GetString().ShouldBe("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Signing_in_with_an_unknown_email_gives_the_same_message()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Ustaz-Language", "en");

        var response = await client.PostAsync("/api/v1/auth/login", Body(new
        {
            email = "nobody.here@ustaz.test",
            password = "Test@1234"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var payload = await ReadAsync(response);

        payload.GetProperty("code").GetString().ShouldBe("INVALID_CREDENTIALS");
        payload.GetProperty("detail").GetString().ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Refresh_tokens_rotate_and_reuse_is_detected()
    {
        var (_, refresh, _) = await RegisterStudentAsync("flow.rotation@ustaz.test");

        var first = await _client.PostAsync("/api/v1/auth/refresh-token", Body(new { refreshToken = refresh }));
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        var rotated = (await ReadAsync(first)).GetProperty("data").GetProperty("refreshToken").GetString()!;
        rotated.ShouldNotBe(refresh);

        // Replaying the consumed token must fail and burn the whole family.
        var replay = await _client.PostAsync("/api/v1/auth/refresh-token", Body(new { refreshToken = refresh }));
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var afterBreach = await _client.PostAsync("/api/v1/auth/refresh-token", Body(new { refreshToken = rotated }));
        afterBreach.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (_, refresh, _) = await RegisterStudentAsync("flow.logout@ustaz.test");

        var logout = await _client.PostAsync("/api/v1/auth/logout", Body(new { refreshToken = refresh }));
        logout.StatusCode.ShouldBe(HttpStatusCode.OK);

        var afterLogout = await _client.PostAsync("/api/v1/auth/refresh-token", Body(new { refreshToken = refresh }));
        afterLogout.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_student_token_is_refused_by_teacher_and_admin_routes()
    {
        var (access, _, _) = await RegisterStudentAsync("flow.roles@ustaz.test");
        var client = Authenticated(access);

        (await client.GetAsync("/api/v1/teacher/profile")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/teacher/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/admin/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/admin/teachers")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_student_token_is_accepted_by_student_routes()
    {
        var (access, _, _) = await RegisterStudentAsync("flow.studentroutes@ustaz.test");
        var client = Authenticated(access);

        (await client.GetAsync("/api/v1/student/dashboard")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/student/favorites/teachers")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/notifications")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var (access, _, _) = await RegisterStudentAsync("flow.tampered@ustaz.test");

        var tampered = access[..^4] + "AAAA";
        var response = await Authenticated(tampered).GetAsync("/api/v1/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registering_a_teacher_requires_subjects_and_grades()
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/teacher", Body(new
        {
            firstName = "أحمد",
            lastName = "محمد",
            email = "flow.teacher.invalid@ustaz.test",
            password = "Test@1234",
            phoneNumber = "01012345679",
            subjectIds = Array.Empty<Guid>(),
            gradeIds = Array.Empty<Guid>()
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var errors = (await ReadAsync(response)).GetProperty("errors");
        errors.TryGetProperty("SubjectIds", out _).ShouldBeTrue();
        errors.TryGetProperty("GradeIds", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task A_newly_registered_teacher_is_unverified_and_cannot_publish()
    {
        await _factory.SeedEssentialsAsync();

        using var context = _factory.CreateContext();
        var subjectId = context.Subjects.First().Id;
        var gradeId = context.EducationalGrades.First().Id;

        var response = await _client.PostAsync("/api/v1/auth/register/teacher", Body(new
        {
            firstName = "عمر",
            lastName = "خالد",
            email = "flow.teacher@ustaz.test",
            password = "Test@1234",
            phoneNumber = "01012345680",
            headline = "مدرس فيزياء",
            yearsOfExperience = 5,
            subjectIds = new[] { subjectId },
            gradeIds = new[] { gradeId }
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var data = (await ReadAsync(response)).GetProperty("data");
        data.GetProperty("user").GetProperty("isVerified").GetBoolean().ShouldBeFalse();
        data.GetProperty("user").GetProperty("teacherId").GetString().ShouldNotBeNullOrWhiteSpace();

        var client = Authenticated(data.GetProperty("accessToken").GetString()!);

        var create = await client.PostAsync("/api/v1/teacher/groups", Body(new
        {
            subjectId,
            gradeId,
            name = "مجموعة غير موثقة",
            description = "اختبار",
            monthlyPrice = 300,
            capacity = 10,
            startDate = DateTime.UtcNow.AddDays(7),
            teachingMode = "Offline",
            location = "القاهرة",
            schedules = new[]
            {
                new { dayOfWeek = "Monday", startTime = "16:00:00", endTime = "18:00:00" }
            }
        }));

        create.StatusCode.ShouldBe(HttpStatusCode.Created);
        var groupId = (await ReadAsync(create)).GetProperty("data").GetProperty("id").GetString();

        // Drafting is allowed, publishing is not, until an admin verifies the teacher.
        var publish = await client.PostAsync("/api/v1/teacher/groups/" + groupId + "/publish", content: null);
        publish.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var problem = await ReadAsync(publish);
        problem.GetProperty("code").GetString().ShouldBe("TEACHER_NOT_VERIFIED");
    }
}
