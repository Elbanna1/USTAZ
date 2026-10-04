using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Common.Emails;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Notifications;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// The transactional email system: the catalog's completeness, language selection, rendering
/// (including the RTL metadata Arabic mail clients need), deduplication, preference handling,
/// failure behaviour, and the guarantee that no secret or token reaches a stored record.
/// </summary>
public class EmailSystemTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private Guid UserId => _harness.Student.UserId;

    private const string StudentEmail = "salma@ustaz.test";

    private static TransactionalEmailRequest HomeworkGraded() => new()
    {
        Event = EmailEvents.HomeworkGraded,
        Values = new Dictionary<string, string>
        {
            ["homework"] = "واجب الفيزياء",
            ["homeworkId"] = Guid.NewGuid().ToString(),
            ["score"] = "9",
            ["max"] = "10"
        }
    };

    // -----------------------------------------------------------------------
    // The catalog
    // -----------------------------------------------------------------------

    [Fact]
    public void Every_template_has_copy_in_both_languages()
    {
        foreach (var template in EmailCatalog.All)
        {
            template.SubjectAr.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.SubjectEn.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.HeadingAr.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.HeadingEn.ShouldNotBeNullOrWhiteSpace(template.Event);
            template.BodyAr.ShouldNotBeEmpty(template.Event);
            template.BodyEn.ShouldNotBeEmpty(template.Event);
        }
    }

    [Fact]
    public void Every_event_constant_resolves_to_a_template()
    {
        var events = typeof(EmailEvents)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        events.ShouldNotBeEmpty();

        foreach (var name in events)
        {
            EmailCatalog.TryGet(name, out _).ShouldBeTrue(name);
        }
    }

    [Fact]
    public void Every_placeholder_in_the_copy_is_declared_as_a_required_value()
    {
        foreach (var template in EmailCatalog.All)
        {
            var text = template.SubjectAr + template.SubjectEn + template.HeadingAr + template.HeadingEn
                + string.Concat(template.BodyAr) + string.Concat(template.BodyEn)
                + (template.CtaPath ?? string.Empty)
                + (template.FootnoteAr ?? string.Empty);

            var placeholders = System.Text.RegularExpressions.Regex
                .Matches(text, "{(?<name>[a-zA-Z]+)}")
                .Select(match => match.Groups["name"].Value)
                .Distinct()
                // Every recipient gets a name injected by the sender.
                .Where(name => name != "userName");

            foreach (var placeholder in placeholders)
            {
                template.RequiredValues.ShouldContain(placeholder, template.Event + " -> " + placeholder);
            }
        }
    }

    [Fact]
    public void Security_templates_are_marked_so_they_bypass_preferences_and_retries()
    {
        foreach (var name in new[]
                 {
                     EmailEvents.PasswordChanged,
                     EmailEvents.PasswordReset,
                     EmailEvents.PasswordResetRequested,
                     EmailEvents.EmailVerification,
                     EmailEvents.AccountSuspended,
                     EmailEvents.AccountActivated
                 })
        {
            EmailCatalog.Get(name).IsSecurity.ShouldBeTrue(name);
        }
    }

    // -----------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------

    [Fact]
    public void An_arabic_email_renders_right_to_left_with_the_arabic_language_tag()
    {
        var message = EmailRenderer.Render(
            EmailCatalog.Get(EmailEvents.PasswordChanged),
            EmailLanguage.Arabic,
            StudentEmail,
            "سلمى",
            new Dictionary<string, string> { ["userName"] = "سلمى" },
            new EmailBranding());

        message.Subject.ShouldBe("الباسورد بتاعك اتغير 🔐");
        message.HtmlBody.ShouldContain("dir=\"rtl\"");
        message.HtmlBody.ShouldContain("lang=\"ar\"");
        message.HtmlBody.ShouldContain("أمّن حسابك فورًا");

        // Responsive on a phone, and a plain-text part for clients that refuse HTML.
        message.HtmlBody.ShouldContain("width=device-width");
        message.PlainTextBody.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void An_english_email_renders_left_to_right_with_the_english_language_tag()
    {
        var message = EmailRenderer.Render(
            EmailCatalog.Get(EmailEvents.PasswordChanged),
            EmailLanguage.English,
            StudentEmail,
            "Salma",
            new Dictionary<string, string> { ["userName"] = "Salma" },
            new EmailBranding());

        message.Subject.ShouldBe("Your password was changed 🔐");
        message.HtmlBody.ShouldContain("dir=\"ltr\"");
        message.HtmlBody.ShouldContain("lang=\"en\"");
    }

    [Fact]
    public void A_display_name_is_html_encoded_so_it_cannot_inject_markup()
    {
        var message = EmailRenderer.Render(
            EmailCatalog.Get(EmailEvents.Welcome),
            EmailLanguage.Arabic,
            StudentEmail,
            "x",
            new Dictionary<string, string> { ["userName"] = "<script>alert(1)</script>" },
            new EmailBranding());

        message.HtmlBody.ShouldNotContain("<script>");
        message.HtmlBody.ShouldContain("&lt;script&gt;");
    }

    [Fact]
    public void Every_email_carries_the_support_address_in_its_footer()
    {
        var message = EmailRenderer.Render(
            EmailCatalog.Get(EmailEvents.Welcome),
            EmailLanguage.Arabic,
            StudentEmail,
            "سلمى",
            null,
            new EmailBranding { SupportEmail = "support.webcraft.eg@gmail.com" });

        message.HtmlBody.ShouldContain("support.webcraft.eg@gmail.com");
    }

    [Fact]
    public void A_relative_cta_resolves_against_the_client_url_and_an_absolute_one_is_left_alone()
    {
        EmailRenderer.BuildCtaUrl("/student/homework/7", null, "https://app.ustaz.test")
            .ShouldBe("https://app.ustaz.test/student/homework/7");

        EmailRenderer.BuildCtaUrl(
                "{resetUrl}",
                new Dictionary<string, string> { ["resetUrl"] = "https://app.ustaz.test/reset?token=abc" },
                "https://ignored.test")
            .ShouldBe("https://app.ustaz.test/reset?token=abc");
    }

    // -----------------------------------------------------------------------
    // Sending
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Sending_to_a_user_renders_their_language_and_records_the_delivery()
    {
        await _harness.TransactionalEmail.SendToUserAsync(UserId, HomeworkGraded(), CancellationToken.None);

        var message = _harness.Emails.Sent.ShouldHaveSingleItem();
        message.To.ShouldBe(StudentEmail);
        message.Subject.ShouldBe("المدرس صحّح الواجب بتاعك ✍️");
        message.HtmlBody.ShouldContain("جبت 9 من 10");

        var delivery = await _harness.Context.EmailDeliveries.SingleAsync();
        delivery.Status.ShouldBe(EmailDeliveryStatus.Sent);
        delivery.Event.ShouldBe(EmailEvents.HomeworkGraded);
        delivery.Language.ShouldBe("ar-EG");
        delivery.Attempts.ShouldBe(1);
        delivery.SentAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_user_who_reads_english_gets_the_english_email()
    {
        var user = await _harness.Context.Users.FirstAsync(entity => entity.Id == UserId);
        user.PreferredLanguage = "en";
        await _harness.Context.SaveChangesAsync();

        await _harness.TransactionalEmail.SendToUserAsync(UserId, HomeworkGraded(), CancellationToken.None);

        var message = _harness.Emails.Sent.ShouldHaveSingleItem();
        message.Subject.ShouldBe("Your homework was graded ✍️");
        message.HtmlBody.ShouldContain("dir=\"ltr\"");
    }

    [Fact]
    public async Task Arabic_is_the_default_for_an_account_that_never_chose()
    {
        var user = await _harness.Context.Users.FirstAsync(entity => entity.Id == UserId);
        user.PreferredLanguage.ShouldBe("ar-EG");

        await _harness.TransactionalEmail.SendToUserAsync(UserId, HomeworkGraded(), CancellationToken.None);

        _harness.Emails.Sent.ShouldHaveSingleItem().HtmlBody.ShouldContain("dir=\"rtl\"");
    }

    [Fact]
    public async Task A_deduplication_key_allows_exactly_one_email()
    {
        var request = HomeworkGraded() with { DeduplicationKey = "homework-graded:1" };

        (await _harness.TransactionalEmail.SendToUserAsync(UserId, request, CancellationToken.None)).ShouldBeTrue();
        (await _harness.TransactionalEmail.SendToUserAsync(UserId, request, CancellationToken.None)).ShouldBeFalse();

        _harness.Emails.Sent.Count.ShouldBe(1);
        (await _harness.Context.EmailDeliveries.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task An_address_with_no_account_still_receives_security_mail()
    {
        var sent = await _harness.TransactionalEmail.SendToAddressAsync(
            "stranger@ustaz.test",
            "Stranger",
            new TransactionalEmailRequest
            {
                Event = EmailEvents.PasswordResetRequested,
                Values = new Dictionary<string, string>
                {
                    ["userName"] = "Stranger",
                    ["resetUrl"] = "https://app.ustaz.test/reset?token=secret-token"
                }
            },
            CancellationToken.None);

        sent.ShouldBeTrue();
        _harness.Emails.Sent.ShouldHaveSingleItem().To.ShouldBe("stranger@ustaz.test");
    }

    // -----------------------------------------------------------------------
    // Secrets and tokens
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_security_email_stores_no_payload_so_its_token_is_never_persisted()
    {
        await _harness.TransactionalEmail.SendToAddressAsync(
            StudentEmail,
            "سلمى",
            new TransactionalEmailRequest
            {
                Event = EmailEvents.PasswordResetRequested,
                Values = new Dictionary<string, string>
                {
                    ["userName"] = "سلمى",
                    ["resetUrl"] = "https://app.ustaz.test/reset?token=super-secret-token"
                }
            },
            CancellationToken.None);

        var delivery = await _harness.Context.EmailDeliveries.SingleAsync();

        delivery.PayloadJson.ShouldBeNull();

        // Nothing stored on the row leaks the one-time token.
        var serialised = System.Text.Json.JsonSerializer.Serialize(delivery);
        serialised.ShouldNotContain("super-secret-token");
    }

    [Fact]
    public async Task An_ordinary_email_stores_its_values_so_it_can_be_retried()
    {
        await _harness.TransactionalEmail.SendToUserAsync(UserId, HomeworkGraded(), CancellationToken.None);

        var delivery = await _harness.Context.EmailDeliveries.SingleAsync();
        delivery.PayloadJson.ShouldNotBeNull();
        delivery.PayloadJson!.ShouldContain("homework");
    }

    [Fact]
    public void The_recipient_hash_is_stable_and_reveals_nothing()
    {
        var hash = Ustaz.Infrastructure.Services.Email.TransactionalEmailService.Hash(StudentEmail);

        hash.Length.ShouldBe(12);
        hash.ShouldNotContain("@");
        hash.ShouldNotContain("salma");
        hash.ShouldBe(Ustaz.Infrastructure.Services.Email.TransactionalEmailService.Hash("SALMA@ustaz.test"));
    }

    // -----------------------------------------------------------------------
    // Failure behaviour
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_failed_send_is_recorded_and_never_thrown_at_the_caller()
    {
        _harness.Emails.ThrowOnSend = true;

        var sent = await _harness.TransactionalEmail.SendToUserAsync(
            UserId, HomeworkGraded(), CancellationToken.None);

        sent.ShouldBeFalse();

        var delivery = await _harness.Context.EmailDeliveries.SingleAsync();
        delivery.Status.ShouldBe(EmailDeliveryStatus.Failed);
        delivery.Attempts.ShouldBe(1);
        delivery.FailedAt.ShouldNotBeNull();
        delivery.ErrorCategory.ShouldBe("NotConfigured");
        delivery.IsRetryable.ShouldBeTrue();
    }

    [Fact]
    public async Task A_failure_stops_being_retryable_once_the_attempt_cap_is_reached()
    {
        _harness.EmailOptions.MaxDeliveryAttempts = 1;
        _harness.Emails.ThrowOnSend = true;

        await _harness.TransactionalEmail.SendToUserAsync(UserId, HomeworkGraded(), CancellationToken.None);

        var delivery = await _harness.Context.EmailDeliveries.SingleAsync();
        delivery.Status.ShouldBe(EmailDeliveryStatus.Abandoned);
        delivery.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public async Task Turning_email_off_records_the_message_as_skipped_and_sends_nothing()
    {
        _harness.EmailOptions.Enabled = false;

        var sent = await _harness.TransactionalEmail.SendToUserAsync(
            UserId, HomeworkGraded(), CancellationToken.None);

        sent.ShouldBeFalse();
        _harness.Emails.Sent.ShouldBeEmpty();

        var delivery = await _harness.Context.EmailDeliveries.SingleAsync();
        delivery.Status.ShouldBe(EmailDeliveryStatus.Skipped);
        delivery.ErrorCategory.ShouldBe("EmailDisabled");
    }

    // -----------------------------------------------------------------------
    // Notification → email integration
    // -----------------------------------------------------------------------

    [Fact]
    public async Task An_important_notification_also_arrives_as_a_templated_email()
    {
        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkGraded,
                Values = new Dictionary<string, string>
                {
                    ["homework"] = "واجب الفيزياء",
                    ["homeworkId"] = Guid.NewGuid().ToString(),
                    ["score"] = "9",
                    ["max"] = "10"
                }
            },
            CancellationToken.None);

        // The bell row and the email are separate deliveries of the same event.
        (await _harness.Context.Notifications.CountAsync()).ShouldBe(1);

        var email = _harness.Emails.Sent.ShouldHaveSingleItem();
        email.Subject.ShouldBe("المدرس صحّح الواجب بتاعك ✍️");
        email.HtmlBody.ShouldContain("dir=\"rtl\"");
    }

    [Fact]
    public async Task A_chatty_notification_stays_in_the_app()
    {
        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.AttendanceLate,
                Values = new Dictionary<string, string> { ["group"] = "فيزياء" }
            },
            CancellationToken.None);

        (await _harness.Context.Notifications.CountAsync()).ShouldBe(1);
        _harness.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task Turning_a_category_off_silences_its_email_too()
    {
        _harness.Context.NotificationPreferences.Add(new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Category = NotificationCategory.Homework,
            Channels = NotificationChannel.InApp,
            IsEnabled = false
        });

        await _harness.Context.SaveChangesAsync();

        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest
            {
                Event = NotificationEvents.HomeworkGraded,
                Values = new Dictionary<string, string>
                {
                    ["homework"] = "واجب",
                    ["homeworkId"] = Guid.NewGuid().ToString(),
                    ["score"] = "9",
                    ["max"] = "10"
                }
            },
            CancellationToken.None);

        _harness.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_security_notification_is_emailed_even_when_the_category_is_off()
    {
        _harness.Context.NotificationPreferences.Add(new NotificationPreference
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Category = NotificationCategory.Security,
            Channels = NotificationChannel.InApp,
            IsEnabled = false
        });

        await _harness.Context.SaveChangesAsync();

        await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest { Event = NotificationEvents.PasswordChanged },
            CancellationToken.None);

        _harness.Emails.Sent.ShouldHaveSingleItem().Subject.ShouldBe("الباسورد بتاعك اتغير 🔐");
    }

    [Fact]
    public async Task A_failing_mail_server_never_costs_the_in_app_notification()
    {
        _harness.Emails.ThrowOnSend = true;

        var delivered = await _harness.RealNotifications.SendAsync(
            UserId,
            new NotificationRequest { Event = NotificationEvents.PasswordChanged },
            CancellationToken.None);

        delivered.ShouldBeTrue();
        (await _harness.Context.Notifications.CountAsync()).ShouldBe(1);
        (await _harness.Context.EmailDeliveries.SingleAsync()).Status.ShouldBe(EmailDeliveryStatus.Failed);
    }

    // -----------------------------------------------------------------------
    // Messaging is an offline channel
    // -----------------------------------------------------------------------

    [Fact]
    public async Task A_message_email_is_skipped_for_someone_who_is_online()
    {
        _harness.Presence.SetOnline(UserId);

        var sent = await _harness.TransactionalEmail.SendToUserAsync(
            UserId,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.MessageReceived,
                Values = new Dictionary<string, string>
                {
                    ["sender"] = "أ. أحمد",
                    ["conversationId"] = Guid.NewGuid().ToString()
                }
            },
            CancellationToken.None);

        sent.ShouldBeFalse();
        _harness.Emails.Sent.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_message_email_is_sent_to_someone_who_is_away_and_never_quotes_the_message()
    {
        var sent = await _harness.TransactionalEmail.SendToUserAsync(
            UserId,
            new TransactionalEmailRequest
            {
                Event = EmailEvents.MessageReceived,
                Values = new Dictionary<string, string>
                {
                    ["sender"] = "أ. أحمد",
                    ["conversationId"] = Guid.NewGuid().ToString()
                }
            },
            CancellationToken.None);

        sent.ShouldBeTrue();

        var email = _harness.Emails.Sent.ShouldHaveSingleItem();
        email.Subject.ShouldBe("عندك رسالة جديدة من أ. أحمد");

        // The body points at the conversation; it never carries the message text.
        email.HtmlBody.ShouldContain("افتح المحادثة");
    }

    public void Dispose() => _harness.Dispose();
}
