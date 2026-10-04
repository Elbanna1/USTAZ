using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Ustaz.Application.Features.Admin.Commands;
using Ustaz.Application.Features.Teachers.Commands;
using Ustaz.Application.Features.Teachers.Dtos;
using Ustaz.Application.Features.Teachers.Queries;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// Covers the verification workflow end to end: a teacher may only submit a profile worth
/// reviewing, an admin moves it through review, and a rejected teacher can fix and resubmit.
/// </summary>
public class TeacherVerificationTests : IDisposable
{
    private readonly TestHarness _harness = new();
    private readonly Mock<IMediator> _mediator = new();

    public TeacherVerificationTests()
    {
        _harness.SignInAsTeacher();
    }

    private GetTeacherVerificationQueryHandler QueryHandler() =>
        new(_harness.Context, _harness.CurrentUser.Object);

    private SubmitTeacherVerificationCommandHandler SubmitHandler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object,
            _mediator.Object);

    private VerifyTeacherCommandHandler VerifyHandler() =>
        new(_harness.Context,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object);

    private BeginTeacherReviewCommandHandler ReviewHandler() =>
        new(_harness.Context,
            _harness.Clock,
            _harness.Notifications.Object,
            _harness.Cache.Object);

    /// <summary>Returns the teacher to a fresh, unsubmitted, incomplete state.</summary>
    private async Task<TeacherProfile> ResetToUnsubmittedAsync(bool complete)
    {
        var teacher = await _harness.Context.TeacherProfiles
            .Include(profile => profile.TeacherSubjects)
            .Include(profile => profile.TeacherGrades)
            .FirstAsync(profile => profile.Id == _harness.Teacher.Id);

        teacher.IsVerified = false;
        teacher.VerificationStatus = VerificationStatus.NotSubmitted;
        teacher.VerifiedAt = null;
        teacher.VerificationSubmittedAt = null;
        teacher.VerificationNotes = null;

        if (complete)
        {
            teacher.Headline = "مدرّس رياضيات للثانوية العامة";
            teacher.Bio = new string('م', TeacherProfile.MinVerifiableBioLength + 10);
            teacher.WhatsAppNumber = "01000000000";
        }
        else
        {
            teacher.Headline = null;
            teacher.Bio = null;
            teacher.WhatsAppNumber = null;
        }

        await _harness.Context.SaveChangesAsync(CancellationToken.None);
        return teacher;
    }

    [Fact]
    public async Task Submitting_an_incomplete_profile_is_refused_with_a_coded_error()
    {
        await ResetToUnsubmittedAsync(complete: false);

        var error = await Should.ThrowAsync<BusinessRuleException>(
            () => SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None));

        error.Code.ShouldBe("VERIFICATION_PROFILE_INCOMPLETE");

        var teacher = await _harness.Context.TeacherProfiles.FirstAsync(p => p.Id == _harness.Teacher.Id);
        teacher.VerificationStatus.ShouldBe(VerificationStatus.NotSubmitted);
    }

    [Fact]
    public async Task The_checklist_reports_exactly_which_requirements_are_missing()
    {
        await ResetToUnsubmittedAsync(complete: false);

        var result = await QueryHandler().Handle(new GetTeacherVerificationQuery(), CancellationToken.None);

        result.IsProfileComplete.ShouldBeFalse();
        result.CanSubmit.ShouldBeFalse();
        Missing(result, "headline").ShouldBeTrue();
        Missing(result, "bio").ShouldBeTrue();
        Missing(result, "whatsAppNumber").ShouldBeTrue();

        // The harness seeds a subject and a grade, so those requirements are already met.
        Missing(result, "subjects").ShouldBeFalse();
        Missing(result, "grades").ShouldBeFalse();
    }

    private static bool Missing(TeacherVerificationDto dto, string key) =>
        !dto.Requirements.Single(requirement => requirement.Key == key).IsMet;

    [Fact]
    public async Task A_complete_profile_can_be_submitted_and_lands_in_the_pending_queue()
    {
        await ResetToUnsubmittedAsync(complete: true);

        await SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None);

        var teacher = await _harness.Context.TeacherProfiles.FirstAsync(p => p.Id == _harness.Teacher.Id);
        teacher.VerificationStatus.ShouldBe(VerificationStatus.Pending);
        teacher.VerificationSubmittedAt.ShouldNotBeNull();
        teacher.IsVerified.ShouldBeFalse();
    }

    [Fact]
    public async Task Submitting_twice_is_refused_while_the_request_is_still_in_review()
    {
        await ResetToUnsubmittedAsync(complete: true);
        await SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None);

        var error = await Should.ThrowAsync<BusinessRuleException>(
            () => SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None));

        error.Code.ShouldBe("VERIFICATION_ALREADY_SUBMITTED");
    }

    [Fact]
    public async Task An_admin_moves_a_pending_request_into_review()
    {
        await ResetToUnsubmittedAsync(complete: true);
        await SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None);

        await ReviewHandler().Handle(new BeginTeacherReviewCommand(_harness.Teacher.Id), CancellationToken.None);

        var teacher = await _harness.Context.TeacherProfiles.FirstAsync(p => p.Id == _harness.Teacher.Id);
        teacher.VerificationStatus.ShouldBe(VerificationStatus.UnderReview);
    }

    [Fact]
    public async Task Review_cannot_begin_on_a_request_that_was_never_submitted()
    {
        await ResetToUnsubmittedAsync(complete: true);

        var error = await Should.ThrowAsync<BusinessRuleException>(
            () => ReviewHandler().Handle(new BeginTeacherReviewCommand(_harness.Teacher.Id), CancellationToken.None));

        error.Code.ShouldBe("VERIFICATION_NOT_PENDING");
    }

    [Fact]
    public async Task A_rejected_teacher_can_fix_the_profile_and_submit_again()
    {
        await ResetToUnsubmittedAsync(complete: true);
        await SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None);

        await VerifyHandler().Handle(
            new VerifyTeacherCommand
            {
                TeacherId = _harness.Teacher.Id,
                Approve = false,
                Notes = "الصورة مش واضحة."
            },
            CancellationToken.None);

        var rejected = await _harness.Context.TeacherProfiles.FirstAsync(p => p.Id == _harness.Teacher.Id);
        rejected.VerificationStatus.ShouldBe(VerificationStatus.Rejected);
        rejected.VerificationNotes.ShouldBe("الصورة مش واضحة.");

        // Rejection must not be a dead end.
        await SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None);

        var resubmitted = await _harness.Context.TeacherProfiles.FirstAsync(p => p.Id == _harness.Teacher.Id);
        resubmitted.VerificationStatus.ShouldBe(VerificationStatus.Pending);

        // The old reason must not linger against the new submission.
        resubmitted.VerificationNotes.ShouldBeNull();
    }

    [Fact]
    public async Task An_approved_teacher_may_publish_and_cannot_submit_again()
    {
        await ResetToUnsubmittedAsync(complete: true);
        await SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None);

        await VerifyHandler().Handle(
            new VerifyTeacherCommand { TeacherId = _harness.Teacher.Id, Approve = true },
            CancellationToken.None);

        var teacher = await _harness.Context.TeacherProfiles
            .Include(profile => profile.TeacherSubjects)
            .Include(profile => profile.TeacherGrades)
            .FirstAsync(p => p.Id == _harness.Teacher.Id);

        teacher.VerificationStatus.ShouldBe(VerificationStatus.Verified);
        teacher.IsVerified.ShouldBeTrue();
        teacher.CanPublish.ShouldBeTrue();

        var error = await Should.ThrowAsync<BusinessRuleException>(
            () => SubmitHandler().Handle(new SubmitTeacherVerificationCommand(), CancellationToken.None));

        error.Code.ShouldBe("TEACHER_ALREADY_VERIFIED");
    }

    [Fact]
    public async Task An_unverified_teacher_is_blocked_from_publishing()
    {
        var teacher = await ResetToUnsubmittedAsync(complete: true);

        var error = Should.Throw<BusinessRuleException>(() => teacher.EnsureCanPublish());
        error.Code.ShouldBe("TEACHER_NOT_VERIFIED");
    }

    public void Dispose()
    {
        _harness.Dispose();
        GC.SuppressFinalize(this);
    }
}
