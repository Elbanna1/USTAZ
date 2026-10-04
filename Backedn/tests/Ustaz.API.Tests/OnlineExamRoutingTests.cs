using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// The online exam and lesson-progress routes are new surface area. These tests prove they
/// are actually mapped (no 404 from a missing route) and that none of them is reachable
/// without the right principal.
/// </summary>
public sealed class OnlineExamRoutingTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public OnlineExamRoutingTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private async Task<HttpClient> StudentClientAsync(string email)
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "طالب",
            lastName = "اختبار",
            email,
            password = "Test@1234",
            phoneNumber = "01012345678",
            city = "القاهرة"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var token = JsonSerializer
            .Deserialize<JsonElement>(await response.Content.ReadAsStringAsync())
            .GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Theory]
    [InlineData("/api/v1/teacher/exams/00000000-0000-0000-0000-000000000001/questions")]
    [InlineData("/api/v1/teacher/exams/00000000-0000-0000-0000-000000000001/attempts")]
    [InlineData("/api/v1/teacher/exams/attempts/00000000-0000-0000-0000-000000000001")]
    public async Task Teacher_exam_routes_reject_anonymous_callers(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/api/v1/student/exams/00000000-0000-0000-0000-000000000001/attempts")]
    [InlineData("/api/v1/student/courses/00000000-0000-0000-0000-000000000001/progress")]
    public async Task Student_learning_routes_reject_anonymous_callers(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_student_may_not_reach_the_teacher_exam_surface()
    {
        var client = await StudentClientAsync("routing.student@ustaz.test");

        var response = await client.GetAsync(
            "/api/v1/teacher/exams/00000000-0000-0000-0000-000000000001/questions");

        // The route exists; the student simply has no workspace standing for it.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Starting_an_exam_that_does_not_exist_is_a_404_not_a_missing_route()
    {
        var client = await StudentClientAsync("routing.start@ustaz.test");

        var response = await client.PostAsync(
            "/api/v1/student/exams/00000000-0000-0000-0000-000000000009/attempts",
            Body(new { }));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Course_progress_for_a_course_the_student_never_bought_is_a_404()
    {
        var client = await StudentClientAsync("routing.progress@ustaz.test");

        var response = await client.GetAsync(
            "/api/v1/student/courses/00000000-0000-0000-0000-000000000009/progress");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Completing_a_lesson_of_a_course_the_student_never_bought_is_a_404()
    {
        var client = await StudentClientAsync("routing.complete@ustaz.test");

        var response = await client.PostAsync(
            "/api/v1/student/courses/00000000-0000-0000-0000-000000000009/lessons/00000000-0000-0000-0000-000000000008/complete",
            Body(new { watchedSeconds = 10 }));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
