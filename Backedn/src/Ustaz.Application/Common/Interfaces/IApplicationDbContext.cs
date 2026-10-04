using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Billing;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Messaging;
using Ustaz.Domain.Entities.Tenancy;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// The persistence boundary the Application layer is allowed to see. Handlers compose
/// LINQ queries against these sets; the concrete DbContext lives in Ustaz.Persistence.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<TeacherProfile> TeacherProfiles { get; }

    DbSet<StudentProfile> StudentProfiles { get; }

    DbSet<Subject> Subjects { get; }

    DbSet<EducationalGrade> EducationalGrades { get; }

    DbSet<TeacherSubject> TeacherSubjects { get; }

    DbSet<TeacherGrade> TeacherGrades { get; }

    DbSet<TeacherAchievement> TeacherAchievements { get; }

    DbSet<TeacherSocialLink> TeacherSocialLinks { get; }

    DbSet<Group> Groups { get; }

    DbSet<GroupCurriculum> GroupCurriculums { get; }

    DbSet<GroupMaterial> GroupMaterials { get; }

    DbSet<GroupSchedule> GroupSchedules { get; }

    DbSet<Course> Courses { get; }

    DbSet<CourseSection> CourseSections { get; }

    DbSet<CourseLesson> CourseLessons { get; }

    DbSet<GroupEnrollment> GroupEnrollments { get; }

    DbSet<CourseEnrollment> CourseEnrollments { get; }

    DbSet<CourseLessonProgress> CourseLessonProgress { get; }

    DbSet<Discount> Discounts { get; }

    DbSet<Review> Reviews { get; }

    DbSet<TeacherFavorite> TeacherFavorites { get; }

    DbSet<CourseFavorite> CourseFavorites { get; }

    DbSet<Inquiry> Inquiries { get; }

    DbSet<Notification> Notifications { get; }

    DbSet<EmailDelivery> EmailDeliveries { get; }

    // --- Multi-tenancy ---------------------------------------------------
    DbSet<Tenant> Tenants { get; }

    DbSet<TenantMember> TenantMembers { get; }

    DbSet<TenantMemberPermission> TenantMemberPermissions { get; }

    DbSet<TenantInvitation> TenantInvitations { get; }

    DbSet<TenantStudent> TenantStudents { get; }

    DbSet<TenantSettings> TenantSettings { get; }

    DbSet<TenantBranding> TenantBrandings { get; }

    DbSet<TenantDomain> TenantDomains { get; }

    DbSet<TenantWebsite> TenantWebsites { get; }

    DbSet<WebsiteSection> WebsiteSections { get; }

    DbSet<Testimonial> Testimonials { get; }

    // --- Education -------------------------------------------------------
    DbSet<ParentProfile> ParentProfiles { get; }

    DbSet<ParentStudentLink> ParentStudentLinks { get; }

    DbSet<AttendanceSession> AttendanceSessions { get; }

    DbSet<AttendanceRecord> AttendanceRecords { get; }

    DbSet<Exam> Exams { get; }

    DbSet<ExamResult> ExamResults { get; }

    DbSet<Certificate> Certificates { get; }

    DbSet<ExamQuestion> ExamQuestions { get; }

    DbSet<ExamQuestionOption> ExamQuestionOptions { get; }

    DbSet<ExamAttempt> ExamAttempts { get; }

    DbSet<ExamAttemptAnswer> ExamAttemptAnswers { get; }

    DbSet<Homework> Homeworks { get; }

    DbSet<HomeworkAttachment> HomeworkAttachments { get; }

    DbSet<HomeworkSubmission> HomeworkSubmissions { get; }

    DbSet<Announcement> Announcements { get; }

    DbSet<NotificationPreference> NotificationPreferences { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<ActivityEvent> ActivityEvents { get; }

    DbSet<EducationalPayment> EducationalPayments { get; }

    // --- Messaging -------------------------------------------------------
    DbSet<Conversation> Conversations { get; }

    DbSet<ConversationParticipant> ConversationParticipants { get; }

    DbSet<Message> Messages { get; }

    DbSet<MessageAttachment> MessageAttachments { get; }

    // --- SaaS billing ----------------------------------------------------
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }

    DbSet<PlanFeature> PlanFeatures { get; }

    DbSet<TenantSubscription> TenantSubscriptions { get; }

    DbSet<SubscriptionEvent> SubscriptionEvents { get; }

    DbSet<Invoice> Invoices { get; }

    DbSet<BillingTransaction> BillingTransactions { get; }

    DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
