using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Ustaz.Domain.Common;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Persistence.Contexts;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// Creates a realistic, entirely fictional Egyptian dataset for development and demos.
///
/// Every person, workspace, payment and review below is invented. None of it may ever reach
/// production: <see cref="SeedSafety"/> refuses this mode there outright, with no override.
///
/// Three properties make the dataset useful rather than merely large:
///
/// 1. <b>Deterministic.</b> Every row's primary key comes from <see cref="SeedId"/>, derived
///    from a name like <c>demo:group:physics-3sec-intensive</c>. The same database, a
///    colleague's database and a CI run all agree on every id, which is what lets a test or a
///    frontend fixture reference a row by id and lets a re-run insert only what is missing.
/// 2. <b>Dependency ordered.</b> Modules run parents-before-children and each saves before
///    the next begins, so a foreign key is never written ahead of its target.
/// 3. <b>Varied on purpose.</b> States exist because a screen needs them: a draft group and a
///    published one, a free course and a paid one, a confirmed guardian link and an
///    unconfirmed one, a workspace with no content at all. Empty and forbidden responses are
///    part of correct behaviour, so the data deliberately produces them.
/// </summary>
internal sealed partial class DemoDataSeeder(
    UstazDbContext context,
    UserManager<ApplicationUser> userManager,
    SeedReport report,
    ILogger logger)
{
    // --- Reference ids, fixed by SeedCatalog -----------------------------------
    private static readonly Guid MathematicsId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid PhysicsId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid ChemistryId = Guid.Parse("11111111-0000-0000-0000-000000000003");
    private static readonly Guid BiologyId = Guid.Parse("11111111-0000-0000-0000-000000000004");
    private static readonly Guid ArabicId = Guid.Parse("11111111-0000-0000-0000-000000000005");
    private static readonly Guid EnglishId = Guid.Parse("11111111-0000-0000-0000-000000000006");
    private static readonly Guid GeographyId = Guid.Parse("11111111-0000-0000-0000-000000000008");

    private static readonly Guid PrimaryId = Guid.Parse("22222222-0000-0000-0000-000000000001");
    private static readonly Guid PreparatoryId = Guid.Parse("22222222-0000-0000-0000-000000000002");
    private static readonly Guid FirstSecondaryId = Guid.Parse("22222222-0000-0000-0000-000000000003");
    private static readonly Guid SecondSecondaryId = Guid.Parse("22222222-0000-0000-0000-000000000004");
    private static readonly Guid ThirdSecondaryId = Guid.Parse("22222222-0000-0000-0000-000000000005");

    /// <summary>
    /// The clock the whole dataset is anchored to. Dates are expressed relative to it — a
    /// session four weeks ago, a deadline three days from now — so the data stays meaningful
    /// however long after seeding it is read.
    /// </summary>
    private readonly DateTime _utcNow = DateTime.UtcNow;

    // --- Resolved state, shared between the modules ----------------------------
    private readonly Dictionary<string, Tenant> _tenants = [];
    private readonly Dictionary<string, TeacherProfile> _teachers = [];
    private readonly Dictionary<string, StudentProfile> _students = [];
    private readonly Dictionary<string, ParentProfile> _parents = [];
    private readonly Dictionary<string, Group> _groups = [];
    private readonly Dictionary<string, Course> _courses = [];
    private readonly Dictionary<Guid, ApplicationUser> _users = [];
    private readonly Dictionary<string, GroupEnrollment> _groupEnrollments = [];
    private readonly Dictionary<string, CourseEnrollment> _courseEnrollments = [];

    public async Task SeedAsync(string password, CancellationToken cancellationToken)
    {
        // Workspaces before the people who own them: a TeacherProfile carries a required
        // TenantId, so the tenant row has to exist first.
        await SeedWorkspacesAsync(cancellationToken);
        await SeedTeachersAsync(password, cancellationToken);
        await SeedWorkspaceOwnershipAsync(cancellationToken);
        await SeedStudentsAsync(password, cancellationToken);
        await SeedParentsAsync(password, cancellationToken);
        await SeedTeamAsync(cancellationToken);
        await SeedBillingAsync(cancellationToken);
        await SeedWebsitesAsync(cancellationToken);
        await SeedCatalogueAsync(cancellationToken);
        await SeedEnrollmentsAsync(cancellationToken);
        await SeedAttendanceAsync(cancellationToken);
        await SeedExamsAsync(cancellationToken);
        await SeedHomeworkAsync(cancellationToken);
        await SeedCompletionAsync(cancellationToken);
        await SeedEngagementAsync(cancellationToken);
        await SeedCommunicationAsync(cancellationToken);
        await SeedTuitionPaymentsAsync(cancellationToken);
        await SeedAuditTrailAsync(cancellationToken);

        // Last, because it reads rows the modules above insert: anything an older seeding
        // run left pointing at a dead /storage/demo path gets a real URL.
        await BackfillLegacyMediaAsync(cancellationToken);

        logger.LogInformation(
            "Demo seeding finished: {Tenants} workspaces, {Teachers} teachers, {Students} students, {Parents} guardians.",
            _tenants.Count,
            _teachers.Count,
            _students.Count,
            _parents.Count);
    }

    // -----------------------------------------------------------------------
    // Shared helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Inserts the rows of <paramref name="candidates"/> that are not in the database yet.
    ///
    /// The existence check is a projected id query restricted to the candidate keys, so it
    /// costs one indexed seek per batch rather than reading the table. Because every
    /// candidate id is deterministic, "missing" means exactly "not seeded yet" — never "looks
    /// different from what the operator has since edited".
    /// </summary>
    private async Task<IReadOnlyList<TEntity>> AddMissingAsync<TEntity>(
        string entityName,
        IReadOnlyList<TEntity> candidates,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var ids = candidates.Select(candidate => candidate.Id).ToList();

        var present = (await context.Set<TEntity>()
                .AsNoTracking()
                .Where(entity => ids.Contains(entity.Id))
                .Select(entity => entity.Id)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var missing = candidates.Where(candidate => !present.Contains(candidate.Id)).ToList();

        if (missing.Count > 0)
        {
            await context.Set<TEntity>().AddRangeAsync(missing, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
        }

        report.Record(entityName, missing.Count, skipped: present.Count);
        return missing;
    }

    /// <summary>
    /// Returns the persisted rows for the candidate ids, inserting the missing ones first.
    /// Used where later modules need the actual tracked entities, not just a count.
    /// </summary>
    private async Task<List<TEntity>> EnsureAsync<TEntity>(
        string entityName,
        IReadOnlyList<TEntity> candidates,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        await AddMissingAsync(entityName, candidates, cancellationToken);

        var ids = candidates.Select(candidate => candidate.Id).ToList();

        return await context.Set<TEntity>()
            .Where(entity => ids.Contains(entity.Id))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Creates an identity account, or returns the one already seeded under the same id.
    ///
    /// Goes through <see cref="UserManager{TUser}"/> rather than inserting rows, so the demo
    /// accounts get the same password hashing, normalisation and role assignment as a real
    /// registration. The password never leaves this method.
    /// </summary>
    private async Task<ApplicationUser> EnsureUserAsync(
        Guid id,
        string email,
        string password,
        string roleName,
        Action<ApplicationUser> configure,
        CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var existing = await context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

        if (existing is not null)
        {
            _users[id] = existing;
            report.Record("ApplicationUsers", 0, skipped: 1);
            return existing;
        }

        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            IsActive = true,
            CreatedAt = _utcNow
        };

        configure(user);

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed the demo account " + email + ": "
                + string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        var roleResult = await userManager.AddToRoleAsync(user, roleName);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to assign role " + roleName + " to " + email + ": "
                + string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        _users[id] = user;
        report.Record("ApplicationUsers", 1);
        return user;
    }

    private static string WhatsAppLink(string phone) => "https://wa.me/" + phone.TrimStart('+');

    /// <summary>
    /// The short name <see cref="DemoMedia"/> keys its subject-matched media on. Separate
    /// from the subject's own slug because only the subjects the demo actually teaches need
    /// media, and the fallback has to be explicit rather than a missing dictionary entry.
    ///
    /// Takes a nullable id because a homework assignment's subject is optional — a general
    /// task belongs to the group rather than to one subject, and falls through to "general".
    /// </summary>
    private static string SubjectKey(Guid? subjectId) =>
        subjectId == PhysicsId ? "physics"
        : subjectId == MathematicsId ? "mathematics"
        : subjectId == ChemistryId ? "chemistry"
        : subjectId == BiologyId ? "biology"
        : subjectId == EnglishId ? "english"
        : subjectId == ArabicId ? "arabic"
        : subjectId == GeographyId ? "geography"
        : "general";
}
