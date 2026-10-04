using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Features.Messaging;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Entities.Messaging;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;
using Ustaz.Infrastructure.Options;
using Ustaz.Infrastructure.Services.Storage;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Files sent in a conversation are private. These tests cover the send path, and — more
/// importantly — that reading one is gated on live conversation membership rather than on
/// possession of an attachment id.
/// </summary>
public class MessageAttachmentTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    private Conversation _conversation = null!;
    private ApplicationUser _teacherUser = null!;

    public MessageAttachmentTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ustaz-attachments-" + Guid.NewGuid().ToString("N"));

        _storage = new LocalFileStorage(
            Options.Create(new StorageOptions { RootPath = _root }),
            NullLogger<LocalFileStorage>.Instance,
            contentRootPath: _root);

        SeedConversation();
    }

    private SendMessageAttachmentCommandHandler SendHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _storage,
            _harness.PlanLimits,
            _harness.Clock,
            _harness.Notifications.Object);

    private DownloadMessageAttachmentQueryHandler DownloadHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _storage);

    private static FileUploadRequest Pdf(string name = "notes.pdf") => new()
    {
        Content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 notes")),
        FileName = name,
        ContentType = "application/pdf",
        Length = 14
    };

    /// <summary>A conversation between the seeded teacher and the seeded student.</summary>
    private void SeedConversation()
    {
        _teacherUser = _harness.Context.Users.First(user => user.Id == _harness.Teacher.UserId);

        _conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            Type = ConversationType.StudentTeacher,
            CreatedByUserId = _teacherUser.Id,
            CreatedAt = _harness.UtcNow
        };

        foreach (var userId in new[] { _teacherUser.Id, _harness.Student.UserId })
        {
            _conversation.Participants.Add(new ConversationParticipant
            {
                Id = Guid.NewGuid(),
                ConversationId = _conversation.Id,
                TenantId = _harness.Tenant.Id,
                UserId = userId,
                JoinedAt = _harness.UtcNow,
                CreatedAt = _harness.UtcNow
            });
        }

        _harness.Context.Conversations.Add(_conversation);
        _harness.Context.SaveChanges();
    }

    [Fact]
    public async Task Sending_a_file_stores_it_privately_and_posts_it_as_a_message()
    {
        _harness.SignInAsStudent();

        var message = await SendHandler().Handle(
            new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
            CancellationToken.None);

        message.Attachments.Count.ShouldBe(1);

        var attachment = await _harness.Context.MessageAttachments.SingleAsync();
        attachment.FileName.ShouldBe("notes.pdf");
        attachment.TenantId.ShouldBe(_harness.Tenant.Id);

        // The stored handle is a storage key inside the workspace's own folder, never a URL.
        attachment.FileUrl.ShouldStartWith("tenants/" + _harness.Tenant.Id.ToString("D") + "/messages/");
        _storage.BelongsToTenant(attachment.FileUrl, _harness.Tenant.Id).ShouldBeTrue();
    }

    [Fact]
    public async Task Sending_a_file_raises_the_other_participants_unread_count()
    {
        _harness.SignInAsStudent();

        await SendHandler().Handle(
            new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
            CancellationToken.None);

        var teacherParticipant = await _harness.Context.ConversationParticipants
            .FirstAsync(participant => participant.UserId == _teacherUser.Id);

        teacherParticipant.UnreadCount.ShouldBe(1);

        var senderParticipant = await _harness.Context.ConversationParticipants
            .FirstAsync(participant => participant.UserId == _harness.Student.UserId);

        senderParticipant.UnreadCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_non_participant_cannot_post_a_file_into_the_conversation()
    {
        var outsider = _harness.AddMember(_harness.Tenant, "outsider@ustaz.test", TenantMemberRole.Assistant);

        _harness.SignInAsMember(
            await _harness.Context.Users.FirstAsync(user => user.Id == outsider.UserId), _harness.Tenant);

        await Should.ThrowAsync<NotFoundException>(() =>
            SendHandler().Handle(
                new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_participant_can_download_the_file()
    {
        _harness.SignInAsStudent();

        await SendHandler().Handle(
            new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
            CancellationToken.None);

        var attachment = await _harness.Context.MessageAttachments.SingleAsync();

        _harness.SignInAsTeacher();

        var download = await DownloadHandler().Handle(
            new DownloadMessageAttachmentQuery(attachment.Id), CancellationToken.None);

        download.FileName.ShouldBe("notes.pdf");

        // Never a renderable type: an upload must always download.
        download.ContentType.ShouldBe("application/octet-stream");

        await using var content = download.Content;
        using var reader = new StreamReader(content);
        (await reader.ReadToEndAsync()).ShouldContain("notes");
    }

    [Fact]
    public async Task Someone_outside_the_conversation_cannot_download_by_id()
    {
        _harness.SignInAsStudent();

        await SendHandler().Handle(
            new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
            CancellationToken.None);

        var attachment = await _harness.Context.MessageAttachments.SingleAsync();

        // A teacher from another workspace, holding the id.
        _harness.SignInAsTeacher(_harness.OtherTeacher);

        await Should.ThrowAsync<NotFoundException>(() =>
            DownloadHandler().Handle(
                new DownloadMessageAttachmentQuery(attachment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task A_participant_who_left_the_conversation_loses_access()
    {
        _harness.SignInAsStudent();

        await SendHandler().Handle(
            new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
            CancellationToken.None);

        var attachment = await _harness.Context.MessageAttachments.SingleAsync();

        var teacherParticipant = await _harness.Context.ConversationParticipants
            .FirstAsync(participant => participant.UserId == _teacherUser.Id);

        teacherParticipant.LeftAt = _harness.UtcNow;
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsTeacher();

        await Should.ThrowAsync<NotFoundException>(() =>
            DownloadHandler().Handle(
                new DownloadMessageAttachmentQuery(attachment.Id), CancellationToken.None));
    }

    [Fact]
    public async Task An_attachment_whose_key_points_at_another_workspace_is_refused()
    {
        _harness.SignInAsStudent();

        await SendHandler().Handle(
            new SendMessageAttachmentCommand { ConversationId = _conversation.Id, File = Pdf() },
            CancellationToken.None);

        var attachment = await _harness.Context.MessageAttachments.SingleAsync();

        // Simulate a tampered or mis-migrated row pointing into another tenant's folder.
        attachment.FileUrl = "tenants/" + Guid.NewGuid().ToString("D") + "/messages/stolen.pdf";
        await _harness.Context.SaveChangesAsync();

        await Should.ThrowAsync<NotFoundException>(() =>
            DownloadHandler().Handle(
                new DownloadMessageAttachmentQuery(attachment.Id), CancellationToken.None));
    }

    [Fact]
    public void The_validator_rejects_a_disallowed_file_type()
    {
        var validator = new SendMessageAttachmentCommandValidator();

        var result = validator.Validate(new SendMessageAttachmentCommand
        {
            ConversationId = Guid.NewGuid(),
            File = new FileUploadRequest
            {
                Content = Stream.Null,
                FileName = "script.exe",
                ContentType = "application/pdf",
                Length = 10
            }
        });

        result.IsValid.ShouldBeFalse();
    }

    public void Dispose()
    {
        _harness.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
