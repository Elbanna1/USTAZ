using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities.Tenancy;

namespace Ustaz.Persistence.Configurations.Tenancy;

public sealed class TenantMemberConfiguration : BaseEntityConfiguration<TenantMember>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantMember> builder)
    {
        builder.ToTable("TenantMembers");

        builder.Property(member => member.Role).HasConversion<int>();
        builder.Property(member => member.Status).HasConversion<int>();
        builder.Property(member => member.Title).HasMaxLength(150);

        builder.Ignore(member => member.IsOwner);
        builder.Ignore(member => member.IsActive);

        // One membership row per user per workspace.
        builder.HasIndex(member => new { member.TenantId, member.UserId })
            .IsUnique()
            .HasDatabaseName("UX_TenantMembers_TenantId_UserId");

        builder.HasIndex(member => member.UserId)
            .HasDatabaseName("IX_TenantMembers_UserId");

        builder.HasIndex(member => new { member.TenantId, member.Role, member.Status })
            .HasDatabaseName("IX_TenantMembers_TenantId_Role_Status");

        builder.HasOne(member => member.Tenant)
            .WithMany(tenant => tenant.Members)
            .HasForeignKey(member => member.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        // NoAction on the user side: the cascade already arrives via the tenant.
        builder.HasOne(member => member.User)
            .WithMany(user => user.TenantMemberships)
            .HasForeignKey(member => member.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(member => member.Permissions)
            .WithOne(permission => permission.TenantMember)
            .HasForeignKey(permission => permission.TenantMemberId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TenantMemberPermissionConfiguration : BaseEntityConfiguration<TenantMemberPermission>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantMemberPermission> builder)
    {
        builder.ToTable("TenantMemberPermissions");

        builder.Property(permission => permission.Permission).HasMaxLength(100).IsRequired();

        builder.HasIndex(permission => new { permission.TenantMemberId, permission.Permission })
            .IsUnique()
            .HasDatabaseName("UX_TenantMemberPermissions_Member_Permission");
    }
}

public sealed class TenantInvitationConfiguration : BaseEntityConfiguration<TenantInvitation>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantInvitation> builder)
    {
        builder.ToTable("TenantInvitations");

        builder.Property(invitation => invitation.Email).HasMaxLength(256).IsRequired();
        builder.Property(invitation => invitation.TokenHash).HasMaxLength(128).IsRequired();
        builder.Property(invitation => invitation.Title).HasMaxLength(150);
        builder.Property(invitation => invitation.PermissionsCsv).HasMaxLength(2000);
        builder.Property(invitation => invitation.Role).HasConversion<int>();

        builder.Ignore(invitation => invitation.IsPending);

        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName("UX_TenantInvitations_TokenHash");

        builder.HasIndex(invitation => new { invitation.TenantId, invitation.Email })
            .HasDatabaseName("IX_TenantInvitations_TenantId_Email");

        builder.HasIndex(invitation => invitation.Email)
            .HasDatabaseName("IX_TenantInvitations_Email");

        builder.HasOne(invitation => invitation.Tenant)
            .WithMany()
            .HasForeignKey(invitation => invitation.TenantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TenantStudentConfiguration : BaseEntityConfiguration<TenantStudent>
{
    protected override void ConfigureEntity(EntityTypeBuilder<TenantStudent> builder)
    {
        builder.ToTable("TenantStudents");

        builder.Property(link => link.StudentCode).HasMaxLength(50);
        builder.Property(link => link.Notes).HasMaxLength(DomainRules.ShortTextMaxLength);

        builder.HasIndex(link => new { link.TenantId, link.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_TenantStudents_TenantId_StudentId");

        builder.HasIndex(link => link.StudentId)
            .HasDatabaseName("IX_TenantStudents_StudentId");

        // Drives the plan's student quota check.
        builder.HasIndex(link => new { link.TenantId, link.IsActive })
            .HasDatabaseName("IX_TenantStudents_TenantId_IsActive");

        builder.HasOne(link => link.Tenant)
            .WithMany(tenant => tenant.Students)
            .HasForeignKey(link => link.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.Student)
            .WithMany(student => student.TenantMemberships)
            .HasForeignKey(link => link.StudentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
