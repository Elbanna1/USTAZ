using Microsoft.EntityFrameworkCore;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Messaging;
using Ustaz.Domain.Enums;

namespace Ustaz.Persistence.Seeding;

internal sealed partial class DemoDataSeeder
{
    /// <summary>
    /// In-app notifications and private message threads.
    ///
    /// Nothing is dispatched: these are the stored records a notification bell and an inbox
    /// read, written directly. No email, SMS, WhatsApp or push leaves the process during
    /// seeding, which is the whole reason this module writes rows instead of calling the
    /// dispatcher.
    ///
    /// Every thread's participants are people who have a real relationship in the seeded
    /// data — a guardian and their child's teacher, a student and a teacher they are enrolled
    /// with. Access to a conversation is decided by participation, so a thread between two
    /// strangers would be a fixture that proves nothing.
    /// </summary>
    private async Task SeedCommunicationAsync(CancellationToken cancellationToken)
    {
        await SeedNotificationsAsync(cancellationToken);
        await SeedConversationsAsync(cancellationToken);
    }

    private async Task SeedNotificationsAsync(CancellationToken cancellationToken)
    {
        var notifications = new List<Notification>();

        // recipient, title, body, type, category, read, priority
        (string Key, Guid UserId, Guid? TenantId, string Title, string Message, NotificationType Type,
            NotificationCategory Category, bool IsRead, NotificationPriority Priority, string Event)[] plan =
        [
            ("salma-homework-graded", _students["salma"].UserId, _tenants["ahmed"].Id,
                "تم تصحيح واجبك", "حصلت على 6 من 10 في واجب الوحدة الأولى - الميكانيكا.",
                NotificationType.Homework, NotificationCategory.Homework, false, NotificationPriority.Normal,
                "homework.graded"),

            ("salma-exam-result", _students["salma"].UserId, _tenants["ahmed"].Id,
                "نتيجة امتحان منتصف الترم", "تم إعلان نتيجة امتحان منتصف الترم - الفيزياء.",
                NotificationType.Exam, NotificationCategory.Exam, false, NotificationPriority.High,
                "exam.results_published"),

            ("salma-announcement", _students["salma"].UserId, _tenants["ahmed"].Id,
                "مواعيد المراجعة النهائية", "تبدأ المراجعات النهائية الأسبوع القادم.",
                NotificationType.Announcement, NotificationCategory.Announcement, true, NotificationPriority.Normal,
                "announcement.published"),

            ("youssef-certificate", _students["youssef"].UserId, _tenants["ahmed"].Id,
                "تم إصدار شهادتك", "مبروك! أكملت كورس الفيزياء الحديثة من الصفر وتم إصدار شهادتك.",
                NotificationType.Certificate, NotificationCategory.Certificate, false, NotificationPriority.High,
                "certificate.issued"),

            ("youssef-absence", _students["youssef"].UserId, _tenants["ahmed"].Id,
                "تسجيل غياب", "تم تسجيل غيابك في حصة الفيزياء.",
                NotificationType.Attendance, NotificationCategory.Attendance, true, NotificationPriority.Normal,
                "attendance.absent"),

            ("ibrahim-absence", _parents["ibrahim"].UserId, _tenants["ahmed"].Id,
                "غياب الطالبة سلمى", "لم تحضر سلمى حصة الفيزياء اليوم.",
                NotificationType.Attendance, NotificationCategory.Attendance, false, NotificationPriority.High,
                "attendance.absent_parent"),

            ("ibrahim-low-grade", _parents["ibrahim"].UserId, _tenants["ahmed"].Id,
                "درجة منخفضة", "حصلت سلمى على درجة أقل من الحد المتوقع في الواجب الأخير.",
                NotificationType.Homework, NotificationCategory.Grade, false, NotificationPriority.High,
                "grade.below_threshold"),

            ("ahmed-enrollment", _teachers["ahmed"].UserId, _tenants["ahmed"].Id,
                "طلب انضمام جديد", "كريم عادل طلب الانضمام لمجموعة الفيزياء المكثفة.",
                NotificationType.Enrollment, NotificationCategory.Enrollment, false, NotificationPriority.Normal,
                "enrollment.requested"),

            ("ahmed-review", _teachers["ahmed"].UserId, _tenants["ahmed"].Id,
                "تقييم جديد", "حصلت على تقييم 5 نجوم من إحدى طالباتك.",
                NotificationType.Review, NotificationCategory.System, true, NotificationPriority.Low,
                "review.received"),

            ("ahmed-inquiry", _teachers["ahmed"].UserId, _tenants["ahmed"].Id,
                "استفسار جديد", "وصلك استفسار جديد عن مجموعة أولى ثانوي.",
                NotificationType.Inquiry, NotificationCategory.System, false, NotificationPriority.Normal,
                "inquiry.received"),

            ("omar-payment-failed", _teachers["omar"].UserId, _tenants["omar"].Id,
                "فشل تجديد الاشتراك", "لم نتمكن من تحصيل قيمة اشتراك المنصة. يرجى تحديث طريقة الدفع.",
                NotificationType.Subscription, NotificationCategory.Subscription, false, NotificationPriority.Critical,
                "subscription.payment_failed"),

            ("mariam-trial", _teachers["mariam"].UserId, _tenants["mariam"].Id,
                "فترتك التجريبية", "تبقى من فترتك التجريبية 11 يوماً.",
                NotificationType.Subscription, NotificationCategory.Subscription, true, NotificationPriority.Normal,
                "subscription.trial_reminder"),

            ("tarek-verification", _teachers["tarek"].UserId, _tenants["tarek"].Id,
                "طلب التوثيق قيد المراجعة", "فريق أستاذ يراجع مستنداتك حالياً.",
                NotificationType.Verification, NotificationCategory.System, false, NotificationPriority.Normal,
                "verification.under_review"),

            ("sameh-verification", _teachers["sameh"].UserId, _tenants["dalia"].Id,
                "تم رفض طلب التوثيق", "المستندات المرفقة غير واضحة. يرجى إعادة الإرسال.",
                NotificationType.Verification, NotificationCategory.System, false, NotificationPriority.High,
                "verification.rejected")
        ];

        foreach (var entry in plan)
        {
            var createdAt = _utcNow.AddHours(-plan.Length + Array.IndexOf(plan, entry));

            notifications.Add(new Notification
            {
                Id = SeedId.For("demo:notification", entry.Key),
                UserId = entry.UserId,
                TenantId = entry.TenantId,
                Title = entry.Title,
                Message = entry.Message,
                Type = entry.Type,
                Category = entry.Category,
                IsRead = entry.IsRead,
                ReadAt = entry.IsRead ? createdAt.AddMinutes(30) : null,
                Priority = entry.Priority,
                Event = entry.Event,
                // Unique per (UserId, DeduplicationKey): the index is what stops a retry
                // delivering the same alert twice.
                DeduplicationKey = "demo:" + entry.Key,
                CreatedAt = createdAt
            });
        }

        await AddMissingAsync("Notifications", notifications, cancellationToken);
    }

