using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Persistence.Contexts;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// The identifiers and addresses of the automated-test fixtures.
///
/// Public and fixed, so a test can assert against a row without first querying for it. Two
/// complete workspaces exist because almost every authorization test is really a question
/// about the second one: can the owner of workspace A read, change or delete something in
/// workspace B.
/// </summary>
public static class TestFixtureIds
{
    public static Guid PrimaryTenant => SeedId.For("test:tenant", "primary");

    public static Guid SecondaryTenant => SeedId.For("test:tenant", "secondary");

    public static Guid PrimaryTeacher => SeedId.For("test:teacher", "primary");

    public static Guid SecondaryTeacher => SeedId.For("test:teacher", "secondary");

    public static Guid PrimaryStudent => SeedId.For("test:student", "primary");

    public static Guid SecondaryStudent => SeedId.For("test:student", "secondary");

    public static Guid PrimaryParent => SeedId.For("test:parent", "primary");

    /// <summary>A guardian whose link to <see cref="PrimaryStudent"/> is not confirmed.</summary>
    public static Guid UnlinkedParent => SeedId.For("test:parent", "unlinked");

    public static Guid PrimaryGroup => SeedId.For("test:group", "primary");

    public static Guid SecondaryGroup => SeedId.For("test:group", "secondary");

    /// <summary>A draft group in the primary workspace: never visible publicly.</summary>
    public static Guid DraftGroup => SeedId.For("test:group", "draft");

    public static Guid PrimaryCourse => SeedId.For("test:course", "primary");

    public static Guid SecondaryCourse => SeedId.For("test:course", "secondary");

    public const string PrimaryTeacherEmail = "teacher.primary@ustaz.test";
    public const string SecondaryTeacherEmail = "teacher.secondary@ustaz.test";
    public const string PrimaryStudentEmail = "student.primary@ustaz.test";
    public const string SecondaryStudentEmail = "student.secondary@ustaz.test";
    public const string PrimaryParentEmail = "parent.primary@ustaz.test";
    public const string UnlinkedParentEmail = "parent.unlinked@ustaz.test";
}

