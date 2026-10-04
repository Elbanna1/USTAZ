using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Extensions;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Features.Admin.Queries;

/// <summary>
/// The platform's shape at a glance: how many schools there are, how many are actually running,
/// and how the people on the platform divide between the roles.
///
/// This is separate from <c>GetAdminDashboardQuery</c>, which grew up around the single-teacher
/// marketplace and counts teachers, groups, courses and revenue. Neither it nor anything else
/// counted <em>schools</em>, which is the first number a platform operator wants — USTAZ sells
/// to schools, so "three suspended out of forty" is the headline and "nine hundred users" is
/// not.
///
/// Every count is an aggregate pushed to the database, grouped so that one query answers every
/// counter for a given table. Nothing is materialised: a platform overview that loaded the user
/// table to count it would get slower exactly as the platform succeeded.
/// </summary>
public sealed record PlatformOverviewDto
{
    // Schools ---------------------------------------------------------------
    public int TotalSchools { get; init; }

    public int ActiveSchools { get; init; }

    public int SuspendedSchools { get; init; }

    public int ArchivedSchools { get; init; }

    /// <summary>Schools created in the last 30 days, as a crude growth signal.</summary>
    public int NewSchoolsLast30Days { get; init; }

    // People ----------------------------------------------------------------
    public int TotalUsers { get; init; }

    public int ActiveUsers { get; init; }

    public int SuspendedUsers { get; init; }

    public int TotalStudents { get; init; }

    public int TotalTeachers { get; init; }

    public int TotalParents { get; init; }

    /// <summary>Platform administrators — the people who run USTAZ, not school staff.</summary>
    public int PlatformAdministrators { get; init; }

    /// <summary>School-level staff: members of a workspace, in any workspace role.</summary>
    public int SchoolStaffMemberships { get; init; }

    // Subscriptions ---------------------------------------------------------
    public int ActiveSubscriptions { get; init; }

    public int TrialSubscriptions { get; init; }

    public int ExpiredSubscriptions { get; init; }

    public int CancelledSubscriptions { get; init; }

    /// <summary>Past due or inside a grace period: paying has stopped, access has not.</summary>
    public int AtRiskSubscriptions { get; init; }

    /// <summary>When these numbers were taken, so a console can show their age.</summary>
    public DateTime GeneratedAt { get; init; }
}

public sealed record GetPlatformOverviewQuery : IRequest<PlatformOverviewDto>;