    private async Task SeedConversationsAsync(CancellationToken cancellationToken)
    {
        var conversations = new List<Conversation>();
        var participants = new List<ConversationParticipant>();
        var messages = new List<Message>();
        var attachments = new List<MessageAttachment>();

        // --- Guardian and teacher ------------------------------------------------
        var parentThread = Thread(
            "parent-teacher",
            _tenants["ahmed"],
            ConversationType.ParentTeacher,
            "متابعة مستوى سلمى",
            groupId: _groups["physics-3sec-intensive"].Id,
            createdByUserId: _parents["ibrahim"].UserId,
            [
                (_parents["ibrahim"].UserId, "مساء الخير يا أستاذ. عايز أعرف مستوى سلمى في الفيزياء بعد الامتحان الأخير.", -3),
                (_teachers["ahmed"].UserId, "مساء النور. سلمى مستواها جيد جداً، حصلت على 11 من 12 في امتحان منتصف الترم.", -3),
                (_parents["ibrahim"].UserId, "الحمد لله. هل محتاجة مراجعة إضافية في أي فصل؟", -2),
                (_teachers["ahmed"].UserId, "تحتاج تركيز أكثر على وحدات القياس فقط. أرسلت لها ورقة عمل إضافية.", -2)
            ]);

        // --- Student and teacher -------------------------------------------------
        var studentThread = Thread(
            "student-teacher",
            _tenants["ahmed"],
            ConversationType.StudentTeacher,
            "سؤال في الواجب",
            groupId: _groups["physics-3sec-intensive"].Id,
            createdByUserId: _students["salma"].UserId,
            [
                (_students["salma"].UserId, "أستاذ، المسألة رقم 7 في الواجب مش فاهمة الخطوة الثانية.", -1),
                (_teachers["ahmed"].UserId, "ابدأي بحساب المحصلة أولاً ثم طبقي قانون نيوتن الثاني. راجعي المثال 4 في الملزمة.", -1)
            ]);

        // --- Teacher and assistant, inside the centre ----------------------------
        var staffThread = Thread(
            "teacher-assistant",
            _tenants["center"],
            ConversationType.TeacherAssistant,
            "تنظيم حضور السبت",
            groupId: _groups["center-math-3sec"].Id,
            createdByUserId: _teachers["hossam"].UserId,
            [
                (_teachers["hossam"].UserId, "من فضلك جهزي كشف الحضور لمجموعة السبت قبل الحصة.", -5),
                (_teachers["fatma"].UserId, "تم. الكشف جاهز ومرفق.", -5)
            ]);

        // --- Group thread with no replies yet ------------------------------------
        var quietThread = Thread(
            "group-quiet",
            _tenants["mariam"],
            ConversationType.Group,
            "إعلانات مجموعة التفاضل",
            groupId: _groups["math-3sec-calculus"].Id,
            createdByUserId: _teachers["mariam"].UserId,
            [
                (_teachers["mariam"].UserId, "أهلاً بكم. سيتم نشر كل المواعيد والملفات هنا.", -20)
            ]);

        foreach (var thread in new[] { parentThread, studentThread, staffThread, quietThread })
        {
            conversations.Add(thread.Conversation);
            participants.AddRange(thread.Participants);
            messages.AddRange(thread.Messages);
        }

        // One attachment on the staff thread's reply.
        var staffReply = staffThread.Messages[^1];

        attachments.Add(new MessageAttachment
        {
            Id = SeedId.For("demo:message-attachment", staffReply.Id, 1),
            MessageId = staffReply.Id,
            TenantId = staffReply.TenantId,
            FileName = "attendance-sheet.pdf",
            // No invented attendance register exists to link to, so this points at the
            // group's own subject handout: a real, loadable PDF rather than a dead path.
            FileUrl = DemoMedia.SubjectDocument(SubjectKey(_groups["center-math-3sec"].SubjectId), 2),
            FileType = MaterialFileType.Pdf,
            FileSizeInBytes = 96_000,
            CreatedAt = staffReply.SentAt
        });

        await AddMissingAsync("Conversations", conversations, cancellationToken);
        await AddMissingAsync("ConversationParticipants", participants, cancellationToken);
        await AddMissingAsync("Messages", messages, cancellationToken);
        await AddMissingAsync("MessageAttachments", attachments, cancellationToken);
    }

