using Microsoft.Extensions.Options;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Exceptions;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Media;

/// <summary>
/// Validates size, extension, MIME type and the file's own leading bytes before anything is
/// written. Frontend validation is never trusted, and neither is the extension or the
/// <c>Content-Type</c> the client supplied — both are just strings in a request.
/// </summary>
public sealed class MediaValidator(IOptions<StorageOptions> options)
{
    private readonly StorageOptions _options = options.Value;

    public void Validate(string fileName, string? contentType, long length) =>
        Validate(fileName, contentType, length, header: ReadOnlySpan<byte>.Empty);

    /// <summary>
    /// The full check. <paramref name="header"/> is the file's first bytes; pass it whenever
    /// they are available, which is every path that starts from an HTTP upload.
    /// </summary>
    public void Validate(string fileName, string? contentType, long length, ReadOnlySpan<byte> header)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new BusinessRuleException("FILE_NAME_REQUIRED", "The uploaded file must have a name.");
        }

        if (length <= 0)
        {
            throw new BusinessRuleException("FILE_EMPTY", "The uploaded file is empty.");
        }

        var maxBytes = (long)_options.MaxFileSizeInMegabytes * 1024 * 1024;
        if (length > maxBytes)
        {
            throw new BusinessRuleException(
                "FILE_TOO_LARGE",
                "The uploaded file exceeds the maximum allowed size of " + _options.MaxFileSizeInMegabytes + " MB.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new BusinessRuleException("FILE_EXTENSION_REQUIRED", "The uploaded file must have a valid extension.");
        }

        var allowedExtensions = _options.AllowedImageExtensions
            .Concat(_options.AllowedDocumentExtensions)
            .Concat(_options.AllowedVideoExtensions)
            .ToArray();

        if (!allowedExtensions.Contains(extension))
        {
            throw new BusinessRuleException(
                "FILE_EXTENSION_NOT_ALLOWED",
                "Files of type " + extension + " are not allowed.");
        }

        if (!string.IsNullOrWhiteSpace(contentType)
            && _options.AllowedContentTypes.Length > 0
            && !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "FILE_CONTENT_TYPE_NOT_ALLOWED",
                "The content type " + contentType + " is not allowed.");
        }

        // The last and only non-forgeable check. Skipped when no header was captured, and
        // when the format is one whose container cannot be told apart from another's.
        if (header.Length > 0
            && FileSignature.VerifiableExtensions.Contains(extension)
            && !FileSignature.Matches(header, extension))
        {
            throw new BusinessRuleException(
                "FILE_SIGNATURE_MISMATCH",
                "The file's content does not match its extension.");
        }
    }

    public bool IsImage(string fileName) =>
        _options.AllowedImageExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    public bool IsVideo(string fileName) =>
        _options.AllowedVideoExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());
}