public sealed class GetPlatformOverviewQueryHandler(
    IApplicationDbContext context,
    UserManager<ApplicationUser> userRoles,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<GetPlatformOverviewQuery, PlatformOverviewDto>
{
    public async Task<PlatformOverviewDto> Handle(
        GetPlatformOverviewQuery query,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var thirtyDaysAgo = utcNow.AddDays(-30);

        // One grouped aggregate per table: every counter for a given table is computed in the
        // same query, so this costs seven round trips for seventeen numbers rather than
        // seventeen.
        //
        // It is not one round trip, and it cannot easily be. Writing the other tables' counts
        // as sub-selects inside a single projection looks like it would fold them together,
        // but EF Core funcletizes a scalar `context.OtherTable.Count()` and executes it as its
        // own query — measured at seven commands for a seven-count projection, i.e. no saving
        // at all, plus a null result row when the anchor table is empty. Genuinely collapsing
        // these into one statement needs raw SQL, which the in-memory provider the tests run
        // on cannot execute. See docs/backend-audit-report.md for the measurement and the
        // conditions under which that trade becomes worth making.
        var schools = await context.Tenants
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(tenant => tenant.Status == TenantStatus.Active),
                Suspended = group.Count(tenant => tenant.Status == TenantStatus.Suspended),
                Archived = group.Count(tenant => tenant.Status == TenantStatus.Archived),
                Recent = group.Count(tenant => tenant.CreatedAt >= thirtyDaysAgo)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var people = await context.Users
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                Active = group.Count(user => user.IsActive),
                Suspended = group.Count(user => !user.IsActive)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var students = await context.StudentProfiles.AsNoTracking().CountAsync(cancellationToken);
        var teachers = await context.TeacherProfiles.AsNoTracking().CountAsync(cancellationToken);
        var parents = await context.ParentProfiles.AsNoTracking().CountAsync(cancellationToken);

        var staff = await context.TenantMembers
            .AsNoTracking()
            .CountAsync(member => member.Status == TenantMemberStatus.Active, cancellationToken);

        var subscriptions = await context.TenantSubscriptions
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Active = group.Count(subscription => subscription.Status == SubscriptionStatus.Active),
                Trial = group.Count(subscription => subscription.Status == SubscriptionStatus.Trial),
                Expired = group.Count(subscription => subscription.Status == SubscriptionStatus.Expired),
                Cancelled = group.Count(subscription => subscription.Status == SubscriptionStatus.Canceled),
                // Neither healthy nor finished: a school that has stopped paying but has not
                // been cut off yet is the set an operator most needs to see.
                AtRisk = group.Count(subscription =>
                    subscription.Status == SubscriptionStatus.PastDue
                    || subscription.Status == SubscriptionStatus.GracePeriod)
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Role membership lives in the Identity tables, which the application DbContext does
        // not expose as a DbSet, so this one unavoidably goes through UserManager.
        var administrators = await userRoles.GetUsersInRoleAsync(RoleNames.Admin);

        // A grouped aggregate over an empty table yields no row at all rather than a row of
        // zeros, so every read below tolerates null. That is the case on a brand-new platform.
        return new PlatformOverviewDto
        {
            TotalSchools = schools?.Total ?? 0,
            ActiveSchools = schools?.Active ?? 0,
            SuspendedSchools = schools?.Suspended ?? 0,
            ArchivedSchools = schools?.Archived ?? 0,
            NewSchoolsLast30Days = schools?.Recent ?? 0,

            TotalUsers = people?.Total ?? 0,
            ActiveUsers = people?.Active ?? 0,
            SuspendedUsers = people?.Suspended ?? 0,
            TotalStudents = students,
            TotalTeachers = teachers,
            TotalParents = parents,
            PlatformAdministrators = administrators.Count,
            SchoolStaffMemberships = staff,

            ActiveSubscriptions = subscriptions?.Active ?? 0,
            TrialSubscriptions = subscriptions?.Trial ?? 0,
            ExpiredSubscriptions = subscriptions?.Expired ?? 0,
            CancelledSubscriptions = subscriptions?.Cancelled ?? 0,
            AtRiskSubscriptions = subscriptions?.AtRisk ?? 0,

            GeneratedAt = utcNow
        };
    }
}

// ---------------------------------------------------------------------------
// Platform-wide user search
// ---------------------------------------------------------------------------

/// <summary>
/// A person on the platform, as an operator sees them.
///
/// What is not here matters as much as what is: no password hash, no security stamp, no token,
/// no address. An administrative list is still a list of other people's data, and the fact that
/// the caller is an administrator is not a reason to hand over everything the row holds.
/// </summary>
public sealed record PlatformUserDto
{
    public Guid UserId { get; init; }

    public required string DisplayName { get; init; }

    public required string Email { get; init; }

    public string? PhoneNumber { get; init; }

    public bool IsActive { get; init; }

    public bool EmailConfirmed { get; init; }

    public string? ProfileImageUrl { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? LastLoginAt { get; init; }

    /// <summary>Which role profiles this account has, so a console can label the row.</summary>
    public bool IsStudent { get; init; }

    public bool IsTeacher { get; init; }

    public bool IsParent { get; init; }

    /// <summary>Number of workspaces this user is an active member of.</summary>
    public int SchoolMemberships { get; init; }
}

/// <summary>
/// Searches and filters every account on the platform, with pagination.
///
/// The one place on the platform where a list is not tenant-scoped, which is precisely why it
/// is reachable only by the platform <c>Admin</c> role — see
/// <c>PlatformAdminSeparationTests</c>, which proves a school admin cannot call it.
/// </summary>
public sealed record SearchPlatformUsersQuery : PagedRequest, IRequest<PaginatedList<PlatformUserDto>>
{
    // Search comes from PagedRequest, where every searchable query in the application already
    // declares it. It matches the display name, email or phone number, case-insensitively.

    /// <summary>Restricts to one kind of account: Student, Teacher or Parent.</summary>
    public string? Role { get; init; }

    /// <summary>Null for either, true for active accounts only, false for suspended only.</summary>
    public bool? IsActive { get; init; }

    /// <summary>Only members of this school.</summary>
    public Guid? SchoolId { get; init; }

    /// <summary><c>createdAt</c> (default), <c>displayName</c> or <c>lastLoginAt</c>.</summary>
    public string? SortBy { get; init; }

    public bool SortDescending { get; init; } = true;
}

public sealed class SearchPlatformUsersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchPlatformUsersQuery, PaginatedList<PlatformUserDto>>
{
    public async Task<PaginatedList<PlatformUserDto>> Handle(
        SearchPlatformUsersQuery query,
        CancellationToken cancellationToken)
    {
        var users = context.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            users = users.Where(user =>
                EF.Functions.Like(user.DisplayName, "%" + term + "%")
                || EF.Functions.Like(user.Email!, "%" + term + "%")
                || (user.PhoneNumber != null && EF.Functions.Like(user.PhoneNumber, "%" + term + "%")));
        }

        if (query.IsActive is not null)
        {
            users = users.Where(user => user.IsActive == query.IsActive.Value);
        }

        // Role is filtered by the presence of the role's profile row, which is the fact of
        // record; the Identity role table only mirrors it.
        users = query.Role?.Trim().ToLowerInvariant() switch
        {
            "student" => users.Where(user => context.StudentProfiles.Any(p => p.UserId == user.Id)),
            "teacher" => users.Where(user => context.TeacherProfiles.Any(p => p.UserId == user.Id)),
            "parent" => users.Where(user => context.ParentProfiles.Any(p => p.UserId == user.Id)),
            _ => users
        };

        if (query.SchoolId is not null)
        {
            users = users.Where(user => context.TenantMembers.Any(member =>
                member.UserId == user.Id
                && member.TenantId == query.SchoolId.Value
                && member.Status == TenantMemberStatus.Active));
        }

        users = (query.SortBy?.Trim().ToLowerInvariant(), query.SortDescending) switch
        {
            ("displayname", true) => users.OrderByDescending(user => user.DisplayName),
            ("displayname", false) => users.OrderBy(user => user.DisplayName),
            ("lastloginat", true) => users.OrderByDescending(user => user.LastLoginAt),
            ("lastloginat", false) => users.OrderBy(user => user.LastLoginAt),
            (_, false) => users.OrderBy(user => user.CreatedAt),
            _ => users.OrderByDescending(user => user.CreatedAt)
        };

        // Projected in the database, so a page of twenty costs a page of twenty — and the
        // columns that are never selected (the password hash, the security stamp) cannot leak
        // by being present on a materialised entity.
        var projection = users.Select(user => new PlatformUserDto
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            EmailConfirmed = user.EmailConfirmed,
            ProfileImageUrl = user.ProfileImageUrl,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            IsStudent = context.StudentProfiles.Any(p => p.UserId == user.Id),
            IsTeacher = context.TeacherProfiles.Any(p => p.UserId == user.Id),
            IsParent = context.ParentProfiles.Any(p => p.UserId == user.Id),
            SchoolMemberships = context.TenantMembers.Count(member =>
                member.UserId == user.Id && member.Status == TenantMemberStatus.Active)
        });

        return await projection.ToPaginatedListAsync(query.Page, query.PageSize, cancellationToken);
    }
}