    private sealed record ThreadFixture(
        Conversation Conversation,
        List<ConversationParticipant> Participants,
        List<Message> Messages);

    /// <summary>
    /// Builds one thread with its participants and messages.
    ///
    /// The unread counter and the last-read timestamp are derived from the messages: the
    /// sender of the final message has read everything, and the other participants have not
    /// read what arrived after their last reply. That is the only way the badge a client shows
    /// can match the thread it is counting.
    /// </summary>
    private ThreadFixture Thread(
        string key,
        Domain.Entities.Tenancy.Tenant tenant,
        ConversationType type,
        string subject,
        Guid? groupId,
        Guid createdByUserId,
        (Guid SenderUserId, string Body, int DaysAgo)[] messages)
    {
        var conversationId = SeedId.For("demo:conversation", key);
        var sentTimes = new List<DateTime>();

        var messageRows = new List<Message>();
        var order = 0;

        foreach (var (senderUserId, body, daysAgo) in messages)
        {
            order++;
            var sentAt = _utcNow.AddDays(daysAgo).AddMinutes(order * 7);
            sentTimes.Add(sentAt);

            messageRows.Add(new Message
            {
                Id = SeedId.For("demo:message", conversationId, order),
                ConversationId = conversationId,
                TenantId = tenant.Id,
                SenderUserId = senderUserId,
                Body = body,
                SentAt = sentAt,
                CreatedAt = sentAt
            });
        }

        var lastMessage = messageRows[^1];

        var conversation = new Conversation
        {
            Id = conversationId,
            TenantId = tenant.Id,
            Type = type,
            Subject = subject,
            GroupId = groupId,
            CreatedByUserId = createdByUserId,
            LastMessageAt = lastMessage.SentAt,
            LastMessagePreview = lastMessage.Body.Length <= 120 ? lastMessage.Body : lastMessage.Body[..120],
            IsArchived = false,
            CreatedAt = sentTimes[0]
        };

        var participantRows = new List<ConversationParticipant>();

        foreach (var userId in messageRows.Select(message => message.SenderUserId).Distinct())
        {
            var ownLast = messageRows
                .Where(message => message.SenderUserId == userId)
                .Max(message => message.SentAt);

            var unread = messageRows.Count(message => message.SenderUserId != userId && message.SentAt > ownLast);

            participantRows.Add(new ConversationParticipant
            {
                Id = SeedId.For("demo:conversation-participant", conversationId, userId),
                ConversationId = conversationId,
                TenantId = tenant.Id,
                UserId = userId,
                JoinedAt = conversation.CreatedAt,
                LastReadAt = ownLast,
                UnreadCount = unread,
                IsMuted = false,
                CreatedAt = conversation.CreatedAt
            });
        }

        return new ThreadFixture(conversation, participantRows, messageRows);
    }
}
