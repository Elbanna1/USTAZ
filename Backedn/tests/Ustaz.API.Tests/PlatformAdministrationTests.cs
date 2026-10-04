using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;

namespace Ustaz.API.Tests;

/// <summary>
/// The platform operator's own surface, and the guards that stop it locking everyone out.
///
/// The lockout test is the reason this file exists. Revoking the only remaining platform
/// administrator, or suspending their account, leaves nobody able to grant the role back —
/// the product has no recovery path from that state short of editing the database. Both
/// routes refuse it, and both refusals are pinned here.
/// </summary>
public sealed class PlatformAdministrationTests : IClassFixture<PlatformAdministrationTests.PlatformApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Its own host, so the administrator accounts these tests create and promote cannot be
    /// seen by — or collide with — the other test classes sharing the default factory.
    /// </summary>
    public sealed class PlatformApiFactory : UstazApiFactory;

    private const string Password = "Admin@12345";

    private readonly PlatformApiFactory _factory;
    private readonly HttpClient _client;

    public PlatformAdministrationTests(PlatformApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    /// <summary>Creates an account, optionally already holding the platform role.</summary>
    private async Task<Guid> CreateAccountAsync(string email, bool asAdministrator)
    {
        await _factory.SeedEssentialsAsync();

        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var existing = await users.FindByEmailAsync(email);

        if (existing is not null)
        {
            return existing.Id;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "منى",
            LastName = "عادل",
            DisplayName = "منى عادل",
            IsActive = true,
            IsVerified = true
        };

        (await users.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();

        if (asAdministrator)
        {
            (await users.AddToRoleAsync(user, RoleNames.Admin)).Succeeded.ShouldBeTrue();
        }

        return user.Id;
    }

    private async Task<HttpClient> SignInAsync(string email)
    {
        var login = await _client.PostAsync("/api/v1/auth/login", Body(new { email, password = Password }));

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var token = (await ReadAsync(login)).GetProperty("data").GetProperty("accessToken").GetString()!;

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>The platform admin these tests act as. Created once, kept for the class.</summary>
    private async Task<HttpClient> PlatformAdminAsync()
    {
        await CreateAccountAsync("platform.primary@ustaz.test", asAdministrator: true);
        return await SignInAsync("platform.primary@ustaz.test");
    }

    // -----------------------------------------------------------------------
    // Overview
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_platform_overview_counts_schools_which_nothing_else_reported()
    {
        var client = await PlatformAdminAsync();

        var response = await client.GetAsync("/api/v1/platform/overview");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var data = (await ReadAsync(response)).GetProperty("data");

        // The numbers a platform operator actually opens the console for.
        foreach (var field in new[]
        {
            "totalSchools", "activeSchools", "suspendedSchools", "archivedSchools",
            "totalUsers", "activeUsers", "totalStudents", "totalTeachers", "totalParents",
            "platformAdministrators", "schoolStaffMemberships",
            "activeSubscriptions", "atRiskSubscriptions"
        })
        {
            data.TryGetProperty(field, out var value).ShouldBeTrue(field + " is missing");
            value.GetInt32().ShouldBeGreaterThanOrEqualTo(0, field);
        }

        // At least the administrator making the call.
        data.GetProperty("platformAdministrators").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task The_overview_reports_a_school_once_a_teacher_registers()
    {
        var client = await PlatformAdminAsync();

        var before = (await ReadAsync(await client.GetAsync("/api/v1/platform/overview")))
            .GetProperty("data").GetProperty("totalSchools").GetInt32();

        Guid subjectId;
        Guid gradeId;

        using (var context = _factory.CreateContext())
        {
            subjectId = context.Subjects.First().Id;
            gradeId = context.EducationalGrades.First().Id;
        }

        var registration = await _client.PostAsync("/api/v1/auth/register/teacher", Body(new
        {
            firstName = "طارق",
            lastName = "فؤاد",
            email = "platform.newschool@ustaz.test",
            password = "Test@1234",
            phoneNumber = "01055556666",
            subjectIds = new[] { subjectId },
            gradeIds = new[] { gradeId },
            workspaceName = "مدرسة النيل"
        }));

        registration.StatusCode.ShouldBe(HttpStatusCode.Created);

        var after = (await ReadAsync(await client.GetAsync("/api/v1/platform/overview")))
            .GetProperty("data").GetProperty("totalSchools").GetInt32();

        after.ShouldBe(before + 1);
    }

    // -----------------------------------------------------------------------
    // Platform-wide user search
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Platform_user_search_paginates_and_filters()
    {
        var client = await PlatformAdminAsync();
        await CreateAccountAsync("platform.searchme@ustaz.test", asAdministrator: false);

        var page = (await ReadAsync(await client.GetAsync("/api/v1/platform/users?page=1&pageSize=5")))
            .GetProperty("data");

        page.GetProperty("pageSize").GetInt32().ShouldBe(5);
        page.GetProperty("items").GetArrayLength().ShouldBeLessThanOrEqualTo(5);
        page.GetProperty("totalCount").GetInt32().ShouldBeGreaterThan(0);

        var found = (await ReadAsync(
                await client.GetAsync("/api/v1/platform/users?search=platform.searchme")))
            .GetProperty("data").GetProperty("items");

        found.GetArrayLength().ShouldBe(1);
        found[0].GetProperty("email").GetString().ShouldBe("platform.searchme@ustaz.test");
    }

    [Fact]
    public async Task Platform_user_search_never_returns_a_credential()
    {
        var client = await PlatformAdminAsync();

        var payload = await (await client.GetAsync("/api/v1/platform/users?page=1&pageSize=20"))
            .Content.ReadAsStringAsync();

        foreach (var forbidden in new[]
        {
            "passwordHash", "securityStamp", "concurrencyStamp", "refreshToken", "twoFactor"
        })
        {
            payload.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public async Task Platform_user_search_can_filter_to_one_role()
    {
        var client = await PlatformAdminAsync();

        var students = (await ReadAsync(
                await client.GetAsync("/api/v1/platform/users?role=student&pageSize=50")))
            .GetProperty("data").GetProperty("items");

        foreach (var student in students.EnumerateArray())
        {
            student.GetProperty("isStudent").GetBoolean().ShouldBeTrue();
        }
    }

    // -----------------------------------------------------------------------
    // Granting and revoking platform authority
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_administrator_can_grant_and_revoke_platform_access()
    {
        var client = await PlatformAdminAsync();
        var subjectId = await CreateAccountAsync("platform.promote@ustaz.test", asAdministrator: false);

        // Before: not an administrator, so the platform routes are closed to them.
        var subjectClient = await SignInAsync("platform.promote@ustaz.test");

        (await subjectClient.GetAsync("/api/v1/platform/overview")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);

        // Grant.
        var granted = await client.PostAsync(
            "/api/v1/platform/administrators/" + subjectId,
            Body(new { reason = "انضم لفريق تشغيل المنصة" }));

        granted.StatusCode.ShouldBe(HttpStatusCode.OK);

        // A fresh token now carries the role.
        var promoted = await SignInAsync("platform.promote@ustaz.test");

        (await promoted.GetAsync("/api/v1/platform/overview")).StatusCode
            .ShouldBe(HttpStatusCode.OK);

        // Revoke.
        var revoked = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, "/api/v1/platform/administrators/" + subjectId)
        {
            Content = Body(new { reason = "انتهى تكليفه" })
        });

        revoked.StatusCode.ShouldBe(HttpStatusCode.OK);

        var demoted = await SignInAsync("platform.promote@ustaz.test");

        (await demoted.GetAsync("/api/v1/platform/overview")).StatusCode
            .ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Granting_requires_a_reason_for_the_audit_trail()
    {
        var client = await PlatformAdminAsync();
        var subjectId = await CreateAccountAsync("platform.noreason@ustaz.test", asAdministrator: false);

        var response = await client.PostAsync(
            "/api/v1/platform/administrators/" + subjectId,
            Body(new { reason = "" }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Granting_a_suspended_account_platform_access_is_refused()
    {
        var client = await PlatformAdminAsync();
        var subjectId = await CreateAccountAsync("platform.suspended@ustaz.test", asAdministrator: false);

        var suspended = await client.PostAsync(
            "/api/v1/admin/users/" + subjectId + "/state",
            Body(new { isActive = false, reason = "تحت المراجعة" }));

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await client.PostAsync(
            "/api/v1/platform/administrators/" + subjectId,
            Body(new { reason = "محاولة ترقية حساب موقوف" }));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadAsync(response)).GetProperty("code").GetString().ShouldBe("ACCOUNT_INACTIVE");
    }

    // -----------------------------------------------------------------------
    // Lockout guards
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_administrator_cannot_revoke_their_own_platform_access()
    {
        var selfId = await CreateAccountAsync("platform.self@ustaz.test", asAdministrator: true);
        var client = await SignInAsync("platform.self@ustaz.test");

        var response = await client.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, "/api/v1/platform/administrators/" + selfId)
        {
            Content = Body(new { reason = "محاولة إزالة صلاحياتي" })
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadAsync(response)).GetProperty("code").GetString()
            .ShouldBe("PLATFORM_ADMIN_SELF_DEMOTION");
    }

    [Fact]
    public async Task An_administrator_cannot_suspend_their_own_account()
    {
        var selfId = await CreateAccountAsync("platform.selfsuspend@ustaz.test", asAdministrator: true);
        var client = await SignInAsync("platform.selfsuspend@ustaz.test");

        var response = await client.PostAsync(
            "/api/v1/admin/users/" + selfId + "/state",
            Body(new { isActive = false, reason = "محاولة إيقاف حسابي" }));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadAsync(response)).GetProperty("code").GetString()
            .ShouldBe("PLATFORM_ADMIN_SELF_DEMOTION");
    }

    [Fact]
    public async Task The_platform_cannot_be_left_without_an_administrator()
    {
        // A host of its own, so how many administrators exist is something this test controls.
        using var factory = new PlatformApiFactory();
        var anonymous = factory.CreateClient();

        await factory.SeedEssentialsAsync();

        Guid firstId;
        Guid secondId;

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            async Task<Guid> AddAdministratorAsync(string email)
            {
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "عمر",
                    LastName = "شريف",
                    DisplayName = "عمر شريف",
                    IsActive = true,
                    IsVerified = true
                };

                (await users.CreateAsync(user, Password)).Succeeded.ShouldBeTrue();
                (await users.AddToRoleAsync(user, RoleNames.Admin)).Succeeded.ShouldBeTrue();

                return user.Id;
            }

            firstId = await AddAdministratorAsync("lockout.first@ustaz.test");
            secondId = await AddAdministratorAsync("lockout.second@ustaz.test");
        }

        async Task<HttpClient> SignInOnAsync(string email)
        {
            var login = await anonymous.PostAsync(
                "/api/v1/auth/login", Body(new { email, password = Password }));

            login.StatusCode.ShouldBe(HttpStatusCode.OK);

            var token = (await ReadAsync(login)).GetProperty("data")
                .GetProperty("accessToken").GetString()!;

            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        var first = await SignInOnAsync("lockout.first@ustaz.test");

        // Two administrators, so removing one is allowed. This is the step that establishes
        // the dangerous state rather than the one being guarded against.
        var revokeSecond = await first.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, "/api/v1/platform/administrators/" + secondId)
        {
            Content = Body(new { reason = "إعادة توزيع الصلاحيات" })
        });

        revokeSecond.StatusCode.ShouldBe(HttpStatusCode.OK);

        // "first" is now the only administrator on the platform.
        var administrators = (await ReadAsync(await first.GetAsync("/api/v1/platform/administrators")))
            .GetProperty("data");

        administrators.GetArrayLength().ShouldBe(1);

        var only = administrators[0];
        only.GetProperty("userId").GetString().ShouldBe(firstId.ToString());
        only.GetProperty("isLastAdministrator").GetBoolean().ShouldBeTrue();
        only.GetProperty("isCurrentUser").GetBoolean().ShouldBeTrue();

        // Both ways of losing them are refused.
        //
        // Note which guard answers. Every route here requires an *active* platform
        // administrator, so whoever is making the request is themselves an administrator — and
        // when only one is left, that one is the caller. The self-protection check is
        // therefore the one that fires, and the last-administrator check behind it is defence
        // in depth for any future caller that is not the subject (a migration, a background
        // job, an internal tool). What matters to the platform is that neither request
        // succeeds, which is what this asserts.
        var revokeSelf = await first.SendAsync(new HttpRequestMessage(
            HttpMethod.Delete, "/api/v1/platform/administrators/" + firstId)
        {
            Content = Body(new { reason = "تنازل" })
        });

        revokeSelf.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadAsync(revokeSelf)).GetProperty("code").GetString()
            .ShouldBeOneOf("PLATFORM_ADMIN_SELF_DEMOTION", "LAST_PLATFORM_ADMIN");

        var suspendSelf = await first.PostAsync(
            "/api/v1/admin/users/" + firstId + "/state",
            Body(new { isActive = false, reason = "إيقاف" }));

        suspendSelf.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await ReadAsync(suspendSelf)).GetProperty("code").GetString()
            .ShouldBeOneOf("PLATFORM_ADMIN_SELF_DEMOTION", "LAST_PLATFORM_ADMIN");

        // And the platform still has its administrator afterwards.
        (await first.GetAsync("/api/v1/platform/overview")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_administrator_list_flags_the_caller_and_the_last_administrator()
    {
        var client = await PlatformAdminAsync();

        var administrators = (await ReadAsync(await client.GetAsync("/api/v1/platform/administrators")))
            .GetProperty("data");

        administrators.GetArrayLength().ShouldBeGreaterThan(0);

        var caller = administrators.EnumerateArray()
            .Single(entry => entry.GetProperty("email").GetString() == "platform.primary@ustaz.test");

        caller.GetProperty("isCurrentUser").GetBoolean().ShouldBeTrue();

        // No credential material in an administrative list either.
        var payload = await (await client.GetAsync("/api/v1/platform/administrators"))
            .Content.ReadAsStringAsync();

        payload.ShouldNotContain("passwordHash", Case.Insensitive);
        payload.ShouldNotContain("securityStamp", Case.Insensitive);
    }
}
