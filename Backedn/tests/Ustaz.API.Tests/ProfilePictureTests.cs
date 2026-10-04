using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Shouldly;

namespace Ustaz.API.Tests;

/// <summary>
/// The profile-picture surface every role shares, and the checks that stop it being a file-drop.
///
/// The spoofing tests are the point of the file: an extension and a <c>Content-Type</c> are
/// both just strings the client chose, so an allow-list over them alone accepts an HTML
/// document named <c>avatar.png</c>. Only the bytes decide.
/// </summary>
public sealed class ProfilePictureTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>A real, minimal PNG: signature, IHDR, one IDAT, IEND.</summary>
    private static readonly byte[] RealPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFAAH/q842iQAAAABJRU5ErkJggg==");

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public ProfilePictureTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private static MultipartFormDataContent Upload(byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(part, "file", fileName);
        return content;
    }

    private async Task<HttpClient> StudentClientAsync(string email)
    {
        await _factory.SeedEssentialsAsync();

        var response = await _client.PostAsync("/api/v1/auth/register/student", Body(new
        {
            firstName = "يوسف",
            lastName = "منير",
            email,
            password = "Test@1234",
            phoneNumber = "01033334444",
            city = "أسيوط"
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var token = (await ReadAsync(response)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // -----------------------------------------------------------------------
    // Authorization
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_profile_picture_routes_reject_anonymous_callers()
    {
        (await _client.GetAsync("/api/v1/me/profile-picture")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

        (await _client.DeleteAsync("/api/v1/me/profile-picture")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);

        using var content = Upload(RealPng, "avatar.png", "image/png");

        (await _client.PostAsync("/api/v1/me/profile-picture", content)).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -----------------------------------------------------------------------
    // The lifecycle a student can now actually complete
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_student_can_set_replace_and_remove_their_own_picture()
    {
        var client = await StudentClientAsync("avatar.lifecycle@ustaz.test");

        // Starts with nothing.
        var initial = (await ReadAsync(await client.GetAsync("/api/v1/me/profile-picture")))
            .GetProperty("data");

        initial.GetProperty("hasPicture").GetBoolean().ShouldBeFalse();

        // Upload.
        using var first = Upload(RealPng, "avatar.png", "image/png");
        var uploaded = await client.PostAsync("/api/v1/me/profile-picture", first);

        uploaded.StatusCode.ShouldBe(HttpStatusCode.OK);

        var firstUrl = (await ReadAsync(uploaded)).GetProperty("data").GetProperty("url").GetString();
        firstUrl.ShouldNotBeNullOrWhiteSpace();
        firstUrl!.ShouldStartWith("/uploads/users_profile/");

        // The URL must not expose a filesystem path or the uploader's own file name.
        firstUrl.ShouldNotContain(":");
        firstUrl.ShouldNotContain("avatar");

        // Replace: a new URL, so a cached copy of the old one is never mistaken for the new.
        using var second = Upload(RealPng, "avatar.png", "image/png");
        var replaced = await client.PostAsync("/api/v1/me/profile-picture", second);

        replaced.StatusCode.ShouldBe(HttpStatusCode.OK);

        var secondUrl = (await ReadAsync(replaced)).GetProperty("data").GetProperty("url").GetString();
        secondUrl.ShouldNotBe(firstUrl);

        // Read back.
        var current = (await ReadAsync(await client.GetAsync("/api/v1/me/profile-picture")))
            .GetProperty("data");

        current.GetProperty("url").GetString().ShouldBe(secondUrl);
        current.GetProperty("hasPicture").GetBoolean().ShouldBeTrue();

        // Remove, back to the default avatar.
        var removed = await client.DeleteAsync("/api/v1/me/profile-picture");
        removed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = (await ReadAsync(await client.GetAsync("/api/v1/me/profile-picture")))
            .GetProperty("data");

        after.GetProperty("hasPicture").GetBoolean().ShouldBeFalse();

        // Removing again is not an error: the caller asked for an end state and it holds.
        (await client.DeleteAsync("/api/v1/me/profile-picture")).StatusCode
            .ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_uploaded_picture_shows_up_on_the_students_own_profile()
    {
        var client = await StudentClientAsync("avatar.onprofile@ustaz.test");

        using var content = Upload(RealPng, "me.png", "image/png");
        var uploaded = await client.PostAsync("/api/v1/me/profile-picture", content);

        uploaded.StatusCode.ShouldBe(HttpStatusCode.OK);

        var url = (await ReadAsync(uploaded)).GetProperty("data").GetProperty("url").GetString();

        // The field the student dashboard and the teacher's class list both read.
        var profile = (await ReadAsync(await client.GetAsync("/api/v1/student/profile")))
            .GetProperty("data");

        profile.GetProperty("profileImageUrl").GetString().ShouldBe(url);
    }

    // -----------------------------------------------------------------------
    // Spoofing
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_HTML_document_renamed_to_png_is_refused()
    {
        var client = await StudentClientAsync("avatar.htmlspoof@ustaz.test");

        var html = Encoding.UTF8.GetBytes("<html><script>alert('xss')</script></html>");

        // Allowed extension, allowed content type, and content that is neither.
        using var content = Upload(html, "avatar.png", "image/png");

        var response = await client.PostAsync("/api/v1/me/profile-picture", content);

        ((int)response.StatusCode).ShouldBeOneOf(400, 422);

        var payload = await response.Content.ReadAsStringAsync();
        payload.ShouldNotContain("alert");
    }

    [Theory]
    [InlineData("payload.exe.png", "image/png")]
    [InlineData("payload.png", "image/jpeg")]
    [InlineData("payload.jpg", "image/jpeg")]
    [InlineData("payload.webp", "image/webp")]
    [InlineData("payload.gif", "image/gif")]
    public async Task Arbitrary_bytes_under_an_image_name_are_refused(string fileName, string contentType)
    {
        var client = await StudentClientAsync("avatar.spoof." + Math.Abs(fileName.GetHashCode()) + "@ustaz.test");

        var bytes = Encoding.UTF8.GetBytes("MZ\0this is not an image at all");

        using var content = Upload(bytes, fileName, contentType);

        var response = await client.PostAsync("/api/v1/me/profile-picture", content);

        ((int)response.StatusCode).ShouldBeOneOf(400, 422);
    }

    [Fact]
    public async Task A_document_is_refused_where_an_image_is_expected()
    {
        var client = await StudentClientAsync("avatar.pdf@ustaz.test");

        var pdf = Encoding.UTF8.GetBytes("%PDF-1.7\nnot an avatar");

        // Genuinely a PDF, honestly declared — and still not an image.
        using var content = Upload(pdf, "scan.pdf", "application/pdf");

        var response = await client.PostAsync("/api/v1/me/profile-picture", content);

        ((int)response.StatusCode).ShouldBeOneOf(400, 422);
    }

    [Fact]
    public async Task An_empty_file_is_refused()
    {
        var client = await StudentClientAsync("avatar.empty@ustaz.test");

        using var content = Upload([], "avatar.png", "image/png");

        var response = await client.PostAsync("/api/v1/me/profile-picture", content);

        ((int)response.StatusCode).ShouldBeOneOf(400, 422);
    }

    [Fact]
    public async Task An_oversized_image_is_refused()
    {
        var client = await StudentClientAsync("avatar.toobig@ustaz.test");

        // A real PNG header followed by enough padding to pass the 5 MB image ceiling.
        var oversized = new byte[6 * 1024 * 1024];
        RealPng.CopyTo(oversized, 0);

        using var content = Upload(oversized, "huge.png", "image/png");

        var response = await client.PostAsync("/api/v1/me/profile-picture", content);

        ((int)response.StatusCode).ShouldBeOneOf(400, 413, 422);
    }

    // -----------------------------------------------------------------------
    // Path traversal
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("../../../../evil.png")]
    [InlineData("..\\..\\evil.png")]
    [InlineData("/etc/passwd.png")]
    public async Task A_file_name_that_tries_to_escape_the_storage_root_lands_inside_it(string fileName)
    {
        var client = await StudentClientAsync("avatar.traversal." + Math.Abs(fileName.GetHashCode()) + "@ustaz.test");

        using var content = Upload(RealPng, fileName, "image/png");

        var response = await client.PostAsync("/api/v1/me/profile-picture", content);

        // The stored name is generated, so a hostile name is simply discarded rather than
        // rejected — what matters is that nothing it contained reaches the path.
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var url = (await ReadAsync(response)).GetProperty("data").GetProperty("url").GetString()!;

            // One flat folder segment then a generated name: the media service collapses the
            // folder's separator, so a stored path is always exactly two segments deep and
            // nothing from the request can add a third.
            url.ShouldStartWith("/uploads/users_profile/");
            url.Split('/', StringSplitOptions.RemoveEmptyEntries).Length.ShouldBe(3);

            url.ShouldNotContain("..");
            url.ShouldNotContain("etc");
            url.ShouldNotContain("evil");
        }
        else
        {
            ((int)response.StatusCode).ShouldBeOneOf(400, 422);
        }
    }
}
