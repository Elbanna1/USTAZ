using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// Tuition: a family paying a teacher or a centre.
    ///
    /// Deliberately separate from the SaaS billing module — that one records a workspace
    /// paying USTAZ, this one records a student paying the workspace. The two never share a
    /// table, a number sequence or a provider record, and the seeded numbers reflect that:
    /// tuition invoices run <c>T-{year}-{nnnnn}</c> per workspace, SaaS invoices run
    /// <c>USTAZ-{year}-{nnnnnn}</c> platform-wide.
    ///
    /// The common case in an Egyptian centre is cash handed over at the door, so most rows
    /// carry <c>cash</c> as the provider. No gateway is contacted and no charge is created:
    /// a cash payment is a record of something that happened offline, which is exactly what
    /// makes it safe to seed.
    /// </summary>
    private async Task SeedTuitionPaymentsAsync(CancellationToken cancellationToken)
    {
        var payments = new List<EducationalPayment>();
        var sequencePerTenant = new Dictionary<Guid, int>();

        // student, group, month offset, status, provider, paid by a guardian?
        (string StudentKey, string GroupKey, int MonthsAgo, PaymentStatus Status, string Provider, string? ParentKey)[] plan =
        [
            ("salma", "physics-3sec-intensive", 2, PaymentStatus.Paid, "cash", "ibrahim"),
            ("salma", "physics-3sec-intensive", 1, PaymentStatus.Paid, "instapay", "ibrahim"),
            // This month's tuition, not yet settled.
            ("salma", "physics-3sec-intensive", 0, PaymentStatus.Pending, "cash", "ibrahim"),

            ("youssef", "physics-3sec-intensive", 1, PaymentStatus.Paid, "cash", "ibrahim"),
            ("youssef", "physics-3sec-intensive", 0, PaymentStatus.Pending, "cash", "ibrahim"),

            ("zeyad", "physics-3sec-intensive", 1, PaymentStatus.Paid, "fawry", null),
            ("omnia", "physics-3sec-intensive", 1, PaymentStatus.Paid, "cash", null),

            ("habiba", "chemistry-2sec", 1, PaymentStatus.Paid, "cash", "samir"),
            // Refunded after the student left the group.
            ("aya", "chemistry-2sec", 1, PaymentStatus.Refunded, "instapay", null),

            ("omnia", "center-math-3sec", 2, PaymentStatus.Paid, "cash", null),
            ("omnia", "center-math-3sec", 1, PaymentStatus.Paid, "cash", null),
            ("hassan", "center-math-3sec", 1, PaymentStatus.Paid, "instapay", null),
            // Failed card attempt: the workspace sees an unsettled month.
            ("malak", "center-math-3sec", 1, PaymentStatus.Failed, "fawry", null)
        ];

        foreach (var (studentKey, groupKey, monthsAgo, status, provider, parentKey) in plan)
        {
            var student = _students[studentKey];
            var group = _groups[groupKey];
            var enrollment = _groupEnrollments[groupKey + "/" + studentKey];

            sequencePerTenant[group.TenantId] = sequencePerTenant.GetValueOrDefault(group.TenantId) + 1;
            var sequence = sequencePerTenant[group.TenantId];

            var periodStart = new DateTime(_utcNow.Year, _utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(-monthsAgo);

            var payment = new EducationalPayment
            {
                Id = SeedId.For("demo:tuition", group.Id, student.Id, monthsAgo),
                TenantId = group.TenantId,
                StudentId = student.Id,
                ParentId = parentKey is null ? null : _parents[parentKey].Id,
                Type = EducationalPaymentType.MonthlySubscription,
                GroupId = group.Id,
                GroupEnrollmentId = enrollment.Id,
                InvoiceNumber = "T-" + periodStart.Year + "-" + (90000 + sequence).ToString("D5"),
                Amount = group.MonthlyPrice,
                DiscountAmount = enrollment.DiscountAmount,
                Currency = "EGP",
                DiscountId = enrollment.DiscountId,
                DiscountCode = enrollment.DiscountCode,
                Status = PaymentStatus.Pending,
                Provider = provider,
                // Unique per (Provider, ProviderTransactionId) where present. Cash has no
                // provider reference, so those rows leave it null rather than inventing one.
                ProviderTransactionId = provider == "cash"
                    ? null
                    : "demo-tuition-" + group.Id.ToString("N")[..8] + "-" + student.Id.ToString("N")[..8] + "-" + monthsAgo,
                PeriodMonth = DateOnly.FromDateTime(periodStart),
                DueAt = periodStart.AddDays(7),
                Notes = status == PaymentStatus.Refunded ? "تم الاسترداد بعد إلغاء الاشتراك." : null,
                RecordedByUserId = _teachers.Values.First(teacher => teacher.Id == group.TeacherId).UserId,
                CreatedAt = periodStart
            };

            payment.RecalculateNet();

            switch (status)
            {
                case PaymentStatus.Paid:
                    payment.MarkPaid(periodStart.AddDays(3), provider, payment.ProviderTransactionId);
                    break;

                case PaymentStatus.Refunded:
                    payment.MarkPaid(periodStart.AddDays(2), provider, payment.ProviderTransactionId);
                    payment.Refund(periodStart.AddDays(20), "إلغاء الاشتراك بناءً على طلب الطالب.");
                    break;

                case PaymentStatus.Failed:
                    payment.Status = PaymentStatus.Failed;
                    payment.Notes = "فشلت عملية الدفع الإلكتروني.";
                    payment.UpdatedAt = periodStart.AddDays(4);
                    break;

                case PaymentStatus.Pending:
                default:
                    break;
            }

            payments.Add(payment);
        }

        // One course purchase, so the course-enrollment payment type is represented too.
        var coursePayment = BuildCoursePayment(sequencePerTenant);

        if (coursePayment is not null)
        {
            payments.Add(coursePayment);
        }

        await AddMissingAsync("EducationalPayments", payments, cancellationToken);
    }

    private EducationalPayment? BuildCoursePayment(Dictionary<Guid, int> sequencePerTenant)
    {
        if (!_courseEnrollments.TryGetValue("physics-modern/salma", out var enrollment))
        {
            return null;
        }

        var course = _courses["physics-modern"];
        var student = _students["salma"];

        sequencePerTenant[course.TenantId] = sequencePerTenant.GetValueOrDefault(course.TenantId) + 1;
        var paidAt = enrollment.EnrolledAt;

        var payment = new EducationalPayment
        {
            Id = SeedId.For("demo:tuition-course", course.Id, student.Id),
            TenantId = course.TenantId,
            StudentId = student.Id,
            ParentId = _parents["ibrahim"].Id,
            Type = EducationalPaymentType.CourseEnrollment,
            CourseId = course.Id,
            CourseEnrollmentId = enrollment.Id,
            InvoiceNumber = "T-" + paidAt.Year + "-" + (90000 + sequencePerTenant[course.TenantId]).ToString("D5"),
            Amount = course.Price,
            DiscountAmount = 0m,
            Currency = "EGP",
            Status = PaymentStatus.Pending,
            Provider = "instapay",
            ProviderTransactionId = "demo-tuition-course-" + course.Id.ToString("N")[..8],
            DueAt = paidAt,
            Notes = "شراء كورس مسجل.",
            RecordedByUserId = _teachers["ahmed"].UserId,
            CreatedAt = paidAt
        };

        payment.RecalculateNet();
        payment.MarkPaid(paidAt, "instapay", payment.ProviderTransactionId);
        return payment;
    }

    /// <summary>
    /// A handful of audit entries for the sensitive actions the demo data implies happened:
    /// a verification decision, a permission grant, a refund.
    ///
    /// Values are the redacted JSON the real logger writes. Nothing here carries a password,
    /// a token or a full document body — the audit log is a record of who did what, not a
    /// copy of the data they touched.
    /// </summary>
    private async Task SeedAuditTrailAsync(CancellationToken cancellationToken)
    {
        var ahmed = _tenants["ahmed"];
        var centre = _tenants["center"];
        var omar = _tenants["omar"];

        var logs = new List<AuditLog>
        {
            new()
            {
                Id = SeedId.For("demo:audit", "teacher-verified-ahmed"),
                TenantId = ahmed.Id,
                UserId = null,
                UserEmail = "platform-admin@ustaz.demo",
                Action = AuditActions.AdminTeacherVerified,
                EntityName = nameof(Domain.Entities.TeacherProfile),
                EntityId = _teachers["ahmed"].Id.ToString(),
                NewValues = "{\"VerificationStatus\":\"Verified\"}",
                IpAddress = "203.0.113.10",
                UserAgent = "USTAZ Admin Console (demo)",
                Succeeded = true,
                CreatedAt = _utcNow.AddMonths(-7)
            },
            new()
            {
                Id = SeedId.For("demo:audit", "verification-rejected-sameh"),
                TenantId = _tenants["dalia"].Id,
                UserEmail = "platform-admin@ustaz.demo",
                Action = AuditActions.AdminTeacherVerified,
                EntityName = nameof(Domain.Entities.TeacherProfile),
                EntityId = _teachers["sameh"].Id.ToString(),
                NewValues = "{\"VerificationStatus\":\"Rejected\"}",
                IpAddress = "203.0.113.10",
                UserAgent = "USTAZ Admin Console (demo)",
                Succeeded = true,
                CreatedAt = _utcNow.AddDays(-9)
            },
            new()
            {
                Id = SeedId.For("demo:audit", "permission-granted-center"),
                TenantId = centre.Id,
                UserId = centre.OwnerUserId,
                UserEmail = _users[centre.OwnerUserId].Email,
                Action = AuditActions.PermissionsChanged,
                EntityName = nameof(Domain.Entities.Tenancy.TenantMember),
                EntityId = SeedId.For("demo:member", "center-assistant").ToString(),
                NewValues = "{\"Permissions\":[\"Students.View\",\"Attendance.View\",\"Attendance.Manage\",\"Homework.View\"]}",
                IpAddress = "198.51.100.24",
                Succeeded = true,
                CreatedAt = _utcNow.AddMonths(-6)
            },
            new()
            {
                Id = SeedId.For("demo:audit", "payment-refunded"),
                TenantId = omar.Id,
                UserId = omar.OwnerUserId,
                UserEmail = _users[omar.OwnerUserId].Email,
                Action = AuditActions.PaymentRefunded,
                EntityName = nameof(EducationalPayment),
                EntityId = SeedId.For("demo:tuition", _groups["chemistry-2sec"].Id, _students["aya"].Id, 1).ToString(),
                OldValues = "{\"Status\":\"Paid\"}",
                NewValues = "{\"Status\":\"Refunded\"}",
                IpAddress = "198.51.100.77",
                Succeeded = true,
                CreatedAt = _utcNow.AddDays(-11)
            },
            // A failed attempt, because an audit log that only records successes is not one.
            new()
            {
                Id = SeedId.For("demo:audit", "failed-permission-escalation"),
                TenantId = centre.Id,
                UserId = _teachers["fatma"].UserId,
                UserEmail = _users[_teachers["fatma"].UserId].Email,
                Action = AuditActions.PermissionsChanged,
                EntityName = nameof(Domain.Entities.Tenancy.TenantMember),
                EntityId = SeedId.For("demo:member", "center-assistant").ToString(),
                Succeeded = false,
                FailureReason = "Forbidden: Team.Manage is an owner-only permission.",
                IpAddress = "198.51.100.91",
                CreatedAt = _utcNow.AddDays(-4)
            }
        };

        await AddMissingAsync("AuditLogs", logs, cancellationToken);
    }
}
