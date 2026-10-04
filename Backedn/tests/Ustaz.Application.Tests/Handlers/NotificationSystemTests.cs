using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Features.Notifications;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// The notification system is the platform's voice, so these tests pin the rules that decide
/// whether a user hears something: the catalog's completeness, per-category preferences, the
/// critical override, deduplication, and the guarantee that a dead live transport never costs
/// a stored notification.
/// </summary>
public class NotificationSystemTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private Guid UserId => _harness.Student.UserId;

    private void DisableCategory(NotificationCategory category)
    {
        _harness.Context.NotificationPreferences.Add(new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Category = category,
            Channels = NotificationChannel.InApp,
            IsEnabled = false
        });

        _harness.Context.SaveChanges();
    }

    // -----------------------------------------------------------------------
    // The catalog
    // -----------------------------------------------------------------------

    [Fact]
    public void Every_catalog_entry_has_copy_in_both_languages()
    {
        foreach (var template in NotificationCatalog.All)
        {
            template.TitleAr.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.BodyAr.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.TitleEn.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.BodyEn.ShouldNotBeNullOrWhiteSpace(template.Event);
        }
    }

    [Fact]
    public void Every_event_constant_resolves_to_a_template()
    {
        var events = typeof(NotificationEvents)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        events.ShouldNotBeEmpty();

        foreach (var name in events)
        {
            NotificationCatalog.TryGet(name, out _).ShouldBeTrue(name);
        }
    }

    [Fact]
    public void Every_placeholder_used_in_the_copy_is_declared_as_a_required_value()
    {
        foreach (var template in NotificationCatalog.All)
        {
            var text = template.TitleAr + template.BodyAr + template.TitleEn + template.BodyEn
                + (template.ActionUrl ?? string.Empty);

            var placeholders = System.Text.RegularExpressions.Regex
                .Matches(text, "{(?<name>[a-zA-Z]+)}")
                .Select(match => match.Groups["name"].Value)
                .Distinct();

            foreach (var placeholder in placeholders)
            {
                template.RequiredValues.ShouldContain(placeholder, template.Event + " -> " + placeholder);
            }
        }
    }

    [Fact]
    public void Rendering_substitutes_every_supplied_value()
    {
        var rendered = NotificationTemplate.Render(
            "{student} جاب {score} من {max}",
            new Dictionary<string, string> { ["student"] = "سلمى", ["score"] = "9", ["max"] = "10" });

        rendered.ShouldBe("سلمى جاب 9 من 10");
    }

    // -----------------------------------------------------------------------
    // Delivery
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_catalog_notification_is_stored_in_arabic_and_english_with_its_deep_link()
    {
        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkGraded,
                TenantId = _harness.Tenant.Id,
                ReferenceId = _harness.Group.Id,
                Values = new Dictionary<string, string>
                {
                    ["homework"] = "واجب الفيزياء",
                    ["homeworkId"] = _harness.Group.Id.ToString(),
                    ["score"] = "9",
                    ["max"] = "10"
                }
            },
            CancellationToken.None);

        var stored = await _harness.Context.Notifications.SingleAsync();

        stored.Title.ShouldBe("المدرس صحّح الواجب بتاعك ✍️");
        stored.Message.ShouldBe("جبت 9 من 10 في واجب واجب الفيزياء.");
        stored.TitleEn.ShouldBe("Your homework was graded");
        stored.Category.ShouldBe(NotificationCategory.Homework);
        stored.Priority.ShouldBe(NotificationPriority.High);
        stored.Event.ShouldBe(NotificationEvents.HomeworkGraded);
        stored.ActionUrl.ShouldBe("/student/homework/" + _harness.Group.Id);
        stored.RelatedEntityType.ShouldBe("HomeworkSubmission");
        stored.IsRead.ShouldBeFalse();
    }

    [Fact]
    public async Task A_disabled_category_silences_an_ordinary_notification()
    {
        DisableCategory(NotificationCategory.Homework);

        var delivered = await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkAssigned,
                Values = new Dictionary<string, string>
                {
                    ["homework"] = "واجب",
                    ["homeworkId"] = Guid.NewGuid().ToString(),
                    ["due"] = "2026-06-10"
                }
            },
            CancellationToken.None);

        delivered.ShouldBeFalse();
        (await _harness.Context.Notifications.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_critical_notification_ignores_a_disabled_category()
    {
        DisableCategory(NotificationCategory.Security);

        var delivered = await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest { Event = NotificationEvents.PasswordChanged },
            CancellationToken.None);

        // Security events carry consequences the user must see, whatever they switched off.
        delivered.ShouldBeTrue();

        var stored = await _harness.Context.Notifications.SingleAsync();
        stored.Priority.ShouldBe(NotificationPriority.Critical);
    }

    [Fact]
    public async Task A_deduplication_key_allows_exactly_one_copy()
    {
        var request = new NotificationRequest
        {
            Event = NotificationEvents.AttendanceAbsent,
            TenantId = _harness.Tenant.Id,
            DeduplicationKey = "absence:session-1",
            Values = new Dictionary<string, string> { ["group"] = "فيزياء" }
        };

        (await _harness.RealNotifications.SendAsync(UserId, request, CancellationToken.None)).ShouldBeTrue();
        (await _harness.RealNotifications.SendAsync(UserId, request, CancellationToken.None)).ShouldBeFalse();

        (await _harness.Context.Notifications.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Deduplication_is_per_recipient_not_global()
    {
        var other = _harness.AddMember(
            _harness.Tenant, "second@ustaz.test", TenantMemberRole.Assistant);

        var request = new NotificationRequest
        {
            Event = NotificationEvents.AnnouncementPublished,
            TenantId = _harness.Tenant.Id,
            DeduplicationKey = "announcement:1",
            Values = new Dictionary<string, string>
            {
                ["title"] = "إعلان",
                ["announcementId"] = Guid.NewGuid().ToString()
            }
        };

        await _harness.RealNotifications.SendAsync(UserId, request, CancellationToken.None);
        await _harness.RealNotifications.SendAsync(other.UserId, request, CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task A_fan_out_skips_recipients_who_already_received_that_event()
    {
        var second = _harness.AddMember(_harness.Tenant, "fan1@ustaz.test", TenantMemberRole.Assistant);
        var third = _harness.AddMember(_harness.Tenant, "fan2@ustaz.test", TenantMemberRole.Assistant);

        var request = new NotificationRequest
        {
            Event = NotificationEvents.ExamPublished,
            TenantId = _harness.Tenant.Id,
            DeduplicationKey = "exam-published:1",
            Values = new Dictionary<string, string>
            {
                ["exam"] = "امتحان",
                ["examId"] = Guid.NewGuid().ToString(),
                ["date"] = "2026-06-10"
            }
        };

        await _harness.RealNotifications.SendAsync(second.UserId, request, CancellationToken.None);

        var delivered = await _harness.RealNotifications.SendManyAsync(
            [UserId, second.UserId, third.UserId], request, CancellationToken.None);

        // Three recipients, one already told: two new rows.
        delivered.ShouldBe(2);
        (await _harness.Context.Notifications.CountAsync()).ShouldBe(3);
    }

    [Fact]
    public async Task A_fan_out_honours_each_recipients_own_preference()
    {
        var quiet = _harness.AddMember(_harness.Tenant, "quiet@ustaz.test", TenantMemberRole.Assistant);

        _harness.Context.NotificationPreferences.Add(new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = quiet.UserId,
            Category = NotificationCategory.Announcement,
            IsEnabled = false
        });

        await _harness.Context.SaveChangesAsync();

        var delivered = await _harness.RealNotifications.SendManyAsync(
            [UserId, quiet.UserId],
            new NotificationRequest
            {
                Event = NotificationEvents.AnnouncementPublished,
                TenantId = _harness.Tenant.Id,
                Values = new Dictionary<string, string>
                {
                    ["title"] = "إعلان",
                    ["announcementId"] = Guid.NewGuid().ToString()
                }
            },
            CancellationToken.None);

        delivered.ShouldBe(1);

        var stored = await _harness.Context.Notifications.SingleAsync();
        stored.UserId.ShouldBe(UserId);
    }

    // -----------------------------------------------------------------------
    // Live delivery
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_stored_notification_is_pushed_live_with_the_new_unread_count()
    {
        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest { Event = NotificationEvents.PasswordChanged },
            CancellationToken.None);

        _harness.Realtime.Published.Count.ShouldBe(1);
        _harness.Realtime.Published[0].UserId.ShouldBe(UserId);
        _harness.Realtime.UnreadCounts.ShouldContain(entry => entry.UserId == UserId && entry.Count == 1);
    }

    [Fact]
    public async Task A_failing_live_transport_never_costs_the_stored_notification()
    {
        _harness.Realtime.ThrowOnPublish = true;

        var delivered = await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest { Event = NotificationEvents.PasswordChanged },
            CancellationToken.None);

        delivered.ShouldBeTrue();
        (await _harness.Context.Notifications.CountAsync()).ShouldBe(1);
    }

    // -----------------------------------------------------------------------
    // The inbox API
    // -----------------------------------------------------------------------

    [Fact]
    public async Task The_inbox_returns_the_callers_own_notifications_only()
    {
        var other = _harness.AddMember(_harness.Tenant, "inbox@ustaz.test", TenantMemberRole.Assistant);

        await _harness.RealNotifications.SendAsync(
            UserId, new NotificationRequest { Event = NotificationEvents.PasswordChanged }, CancellationToken.None);

        await _harness.RealNotifications.SendAsync(
            other.UserId, new NotificationRequest { Event = NotificationEvents.PasswordChanged }, CancellationToken.None);

        _harness.SignInAsStudent();

        var page = await new GetNotificationsQueryHandler(_harness.Context, _harness.CurrentUser.Object)
            .Handle(new GetNotificationsQuery(), CancellationToken.None);

        page.TotalCount.ShouldBe(1);
        page.Items[0].TitleEn.ShouldBe("Your password was changed");
    }

    [Fact]
    public async Task The_inbox_can_be_narrowed_to_important_notifications()
    {
        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.ReviewReceived,
                Values = new Dictionary<string, string> { ["student"] = "سلمى", ["rating"] = "5" }
            },
            CancellationToken.None);

        await _harness.RealNotifications.SendAsync(
            UserId, new NotificationRequest { Event = NotificationEvents.PasswordChanged }, CancellationToken.None);

        _harness.SignInAsStudent();

        var page = await new GetNotificationsQueryHandler(_harness.Context, _harness.CurrentUser.Object)
            .Handle(
                new GetNotificationsQuery { MinimumPriority = NotificationPriority.High },
                CancellationToken.None);

        page.TotalCount.ShouldBe(1);
        page.Items[0].Priority.ShouldBe(NotificationPriority.Critical);
    }

    [Fact]
    public async Task Clearing_read_notifications_leaves_the_unread_ones_alone()
    {
        await _harness.RealNotifications.SendAsync(
            UserId, new NotificationRequest { Event = NotificationEvents.PasswordChanged }, CancellationToken.None);

        await _harness.RealNotifications.SendAsync(
            UserId, new NotificationRequest { Event = NotificationEvents.PasswordReset }, CancellationToken.None);

        var first = await _harness.Context.Notifications.FirstAsync();
        first.MarkAsRead(_harness.UtcNow);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();

        var removed = await new ClearReadNotificationsCommandHandler(
                _harness.Context, _harness.CurrentUser.Object)
            .Handle(new ClearReadNotificationsCommand(), CancellationToken.None);

        removed.ShouldBe(1);
        (await _harness.Context.Notifications.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Another_users_notification_cannot_be_read_by_id()
    {
        var other = _harness.AddMember(_harness.Tenant, "private@ustaz.test", TenantMemberRole.Assistant);

        await _harness.RealNotifications.SendAsync(
            other.UserId, new NotificationRequest { Event = NotificationEvents.PasswordChanged }, CancellationToken.None);

        var theirs = await _harness.Context.Notifications.SingleAsync();

        _harness.SignInAsStudent();

        await Should.ThrowAsync<Ustaz.Domain.Exceptions.NotFoundException>(() =>
            new GetNotificationByIdQueryHandler(_harness.Context, _harness.CurrentUser.Object)
                .Handle(new GetNotificationByIdQuery(theirs.Id), CancellationToken.None));
    }

    public void Dispose() => _harness.Dispose();
}
