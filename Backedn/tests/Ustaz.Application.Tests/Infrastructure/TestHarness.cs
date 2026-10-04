using Microsoft.EntityFrameworkCore;
using Moq;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Security;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Persistence.Contexts;

namespace Ustaz.Application.Tests.Infrastructure;

/// <summary>Minimal in-memory <see cref="ITenantContext"/> for handler tests.</summary>
public sealed class StubTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public string? TenantSlug { get; private set; }

    public bool HasTenant => TenantId is not null;

    public Guid RequireTenantId() =>
        TenantId ?? throw new Ustaz.Domain.Exceptions.ForbiddenAccessException("No workspace in context.");

    public void SetTenant(Guid tenantId, string? slug = null)
    {
        TenantId = tenantId;
        TenantSlug = slug;
    }

    public void Clear()
    {
        TenantId = null;
        TenantSlug = null;
    }
}

/// <summary>
/// Spins up an isolated in-memory database plus the ambient services handlers depend on.
/// Two complete workspaces are seeded so cross-tenant isolation can be tested for real.
/// </summary>
public sealed class TestHarness : IDisposable
{
    public TestHarness()
    {
        var options = new DbContextOptionsBuilder<UstazDbContext>()
            .UseInMemoryDatabase("ustaz-tests-" + Guid.NewGuid())
            .EnableSensitiveDataLogging()
            .Options;

        Context = new UstazDbContext(options);

        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(provider => provider.UtcNow).Returns(() => UtcNow);
        clock.SetupGet(provider => provider.TodayUtc).Returns(() => DateOnly.FromDateTime(UtcNow));
        Clock = clock.Object;

        CurrentUser = new Mock<ICurrentUserService>();
        Cache = new Mock<ICacheService>();
        Notifications = new Mock<INotificationDispatcher>();
        Media = new Mock<IMediaService>();
        Email = new Mock<IEmailService>();
        Audit = new Mock<IAuditLogger>();

        Seed();

        // Real services, not mocks: quota and workspace authorization are exactly what runs
        // in production, just pointed at the in-memory store.
        PlanLimits = new PlanLimitService(Context, Clock);
        TenantAuthorization = new TenantAuthorizationService(Context, CurrentUser.Object, TenantContextStub);
        ParentAccess = new ParentAccessGuard(Context, CurrentUser.Object);
        TenantProvisioning = new TenantProvisioningService(Context, Clock, PlanLimits);

        // The real issuer: certificates are part of the completion workflow, not a mock.
        CertificateIssuer = new Ustaz.Application.Features.Certificates.CertificateIssuer(
            Context, Clock, Notifications.Object);

        // The real recorder, writing to the in-memory store: type validation, visibility and
        // the deduplication rules are business behaviour and are exercised for real.
        Activity = new Ustaz.Application.Common.Activity.ActivityRecorder(
            Context,
            Clock,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                Ustaz.Application.Common.Activity.ActivityRecorder>.Instance);

        // The production dispatcher, pointed at the in-memory store: preferences,
        // deduplication and priority are business rules and deserve real coverage.
        Realtime = new RecordingRealtimeNotifier();

        // The real transactional email service, writing to an in-memory SMTP stand-in: the
        // catalog, the deduplication and the failure handling are all exercised for real.
        Emails = new RecordingEmailService();

        TransactionalEmail = new Ustaz.Infrastructure.Services.Email.TransactionalEmailService(
            Context,
            Emails,
            Clock,
            Microsoft.Extensions.Options.Options.Create(EmailOptions),
            new Ustaz.Infrastructure.Services.Common.NullCorrelationContext(),
            Presence,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                Ustaz.Infrastructure.Services.Email.TransactionalEmailService>.Instance);

        RealNotifications = new Ustaz.Infrastructure.Services.Messaging.NotificationDispatcher(
            Context,
            Clock,
            TransactionalEmail,
            Realtime,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<
                Ustaz.Infrastructure.Services.Messaging.NotificationDispatcher>.Instance);
    }

