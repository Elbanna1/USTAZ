using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Constants;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Identity;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// A workspace registers students at the desk, not only through the learner's own app. These
/// tests cover that path plus the workspace membership every tenant-scoped feature reads.
/// </summary>
public class StaffEnrollmentTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private EnrollStudentInGroupCommandHandler StaffHandler() =>
        new(_harness.Context,
            _harness.TenantAuthorization,
            _harness.TenantProvisioning,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object,
            _harness.Audit.Object);

    private EnrollInGroupCommandHandler SelfHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object,
            _harness.TenantProvisioning);

    /// <summary>A learner with no workspace membership of their own.</summary>
    private StudentProfile AddStudent(string email)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = "طالب",
            LastName = "جديد",
            DisplayName = "طالب جديد",
            IsActive = true
        };

        var student = new StudentProfile { Id = Guid.NewGuid(), UserId = user.Id, User = user };

        _harness.Context.Users.Add(user);
        _harness.Context.StudentProfiles.Add(student);
        _harness.Context.SaveChanges();

        return student;
    }

    [Fact]
    public async Task A_teacher_can_enroll_a_student_into_their_own_group()
    {
        var student = AddStudent("walkin@ustaz.test");
        _harness.SignInAsTeacher();

        var result = await StaffHandler().Handle(
            new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
            CancellationToken.None);

        result.Status.ShouldBe(EnrollmentStatus.Active);
        result.FinalPrice.ShouldBe(500m);

        var group = await _harness.Context.Groups.FirstAsync(entity => entity.Id == _harness.Group.Id);
        group.EnrolledStudentsCount.ShouldBe(1);
    }

    [Fact]
    public async Task An_agreed_fee_overrides_the_list_price()
    {
        var student = AddStudent("discounted@ustaz.test");
        _harness.SignInAsTeacher();

        var result = await StaffHandler().Handle(
            new EnrollStudentInGroupCommand
            {
                GroupId = _harness.Group.Id,
                StudentId = student.Id,
                MonthlyPrice = 350m
            },
            CancellationToken.None);

        result.FinalPrice.ShouldBe(350m);
    }

    [Fact]
    public async Task Enrolling_the_same_student_twice_is_a_conflict()
    {
        var student = AddStudent("twice@ustaz.test");
        _harness.SignInAsTeacher();

        var command = new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id };

        await StaffHandler().Handle(command, CancellationToken.None);

        await Should.ThrowAsync<ConflictException>(() =>
            StaffHandler().Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task A_full_group_is_refused()
    {
        var student = AddStudent("full@ustaz.test");

        var group = await _harness.Context.Groups.FirstAsync(entity => entity.Id == _harness.Group.Id);
        group.EnrolledStudentsCount = group.Capacity;
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            StaffHandler().Handle(
                new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
                CancellationToken.None));

        exception.Code.ShouldBe("GROUP_FULL");
    }

    [Fact]
    public async Task Staff_may_fill_a_group_that_is_still_a_draft()
    {
        var student = AddStudent("draft@ustaz.test");

        var group = await _harness.Context.Groups.FirstAsync(entity => entity.Id == _harness.Group.Id);
        group.IsPublished = false;
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsTeacher();

        var result = await StaffHandler().Handle(
            new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
            CancellationToken.None);

        result.Status.ShouldBe(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task A_teacher_from_another_workspace_cannot_fill_this_group()
    {
        var student = AddStudent("crosstenant@ustaz.test");
        _harness.SignInAsTeacher(_harness.OtherTeacher);

        await Should.ThrowAsync<NotFoundException>(() =>
            StaffHandler().Handle(
                new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_member_without_the_students_permission_is_refused()
    {
        var student = AddStudent("nopermission@ustaz.test");

        var member = _harness.AddMember(
            _harness.Tenant, "assistant@ustaz.test", TenantMemberRole.Assistant, TenantPermissions.GroupsView);

        _harness.SignInAsMember(
            await _harness.Context.Users.FirstAsync(user => user.Id == member.UserId), _harness.Tenant);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            StaffHandler().Handle(
                new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
                CancellationToken.None));
    }

    [Fact]
    public async Task An_assistant_holding_the_students_permission_may_enroll()
    {
        var student = AddStudent("assistant-enrolled@ustaz.test");

        var member = _harness.AddMember(
            _harness.Tenant, "desk@ustaz.test", TenantMemberRole.Assistant, TenantPermissions.StudentsManage);

        _harness.SignInAsMember(
            await _harness.Context.Users.FirstAsync(user => user.Id == member.UserId), _harness.Tenant);

        var result = await StaffHandler().Handle(
            new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
            CancellationToken.None);

        result.Status.ShouldBe(EnrollmentStatus.Active);
    }

    [Fact]
    public async Task An_unknown_student_is_not_found()
    {
        _harness.SignInAsTeacher();

        await Should.ThrowAsync<NotFoundException>(() =>
            StaffHandler().Handle(
                new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = Guid.NewGuid() },
                CancellationToken.None));
    }

    // -----------------------------------------------------------------------
    // Workspace membership
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Enrolling_registers_the_student_against_the_workspace()
    {
        var student = AddStudent("membership@ustaz.test");
        _harness.SignInAsTeacher();

        await StaffHandler().Handle(
            new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
            CancellationToken.None);

        var membership = await _harness.Context.TenantStudents
            .FirstOrDefaultAsync(link => link.TenantId == _harness.Tenant.Id && link.StudentId == student.Id);

        membership.ShouldNotBeNull();
        membership.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task A_student_joining_a_group_themselves_also_joins_the_workspace()
    {
        // The seeded student is pre-linked, so use a fresh learner for this.
        var student = AddStudent("selfjoin@ustaz.test");

        _harness.SignInAsStudent();
        _harness.CurrentUser.Setup(user => user.RequireStudentId()).Returns(student.Id);

        await SelfHandler().Handle(
            new EnrollInGroupCommand { GroupId = _harness.Group.Id }, CancellationToken.None);

        var membership = await _harness.Context.TenantStudents
            .FirstOrDefaultAsync(link => link.TenantId == _harness.Tenant.Id && link.StudentId == student.Id);

        membership.ShouldNotBeNull();
        membership.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task The_plans_student_quota_is_charged_for_manual_enrollments()
    {
        var plan = await _harness.Context.SubscriptionPlans.FirstAsync();
        plan.MaxStudents = 1;
        await _harness.Context.SaveChangesAsync();

        // The seeded student already occupies the workspace's only seat.
        var student = AddStudent("over-quota@ustaz.test");
        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            StaffHandler().Handle(
                new EnrollStudentInGroupCommand { GroupId = _harness.Group.Id, StudentId = student.Id },
                CancellationToken.None));

        exception.Code.ShouldBe("PLAN_LIMIT_REACHED");
    }

    public void Dispose() => _harness.Dispose();
}
