using Microsoft.EntityFrameworkCore;
using Shouldly;
using Ustaz.Application.Features.Students;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

public class StudentProfileTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private GetStudentProfileQueryHandler QueryHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object);

    private UpdateStudentProfileCommandHandler UpdateHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object, _harness.Clock);

    [Fact]
    public async Task A_student_reads_their_own_profile()
    {
        _harness.SignInAsStudent();

        var profile = await QueryHandler().Handle(new GetStudentProfileQuery(), CancellationToken.None);

        profile.Id.ShouldBe(_harness.Student.Id);
        profile.Email.ShouldBe("salma@ustaz.test");
        profile.DisplayName.ShouldBe("سلمى إبراهيم");
    }

    [Fact]
    public async Task Updating_the_profile_writes_the_supplied_fields_and_leaves_the_rest_alone()
    {
        _harness.SignInAsStudent();

        await UpdateHandler().Handle(
            new UpdateStudentProfileCommand
            {
                FirstName = "سلمى",
                LastName = "محمد",
                SchoolName = "مدرسة النيل",
                PhoneNumber = "01001234567",
                GradeId = _harness.Grade.Id
            },
            CancellationToken.None);

        var stored = await _harness.Context.StudentProfiles
            .Include(student => student.User)
            .FirstAsync(student => student.Id == _harness.Student.Id);

        stored.User.DisplayName.ShouldBe("سلمى محمد");
        stored.SchoolName.ShouldBe("مدرسة النيل");
        stored.PhoneNumber.ShouldBe("01001234567");
        stored.User.PhoneNumber.ShouldBe("01001234567");
        stored.GradeId.ShouldBe(_harness.Grade.Id);

        // Nothing that was not sent may change.
        stored.User.Email.ShouldBe("salma@ustaz.test");
    }

    [Fact]
    public async Task An_unknown_grade_is_rejected()
    {
        _harness.SignInAsStudent();

        await Should.ThrowAsync<NotFoundException>(() =>
            UpdateHandler().Handle(
                new UpdateStudentProfileCommand { GradeId = Guid.NewGuid() },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_student_without_a_profile_row_gets_a_not_found()
    {
        var stranger = new StudentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };

        _harness.SignInAsStudent();
        _harness.CurrentUser.Setup(user => user.RequireStudentId()).Returns(stranger.Id);

        await Should.ThrowAsync<NotFoundException>(() =>
            QueryHandler().Handle(new GetStudentProfileQuery(), CancellationToken.None));
    }

    [Fact]
    public void The_validator_rejects_a_phone_number_that_is_not_egyptian()
    {
        var validator = new UpdateStudentProfileCommandValidator();

        var result = validator.Validate(new UpdateStudentProfileCommand { PhoneNumber = "12345" });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void The_validator_rejects_a_date_of_birth_in_the_future()
    {
        var validator = new UpdateStudentProfileCommandValidator();

        var result = validator.Validate(new UpdateStudentProfileCommand
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
        });

        result.IsValid.ShouldBeFalse();
    }

    public void Dispose() => _harness.Dispose();
}
