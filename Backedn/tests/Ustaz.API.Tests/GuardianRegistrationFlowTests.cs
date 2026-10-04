using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// Walks guardian sign-up and the child link it requests through the full HTTP pipeline.
///
/// The handler tests mock <c>UserManager</c>, so nothing there proves the Parent role is
/// actually in the role store, that the issued token carries <c>parentId</c>, or that the
/// ParentOnly policy admits it. Those only hold against the real Identity stack, which is
/// what this fixture boots.
///
/// The link half is the security-sensitive one: naming a child must not grant access, and the
/// student must be able to answer the request with the id they were notified with.
/// </summary>
public sealed class GuardianRegistrationFlowTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public GuardianRegistrationFlowTests(UstazApiFactory factory)
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

    /// <summary>Registers a child account, optionally with a parent number on file.</summary>
    private async Task<string> RegisterStudentAsync(string email, string? parentPhoneNumber = null)
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "سلمى",
            lastName = "إبراهيم",
            email,
            password = "Test@1234",
            parentPhoneNumber
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var data = (await ReadAsync(response)).GetProperty("data");
        return data.GetProperty("accessToken").GetString()!;
    }

    private async Task<HttpResponseMessage> RegisterGuardianAsync(
        string email,
        string? childEmail = null,
        string phoneNumber = "01020304050")
    {
        await _factory.SeedEssentialsAsync();

        return await _client.PostAsync("/api/v1/auth/register/parent", Body(new
        {
            firstName = "أحمد",
            lastName = "عبد الله",
            email,
            password = "Test@1234",
            phoneNumber,
            childEmail,
            relationship = "Father"
        }));
    }

    private static async Task<JsonElement> DataOf(HttpResponseMessage response) =>
        (await ReadAsync(response)).GetProperty("data");

    [Fact]
    public async Task Registering_a_guardian_issues_a_token_that_carries_the_parent_role()
    {
        var response = await RegisterGuardianAsync("flow.guardian@ustaz.test");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var data = await DataOf(response);
        var user = data.GetProperty("user");

        data.GetProperty("accessToken").GetString().ShouldNotBeNullOrWhiteSpace();
        data.GetProperty("refreshToken").GetString().ShouldNotBeNullOrWhiteSpace();

        user.GetProperty("displayName").GetString().ShouldBe("أحمد عبد الله");

        // Proves the real AddToRoleAsync found the Parent role in the store, and that the
        // profile existed before the token was issued.
        user.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ShouldBe(["Parent"]);
        user.GetProperty("parentId").GetString().ShouldNotBeNullOrWhiteSpace();

        // Null ids are omitted from the payload, so a guardian carries neither of the others.
        user.TryGetProperty("studentId", out _).ShouldBeFalse();
        user.TryGetProperty("teacherId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task A_guardian_token_opens_the_parent_api_and_nothing_else()
    {
        var response = await RegisterGuardianAsync("flow.guardian.policy@ustaz.test");
        var client = Authenticated((await DataOf(response)).GetProperty("accessToken").GetString()!);

        (await client.GetAsync("/api/v1/parent/profile")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/parent/children")).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await client.GetAsync("/api/v1/student/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/teacher/profile")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAsync("/api/v1/admin/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Naming_a_child_requests_access_but_reaches_none_of_their_data()
    {
        await RegisterStudentAsync("flow.child.pending@ustaz.test");

        var response = await RegisterGuardianAsync(
            "flow.guardian.pending@ustaz.test", childEmail: "flow.child.pending@ustaz.test");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var client = Authenticated((await DataOf(response)).GetProperty("accessToken").GetString()!);

        var children = (await DataOf(await client.GetAsync("/api/v1/parent/children"))).EnumerateArray().ToList();

        var child = children.ShouldHaveSingleItem();
        child.GetProperty("isConfirmed").GetBoolean().ShouldBeFalse();
        child.GetProperty("relationship").GetString().ShouldBe("Father");

        // The row is visible, the child's record is not: the guard requires a confirmed link,
        // and answers 404 so the endpoint never confirms an unrelated student exists.
        var studentId = child.GetProperty("studentId").GetString();
        var guarded = await client.GetAsync("/api/v1/parent/children/" + studentId);
        guarded.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_student_approves_the_request_with_the_link_id_and_access_opens()
    {
        var studentToken = await RegisterStudentAsync("flow.child.approve@ustaz.test");

        var guardian = await RegisterGuardianAsync(
            "flow.guardian.approve@ustaz.test", childEmail: "flow.child.approve@ustaz.test");

        var parentClient = Authenticated((await DataOf(guardian)).GetProperty("accessToken").GetString()!);
        var studentClient = Authenticated(studentToken);

        // The student discovers the pending request, and with it the id to answer.
        var guardians = (await DataOf(await studentClient.GetAsync("/api/v1/student/guardians")))
            .EnumerateArray()
            .ToList();

        var pending = guardians.ShouldHaveSingleItem();
        pending.GetProperty("isConfirmed").GetBoolean().ShouldBeFalse();
        pending.GetProperty("email").GetString().ShouldBe("flow.guardian.approve@ustaz.test");
        pending.GetProperty("phoneNumber").GetString().ShouldBe("01020304050");

        var linkId = pending.GetProperty("linkId").GetString();

        var respond = await studentClient.PostAsync(
            "/api/v1/student/guardians/" + linkId + "/respond?approve=true", content: null);
        respond.StatusCode.ShouldBe(HttpStatusCode.OK);

        var children = (await DataOf(await parentClient.GetAsync("/api/v1/parent/children")))
            .EnumerateArray()
            .ToList();

        var child = children.ShouldHaveSingleItem();
        child.GetProperty("isConfirmed").GetBoolean().ShouldBeTrue();

        // The same route that answered 404 while pending now serves the child.
        var studentId = child.GetProperty("studentId").GetString();
        (await parentClient.GetAsync("/api/v1/parent/children/" + studentId))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_student_rejecting_the_request_drops_the_link_for_both_sides()
    {
        var studentToken = await RegisterStudentAsync("flow.child.reject@ustaz.test");

        var guardian = await RegisterGuardianAsync(
            "flow.guardian.reject@ustaz.test", childEmail: "flow.child.reject@ustaz.test");

        var parentClient = Authenticated((await DataOf(guardian)).GetProperty("accessToken").GetString()!);
        var studentClient = Authenticated(studentToken);

        var linkId = (await DataOf(await studentClient.GetAsync("/api/v1/student/guardians")))
            .EnumerateArray()
            .Single()
            .GetProperty("linkId")
            .GetString();

        var respond = await studentClient.PostAsync(
            "/api/v1/student/guardians/" + linkId + "/respond?approve=false", content: null);
        respond.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await DataOf(await studentClient.GetAsync("/api/v1/student/guardians")))
            .EnumerateArray().ShouldBeEmpty();

        (await DataOf(await parentClient.GetAsync("/api/v1/parent/children")))
            .EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_phone_number_matching_the_one_on_the_child_profile_confirms_at_sign_up()
    {
        await RegisterStudentAsync("flow.child.verified@ustaz.test", parentPhoneNumber: "01122334455");

        var guardian = await RegisterGuardianAsync(
            "flow.guardian.verified@ustaz.test",
            childEmail: "flow.child.verified@ustaz.test",
            phoneNumber: "01122334455");

        var client = Authenticated((await DataOf(guardian)).GetProperty("accessToken").GetString()!);

        var children = (await DataOf(await client.GetAsync("/api/v1/parent/children")))
            .EnumerateArray()
            .ToList();

        var child = children.ShouldHaveSingleItem();
        child.GetProperty("isConfirmed").GetBoolean().ShouldBeTrue();

        // Proving the relationship at sign-up means no approval round trip is needed.
        var studentId = child.GetProperty("studentId").GetString();
        (await client.GetAsync("/api/v1/parent/children/" + studentId))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Naming_an_unknown_child_still_creates_the_account()
    {
        // Failing the sign-up here would turn the field into an oracle for student emails.
        var response = await RegisterGuardianAsync(
            "flow.guardian.unknown@ustaz.test", childEmail: "nobody.at.all@ustaz.test");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var client = Authenticated((await DataOf(response)).GetProperty("accessToken").GetString()!);

        (await DataOf(await client.GetAsync("/api/v1/parent/children")))
            .EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task A_guardian_sign_up_reusing_an_email_conflicts()
    {
        await RegisterGuardianAsync("flow.guardian.duplicate@ustaz.test");

        var again = await RegisterGuardianAsync("flow.guardian.duplicate@ustaz.test");

        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ReadAsync(again)).GetProperty("title").GetString().ShouldBe("Conflict.");
    }

    [Fact]
    public async Task A_guardian_sign_up_without_a_valid_phone_number_is_rejected()
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/parent", Body(new
        {
            firstName = "أحمد",
            lastName = "عبد الله",
            email = "flow.guardian.badphone@ustaz.test",
            password = "Test@1234",
            phoneNumber = "12345",
            childEmail = "not-an-email"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var errors = (await ReadAsync(response)).GetProperty("errors");
        errors.TryGetProperty("PhoneNumber", out _).ShouldBeTrue();
        errors.TryGetProperty("ChildEmail", out _).ShouldBeTrue();
    }
}
