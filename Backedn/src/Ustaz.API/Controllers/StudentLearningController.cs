using Microsoft.AspNetCore.RateLimiting;
using Ustaz.Application.Features.Announcements;
using Ustaz.Application.Features.Enrollments.Commands;
using Ustaz.Application.Features.Enrollments.Dtos;
using Ustaz.Application.Features.Enrollments.Queries;
using Ustaz.Application.Features.Attendance;
using Ustaz.Application.Features.Attendance.Dtos;
using Ustaz.Application.Features.Exams;
using Ustaz.Application.Features.Exams.Dtos;
using Ustaz.Application.Features.Homework;
using Ustaz.API.Extensions;
using Ustaz.Application.Features.Parents;
using Ustaz.Application.Features.Payments;

namespace Ustaz.API.Controllers;

/// <summary>
/// The signed-in student's academic record: attendance, exams, grades, homework,
/// announcements and tuition. Everything is scoped to the caller's own student profile.
/// </summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/student")]
[ApiExplorerSettings(GroupName = "Students")]
[Authorize(Policy = PolicyNames.StudentOnly)]
public sealed class StudentLearningController : ApiControllerBase
{
    /// <summary>The student's own attendance history.</summary>
    [HttpGet("attendance")]
    [SwaggerOperation(Tags = ["Attendance"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AttendanceRecordDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAttendance(
        [FromQuery] GetMyAttendanceQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Published exams for the student's groups.</summary>
    [HttpGet("exams")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ExamDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetExams(
        [FromQuery] GetMyExamsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One exam the student can see.</summary>
    [HttpGet("exams/{id:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExam(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetExamByIdQuery(id), cancellationToken));

    /// <summary>The student's released grades, with per-subject averages.</summary>
    [HttpGet("grades")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<StudentGradesSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGrades(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetMyGradesQuery(), cancellationToken));

    /// <summary>Homework assigned to the student, with their submission status.</summary>
    [HttpGet("homework")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<HomeworkDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetHomework(
        [FromQuery] GetMyHomeworkQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One homework assignment, including the student's own submission.</summary>
    [HttpGet("homework/{id:guid}")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHomeworkById(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetHomeworkByIdQuery(id), cancellationToken));

    /// <summary>Submits homework. Late submissions are accepted only when the teacher allows them.</summary>
    [HttpPost("homework/{id:guid}/submit")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkSubmissionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitHomework(
        Guid id,
        [FromBody] SubmitHomeworkCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { HomeworkId = id }, cancellationToken),
            "Homework submitted successfully.");

    /// <summary>Replaces a submission that has not been graded yet.</summary>
    [HttpPut("homework/submissions/{id:guid}")]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkSubmissionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSubmission(
        Guid id,
        [FromBody] UpdateSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        var submission = await Mediator.Send(new GetHomeworkSubmissionQuery(id), cancellationToken);

        return OkResponse(
            await Mediator.Send(
                new SubmitHomeworkCommand
                {
                    HomeworkId = submission.HomeworkId,
                    Content = request.Content,
                    FileUrl = request.FileUrl
                },
                cancellationToken),
            "Submission updated successfully.");
    }

    /// <summary>Announcements addressed to the student.</summary>
    [HttpGet("announcements")]
    [SwaggerOperation(Tags = ["Announcements"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<AnnouncementDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAnnouncements(
        [FromQuery] GetMyAnnouncementsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>The student's tuition payments.</summary>
    [HttpGet("payments")]
    [SwaggerOperation(Tags = ["Payments"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<EducationalPaymentDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPayments(
        [FromQuery] GetMyPaymentsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>Attaches a file to the student's submission for one homework assignment.</summary>
    [HttpPost("homework/{id:guid}/attachment")]
    [EnableRateLimiting(ApiServiceExtensions.UploadRateLimitPolicy)]
    [SwaggerOperation(Tags = ["Homework"])]
    [ProducesResponseType(typeof(ApiResponse<HomeworkSubmissionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadHomeworkAttachment(
        Guid id,
        IFormFile file,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new UploadHomeworkSubmissionFileCommand { HomeworkId = id, File = await file.ToUploadRequestAsync(cancellationToken) },
                cancellationToken),
            "Your file has been attached.");

    /// <summary>Starts an online exam, or resumes the sitting already in progress.</summary>
    [HttpPost("exams/{id:guid}/attempts")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamAttemptSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> StartExamAttempt(Guid id, CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new StartExamAttemptCommand(id), cancellationToken),
            "Exam started.");

    /// <summary>The student's own sittings of one exam.</summary>
    [HttpGet("exams/{id:guid}/attempts")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamAttemptDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyExamAttempts(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetMyExamAttemptsQuery(id), cancellationToken));

    /// <summary>One of the student's own sittings. Marks appear only once released.</summary>
    [HttpGet("exams/attempts/{attemptId:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamAttemptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyExamAttempt(Guid attemptId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetExamAttemptQuery(attemptId), cancellationToken));

    /// <summary>Submits the answers for a sitting and grades everything that grades itself.</summary>
    [HttpPost("exams/attempts/{attemptId:guid}/submit")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamAttemptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SubmitExamAttempt(
        Guid attemptId,
        [FromBody] SubmitExamAttemptCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { AttemptId = attemptId }, cancellationToken),
            "Your answers have been submitted.");

    /// <summary>Lesson-by-lesson progress through a course the student is enrolled in.</summary>
    [HttpGet("courses/{courseId:guid}/progress")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<CourseProgressReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCourseProgress(
        Guid courseId,
        CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetCourseProgressReportQuery(courseId), cancellationToken));

    /// <summary>Marks one lesson as completed and recomputes the course percentage.</summary>
    [HttpPost("courses/{courseId:guid}/lessons/{lessonId:guid}/complete")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<LessonProgressSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CompleteLesson(
        Guid courseId,
        Guid lessonId,
        [FromBody] CompleteLessonRequest? request,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(
                new CompleteLessonCommand
                {
                    CourseId = courseId,
                    LessonId = lessonId,
                    WatchedSeconds = request?.WatchedSeconds
                },
                cancellationToken),
            "Lesson completed.");

    /// <summary>Reopens a completed lesson and recomputes the course percentage.</summary>
    [HttpDelete("courses/{courseId:guid}/lessons/{lessonId:guid}/complete")]
    [SwaggerOperation(Tags = ["Courses"])]
    [ProducesResponseType(typeof(ApiResponse<LessonProgressSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReopenLesson(
        Guid courseId,
        Guid lessonId,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(new ReopenLessonCommand(courseId, lessonId), cancellationToken),
            "Lesson reopened.");

    /// <summary>
    /// The guardians following this student, pending requests first.
    /// </summary>
    /// <remarks>
    /// Each row carries the <c>linkId</c> that
    /// <c>POST /student/guardians/{linkId}/respond</c> takes, so the screen reached from a
    /// link-request notification can answer it without any other call.
    /// </remarks>
    [HttpGet("guardians")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<GuardianDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyGuardians(CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetMyGuardiansQuery(), cancellationToken));

    /// <summary>Approves or rejects a guardian's request to follow this student.</summary>
    [HttpPost("guardians/{linkId:guid}/respond")]
    [SwaggerOperation(Tags = ["Parents"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondToGuardian(
        Guid linkId,
        [FromQuery] bool approve,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new RespondToGuardianLinkCommand(linkId, approve), cancellationToken);
        return OkResponse(approve ? "Guardian approved." : "Guardian request rejected.");
    }
}

/// <summary>Body for replacing a homework submission.</summary>
public sealed record UpdateSubmissionRequest
{
    public string? Content { get; init; }

    public string? FileUrl { get; init; }
}

/// <summary>Optional body for POST /student/courses/{courseId}/lessons/{lessonId}/complete.</summary>
public sealed record CompleteLessonRequest
{
    public int? WatchedSeconds { get; init; }
}
