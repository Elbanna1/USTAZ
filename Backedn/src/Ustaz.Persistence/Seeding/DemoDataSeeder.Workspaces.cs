using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// The ten demo workspaces - one per teacher, because
    /// <c>UX_TeacherProfiles_TenantId</c> is unique: a teacher profile owns exactly one
    /// workspace and no workspace has two.
    ///
    /// That is also why the multi-teacher centre needs three workspaces rather than one. A
    /// teacher who works at somebody else's centre is a <see cref="TenantMember"/> of it, not
    /// a second teacher profile on its tenant, and they still own the workspace their own
    /// account came with. <c>SeedTeamAsync</c> is where that membership is written.
    ///
    /// The spread is deliberate: a complete published workspace, the centre, one with
    /// incomplete branding, one with nothing published, two belonging to teachers who are not
    /// verified yet and therefore cannot publish at all, and three that their owner never
    /// configured because they teach under the centre's name instead. Together they cover
    /// every state a workspace screen has to render.
    /// </summary>
    private static readonly (string Key, string Slug, string Name, string City, string Phone)[] WorkspaceDefinitions =
    [
        ("ahmed", "ahmed-physics", "أ. أحمد محمد", "القاهرة", "+201012345601"),
        ("mariam", "mariam-math", "أ. مريم حسن", "الجيزة", "+201012345602"),
        ("omar", "omar-chemistry", "أ. عمر خالد", "الإسكندرية", "+201012345603"),
        ("nour", "nour-english", "أ. نور أحمد", "المنصورة", "+201012345604"),
        ("center", "elite-center", "سنتر النخبة التعليمي", "القاهرة", "+201012345605"),
        ("tarek", "tarek-biology", "أ. طارق لطفي", "طنطا", "+201012345608"),
        ("dalia", "dalia-geography", "أ. داليا مصطفى", "أسيوط", "+201012345609"),

        // The centre's staff teachers and dalia's assistant. Each owns the workspace their
        // account came with and never set it up, because they teach under somebody else's
        // name - the "workspace that exists but was never configured" fixture.
        ("yasmin", "yasmin-biology", "أ. ياسمين سعد", "القاهرة", "+201012345606"),
        ("fatma", "fatma-arabic", "أ. فاطمة زكي", "القاهرة", "+201012345607"),
        ("sameh", "sameh-math", "أ. سامح نبيل", "أسيوط", "+201012345610")
    ];

    private async Task SeedWorkspacesAsync(CancellationToken cancellationToken)
    {
        var tenants = WorkspaceDefinitions
            .Select(definition => new Tenant
            {
                Id = SeedId.For("demo:tenant", definition.Key),
                Slug = definition.Slug,
                Name = definition.Name,
                DisplayName = definition.Name,
                Status = TenantStatus.Active,
                // Filled in by SeedWorkspaceOwnershipAsync once the owner account exists.
                OwnerUserId = Guid.Empty,
                ContactEmail = definition.Key + ".demo@ustaz.demo",
                ContactPhone = definition.Phone,
                WhatsAppNumber = definition.Phone,
                City = definition.City,
                CreatedAt = _utcNow.AddMonths(-8)
            })
            .ToList();

        var persisted = await EnsureAsync("Tenants", tenants, cancellationToken);

        foreach (var definition in WorkspaceDefinitions)
        {
            var id = SeedId.For("demo:tenant", definition.Key);
            var tenant = persisted.First(candidate => candidate.Id == id);
            _tenants[definition.Key] = tenant;
        }

        await SeedWorkspaceConfigurationAsync(cancellationToken);
    }

    /// <summary>
    /// Settings and branding, inserted as their own rows rather than as part of the tenant
    /// graph. Each is keyed off the tenant id, so a run that was interrupted between the two
    /// completes rather than leaving the workspace permanently half-configured.
    /// </summary>
    private async Task SeedWorkspaceConfigurationAsync(CancellationToken cancellationToken)
    {
        var settings = new List<TenantSettings>();
        var branding = new List<TenantBranding>();

        foreach (var (key, tenant) in _tenants)
        {
            settings.Add(new TenantSettings
            {
                Id = SeedId.For("demo:tenant-settings", key),
                TenantId = tenant.Id,
                DefaultLanguage = "ar",
                TimeZoneId = "Egypt Standard Time",
                Currency = "EGP",
                // The centre runs a stricter grade alert and a tighter lateness window than
                // the default, so the settings screen has something other than defaults to show.
                GradeNotificationThresholdPercentage = key == "center" ? 70 : 60,
                LateThresholdMinutes = key == "center" ? 5 : 10,
                NotifyParentsOnAbsence = true,
                NotifyParentsOnLowGrade = true,
                NotifyParentsOnHomeworkMissed = key != "omar",
                AllowStudentSelfEnrollment = key != "center",
                AutoApproveReviews = key != "center",
                CreatedAt = tenant.CreatedAt
            });

            // omar's workspace is the "incomplete branding" fixture: no logo, no cover, no
            // tagline, stock colours. Everything a branding screen has to cope with being
            // absent. The three staff teachers' own workspaces are unconfigured for the same
            // reason they are unpublished: they teach under the centre's branding, not theirs.
            var isIncomplete = key is "omar" or "dalia" or "yasmin" or "fatma" or "sameh";

            var primaryColor = key switch
            {
                "ahmed" => "#7C3AED",
                "mariam" => "#2563EB",
                "center" => "#0F766E",
                "nour" => "#0891B2",
                _ => "#2563EB"
            };

            branding.Add(new TenantBranding
            {
                Id = SeedId.For("demo:tenant-branding", key),
                TenantId = tenant.Id,
                // The logo and favicon are generated monograms in the workspace's own
                // primary colour: a wordmark, not a photograph, which is what a brand slot
                // wants and what an invented workspace can honestly have.
                LogoUrl = isIncomplete ? null : DemoMedia.Monogram(tenant.DisplayName ?? tenant.Name, primaryColor),
                CoverImageUrl = isIncomplete ? null : DemoMedia.BrandingCover(key),
                FaviconUrl = isIncomplete ? null : DemoMedia.Monogram(tenant.Slug, primaryColor),
                PrimaryColorHex = primaryColor,
                SecondaryColorHex = "#1E293B",
                AccentColorHex = "#F59E0B",
                FontFamily = isIncomplete ? null : "Cairo",
                Tagline = isIncomplete ? null : "تعلم بثقة",
                CreatedAt = tenant.CreatedAt
            });
        }

        await AddMissingAsync("TenantSettings", settings, cancellationToken);
        await AddMissingAsync("TenantBranding", branding, cancellationToken);
    }

    /// <summary>
    /// Points each workspace at its owner, then builds the membership rows. Runs after the
    /// teacher accounts exist, because <see cref="Tenant.OwnerUserId"/> and
    /// <see cref="TenantMember.UserId"/> both reference them.
    /// </summary>
    private async Task SeedWorkspaceOwnershipAsync(CancellationToken cancellationToken)
    {
        var owners = new Dictionary<string, string>
        {
            ["ahmed"] = "ahmed",
            ["mariam"] = "mariam",
            ["omar"] = "omar",
            ["nour"] = "nour",
            ["center"] = "hossam",
            ["tarek"] = "tarek",
            ["dalia"] = "dalia",
            ["yasmin"] = "yasmin",
            ["fatma"] = "fatma",
            ["sameh"] = "sameh"
        };

        var updated = 0;

        foreach (var (tenantKey, teacherKey) in owners)
        {
            var tenant = _tenants[tenantKey];
            var ownerUserId = _teachers[teacherKey].UserId;

            if (tenant.OwnerUserId == ownerUserId)
            {
                continue;
            }

            tenant.OwnerUserId = ownerUserId;
            tenant.ContactEmail = _users[ownerUserId].Email;
            updated++;
        }

        if (updated > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            report.Record("Tenants", 0, updated);
        }
    }

    /// <summary>
    /// The centre's team, which is where workspace roles and granular permissions are
    /// actually exercised. The assistant starts with nothing and is granted four capabilities
    /// explicitly — the only route by which an assistant gets any at all.
    /// </summary>
    private async Task SeedTeamAsync(CancellationToken cancellationToken)
    {
        var centre = _tenants["center"];
        var members = new List<TenantMember>();

        // Owner memberships for every workspace.
        foreach (var (tenantKey, tenant) in _tenants)
        {
            var ownerUserId = tenant.OwnerUserId;

            members.Add(new TenantMember
            {
                Id = SeedId.For("demo:member:owner", tenantKey),
                TenantId = tenant.Id,
                UserId = ownerUserId,
                Role = TenantMemberRole.Owner,
                Status = TenantMemberStatus.Active,
                Title = tenantKey == "center" ? "مدير السنتر" : "صاحب المساحة",
                JoinedAt = tenant.CreatedAt,
                CreatedAt = tenant.CreatedAt
            });
        }

        // The centre's staff: a workspace admin, an assistant, a moderator and a content
        // manager, plus the two teachers who work there.
        members.Add(Member("center-admin", centre.Id, _teachers["yasmin"].UserId, TenantMemberRole.Admin, "مدرس أول ومشرف أكاديمي"));
        members.Add(Member("center-assistant", centre.Id, _teachers["fatma"].UserId, TenantMemberRole.Assistant, "مساعد إداري"));
        members.Add(Member("center-moderator", centre.Id, _students["omnia"].UserId, TenantMemberRole.Moderator, "مشرف المحتوى المجتمعي"));
        members.Add(Member("center-content", centre.Id, _students["hassan"].UserId, TenantMemberRole.ContentManager, "مسؤول الموقع"));

        // The rejected-verification teacher sits inside dalia's workspace as an assistant.
        members.Add(Member("dalia-assistant", _tenants["dalia"].Id, _teachers["sameh"].UserId, TenantMemberRole.Assistant, "مدرس مساعد"));

        // An invited member who has not accepted: the "pending" row a team screen must render.
        members.Add(new TenantMember
        {
            Id = SeedId.For("demo:member", "center-invited"),
            TenantId = centre.Id,
            UserId = _students["malak"].UserId,
            Role = TenantMemberRole.Assistant,
            Status = TenantMemberStatus.Invited,
            Title = "مساعد (دعوة معلقة)",
            InvitedByUserId = centre.OwnerUserId,
            InvitedAt = _utcNow.AddDays(-3),
            CreatedAt = _utcNow.AddDays(-3)
        });

        // A member who was deactivated, so the inactive branch of every team query has a row.
        members.Add(new TenantMember
        {
            Id = SeedId.For("demo:member", "center-inactive"),
            TenantId = centre.Id,
            UserId = _students["amir"].UserId,
            Role = TenantMemberRole.Assistant,
            Status = TenantMemberStatus.Inactive,
            Title = "مساعد سابق",
            JoinedAt = _utcNow.AddMonths(-5),
            DeactivatedAt = _utcNow.AddMonths(-1),
            CreatedAt = _utcNow.AddMonths(-5)
        });

        await AddMissingAsync("TenantMembers", members, cancellationToken);

        await SeedMemberPermissionsAsync(cancellationToken);
        await SeedInvitationsAsync(cancellationToken);
        await SeedDomainsAsync(cancellationToken);

        TenantMember Member(string key, Guid tenantId, Guid userId, TenantMemberRole role, string title) =>
            new()
            {
                Id = SeedId.For("demo:member", key),
                TenantId = tenantId,
                UserId = userId,
                Role = role,
                Status = TenantMemberStatus.Active,
                Title = title,
                InvitedByUserId = centre.OwnerUserId,
                InvitedAt = _utcNow.AddMonths(-6),
                JoinedAt = _utcNow.AddMonths(-6).AddDays(1),
                CreatedAt = _utcNow.AddMonths(-6)
            };
    }

    /// <summary>
    /// Explicit grants for the centre's assistant. An assistant's role carries no permissions
    /// at all, so these rows are the entire reason that account can do anything — which makes
    /// them the fixture a permission test needs. Nothing owner-only is granted: the domain
    /// filters those out anyway, and seeding one would be seeding a misunderstanding.
    /// </summary>
    private async Task SeedMemberPermissionsAsync(CancellationToken cancellationToken)
    {
        var assistantMemberId = SeedId.For("demo:member", "center-assistant");
        var grantedBy = _tenants["center"].OwnerUserId;

        string[] granted =
        [
            Domain.Constants.TenantPermissions.StudentsView,
            Domain.Constants.TenantPermissions.AttendanceView,
            Domain.Constants.TenantPermissions.AttendanceManage,
            Domain.Constants.TenantPermissions.HomeworkView
        ];

        var permissions = granted
            .Select(permission => new TenantMemberPermission
            {
                Id = SeedId.For("demo:member-permission", "center-assistant:" + permission),
                TenantMemberId = assistantMemberId,
                Permission = permission,
                IsGranted = true,
                GrantedByUserId = grantedBy,
                CreatedAt = _utcNow.AddMonths(-6)
            })
            .ToList();

        await AddMissingAsync("TenantMemberPermissions", permissions, cancellationToken);
    }

    /// <summary>
    /// Pending, expired and accepted invitations. Only the SHA-256 hash of a token is ever
    /// stored, and these fixtures store a hash of a fixed demo string rather than a real
    /// token — the demo invitations are not redeemable, by design.
    /// </summary>
    private async Task SeedInvitationsAsync(CancellationToken cancellationToken)
    {
        var centre = _tenants["center"];

        var invitations = new List<TenantInvitation>
        {
            new()
            {
                Id = SeedId.For("demo:invitation", "center-pending"),
                TenantId = centre.Id,
                Email = "new.assistant@ustaz.demo",
                Role = TenantMemberRole.Assistant,
                Title = "مساعد جديد",
                TokenHash = DemoTokenHash("center-pending"),
                ExpiresAt = _utcNow.AddDays(5),
                InvitedByUserId = centre.OwnerUserId,
                PermissionsCsv = string.Join(
                    ',',
                    Domain.Constants.TenantPermissions.StudentsView,
                    Domain.Constants.TenantPermissions.HomeworkView),
                CreatedAt = _utcNow.AddDays(-2)
            },
            new()
            {
                Id = SeedId.For("demo:invitation", "center-expired"),
                TenantId = centre.Id,
                Email = "late.reply@ustaz.demo",
                Role = TenantMemberRole.Moderator,
                TokenHash = DemoTokenHash("center-expired"),
                ExpiresAt = _utcNow.AddDays(-4),
                InvitedByUserId = centre.OwnerUserId,
                CreatedAt = _utcNow.AddDays(-11)
            },
            new()
            {
                Id = SeedId.For("demo:invitation", "center-accepted"),
                TenantId = centre.Id,
                Email = "yasmin.saad@ustaz.demo",
                Role = TenantMemberRole.Admin,
                TokenHash = DemoTokenHash("center-accepted"),
                ExpiresAt = _utcNow.AddMonths(-6).AddDays(7),
                AcceptedAt = _utcNow.AddMonths(-6).AddDays(1),
                InvitedByUserId = centre.OwnerUserId,
                CreatedAt = _utcNow.AddMonths(-6)
            },
            new()
            {
                Id = SeedId.For("demo:invitation", "center-revoked"),
                TenantId = centre.Id,
                Email = "withdrawn@ustaz.demo",
                Role = TenantMemberRole.Assistant,
                TokenHash = DemoTokenHash("center-revoked"),
                ExpiresAt = _utcNow.AddDays(3),
                RevokedAt = _utcNow.AddDays(-1),
                InvitedByUserId = centre.OwnerUserId,
                CreatedAt = _utcNow.AddDays(-6)
            }
        };

        await AddMissingAsync("TenantInvitations", invitations, cancellationToken);
    }

    /// <summary>
    /// Custom domains, one per workspace (the schema allows exactly one).
    ///
    /// The hosts are fictional and unregistered. Verification state is set directly because
    /// these are fixtures for the three branches a domain screen renders; no DNS proof is
    /// fabricated, and nothing here changes how a real domain gets verified — that still runs
    /// a live TXT lookup.
    /// </summary>
    private async Task SeedDomainsAsync(CancellationToken cancellationToken)
    {
        var domains = new List<TenantDomain>
        {
            new()
            {
                Id = SeedId.For("demo:domain", "ahmed"),
                TenantId = _tenants["ahmed"].Id,
                Host = "physics.ustaz-demo.invalid",
                IsPrimary = true,
                VerificationStatus = DomainVerificationStatus.Verified,
                VerificationToken = "ustaz-verify-demo-ahmed",
                VerifiedAt = _utcNow.AddMonths(-4),
                VerificationAttempts = 1,
                CreatedAt = _utcNow.AddMonths(-4).AddDays(-1)
            },
            new()
            {
                Id = SeedId.For("demo:domain", "center"),
                TenantId = _tenants["center"].Id,
                Host = "elite.ustaz-demo.invalid",
                IsPrimary = false,
                VerificationStatus = DomainVerificationStatus.Pending,
                VerificationToken = "ustaz-verify-demo-center",
                CreatedAt = _utcNow.AddDays(-2)
            },
            new()
            {
                Id = SeedId.For("demo:domain", "omar"),
                TenantId = _tenants["omar"].Id,
                Host = "chemistry.ustaz-demo.invalid",
                IsPrimary = false,
                VerificationStatus = DomainVerificationStatus.Failed,
                VerificationToken = "ustaz-verify-demo-omar",
                LastVerificationError = "TXT record not found at _ustaz-verify.chemistry.ustaz-demo.invalid",
                VerificationAttempts = 3,
                CreatedAt = _utcNow.AddDays(-9)
            }
        };

        await AddMissingAsync("TenantDomains", domains, cancellationToken);
    }

    /// <summary>
    /// A stable stand-in for an invitation token hash. Not derived from a usable token: the
    /// demo invitations exist to be listed, not accepted.
    /// </summary>
    private static string DemoTokenHash(string key) =>
        Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("ustaz-demo-invitation-not-redeemable:" + key)));
}
