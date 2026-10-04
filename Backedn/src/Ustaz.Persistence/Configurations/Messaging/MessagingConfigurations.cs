using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Messaging;

namespace Ustaz.Persistence.Configurations.Messaging;

public sealed class ConversationConfiguration : BaseEntityConfiguration<Conversation>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("Conversations");

        builder.Property(conversation => conversation.Type).HasConversion<int>();
        builder.Property(conversation => conversation.Subject).HasMaxLength(DomainRules.NameMaxLength);
        builder.Property(conversation => conversation.LastMessagePreview).HasMaxLength(300);

        builder.HasIndex(conversation => new { conversation.TenantId, conversation.LastMessageAt })
            .HasDatabaseName("IX_Conversations_TenantId_LastMessageAt");

        builder.HasIndex(conversation => conversation.GroupId)
            .HasDatabaseName("IX_Conversations_GroupId");

        builder.HasOne(conversation => conversation.Tenant)
            .WithMany()
            .HasForeignKey(conversation => conversation.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(conversation => conversation.Group)
            .WithMany()
            .HasForeignKey(conversation => conversation.GroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(conversation => conversation.Participants)
            .WithOne(participant => participant.Conversation)
            .HasForeignKey(participant => participant.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ConversationParticipantConfiguration : BaseEntityConfiguration<ConversationParticipant>
{
    protected override void ConfigureEntity(EntityTypeBuilder<ConversationParticipant> builder)
    {
        builder.ToTable("ConversationParticipants");

        builder.Ignore(participant => participant.HasLeft);

        // Participation is the authorization check, so this lookup must be unique and indexed.
        builder.HasIndex(participant => new { participant.ConversationId, participant.UserId })
            .IsUnique()
            .HasDatabaseName("UX_ConversationParticipants_ConversationId_UserId");

        builder.HasIndex(participant => new { participant.UserId, participant.TenantId })
            .HasDatabaseName("IX_ConversationParticipants_UserId_TenantId");

        builder.HasOne(participant => participant.User)
            .WithMany()
            .HasForeignKey(participant => participant.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public sealed class MessageConfiguration : BaseEntityConfiguration<Message>
{
    protected override void ConfigureEntity(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.Property(message => message.Body).HasMaxLength(DomainRules.DescriptionMaxLength).IsRequired();

        builder.HasIndex(message => new { message.ConversationId, message.SentAt })
            .HasDatabaseName("IX_Messages_ConversationId_SentAt");

        builder.HasIndex(message => message.SenderUserId)
            .HasDatabaseName("IX_Messages_SenderUserId");

        builder.HasIndex(message => message.TenantId)
            .HasDatabaseName("IX_Messages_TenantId");

        builder.HasOne(message => message.Sender)
            .WithMany()
            .HasForeignKey(message => message.SenderUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(message => message.Attachments)
            .WithOne(attachment => attachment.Message)
            .HasForeignKey(attachment => attachment.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class MessageAttachmentConfiguration : BaseEntityConfiguration<MessageAttachment>
{
    protected override void ConfigureEntity(EntityTypeBuilder<MessageAttachment> builder)
    {
        builder.ToTable("MessageAttachments");

        builder.Property(attachment => attachment.FileName).HasMaxLength(DomainRules.NameMaxLength).IsRequired();
        builder.Property(attachment => attachment.FileUrl).HasMaxLength(DomainRules.UrlMaxLength).IsRequired();
        builder.Property(attachment => attachment.FilePublicId).HasMaxLength(300);
        builder.Property(attachment => attachment.FileType).HasConversion<int>();

        builder.HasIndex(attachment => attachment.MessageId)
            .HasDatabaseName("IX_MessageAttachments_MessageId");
    }
}
