using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ustaz.Application.Common.Localization;
using Ustaz.Domain.Exceptions;
using ValidationException = Ustaz.Application.Common.Exceptions.ValidationException;

namespace Ustaz.API.Middleware;

/// <summary>
/// Translates exceptions into RFC 7807 ProblemDetails. Stack traces and internal messages
/// never reach the client; unexpected failures are logged and reported generically.
///
/// Every response carries three things the client can rely on:
///
/// * <c>code</c> — a stable, never-translated identifier. The domain already attaches one to
///   each business-rule failure; this handler supplies a canonical one for every other shape.
///   This is what a client switches on, never the prose.
/// * <c>detail</c> — the message to show a person, in their language. Arabic comes from
///   <see cref="ErrorCatalog"/>; English is the message authored at the throw site, which is
///   more specific because it can interpolate real values.
/// * <c>errors</c> — field-level failures, for a form to render beside its inputs.
/// </summary>
public sealed class GlobalExceptionHandler(
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = Map(exception, httpContext);

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path} (traceId {TraceId}).",
                httpContext.Request.Method,
                httpContext.Request.Path,
                httpContext.TraceIdentifier);
        }
        else
        {
            logger.LogInformation(
                "Request on {Method} {Path} rejected with {StatusCode}: {Code}",
                httpContext.Request.Method,
                httpContext.Request.Path,
                problem.Status,
                problem.Extensions.TryGetValue("code", out var code) ? code : "(none)");
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        // The content type has to be passed explicitly: WriteAsJsonAsync would otherwise
        // overwrite it with application/json and drop the RFC 7807 media type.
        await httpContext.Response.WriteAsJsonAsync(
            problem,
            options: null,
            contentType: "application/problem+json",
            cancellationToken);

        return true;
    }

    private ProblemDetails Map(Exception exception, HttpContext httpContext)
    {
        var language = UstazLanguages.FromCurrentCulture();

        var problem = exception switch
        {
            ValidationException validation => BuildValidation(validation, language),

            // The "Entity with identifier 'x' was not found" form names an internal type and
            // echoes the caller's own identifier back. Nobody should be shown that, so it is
            // dropped in favour of the catalogue's wording in both languages.
            NotFoundException notFound => Build(
                StatusCodes.Status404NotFound,
                ErrorCodes.ResourceNotFound,
                notFound.NamesAnIdentifier ? null : notFound.Message,
                language),

            ConflictException conflict => Build(
                StatusCodes.Status409Conflict,
                conflict.Code ?? ErrorCodes.Conflict,
                conflict.Message,
                language),

            ForbiddenAccessException forbidden => Build(
                StatusCodes.Status403Forbidden,
                forbidden.Code ?? ErrorCodes.Forbidden,
                forbidden.Message,
                language),

            UnauthorizedException unauthorized => Build(
                StatusCodes.Status401Unauthorized,
                unauthorized.Code ?? ErrorCodes.Unauthorized,
                unauthorized.Message,
                language),

            BusinessRuleException businessRule => Build(
                StatusCodes.Status422UnprocessableEntity,
                businessRule.Code ?? ErrorCodes.BusinessRuleViolation,
                businessRule.Message,
                language),

            UnauthorizedAccessException => Build(
                StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized, authoredMessage: null, language),

            BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } => Build(
                StatusCodes.Status413PayloadTooLarge, ErrorCodes.PayloadTooLarge, authoredMessage: null, language),

            OperationCanceledException => Build(
                499, ErrorCodes.RequestCancelled, authoredMessage: null, language),

            DbUpdateConcurrencyException => Build(
                StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict, authoredMessage: null, language),

            DbUpdateException dbUpdate => BuildDbUpdate(dbUpdate, language),

            _ => Build(
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                // Only a developer ever sees the real message, and only outside production.
                environment.IsDevelopment() ? exception.Message : null,
                language)
        };

        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        problem.Extensions["success"] = false;
        problem.Extensions["language"] = UstazLanguages.TagFor(language);

        return problem;
    }

    private static ProblemDetails BuildValidation(ValidationException exception, UstazLanguage language)
    {
        var problem = Build(
            StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, authoredMessage: null, language);

        problem.Extensions["errors"] = exception.Errors;

        return problem;
    }

    private ProblemDetails BuildDbUpdate(DbUpdateException exception, UstazLanguage language)
    {
        // SQL Server 2601/2627 both mean a unique index was violated.
        var isUniqueViolation = exception.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
            || exception.InnerException?.Message.Contains("2601") == true
            || exception.InnerException?.Message.Contains("2627") == true;

        if (isUniqueViolation)
        {
            return Build(
                StatusCodes.Status409Conflict, ErrorCodes.DuplicateValue, authoredMessage: null, language);
        }

        return Build(
            StatusCodes.Status500InternalServerError,
            ErrorCodes.InternalError,
            // The driver's message can name columns, constraints and values. Developers only.
            environment.IsDevelopment() ? exception.InnerException?.Message ?? exception.Message : null,
            language);
    }

    /// <summary>
    /// Assembles one problem document.
    ///
    /// <paramref name="authoredMessage"/> is the English text written at the throw site. It
    /// wins for an English caller because it is the more specific of the two — several of
    /// those messages interpolate a real limit or score, which a static catalogue entry
    /// cannot. For an Arabic caller the catalogue wins, falling back to the authored message
    /// only when the code has no Arabic yet.
    /// </summary>
    private static ProblemDetails Build(
        int status,
        string code,
        string? authoredMessage,
        UstazLanguage language)
    {
        var detail = language == UstazLanguage.Arabic
            ? ErrorCatalog.ArabicFor(code) ?? authoredMessage ?? ErrorCatalog.ArabicForStatus(status)
            : authoredMessage ?? ErrorCatalog.EnglishForStatus(status);

        var problem = new ProblemDetails
        {
            Status = status,
            // The title stays English and stable: it is a category for a developer reading
            // logs, while "detail" is the sentence a person is shown.
            Title = TitleFor(status),
            Detail = detail,
            Type = "https://datatracker.ietf.org/doc/html/rfc7807"
        };

        problem.Extensions["code"] = code;

        return problem;
    }

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Validation failed.",
        StatusCodes.Status401Unauthorized => "Unauthorized.",
        StatusCodes.Status403Forbidden => "Forbidden.",
        StatusCodes.Status404NotFound => "Resource not found.",
        StatusCodes.Status409Conflict => "Conflict.",
        StatusCodes.Status413PayloadTooLarge => "Payload too large.",
        StatusCodes.Status422UnprocessableEntity => "Business rule violation.",
        StatusCodes.Status429TooManyRequests => "Too many requests.",
        499 => "Request cancelled.",
        _ => "Internal server error."
    };
}