    public DateTime UtcNow { get; set; } = new(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    public UstazDbContext Context { get; }

    public IDateTimeProvider Clock { get; }

    public Mock<ICurrentUserService> CurrentUser { get; }

    public Mock<ICacheService> Cache { get; }

    public Mock<INotificationDispatcher> Notifications { get; }

    public Mock<IMediaService> Media { get; }

    public Mock<IEmailService> Email { get; }

    public Mock<IAuditLogger> Audit { get; }

    public StubTenantContext TenantContextStub { get; } = new();

    public PlanLimitService PlanLimits { get; }

    public TenantAuthorizationService TenantAuthorization { get; }

    public ParentAccessGuard ParentAccess { get; }

    public TenantProvisioningService TenantProvisioning { get; }

    public Ustaz.Application.Features.Certificates.CertificateIssuer CertificateIssuer { get; }

    /// <summary>The production activity recorder, writing to the in-memory database.</summary>
    public Ustaz.Application.Common.Activity.ActivityRecorder Activity { get; }

    /// <summary>The real dispatcher, writing to the in-memory database.</summary>
    public Ustaz.Infrastructure.Services.Messaging.NotificationDispatcher RealNotifications { get; }

    /// <summary>Settings the transactional email service runs with.</summary>
    public Ustaz.Infrastructure.Options.EmailOptions EmailOptions { get; } = new()
    {
        Enabled = true,
        ClientBaseUrl = "https://app.ustaz.test",
        SupportEmail = "support.webcraft.eg@gmail.com",
        FromName = "Ustaz",
        FromEmail = "support.webcraft.eg@gmail.com"
    };

    /// <summary>Who the platform believes is connected. Drives offline-only emails.</summary>
    public StubPresenceTracker Presence { get; } = new();

    /// <summary>Every email that reached the transport.</summary>
    public RecordingEmailService Emails { get; }

    public Ustaz.Infrastructure.Services.Email.TransactionalEmailService TransactionalEmail { get; }

    /// <summary>Captures what would have been pushed to connected clients.</summary>
    public RecordingRealtimeNotifier Realtime { get; }

    public Subject Subject { get; private set; } = null!;

    public Subject OtherSubject { get; private set; } = null!;

    public EducationalGrade Grade { get; private set; } = null!;

    public SubscriptionPlan Plan { get; private set; } = null!;

    public Tenant Tenant { get; private set; } = null!;

    public Tenant OtherTenant { get; private set; } = null!;

    public TeacherProfile Teacher { get; private set; } = null!;

    public TeacherProfile OtherTeacher { get; private set; } = null!;

    public StudentProfile Student { get; private set; } = null!;

    public ApplicationUser StudentUser { get; private set; } = null!;

    public Group Group { get; private set; } = null!;

    /// <summary>Points the ambient workspace at the given tenant (defaults to the main one).</summary>
    public void UseTenant(Tenant? tenant = null) => TenantContextStub.SetTenant((tenant ?? Tenant).Id);

    public void SignInAsStudent()
    {
        CurrentUser.SetupGet(user => user.UserId).Returns(Student.UserId);
        CurrentUser.SetupGet(user => user.StudentId).Returns(Student.Id);
        CurrentUser.SetupGet(user => user.TeacherId).Returns((Guid?)null);
        CurrentUser.SetupGet(user => user.ParentId).Returns((Guid?)null);
        CurrentUser.SetupGet(user => user.IsAdmin).Returns(false);
        CurrentUser.SetupGet(user => user.IsStudent).Returns(true);
        CurrentUser.Setup(user => user.RequireStudentId()).Returns(Student.Id);
        CurrentUser.Setup(user => user.RequireUserId()).Returns(Student.UserId);
    }

    public void SignInAsTeacher(TeacherProfile? teacher = null)
    {
        var profile = teacher ?? Teacher;

        CurrentUser.SetupGet(user => user.UserId).Returns(profile.UserId);
        CurrentUser.SetupGet(user => user.TeacherId).Returns(profile.Id);
        CurrentUser.SetupGet(user => user.StudentId).Returns((Guid?)null);
        CurrentUser.SetupGet(user => user.ParentId).Returns((Guid?)null);
        CurrentUser.SetupGet(user => user.IsAdmin).Returns(false);
        CurrentUser.SetupGet(user => user.IsTeacher).Returns(true);
        CurrentUser.SetupGet(user => user.TenantId).Returns(profile.TenantId);
        CurrentUser.Setup(user => user.RequireTeacherId()).Returns(profile.Id);
        CurrentUser.Setup(user => user.RequireUserId()).Returns(profile.UserId);

        TenantContextStub.SetTenant(profile.TenantId);
    }

    /// <summary>Signs in as a workspace member who is not the owner (an assistant, say).</summary>
    public void SignInAsMember(ApplicationUser user, Tenant tenant)
    {
        CurrentUser.SetupGet(current => current.UserId).Returns(user.Id);
        CurrentUser.SetupGet(current => current.TeacherId).Returns((Guid?)null);
        CurrentUser.SetupGet(current => current.StudentId).Returns((Guid?)null);
        CurrentUser.SetupGet(current => current.ParentId).Returns((Guid?)null);
        CurrentUser.SetupGet(current => current.IsAdmin).Returns(false);
        CurrentUser.SetupGet(current => current.TenantId).Returns(tenant.Id);
        CurrentUser.Setup(current => current.RequireUserId()).Returns(user.Id);

        TenantContextStub.SetTenant(tenant.Id);
    }

    public void SignInAsParent(ParentProfile parent)
    {
        CurrentUser.SetupGet(user => user.UserId).Returns(parent.UserId);
        CurrentUser.SetupGet(user => user.ParentId).Returns(parent.Id);
        CurrentUser.SetupGet(user => user.TeacherId).Returns((Guid?)null);
        CurrentUser.SetupGet(user => user.StudentId).Returns((Guid?)null);
        CurrentUser.SetupGet(user => user.IsAdmin).Returns(false);
        CurrentUser.SetupGet(user => user.IsParent).Returns(true);
        CurrentUser.Setup(user => user.RequireParentId()).Returns(parent.Id);
        CurrentUser.Setup(user => user.RequireUserId()).Returns(parent.UserId);
    }

    /// <summary>Creates an extra workspace member with the given role and permissions.</summary>
    public TenantMember AddMember(
        Tenant tenant,
        string email,
        TenantMemberRole role,
        params string[] permissions)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "Member",
            LastName = "Test",
            DisplayName = email,
            IsActive = true
        };

