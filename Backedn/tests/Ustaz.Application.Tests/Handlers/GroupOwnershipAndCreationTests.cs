using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Features.Groups.Commands;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

public class GroupOwnershipAndCreationTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly Mock<IWhatsAppLinkService> _whatsApp = new();
    private readonly Mock<IUrlGuard> _urlGuard = new();
    private readonly Mock<IMediator> _mediator = new();

    public GroupOwnershipAndCreationTests()
    {
        _urlGuard.Setup(guard => guard.IsSafeHttpUrl(It.IsAny<string>())).Returns(true);
        _urlGuard.Setup(guard => guard.EnsureSafeHttpUrl(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string url, string _) => url);
    }

    private CreateGroupCommandHandler CreateHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _whatsApp.Object,
            _urlGuard.Object,
            _harness.Cache.Object,
            _harness.PlanLimits,
            _mediator.Object);

    private UpdateGroupCommandHandler UpdateHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _whatsApp.Object,
            _harness.Cache.Object,
            _mediator.Object);

    private static CreateGroupCommand BuildCommand(TestHarness harness, Guid? subjectId = null, Guid? gradeId = null) =>
        new()
        {
            SubjectId = subjectId ?? harness.Subject.Id,
            GradeId = gradeId ?? harness.Grade.Id,
            Name = "مجموعة جديدة",
            Description = "وصف",
            MonthlyPrice = 400m,
            Capacity = 20,
            StartDate = harness.UtcNow.AddDays(7),
            Location = "القاهرة",
            Schedules =
            [
                new GroupScheduleInput
                {
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(16, 0),
                    EndTime = new TimeOnly(18, 0)
                }
            ]
        };

    [Fact]
    public async Task A_teacher_cannot_create_a_group_for_a_subject_they_do_not_teach()
    {
        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CreateHandler().Handle(
                BuildCommand(_harness, subjectId: _harness.OtherSubject.Id), CancellationToken.None));

        exception.Code.ShouldBe("SUBJECT_NOT_TAUGHT");
    }

    [Fact]
    public async Task A_teacher_cannot_create_a_group_for_a_grade_they_do_not_teach()
    {
        _harness.SignInAsTeacher();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CreateHandler().Handle(
                BuildCommand(_harness, gradeId: Guid.NewGuid()), CancellationToken.None));

        exception.Code.ShouldBe("GRADE_NOT_TAUGHT");
    }

    [Fact]
    public async Task Created_groups_start_as_unpublished_drafts_with_a_unique_slug()
    {
        _harness.SignInAsTeacher();

        await CreateHandler().Handle(BuildCommand(_harness), CancellationToken.None);

        var created = await _harness.Context.Groups
            .FirstAsync(group => group.Name == "مجموعة جديدة");

        created.IsPublished.ShouldBeFalse();
        created.IsActive.ShouldBeTrue();
        created.TeacherId.ShouldBe(_harness.Teacher.Id);
        created.Slug.ShouldNotBeNullOrWhiteSpace();
        created.Slug.ShouldNotBe(_harness.Group.Slug);
        created.Schedules.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Overlapping_sessions_in_the_same_group_are_rejected()
    {
        _harness.SignInAsTeacher();

        var command = BuildCommand(_harness) with
        {
            Schedules =
            [
                new GroupScheduleInput
                {
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(16, 0),
                    EndTime = new TimeOnly(18, 0)
                },
                new GroupScheduleInput
                {
                    DayOfWeek = DayOfWeek.Tuesday,
                    StartTime = new TimeOnly(17, 0),
                    EndTime = new TimeOnly(19, 0)
                }
            ]
        };

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            CreateHandler().Handle(command, CancellationToken.None));

        exception.Code.ShouldBe("SCHEDULE_OVERLAP");
    }

    [Fact]
    public async Task A_teacher_cannot_update_another_teachers_group()
    {
        _harness.SignInAsTeacher(_harness.OtherTeacher);

        await Should.ThrowAsync<ForbiddenAccessException>(() =>
            UpdateHandler().Handle(
                new UpdateGroupCommand { GroupId = _harness.Group.Id, Name = "Hijacked" },
                CancellationToken.None));
    }

    [Fact]
    public async Task Capacity_cannot_drop_below_the_number_of_enrolled_students()
    {
        _harness.SignInAsTeacher();

        var group = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        group.EnrolledStudentsCount = 5;
        group.Capacity = 10;
        await _harness.Context.SaveChangesAsync();

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            UpdateHandler().Handle(
                new UpdateGroupCommand { GroupId = _harness.Group.Id, Capacity = 3 },
                CancellationToken.None));

        exception.Code.ShouldBe("CAPACITY_BELOW_ENROLLED");
    }

    [Fact]
    public async Task Publishing_requires_a_verified_teacher()
    {
        _harness.SignInAsTeacher();

        var teacher = await _harness.Context.TeacherProfiles.FirstAsync(t => t.Id == _harness.Teacher.Id);
        teacher.IsVerified = false;
        teacher.VerificationStatus = Ustaz.Domain.Enums.VerificationStatus.Pending;
        await _harness.Context.SaveChangesAsync();

        var handler = new PublishGroupCommandHandler(
            _harness.Context, _harness.CurrentUser.Object, _harness.Clock, _harness.Cache.Object);

        var exception = await Should.ThrowAsync<BusinessRuleException>(() =>
            handler.Handle(new PublishGroupCommand(_harness.Group.Id), CancellationToken.None));

        exception.Code.ShouldBe("TEACHER_NOT_VERIFIED");
    }

    [Fact]
    public async Task Publishing_succeeds_for_a_verified_teacher_with_an_active_session()
    {
        _harness.SignInAsTeacher();

        var group = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        group.IsPublished = false;
        await _harness.Context.SaveChangesAsync();

        var handler = new PublishGroupCommandHandler(
            _harness.Context, _harness.CurrentUser.Object, _harness.Clock, _harness.Cache.Object);

        await handler.Handle(new PublishGroupCommand(_harness.Group.Id), CancellationToken.None);

        var reloaded = await _harness.Context.Groups.FirstAsync(g => g.Id == _harness.Group.Id);
        reloaded.IsPublished.ShouldBeTrue();
    }

    public void Dispose() => _harness.Dispose();
}
