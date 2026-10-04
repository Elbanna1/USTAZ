using FluentValidation;
using Ustaz.Application.Common.Localization;
using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Validation;

/// <summary>
/// One place that decides what may be uploaded. The extension, the declared content type and
/// the file's own leading bytes all have to agree, and the size is measured server-side from
/// the request body rather than taken from anything the client claims.
///
/// Messages resolve per failure through <see cref="ValidationMessages"/>, so an Arabic request
/// gets Arabic field errors. See <see cref="CommonValidationRules"/> for why the lambda
/// overload of <c>WithMessage</c> is the only correct one here.
/// </summary>
public static class FileUploadRules
{
    public const long MaxImageBytes = 5L * 1024 * 1024;

    public const long MaxDocumentBytes = 20L * 1024 * 1024;

    public const int MaxImageMegabytes = 5;

    public const int MaxDocumentMegabytes = 20;

    public static readonly IReadOnlySet<string> ImageExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    public static readonly IReadOnlySet<string> DocumentExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".csv", ".zip",
            ".jpg", ".jpeg", ".png", ".webp"
        };

    private static readonly IReadOnlySet<string> DocumentContentTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain",
            "text/csv",
            "application/zip",
            "application/x-zip-compressed",
            "image/jpeg",
            "image/png",
            "image/webp"
        };

    public static string Extension(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName) ? string.Empty : Path.GetExtension(fileName);

    public static bool IsAllowedImage(FileUploadRequest? file) =>
        file is not null
        && file.Length > 0
        && file.Length <= MaxImageBytes
        && ImageExtensions.Contains(Extension(file.FileName))
        && (file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)
        && HasMatchingSignature(file);

    public static bool IsAllowedDocument(FileUploadRequest? file) =>
        file is not null
        && file.Length > 0
        && file.Length <= MaxDocumentBytes
        && DocumentExtensions.Contains(Extension(file.FileName))
        && file.ContentType is not null
        && DocumentContentTypes.Contains(file.ContentType)
        && HasMatchingSignature(file);

    /// <summary>
    /// True when the upload's leading bytes are consistent with its extension.
    ///
    /// A header that was never captured (<see cref="FileUploadRequest.Header"/> empty) passes:
    /// the header is populated at the HTTP boundary, and a handler test that constructs the
    /// request directly is not the place this check belongs. The storage layer verifies again
    /// on the bytes it is actually about to write, which is the check that cannot be bypassed.
    /// </summary>
    public static bool HasMatchingSignature(FileUploadRequest? file) =>
        file is null
        || file.Header.Length == 0
        || FileSignature.Matches(file.Header.Span, Extension(file.FileName));

    /// <summary>Rules for an image upload: an avatar, a cover, a logo.</summary>
    public static IRuleBuilderOptions<T, FileUploadRequest> ValidImageUpload<T>(
        this IRuleBuilder<T, FileUploadRequest> rule) =>
        rule.NotNull().WithMessage(_ => ValidationMessages.FileRequired())
            .Must(file => file is null || file.Length > 0)
            .WithMessage(_ => ValidationMessages.FileRequired())
            .Must(file => file is null || file.Length <= MaxImageBytes)
            .WithMessage(_ => ValidationMessages.FileTooLarge(MaxImageMegabytes))
            .Must(file => file is null || ImageExtensions.Contains(Extension(file.FileName)))
            .WithMessage(_ => ValidationMessages.FileTypeNotAllowed())
            .Must(file => file is null
                || (file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
            .WithMessage(_ => ValidationMessages.FileTypeNotAllowed())
            .Must(HasMatchingSignature)
            .WithMessage(_ => ErrorCatalog.ArabicFor("FILE_SIGNATURE_MISMATCH") is { } arabic
                && UstazLanguages.FromCurrentCulture() == UstazLanguage.Arabic
                    ? arabic
                    : "The file's content does not match its extension.");

    /// <summary>Rules for a document upload: a homework answer, a handout.</summary>
    public static IRuleBuilderOptions<T, FileUploadRequest> ValidDocumentUpload<T>(
        this IRuleBuilder<T, FileUploadRequest> rule) =>
        rule.NotNull().WithMessage(_ => ValidationMessages.FileRequired())
            .Must(file => file is null || file.Length > 0)
            .WithMessage(_ => ValidationMessages.FileRequired())
            .Must(file => file is null || file.Length <= MaxDocumentBytes)
            .WithMessage(_ => ValidationMessages.FileTooLarge(MaxDocumentMegabytes))
            .Must(file => file is null || DocumentExtensions.Contains(Extension(file.FileName)))
            .WithMessage(_ => ValidationMessages.FileTypeNotAllowed())
            .Must(file => file is null || (file.ContentType is not null && DocumentContentTypes.Contains(file.ContentType)))
            .WithMessage(_ => ValidationMessages.FileTypeNotAllowed())
            .Must(HasMatchingSignature)
            .WithMessage(_ => ErrorCatalog.ArabicFor("FILE_SIGNATURE_MISMATCH") is { } arabic
                && UstazLanguages.FromCurrentCulture() == UstazLanguage.Arabic
                    ? arabic
                    : "The file's content does not match its extension.");
}
