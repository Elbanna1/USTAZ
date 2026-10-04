using Ustaz.Application.Features.Exams;
using Ustaz.Application.Features.Exams.Dtos;

namespace Ustaz.API.Controllers;

/// <summary>Exam authoring and grading for the caller's workspace.</summary>
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/teacher/exams")]
[ApiExplorerSettings(GroupName = "Exams")]
[Authorize(Policy = PolicyNames.TenantMember)]
public sealed class TeacherExamsController : ApiControllerBase
{
    /// <summary>Creates an exam as a draft.</summary>
    [HttpPost]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateExamCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(await Mediator.Send(command, cancellationToken), "Exam created successfully.");

    /// <summary>Exams in the workspace, filterable by group, subject, type and date.</summary>
    [HttpGet]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ExamDto>>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetExams(
        [FromQuery] GetExamsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query, cancellationToken);

    /// <summary>One exam, with score statistics.</summary>
    [HttpGet("{id:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExam(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetExamByIdQuery(id), cancellationToken));

    /// <summary>Updates an exam. Changing the scale recomputes every recorded percentage.</summary>
    [HttpPut("{id:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateExamCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ExamId = id }, cancellationToken),
            "Exam updated successfully.");

    /// <summary>Deletes an exam that has no recorded results.</summary>
    [HttpDelete("{id:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteExamCommand(id), cancellationToken);
        return NoContentResponse();
    }

    /// <summary>Publishes the exam, and optionally releases its results to students.</summary>
    [HttpPost("{id:guid}/publish")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Publish(
        Guid id,
        [FromQuery] bool publishResults = false,
        CancellationToken cancellationToken = default)
    {
        await Mediator.Send(new PublishExamCommand(id, publishResults), cancellationToken);
        return OkResponse("Exam published successfully.");
    }

    /// <summary>
    /// Full analytics for one exam: overview, cohort performance, score distribution,
    /// per-question difficulty, standings and plain-language insights. Question rows never
    /// carry the answer key. Question-level sections are empty for an offline exam, which
    /// records only the total mark.
    /// </summary>
    [HttpGet("{id:guid}/analytics")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamAnalyticsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalytics(
        Guid id,
        [FromQuery] int take = 5,
        CancellationToken cancellationToken = default) =>
        OkResponse(await Mediator.Send(
            new GetExamAnalyticsQuery(id) { Take = take },
            cancellationToken));

    /// <summary>Results for one exam, highest first.</summary>
    [HttpGet("{id:guid}/results")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResults(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetExamResultsQuery(id), cancellationToken));

    /// <summary>
    /// Records results. Scores below the workspace's configured threshold notify guardians.
    /// </summary>
    [HttpPost("{id:guid}/results")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RecordResults(
        Guid id,
        [FromBody] RecordExamResultsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ExamId = id }, cancellationToken),
            "Results recorded successfully.");

    /// <summary>Bulk entry point; identical semantics to the results endpoint.</summary>
    [HttpPost("{id:guid}/results/bulk")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordResultsBulk(
        Guid id,
        [FromBody] RecordExamResultsCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ExamId = id }, cancellationToken),
            "Results recorded for the whole group.");

    /// <summary>Corrects one student's result.</summary>
    [HttpPut("{id:guid}/results/{studentId:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateResult(
        Guid id,
        Guid studentId,
        [FromBody] UpdateExamResultCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ExamId = id, StudentId = studentId }, cancellationToken),
            "Result updated successfully.");

    // -----------------------------------------------------------------------
    // Online exams: the question paper
    // -----------------------------------------------------------------------

    /// <summary>The question paper with its answer key. Workspace members only.</summary>
    [HttpGet("{id:guid}/questions")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ExamQuestionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuestions(Guid id, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetExamQuestionsQuery(id), cancellationToken));

    /// <summary>Adds a question to an online exam that nobody has started yet.</summary>
    [HttpPost("{id:guid}/questions")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamQuestionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddQuestion(
        Guid id,
        [FromBody] AddExamQuestionCommand command,
        CancellationToken cancellationToken) =>
        CreatedResponse(
            await Mediator.Send(command with { ExamId = id }, cancellationToken),
            "Question added.");

    /// <summary>Edits a question. Refused once a student has started the exam.</summary>
    [HttpPut("{id:guid}/questions/{questionId:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamQuestionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateQuestion(
        Guid id,
        Guid questionId,
        [FromBody] UpdateExamQuestionCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { ExamId = id, QuestionId = questionId }, cancellationToken),
            "Question updated.");

    /// <summary>Removes a question. Refused once a student has started the exam.</summary>
    [HttpDelete("{id:guid}/questions/{questionId:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteQuestion(
        Guid id,
        Guid questionId,
        CancellationToken cancellationToken)
    {
        await Mediator.Send(new DeleteExamQuestionCommand(id, questionId), cancellationToken);
        return OkResponse("Question removed.");
    }

    // -----------------------------------------------------------------------
    // Online exams: sittings
    // -----------------------------------------------------------------------

    /// <summary>Every sitting of one exam, newest first.</summary>
    [HttpGet("{id:guid}/attempts")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<PaginatedList<ExamAttemptDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> GetAttempts(
        Guid id,
        [FromQuery] GetExamAttemptsQuery query,
        CancellationToken cancellationToken) =>
        SendPagedAsync(query with { ExamId = id }, cancellationToken);

    /// <summary>One sitting, with every answer and its marks.</summary>
    [HttpGet("attempts/{attemptId:guid}")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamAttemptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAttempt(Guid attemptId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetExamAttemptQuery(attemptId), cancellationToken));

    /// <summary>Marks the essay answers of a submitted sitting and finalises it.</summary>
    [HttpPut("attempts/{attemptId:guid}/grade")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<ExamAttemptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GradeAttempt(
        Guid attemptId,
        [FromBody] GradeExamAttemptCommand command,
        CancellationToken cancellationToken) =>
        OkResponse(
            await Mediator.Send(command with { AttemptId = attemptId }, cancellationToken),
            "Attempt graded.");

    /// <summary>A teacher's view of one student's grades across the workspace.</summary>
    [HttpGet("students/{studentId:guid}/grades")]
    [SwaggerOperation(Tags = ["Exams"])]
    [ProducesResponseType(typeof(ApiResponse<StudentGradesSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentGrades(Guid studentId, CancellationToken cancellationToken) =>
        OkResponse(await Mediator.Send(new GetStudentGradesQuery(studentId), cancellationToken));
}