/// <summary>
/// The fixed, minimal dataset the integration tests run against.
///
/// Deliberately small. A test suite that shares the demo dataset becomes coupled to every
/// future change in it, and a test that fails because somebody added a demo student is a test
/// nobody trusts. This writes only what the suite asserts on, with ids published in
/// <see cref="TestFixtureIds"/>.
///
/// Two workspaces, each with its own teacher, student, published group and published course,
/// plus a draft group and an unconfirmed guardian link. That is enough to cover ownership,
/// cross-tenant isolation, publication visibility and the guardian access rule.
/// </summary>
internal sealed class TestFixtureSeeder(
    UstazDbContext context,
    UserManager<ApplicationUser> userManager,
    SeedReport report)
{
    /// <summary>
    /// Used when no password is configured. Satisfies the identity policy and is only ever
    /// reachable from a Development or Testing environment, which <see cref="SeedSafety"/>
    /// enforces before this type is constructed.
    /// </summary>
    public const string DefaultPassword = "Fixture@1234";

    private static readonly Guid MathematicsId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid PhysicsId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid ThirdSecondaryId = Guid.Parse("22222222-0000-0000-0000-000000000005");

    private readonly DateTime _utcNow = new(2026, 1, 15, 9, 0, 0, DateTimeKind.Utc);

    public async Task SeedAsync(string password, CancellationToken cancellationToken)
    {
        var tenants = new List<Tenant>
        {
            Tenant(TestFixtureIds.PrimaryTenant, "test-primary", "Primary Test Workspace"),
            Tenant(TestFixtureIds.SecondaryTenant, "test-secondary", "Secondary Test Workspace")
        };

        await AddMissingAsync("Tenants", tenants, cancellationToken);

        var primaryTeacherUser = await EnsureUserAsync(
            SeedId.For("test:teacher-user", "primary"),
            TestFixtureIds.PrimaryTeacherEmail,
            password,
            RoleNames.Teacher,
            "Primary",
            "Teacher",
            cancellationToken);

        var secondaryTeacherUser = await EnsureUserAsync(
            SeedId.For("test:teacher-user", "secondary"),
            TestFixtureIds.SecondaryTeacherEmail,
            password,
            RoleNames.Teacher,
            "Secondary",
            "Teacher",
            cancellationToken);

        var primaryStudentUser = await EnsureUserAsync(
            SeedId.For("test:student-user", "primary"),
            TestFixtureIds.PrimaryStudentEmail,
            password,
            RoleNames.Student,
            "Primary",
            "Student",
            cancellationToken);

        var secondaryStudentUser = await EnsureUserAsync(
            SeedId.For("test:student-user", "secondary"),
            TestFixtureIds.SecondaryStudentEmail,
            password,
            RoleNames.Student,
            "Secondary",
            "Student",
            cancellationToken);

        var primaryParentUser = await EnsureUserAsync(
            SeedId.For("test:parent-user", "primary"),
            TestFixtureIds.PrimaryParentEmail,
            password,
            RoleNames.Parent,
            "Primary",
            "Parent",
            cancellationToken);

        var unlinkedParentUser = await EnsureUserAsync(
            SeedId.For("test:parent-user", "unlinked"),
            TestFixtureIds.UnlinkedParentEmail,
            password,
            RoleNames.Parent,
            "Unlinked",
            "Parent",
            cancellationToken);

        // Point the workspaces at their owners now that the accounts exist.
        var ownership = new[]
        {
            (TestFixtureIds.PrimaryTenant, primaryTeacherUser.Id),
            (TestFixtureIds.SecondaryTenant, secondaryTeacherUser.Id)
        };

        var trackedTenants = await context.Tenants
            .Where(tenant => tenant.Id == TestFixtureIds.PrimaryTenant
                || tenant.Id == TestFixtureIds.SecondaryTenant)
            .ToListAsync(cancellationToken);

        foreach (var (tenantId, ownerUserId) in ownership)
        {
            var tenant = trackedTenants.First(candidate => candidate.Id == tenantId);

            if (tenant.OwnerUserId != ownerUserId)
            {
                tenant.OwnerUserId = ownerUserId;
            }
        }

        await context.SaveChangesAsync(cancellationToken);

        await AddMissingAsync(
            "TenantSettings",
            [
                Settings("primary", TestFixtureIds.PrimaryTenant),
                Settings("secondary", TestFixtureIds.SecondaryTenant)
            ],
            cancellationToken);

        await AddMissingAsync(
            "TenantMembers",
            [
                Member("primary", TestFixtureIds.PrimaryTenant, primaryTeacherUser.Id),
                Member("secondary", TestFixtureIds.SecondaryTenant, secondaryTeacherUser.Id)
            ],
            cancellationToken);

        await AddMissingAsync(
            "TeacherProfiles",
            [
                Teacher(TestFixtureIds.PrimaryTeacher, primaryTeacherUser.Id, TestFixtureIds.PrimaryTenant, "Primary"),
                Teacher(TestFixtureIds.SecondaryTeacher, secondaryTeacherUser.Id, TestFixtureIds.SecondaryTenant, "Secondary")
            ],
            cancellationToken);

        await AddMissingAsync(
            "TeacherSubjects",
            [
                new TeacherSubject
                {
                    Id = SeedId.For("test:teacher-subject", TestFixtureIds.PrimaryTeacher, PhysicsId),
                    TeacherId = TestFixtureIds.PrimaryTeacher,
                    SubjectId = PhysicsId,
                    CreatedAt = _utcNow
                },
                new TeacherSubject
                {
                    Id = SeedId.For("test:teacher-subject", TestFixtureIds.SecondaryTeacher, MathematicsId),
                    TeacherId = TestFixtureIds.SecondaryTeacher,
                    SubjectId = MathematicsId,
                    CreatedAt = _utcNow
                }
            ],
            cancellationToken);

        await AddMissingAsync(
            "TeacherGrades",
            [
                new TeacherGrade
                {
                    Id = SeedId.For("test:teacher-grade", TestFixtureIds.PrimaryTeacher, ThirdSecondaryId),
                    TeacherId = TestFixtureIds.PrimaryTeacher,
                    GradeId = ThirdSecondaryId,
                    CreatedAt = _utcNow
                },
                new TeacherGrade
                {
                    Id = SeedId.For("test:teacher-grade", TestFixtureIds.SecondaryTeacher, ThirdSecondaryId),
                    TeacherId = TestFixtureIds.SecondaryTeacher,
                    GradeId = ThirdSecondaryId,
                    CreatedAt = _utcNow
                }
            ],
            cancellationToken);

        await AddMissingAsync(
            "StudentProfiles",
            [
                Student(TestFixtureIds.PrimaryStudent, primaryStudentUser.Id),
                Student(TestFixtureIds.SecondaryStudent, secondaryStudentUser.Id)
            ],
            cancellationToken);

        await AddMissingAsync(
            "ParentProfiles",
            [
                new ParentProfile
                {
                    Id = TestFixtureIds.PrimaryParent,
                    UserId = primaryParentUser.Id,
                    PhoneNumber = "+201500000001",
                    CreatedAt = _utcNow
                },
                new ParentProfile
                {
                    Id = TestFixtureIds.UnlinkedParent,
                    UserId = unlinkedParentUser.Id,
                    PhoneNumber = "+201500000002",
                    CreatedAt = _utcNow
                }
            ],
            cancellationToken);

        await AddMissingAsync(
            "ParentStudentLinks",
            [
                // Confirmed: this guardian may read the primary student's records.
                new ParentStudentLink
                {
                    Id = SeedId.For("test:parent-link", TestFixtureIds.PrimaryParent, TestFixtureIds.PrimaryStudent),
                    ParentId = TestFixtureIds.PrimaryParent,
                    StudentId = TestFixtureIds.PrimaryStudent,
                    Relationship = "Father",
                    IsConfirmed = true,
                    ConfirmedAt = _utcNow,
                    IsPrimaryContact = true,
                    CreatedAt = _utcNow
                },
                // Unconfirmed: this one may not, and that is what the access guard is for.
                new ParentStudentLink
                {
                    Id = SeedId.For("test:parent-link", TestFixtureIds.UnlinkedParent, TestFixtureIds.PrimaryStudent),
                    ParentId = TestFixtureIds.UnlinkedParent,
                    StudentId = TestFixtureIds.PrimaryStudent,
                    Relationship = "Uncle",
                    IsConfirmed = false,
                    IsPrimaryContact = false,
                    CreatedAt = _utcNow
                }
            ],
            cancellationToken);

        await AddMissingAsync(
            "Groups",
            [
                Group(TestFixtureIds.PrimaryGroup, TestFixtureIds.PrimaryTenant, TestFixtureIds.PrimaryTeacher,
                    PhysicsId, "Primary Physics Group", "test-primary-physics", isPublished: true),
                Group(TestFixtureIds.SecondaryGroup, TestFixtureIds.SecondaryTenant, TestFixtureIds.SecondaryTeacher,
                    MathematicsId, "Secondary Maths Group", "test-secondary-maths", isPublished: true),
                Group(TestFixtureIds.DraftGroup, TestFixtureIds.PrimaryTenant, TestFixtureIds.PrimaryTeacher,
                    PhysicsId, "Primary Draft Group", "test-primary-draft", isPublished: false)
            ],
            cancellationToken);

        await AddMissingAsync(
            "Courses",
            [
                Course(TestFixtureIds.PrimaryCourse, TestFixtureIds.PrimaryTenant, TestFixtureIds.PrimaryTeacher,
                    PhysicsId, "Primary Physics Course", "test-primary-physics-course"),
                Course(TestFixtureIds.SecondaryCourse, TestFixtureIds.SecondaryTenant, TestFixtureIds.SecondaryTeacher,
                    MathematicsId, "Secondary Maths Course", "test-secondary-maths-course")
            ],
            cancellationToken);

        await AddMissingAsync(
            "TenantStudents",
            [
                new TenantStudent
                {
                    Id = SeedId.For("test:tenant-student", TestFixtureIds.PrimaryTenant, TestFixtureIds.PrimaryStudent),
                    TenantId = TestFixtureIds.PrimaryTenant,
                    StudentId = TestFixtureIds.PrimaryStudent,
                    IsActive = true,
                    JoinedAt = _utcNow,
                    CreatedAt = _utcNow
                },
                new TenantStudent
                {
                    Id = SeedId.For("test:tenant-student", TestFixtureIds.SecondaryTenant, TestFixtureIds.SecondaryStudent),
                    TenantId = TestFixtureIds.SecondaryTenant,
                    StudentId = TestFixtureIds.SecondaryStudent,
                    IsActive = true,
                    JoinedAt = _utcNow,
                    CreatedAt = _utcNow
                }
            ],
            cancellationToken);

        await AddMissingAsync(
            "GroupEnrollments",
            [
                new GroupEnrollment
                {
                    Id = SeedId.For("test:group-enrollment", TestFixtureIds.PrimaryGroup, TestFixtureIds.PrimaryStudent),
                    GroupId = TestFixtureIds.PrimaryGroup,
                    StudentId = TestFixtureIds.PrimaryStudent,
                    Status = EnrollmentStatus.Active,
                    EnrolledAt = _utcNow,
                    PricePaid = 500m,
                    FinalPrice = 500m,
                    CreatedAt = _utcNow
                },
                new GroupEnrollment
                {
                    Id = SeedId.For("test:group-enrollment", TestFixtureIds.SecondaryGroup, TestFixtureIds.SecondaryStudent),
                    GroupId = TestFixtureIds.SecondaryGroup,
                    StudentId = TestFixtureIds.SecondaryStudent,
                    Status = EnrollmentStatus.Active,
                    EnrolledAt = _utcNow,
                    PricePaid = 500m,
                    FinalPrice = 500m,
                    CreatedAt = _utcNow
                }
            ],
            cancellationToken);
    }

    // -----------------------------------------------------------------------
    // Builders
    // -----------------------------------------------------------------------

    private Tenant Tenant(Guid id, string slug, string name) => new()
    {
        Id = id,
        Slug = slug,
        Name = name,
        DisplayName = name,
        Status = TenantStatus.Active,
        OwnerUserId = Guid.Empty,
        City = "Cairo",
        CreatedAt = _utcNow
    };

    private TenantSettings Settings(string key, Guid tenantId) => new()
    {
        Id = SeedId.For("test:tenant-settings", key),
        TenantId = tenantId,
        CreatedAt = _utcNow
    };

    private TenantMember Member(string key, Guid tenantId, Guid userId) => new()
    {
        Id = SeedId.For("test:member", key),
        TenantId = tenantId,
        UserId = userId,
        Role = TenantMemberRole.Owner,
        Status = TenantMemberStatus.Active,
        JoinedAt = _utcNow,
        CreatedAt = _utcNow
    };

    private TeacherProfile Teacher(Guid id, Guid userId, Guid tenantId, string label) => new()
    {
        Id = id,
        UserId = userId,
        TenantId = tenantId,
        Headline = label + " test teacher",
        Bio = label + " test teacher profile, long enough to satisfy the verification readiness rule "
              + "that requires a bio of at least eighty characters before submission.",
        YearsOfExperience = 5,
        City = "Cairo",
        WhatsAppNumber = "+201400000001",
        IsVerified = true,
        VerificationStatus = VerificationStatus.Verified,
        VerifiedAt = _utcNow,
        PreferredTeachingMode = TeachingMode.Hybrid,
        CreatedAt = _utcNow
    };

    private StudentProfile Student(Guid id, Guid userId) => new()
    {
        Id = id,
        UserId = userId,
        GradeId = ThirdSecondaryId,
        SchoolName = "Test School",
        City = "Cairo",
        CreatedAt = _utcNow
    };

    private Group Group(
        Guid id, Guid tenantId, Guid teacherId, Guid subjectId, string name, string slug, bool isPublished) => new()
    {
        Id = id,
        TenantId = tenantId,
        TeacherId = teacherId,
        SubjectId = subjectId,
        GradeId = ThirdSecondaryId,
        Name = name,
        Slug = slug,
        Description = name + " used by the integration tests.",
        Type = GroupType.Regular,
        TeachingMode = TeachingMode.Offline,
        Location = "Cairo",
        MonthlyPrice = 500m,
        Capacity = 10,
        EnrolledStudentsCount = 0,
        StartDate = _utcNow.Date,
        IsPublished = isPublished,
        IsActive = true,
        CreatedAt = _utcNow
    };

    private Course Course(Guid id, Guid tenantId, Guid teacherId, Guid subjectId, string title, string slug) => new()
    {
        Id = id,
        TenantId = tenantId,
        TeacherId = teacherId,
        SubjectId = subjectId,
        GradeId = ThirdSecondaryId,
        Title = title,
        Slug = slug,
        Description = title + " used by the integration tests.",
        Price = 300m,
        IsPublished = true,
        CreatedAt = _utcNow
    };

    private async Task AddMissingAsync<TEntity>(
        string entityName,
        IReadOnlyList<TEntity> candidates,
        CancellationToken cancellationToken)
        where TEntity : Domain.Common.BaseEntity
    {
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
    }

    private async Task<ApplicationUser> EnsureUserAsync(
        Guid id,
        string email,
        string password,
        string roleName,
        string firstName,
        string lastName,
        CancellationToken cancellationToken)
    {
        var existing = await context.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

        if (existing is not null)
        {
            report.Record("ApplicationUsers", 0, skipped: 1);
            return existing;
        }

        var user = new ApplicationUser
        {
            Id = id,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = firstName + " " + lastName,
            City = "Cairo",
            IsActive = true,
            IsVerified = roleName == RoleNames.Teacher,
            CreatedAt = _utcNow
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed the test fixture account " + email + ": "
                + string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, roleName);
        report.Record("ApplicationUsers", 1);
        return user;
    }
}
