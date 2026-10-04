using System.Text;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Application.Features.Homework;
using Ustaz.Application.Tests.Infrastructure;
using Ustaz.Domain.Entities;
using Ustaz.Domain.Entities.Education;
using Ustaz.Domain.Enums;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Application.Tests.Handlers;

/// <summary>
/// A student hands work in as a real file. The upload has to respect the same deadline,
/// enrollment and grading rules as a written submission, and the file itself has to clear
/// the shared size and type rules.
/// </summary>
public class HomeworkAttachmentTests : IDisposable
{
    private readonly TestHarness _harness = new();

    private Homework _homework = null!;

    public HomeworkAttachmentTests()
    {
        _harness.Media
            .Setup(media => media.UploadAsync(
                It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaUploadResult
            {
                Url = "https://cdn.ustaz.test/homework/answer.pdf",
                SecureUrl = "https://cdn.ustaz.test/homework/answer.pdf",
                PublicId = "homework/submissions/answer",
                Format = "pdf",
                Bytes = 2048
            });

        SeedHomework();
    }

    private UploadHomeworkSubmissionFileCommandHandler Handler() =>
        new(_harness.Context,
            _harness.CurrentUser.Object,
            _harness.Media.Object,
            _harness.Clock,
            _harness.Notifications.Object);

    private static FileUploadRequest Pdf(string name = "answer.pdf", long length = 2048) =>
        new()
        {
            Content = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.7 test")),
            FileName = name,
            ContentType = "application/pdf",
            Length = length
        };

    private void SeedHomework()
    {
        _harness.Context.GroupEnrollments.Add(new GroupEnrollment
        {
            Id = Guid.NewGuid(),
            GroupId = _harness.Group.Id,
            StudentId = _harness.Student.Id,
            Status = EnrollmentStatus.Active,
            EnrolledAt = _harness.UtcNow.AddDays(-10),
            PricePaid = 500m,
            FinalPrice = 500m
        });

        _homework = new Homework
        {
            Id = Guid.NewGuid(),
            TenantId = _harness.Tenant.Id,
            TeacherId = _harness.Teacher.Id,
            GroupId = _harness.Group.Id,
            SubjectId = _harness.Subject.Id,
            Title = "واجب الفيزياء",
            AssignedAt = _harness.UtcNow.AddDays(-1),
            DueAt = _harness.UtcNow.AddDays(2),
            MaximumScore = 10m,
            AllowLateSubmission = true,
            IsPublished = true,
            PublishedAt = _harness.UtcNow.AddDays(-1),
            CreatedAt = _harness.UtcNow.AddDays(-1)
        };

        _harness.Context.Homeworks.Add(_homework);
        _harness.Context.SaveChanges();
    }

    [Fact]
    public async Task Uploading_a_file_records_the_submission_against_the_student()
    {
        _harness.SignInAsStudent();

        var result = await Handler().Handle(
            new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf() },
            CancellationToken.None);

        result.FileUrl.ShouldBe("https://cdn.ustaz.test/homework/answer.pdf");
        result.Status.ShouldBe(HomeworkSubmissionStatus.Submitted);

        var stored = await _harness.Context.HomeworkSubmissions.FirstAsync();
        stored.StudentId.ShouldBe(_harness.Student.Id);
        stored.FilePublicId.ShouldBe("homework/submissions/answer");
        stored.IsLate.ShouldBeFalse();
    }

    [Fact]
    public async Task Replacing_the_file_removes_the_previous_asset()
    {
        _harness.SignInAsStudent();
        var handler = Handler();

        await handler.Handle(
            new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf() },
            CancellationToken.None);

        await handler.Handle(
            new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf("second.pdf") },
            CancellationToken.None);

        (await _harness.Context.HomeworkSubmissions.CountAsync()).ShouldBe(1);

        // The old asset is only dropped after the new one is stored, and only when it changed.
        _harness.Media.Verify(
            media => media.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task A_student_who_is_not_in_the_group_gets_a_not_found()
    {
        var stranger = new StudentProfile { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        _harness.Context.StudentProfiles.Add(stranger);
        await _harness.Context.SaveChangesAsync();

        _harness.SignInAsStudent();
        _harness.CurrentUser.Setup(user => user.RequireStudentId()).Returns(stranger.Id);

        await Should.ThrowAsync<NotFoundException>(() =>
            Handler().Handle(
                new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf() },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_graded_submission_can_no_longer_be_replaced()
    {
        _harness.SignInAsStudent();

        await Handler().Handle(
            new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf() },
            CancellationToken.None);

        var submission = await _harness.Context.HomeworkSubmissions.FirstAsync();
        submission.Status = HomeworkSubmissionStatus.Graded;
        await _harness.Context.SaveChangesAsync();

        await Should.ThrowAsync<ConflictException>(() =>
            Handler().Handle(
                new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf() },
                CancellationToken.None));
    }

    [Fact]
    public async Task A_late_upload_is_flagged_as_late()
    {
        _harness.SignInAsStudent();
        _harness.UtcNow = _homework.DueAt.AddDays(1);

        await Handler().Handle(
            new UploadHomeworkSubmissionFileCommand { HomeworkId = _homework.Id, File = Pdf() },
            CancellationToken.None);

        var stored = await _harness.Context.HomeworkSubmissions.FirstAsync();
        stored.IsLate.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // The shared upload rules
    // -----------------------------------------------------------------------

    [Fact]
    public void An_executable_is_rejected_whatever_content_type_it_claims()
    {
        var validator = new UploadHomeworkSubmissionFileCommandValidator();

        var result = validator.Validate(new UploadHomeworkSubmissionFileCommand
        {
            HomeworkId = Guid.NewGuid(),
            File = new FileUploadRequest
            {
                Content = Stream.Null,
                FileName = "malware.exe",
                ContentType = "application/pdf",
                Length = 1024
            }
        });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_file_over_the_size_cap_is_rejected()
    {
        var validator = new UploadHomeworkSubmissionFileCommandValidator();

        var result = validator.Validate(new UploadHomeworkSubmissionFileCommand
        {
            HomeworkId = Guid.NewGuid(),
            File = Pdf(length: FileUploadRules.MaxDocumentBytes + 1)
        });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void An_empty_file_is_rejected()
    {
        var validator = new UploadHomeworkSubmissionFileCommandValidator();

        var result = validator.Validate(new UploadHomeworkSubmissionFileCommand
        {
            HomeworkId = Guid.NewGuid(),
            File = Pdf(length: 0)
        });

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_pdf_within_the_cap_is_accepted()
    {
        var validator = new UploadHomeworkSubmissionFileCommandValidator();

        var result = validator.Validate(new UploadHomeworkSubmissionFileCommand
        {
            HomeworkId = Guid.NewGuid(),
            File = Pdf()
        });

        result.IsValid.ShouldBeTrue();
    }

    public void Dispose() => _harness.Dispose();
}
