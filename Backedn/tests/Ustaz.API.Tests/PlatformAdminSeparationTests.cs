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
/// USTAZ has two different kinds of administrator and they must never be confused:
///
/// * A <em>school admin</em> is a teacher who owns a workspace, or a member the owner promoted
///   to <c>TenantMemberRole.Owner</c>/<c>Admin</c>. Their authority stops at their own school.
/// * A <em>global platform admin</em> holds the platform <c>Admin</c> identity role, which is
///   granted only by the seeder, and runs USTAZ itself.
///
/// Owning a workspace confers no platform authority whatsoever. These tests pin that down over
/// the real HTTP pipeline, because it is the kind of boundary that quietly erodes: the two
/// concepts share the word "admin", and the platform routes live one segment away from the
/// workspace ones.
/// </summary>
public sealed class PlatformAdminSeparationTests : IClassFixture<UstazApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Every route that is platform-only, as the controllers declare them.</summary>
    public static TheoryData<string> PlatformOnlyRoutes => new()
    {
        "/api/v1/admin/dashboard",
        "/api/v1/admin/analytics",
        "/api/v1/admin/teachers",
        "/api/v1/admin/students",
        "/api/v1/admin/groups",
        "/api/v1/admin/courses",
        "/api/v1/admin/enrollments",
        "/api/v1/admin/reviews",
        "/api/v1/admin/inquiries",
        "/api/v1/admin/discounts",
        "/api/v1/admin/tenants",
        "/api/v1/admin/plans",
        "/api/v1/admin/audit-logs",
        "/api/v1/admin/analytics/revenue",
        "/api/v1/admin/analytics/tenants",

        // The platform operator's own surface.
        "/api/v1/platform/overview",
        "/api/v1/platform/users",
        "/api/v1/platform/administrators"
    };

    private readonly UstazApiFactory _factory;
    private readonly HttpClient _client;

    public PlatformAdminSeparationTests(UstazApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static StringContent Body(object payload) =>
        new(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private HttpClient Authenticated(string token, Guid? tenantId = null)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        if (tenantId is not null)
        {
            client.DefaultRequestHeaders.Add(Services.TenantContext.TenantHeaderName, tenantId.Value.ToString());
        }

        return client;
    }

    /// <summary>
    /// Registers a teacher. Registration provisions a workspace and makes the teacher its
    /// owner, which is exactly what "school admin" means in this product.
    /// </summary>
    private async Task<(string Access, Guid TenantId)> RegisterSchoolAdminAsync(string email)
    {
        await _factory.SeedEssentialsAsync();

        Guid subjectId;
        Guid gradeId;

        using (var context = _factory.CreateContext())
        {
            subjectId = context.Subjects.First().Id;
            gradeId = context.EducationalGrades.First().Id;
        }

        var response = await _client.PostAsync("/api/v1/auth/register/teacher", Body(new
        {
            firstName = "محمود",
            lastName = "عبد الله",
            email,
            password = "Test@1234",
            phoneNumber = "01098765432",
            city = "الإسكندرية",
            yearsOfExperience = 6,
            subjectIds = new[] { subjectId },
            gradeIds = new[] { gradeId },
            workspaceName = "أكاديمية " + Guid.NewGuid().ToString("N")[..6]
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var user = (await ReadAsync(response)).GetProperty("data");
        var access = user.GetProperty("accessToken").GetString()!;
        var tenantId = Guid.Parse(user.GetProperty("user").GetProperty("tenantId").GetString()!);

        return (access, tenantId);
    }

    /// <summary>Creates a real platform administrator the way the seeder does.</summary>
    private async Task<string> PlatformAdminTokenAsync(string email)
    {
        await _factory.SeedEssentialsAsync();

        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            if (await users.FindByEmailAsync(email) is null)
            {
                var admin = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FirstName = "USTAZ",
                    LastName = "Administrator",
                    DisplayName = "USTAZ Administrator",
                    IsActive = true,
                    IsVerified = true
                };

                (await users.CreateAsync(admin, "Admin@12345")).Succeeded.ShouldBeTrue();
                (await users.AddToRoleAsync(admin, RoleNames.Admin)).Succeeded.ShouldBeTrue();
            }
        }

        var login = await _client.PostAsync("/api/v1/auth/login", Body(new
        {
            email,
            password = "Admin@12345"
        }));

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await ReadAsync(login)).GetProperty("data").GetProperty("accessToken").GetString()!;
    }

    // -----------------------------------------------------------------------
    // A school admin is not a platform admin
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(PlatformOnlyRoutes))]
    public async Task A_school_admin_cannot_reach_a_platform_only_route(string route)
    {
        var (access, tenantId) = await RegisterSchoolAdminAsync(
            "sep." + Math.Abs(route.GetHashCode()) + "@ustaz.test");

        var client = Authenticated(access, tenantId);

        var response = await client.GetAsync(route);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_school_admin_cannot_suspend_a_school()
    {
        var (access, tenantId) = await RegisterSchoolAdminAsync("sep.suspend@ustaz.test");
        var client = Authenticated(access, tenantId);

        // Not even their own: suspending a school is a platform decision.
        var own = await client.PostAsync(
            "/api/v1/admin/tenants/" + tenantId + "/suspend",
            Body(new { reason = "محاولة غير مصرح بها" }));

        own.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_school_admin_cannot_change_a_users_account_state()
    {
        var (access, tenantId) = await RegisterSchoolAdminAsync("sep.userstate@ustaz.test");
        var client = Authenticated(access, tenantId);

        var response = await client.PostAsync(
            "/api/v1/admin/users/" + Guid.NewGuid() + "/state",
            Body(new { isActive = false, reason = "محاولة غير مصرح بها" }));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_school_admins_token_carries_a_workspace_but_not_the_platform_role()
    {
        var (access, tenantId) = await RegisterSchoolAdminAsync("sep.claims@ustaz.test");
        var client = Authenticated(access, tenantId);

        var me = await client.GetAsync("/api/v1/auth/me");
        me.StatusCode.ShouldBe(HttpStatusCode.OK);

        var user = (await ReadAsync(me)).GetProperty("data");

        user.GetProperty("tenantId").GetString().ShouldBe(tenantId.ToString());

        var roles = user.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToList();
        roles.ShouldContain(RoleNames.Teacher);
        roles.ShouldNotContain(RoleNames.Admin);
    }

    // -----------------------------------------------------------------------
    // A platform admin is
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_platform_admin_reaches_the_platform_overview()
    {
        var token = await PlatformAdminTokenAsync("platform.overview@ustaz.test");
        var client = Authenticated(token);

        foreach (var route in new[] { "/api/v1/admin/dashboard", "/api/v1/admin/tenants", "/api/v1/admin/audit-logs" })
        {
            var response = await client.GetAsync(route);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, route);
        }
    }

    [Fact]
    public async Task A_platform_admin_holds_the_platform_role_and_no_workspace()
    {
        var token = await PlatformAdminTokenAsync("platform.claims@ustaz.test");
        var client = Authenticated(token);

        var user = (await ReadAsync(await client.GetAsync("/api/v1/auth/me"))).GetProperty("data");

        var roles = user.GetProperty("roles").EnumerateArray().Select(role => role.GetString()).ToList();
        roles.ShouldContain(RoleNames.Admin);

        // A platform admin runs USTAZ; they are not a member of any school by default.
        user.TryGetProperty("tenantId", out var tenant).ShouldBeFalse(
            "a platform administrator should not be scoped to a workspace, but got " + tenant);
    }

    // -----------------------------------------------------------------------
    // Cross-tenant access
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Naming_another_school_in_the_tenant_header_does_not_grant_access_to_it()
    {
        var (_, victimTenantId) = await RegisterSchoolAdminAsync("sep.victim@ustaz.test");
        var (attackerAccess, _) = await RegisterSchoolAdminAsync("sep.attacker@ustaz.test");

        // The header is only ever a hint about which of *your own* workspaces you mean.
        // Membership is re-resolved from the database on every request.
        var client = Authenticated(attackerAccess, victimTenantId);

        foreach (var route in new[]
        {
            "/api/v1/tenant",
            "/api/v1/tenant/settings",
            "/api/v1/tenant/team",
            "/api/v1/tenant/audit-logs",
            "/api/v1/teacher/attendance/sessions",
            "/api/v1/teacher/homework",
            "/api/v1/teacher/exams"
        })
        {
            var response = await client.GetAsync(route);

            ((int)response.StatusCode).ShouldBeOneOf(
                StatusCodes.Forbidden,
                StatusCodes.NotFound);
        }
    }

    [Fact]
    public async Task A_schools_own_workspace_routes_still_work_with_its_own_tenant_header()
    {
        var (access, tenantId) = await RegisterSchoolAdminAsync("sep.ownworkspace@ustaz.test");
        var client = Authenticated(access, tenantId);

        // The counterpart to the test above: the guard must reject the other school without
        // breaking the legitimate case.
        (await client.GetAsync("/api/v1/tenant")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/tenant/settings")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/tenant/team")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static class StatusCodes
    {
        public const int Forbidden = 403;
        public const int NotFound = 404;
    }
}