        var member = new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            User = user,
            Role = role,
            Status = TenantMemberStatus.Active,
            JoinedAt = UtcNow
        };

        foreach (var permission in permissions)
        {
            member.Permissions.Add(new TenantMemberPermission
            {
                Id = Guid.NewGuid(),
                TenantMemberId = member.Id,
                Permission = permission,
                IsGranted = true
            });
        }

        Context.Users.Add(user);
        Context.TenantMembers.Add(member);
        Context.SaveChanges();

        return member;
    }

    /// <summary>Creates a guardian, optionally linked to the seeded student.</summary>
    public ParentProfile AddParent(string email, Guid? linkedStudentId = null, bool confirmed = true)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "ولي",
            LastName = "أمر",
            DisplayName = "ولي أمر",
            IsActive = true
        };

        var parent = new ParentProfile { Id = Guid.NewGuid(), UserId = user.Id, User = user };

        if (linkedStudentId is not null)
        {
            parent.Children.Add(new ParentStudentLink
            {
                Id = Guid.NewGuid(),
                ParentId = parent.Id,
                StudentId = linkedStudentId.Value,
                IsConfirmed = confirmed,
                ConfirmedAt = confirmed ? UtcNow : null,
                Relationship = "Guardian"
            });
        }

        Context.Users.Add(user);
        Context.ParentProfiles.Add(parent);
        Context.SaveChanges();

        return parent;
    }

    private void Seed()
    {
        Plan = new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Code = "test-pro",
            NameAr = "احترافي",
            NameEn = "Pro",
            MonthlyPrice = 500m,
            TrialDays = 14,
            IsActive = true,
            IsPublic = true,
            MaxStudents = 100,
            MaxGroups = 10,
            MaxCourses = 10,
            MaxAssistants = 3,
            MaxTeamMembers = 5
        };

        foreach (var key in new[]
                 {
                     Ustaz.Domain.Constants.PlanFeatures.Attendance,
                     Ustaz.Domain.Constants.PlanFeatures.Exams,
                     Ustaz.Domain.Constants.PlanFeatures.Homework,
                     Ustaz.Domain.Constants.PlanFeatures.Messaging,
                     Ustaz.Domain.Constants.PlanFeatures.Announcements,
                     Ustaz.Domain.Constants.PlanFeatures.EducationalPayments
                 })
        {
            Plan.Features.Add(new PlanFeature
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId = Plan.Id,
                FeatureKey = key,
                IsEnabled = true
            });
        }

        Context.SubscriptionPlans.Add(Plan);

        Subject = new Subject
        {
            Id = Guid.NewGuid(), NameAr = "الفيزياء", NameEn = "Physics", Slug = "physics", IsActive = true
        };

        OtherSubject = new Subject
        {
            Id = Guid.NewGuid(), NameAr = "الأحياء", NameEn = "Biology", Slug = "biology", IsActive = true
        };

        Grade = new EducationalGrade
        {
            Id = Guid.NewGuid(),
            NameAr = "الثالث الثانوي",
            NameEn = "Third Secondary",
            Slug = "third-secondary",
            Stage = EducationalStage.Secondary,
            IsActive = true
        };

        Context.Subjects.AddRange(Subject, OtherSubject);
        Context.EducationalGrades.Add(Grade);

        Tenant = CreateTenant("ahmed-workspace", "ورشة أحمد");
        OtherTenant = CreateTenant("mariam-workspace", "ورشة مريم");

        Teacher = CreateTeacher("ahmed@ustaz.test", "أ. أحمد", Tenant);
        OtherTeacher = CreateTeacher("mariam@ustaz.test", "أ. مريم", OtherTenant);

        StudentUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "salma@ustaz.test",
            Email = "salma@ustaz.test",
            FirstName = "سلمى",
            LastName = "إبراهيم",
            DisplayName = "سلمى إبراهيم",
            IsActive = true
        };

        Student = new StudentProfile { Id = Guid.NewGuid(), UserId = StudentUser.Id, User = StudentUser };

        Group = new Group
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant.Id,
            TeacherId = Teacher.Id,
            SubjectId = Subject.Id,
            GradeId = Grade.Id,
            Name = "فيزياء الثانوية العامة",
            Slug = "physics-group",
            MonthlyPrice = 500m,
            Capacity = 2,
            EnrolledStudentsCount = 0,
            IsPublished = true,
            IsActive = true,
            StartDate = UtcNow.AddDays(7)
        };

        Group.Schedules.Add(new GroupSchedule
        {
            Id = Guid.NewGuid(),
            GroupId = Group.Id,
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeOnly(16, 0),
            EndTime = new TimeOnly(18, 0),
            IsActive = true
        });

        Context.Users.Add(StudentUser);
        Context.StudentProfiles.Add(Student);
        Context.Groups.Add(Group);

        Context.TenantStudents.Add(new TenantStudent
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant.Id,
            StudentId = Student.Id,
            IsActive = true,
            JoinedAt = UtcNow
        });

        Context.SaveChanges();
    }

    private Tenant CreateTenant(string slug, string name)
    {
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = slug,
            Name = name,
            Status = TenantStatus.Active,
            OwnerUserId = Guid.NewGuid(),
            Settings = new TenantSettings { Id = Guid.NewGuid(), TenantId = tenantId },
            Branding = new TenantBranding { Id = Guid.NewGuid(), TenantId = tenantId },
            Website = new TenantWebsite { Id = Guid.NewGuid(), TenantId = tenantId }
        };

        Context.Tenants.Add(tenant);

        Context.TenantSubscriptions.Add(new TenantSubscription
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriptionPlanId = Plan.Id,
            Status = SubscriptionStatus.Active,
            BillingCycle = BillingCycle.Monthly,
            Amount = Plan.MonthlyPrice,
            CurrentPeriodStart = UtcNow.AddDays(-1),
            CurrentPeriodEnd = UtcNow.AddDays(29)
        });

        return tenant;
    }

    private TeacherProfile CreateTeacher(string email, string displayName, Tenant tenant)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = displayName,
            LastName = "Test",
            DisplayName = displayName,
            IsActive = true,
            IsVerified = true
        };

        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TenantId = tenant.Id,
            IsVerified = true,
            VerificationStatus = VerificationStatus.Verified
        };

        profile.TeacherSubjects.Add(new TeacherSubject { Id = Guid.NewGuid(), TeacherId = profile.Id, SubjectId = Subject.Id });
        profile.TeacherGrades.Add(new TeacherGrade { Id = Guid.NewGuid(), TeacherId = profile.Id, GradeId = Grade.Id });

        // The teacher owns their workspace.
        tenant.OwnerUserId = user.Id;
        tenant.Members.Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            UserId = user.Id,
            Role = TenantMemberRole.Owner,
            Status = TenantMemberStatus.Active,
            JoinedAt = UtcNow
        });

        Context.Users.Add(user);
        Context.TeacherProfiles.Add(profile);

        return profile;
    }

    public void Dispose() => Context.Dispose();
}
