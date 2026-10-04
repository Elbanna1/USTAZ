using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Tenancy;
using Ustaz.Domain.Enums;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    private async Task<TeacherProfile> CreateTeacherAsync(
        string password,
        string email,
        string firstName,
        string lastName,
        string displayName,
        string headline,
        int yearsOfExperience,
        string city,
        string whatsApp,
        Guid[] subjectIds,
        Guid[] gradeIds,
        bool isFeatured)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName,
            DisplayName = displayName,
            City = city,
            IsActive = true,
            IsVerified = true,
            PhoneNumber = whatsApp
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Failed to seed demo teacher " + email + ": " +
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }

        await userManager.AddToRoleAsync(user, RoleNames.Teacher);

        var tenant = await CreateTenantAsync(user, displayName, city, whatsApp);

        var profile = new TeacherProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenant.Id,
            Headline = headline,
            Bio = displayName + " - " + headline + ". خبرة " + yearsOfExperience +
                  " سنة في التدريس مع التركيز على الفهم العميق وحل المسائل.",
            YearsOfExperience = yearsOfExperience,
            City = city,
            WhatsAppNumber = whatsApp,
            IsFeatured = isFeatured,
            IsVerified = true,
            VerificationStatus = VerificationStatus.Verified,
            VerifiedAt = _utcNow,
            PreferredTeachingMode = TeachingMode.Hybrid
        };

        foreach (var subjectId in subjectIds)
        {
            profile.TeacherSubjects.Add(new TeacherSubject { Id = Guid.NewGuid(), SubjectId = subjectId });
        }

        foreach (var gradeId in gradeIds)
        {
            profile.TeacherGrades.Add(new TeacherGrade { Id = Guid.NewGuid(), GradeId = gradeId });
        }

        profile.Achievements.Add(new TeacherAchievement
        {
            Id = Guid.NewGuid(),
            Title = "نتائج متميزة في الثانوية العامة",
            Description = "إشراف على طلاب حققوا مراكز متقدمة على مستوى المحافظة.",
            Year = _utcNow.Year - 1,
            SortOrder = 1
        });

        profile.SocialLinks.Add(new TeacherSocialLink
        {
            Id = Guid.NewGuid(),
            Platform = SocialPlatform.WhatsApp,
            Url = "https://wa.me/" + whatsApp.TrimStart('+'),
            SortOrder = 1
        });

        profile.SocialLinks.Add(new TeacherSocialLink
        {
            Id = Guid.NewGuid(),
            Platform = SocialPlatform.YouTube,
            Url = "https://www.youtube.com/@ustaz-demo",
            SortOrder = 2
        });

        await context.TeacherProfiles.AddAsync(profile);
        return profile;
    }

    /// <summary>Builds the demo teacher's workspace: tenant, owner membership and a trial plan.</summary>
    private async Task<Tenant> CreateTenantAsync(ApplicationUser owner, string displayName, string city, string whatsApp)
    {
        var tenantId = Guid.NewGuid();

        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = Slug.CreateUnique(displayName).Value,
            Name = displayName,
            DisplayName = displayName,
            Status = TenantStatus.Active,
            OwnerUserId = owner.Id,
            ContactEmail = owner.Email,
            ContactPhone = whatsApp,
            WhatsAppNumber = whatsApp,
            City = city,
            CreatedAt = _utcNow,
            Settings = new TenantSettings { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = _utcNow },
            Branding = new TenantBranding { Id = Guid.NewGuid(), TenantId = tenantId, CreatedAt = _utcNow },
            Website = new TenantWebsite
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IsPublished = true,
                HeroTitle = displayName,
                HeroSubtitle = "تعلم بثقة مع منصة أستاذ",
                AboutTitle = "نبذة عني",
                AboutBody = displayName + " - خبرة طويلة في التدريس وشرح مبسط يركز على الفهم.",
                ContactEmail = owner.Email,
                ContactPhone = whatsApp,
                WhatsAppNumber = whatsApp,
                SeoTitle = displayName + " | أستاذ",
                SeoDescription = "احجز مكانك في مجموعات " + displayName + " على منصة أستاذ.",
                CreatedAt = _utcNow
            }
        };

        tenant.Members.Add(new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = owner.Id,
            Role = TenantMemberRole.Owner,
            Status = TenantMemberStatus.Active,
            JoinedAt = _utcNow,
            CreatedAt = _utcNow
        });

        await context.Tenants.AddAsync(tenant);

        // Put every demo workspace on the professional plan so demo data exercises real quotas.
        var plan = await context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Code == "professional");

        if (plan is not null)
        {
            var subscription = new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SubscriptionPlanId = plan.Id,
                BillingCycle = BillingCycle.Monthly,
                Amount = plan.MonthlyPrice,
                Currency = plan.Currency,
                CreatedAt = _utcNow
            };

            subscription.MarkPaid(_utcNow, BillingCycle.Monthly, plan.MonthlyPrice);
            await context.TenantSubscriptions.AddAsync(subscription);
        }

        return tenant;
    }
}
