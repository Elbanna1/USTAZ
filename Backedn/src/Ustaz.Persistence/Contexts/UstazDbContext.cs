using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Messaging;
using Ustaz.Domain.Entities.Tenancy;

namespace Ustaz.Persistence.Contexts;

/// <summary>
/// The single EF Core context for USTAZ. Entity configuration lives in
/// <see cref="IEntityTypeConfiguration{TEntity}"/> classes, never inline here.
/// </summary>
public class UstazDbContext(DbContextOptions<UstazDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IApplicationDbContext
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();

    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<EducationalGrade> EducationalGrades => Set<EducationalGrade>();

    public DbSet<TeacherSubject> TeacherSubjects => Set<TeacherSubject>();

    public DbSet<TeacherGrade> TeacherGrades => Set<TeacherGrade>();

    public DbSet<TeacherAchievement> TeacherAchievements => Set<TeacherAchievement>();

    public DbSet<TeacherSocialLink> TeacherSocialLinks => Set<TeacherSocialLink>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupCurriculum> GroupCurriculums => Set<GroupCurriculum>();

    public DbSet<GroupMaterial> GroupMaterials => Set<GroupMaterial>();

    public DbSet<GroupSchedule> GroupSchedules => Set<GroupSchedule>();

    public DbSet<Course> Courses => Set<Course>();

    public DbSet<CourseSection> CourseSections => Set<CourseSection>();

    public DbSet<CourseLesson> CourseLessons => Set<CourseLesson>();

    public DbSet<GroupEnrollment> GroupEnrollments => Set<GroupEnrollment>();

    public DbSet<CourseEnrollment> CourseEnrollments => Set<CourseEnrollment>();

    public DbSet<CourseLessonProgress> CourseLessonProgress => Set<CourseLessonProgress>();

    public DbSet<Discount> Discounts => Set<Discount>();

    public DbSet<Review> Reviews => Set<Review>();

    public DbSet<TeacherFavorite> TeacherFavorites => Set<TeacherFavorite>();

    public DbSet<CourseFavorite> CourseFavorites => Set<CourseFavorite>();

    public DbSet<Inquiry> Inquiries => Set<Inquiry>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<EmailDelivery> EmailDeliveries => Set<EmailDelivery>();

    // --- Multi-tenancy ---------------------------------------------------
    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantMember> TenantMembers => Set<TenantMember>();

    public DbSet<TenantMemberPermission> TenantMemberPermissions => Set<TenantMemberPermission>();

    public DbSet<TenantInvitation> TenantInvitations => Set<TenantInvitation>();

    public DbSet<TenantStudent> TenantStudents => Set<TenantStudent>();

    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();

    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();

    public DbSet<TenantDomain> TenantDomains => Set<TenantDomain>();

    public DbSet<TenantWebsite> TenantWebsites => Set<TenantWebsite>();

    public DbSet<WebsiteSection> WebsiteSections => Set<WebsiteSection>();

    public DbSet<Testimonial> Testimonials => Set<Testimonial>();

    // --- Education -------------------------------------------------------
    public DbSet<ParentProfile> ParentProfiles => Set<ParentProfile>();

    public DbSet<ParentStudentLink> ParentStudentLinks => Set<ParentStudentLink>();

    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();

    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();

    public DbSet<Exam> Exams => Set<Exam>();

    public DbSet<ExamResult> ExamResults => Set<ExamResult>();

    public DbSet<Certificate> Certificates => Set<Certificate>();

    public DbSet<ExamQuestion> ExamQuestions => Set<ExamQuestion>();

    public DbSet<ExamQuestionOption> ExamQuestionOptions => Set<ExamQuestionOption>();

    public DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    public DbSet<ExamAttemptAnswer> ExamAttemptAnswers => Set<ExamAttemptAnswer>();

    public DbSet<Homework> Homeworks => Set<Homework>();

    public DbSet<HomeworkAttachment> HomeworkAttachments => Set<HomeworkAttachment>();

    public DbSet<HomeworkSubmission> HomeworkSubmissions => Set<HomeworkSubmission>();

    public DbSet<Announcement> Announcements => Set<Announcement>();

    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<ActivityEvent> ActivityEvents => Set<ActivityEvent>();

    public DbSet<EducationalPayment> EducationalPayments => Set<EducationalPayment>();

    // --- Messaging -------------------------------------------------------
    public DbSet<Conversation> Conversations => Set<Conversation>();

    public DbSet<ConversationParticipant> ConversationParticipants => Set<ConversationParticipant>();

    public DbSet<Message> Messages => Set<Message>();

    public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

    // --- SaaS billing ----------------------------------------------------
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();

    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();

    public DbSet<SubscriptionEvent> SubscriptionEvents => Set<SubscriptionEvent>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<BillingTransaction> BillingTransactions => Set<BillingTransaction>();

    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Identity tables get friendlier names than the AspNet* defaults.
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<ApplicationRole>().ToTable("Roles");
    }
}
