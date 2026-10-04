using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// Drives the verification workflow through the real HTTP pipeline: a teacher registers
/// unsubmitted, is told what is missing, submits once the profile is complete, and is
/// blocked from publishing until an admin approves.
/// </summary>
public sealed class TeacherVerificationEndpointTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public TeacherVerificationEndpointTests(UstazApiFactory factory)
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

    private async Task<string> RegisterTeacherAsync(string email)
    {
        await _factory.SeedEssentialsAsync();

        using var context = _factory.CreateContext();
        var subjectId = context.Subjects.First().Id;
        var gradeId = context.EducationalGrades.First().Id;

        var response = await _client.PostAsync("/api/v1/auth/register/teacher", Body(new
        {
            firstName = "كريم",
            lastName = "حسن",
            email,
            password = "Test@1234",
            phoneNumber = "01012345699",
            subjectIds = new[] { subjectId },
            gradeIds = new[] { gradeId }
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await ReadAsync(response)).GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    private static bool RequirementMet(JsonElement verification, string key) =>
        verification.GetProperty("requirements")
            .EnumerateArray()
            .Single(requirement => requirement.GetProperty("key").GetString() == key)
            .GetProperty("isMet")
            .GetBoolean();

    [Fact]
    public async Task A_new_teacher_starts_unsubmitted_with_an_actionable_checklist()
    {
        var token = await RegisterTeacherAsync("verify.fresh@ustaz.test");

        var response = await Authenticated(token).GetAsync("/api/v1/teacher/profile/verification");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var data = (await ReadAsync(response)).GetProperty("data");

        data.GetProperty("statusName").GetString().ShouldBe("NotSubmitted");
        data.GetProperty("isVerified").GetBoolean().ShouldBeFalse();
        data.GetProperty("isProfileComplete").GetBoolean().ShouldBeFalse();
        data.GetProperty("canSubmit").GetBoolean().ShouldBeFalse();

        // Registration supplies subjects and grades, but never a headline or bio.
        RequirementMet(data, "subjects").ShouldBeTrue();
        RequirementMet(data, "grades").ShouldBeTrue();
        RequirementMet(data, "headline").ShouldBeFalse();
        RequirementMet(data, "bio").ShouldBeFalse();
    }

    [Fact]
    public async Task Submitting_an_incomplete_profile_returns_a_coded_422()
    {
        var token = await RegisterTeacherAsync("verify.incomplete@ustaz.test");

        var response = await Authenticated(token)
            .PostAsync("/api/v1/teacher/profile/verification", Body(new { }));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);

        var problem = await ReadAsync(response);
        problem.GetProperty("code").GetString().ShouldBe("VERIFICATION_PROFILE_INCOMPLETE");
    }

    [Fact]
    public async Task Completing_the_profile_unlocks_submission()
    {
        var token = await RegisterTeacherAsync("verify.complete@ustaz.test");
        var client = Authenticated(token);

        var update = await client.PutAsync("/api/v1/teacher/profile", Body(new
        {
            headline = "مدرّس رياضيات للثانوية العامة",
            bio = new string('م', 120),
            whatsAppNumber = "01012345699"
        }));
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        var ready = (await ReadAsync(
            await client.GetAsync("/api/v1/teacher/profile/verification"))).GetProperty("data");
        ready.GetProperty("isProfileComplete").GetBoolean().ShouldBeTrue();
        ready.GetProperty("canSubmit").GetBoolean().ShouldBeTrue();

        var submit = await client.PostAsync("/api/v1/teacher/profile/verification", Body(new { }));
        submit.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = (await ReadAsync(submit)).GetProperty("data");
        after.GetProperty("statusName").GetString().ShouldBe("Pending");
        after.GetProperty("isUnderReview").GetBoolean().ShouldBeTrue();
        after.GetProperty("canSubmit").GetBoolean().ShouldBeFalse();
        after.GetProperty("submittedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Submitting_twice_is_refused_while_in_review()
    {
        var token = await RegisterTeacherAsync("verify.twice@ustaz.test");
        var client = Authenticated(token);

        await client.PutAsync("/api/v1/teacher/profile", Body(new
        {
            headline = "مدرّس فيزياء",
            bio = new string('م', 120),
            whatsAppNumber = "01012345699"
        }));

        (await client.PostAsync("/api/v1/teacher/profile/verification", Body(new { })))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await client.PostAsync("/api/v1/teacher/profile/verification", Body(new { }));
        again.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadAsync(again)).GetProperty("code").GetString()
            .ShouldBe("VERIFICATION_ALREADY_SUBMITTED");
    }

    [Fact]
    public async Task Verification_endpoints_reject_anonymous_and_non_teacher_callers()
    {
        var anonymous = await _client.GetAsync("/api/v1/teacher/profile/verification");
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await _factory.SeedEssentialsAsync();

        var studentResponse = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "نور",
            lastName = "عادل",
            email = "verify.student@ustaz.test",
            password = "Test@1234",
            phoneNumber = "01012345688",
            city = "القاهرة"
        }));
        studentResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var studentToken = (await ReadAsync(studentResponse))
            .GetProperty("data").GetProperty("accessToken").GetString()!;

        var forbidden = await Authenticated(studentToken)
            .GetAsync("/api/v1/teacher/profile/verification");

        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
