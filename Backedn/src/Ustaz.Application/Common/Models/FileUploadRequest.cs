namespace Ustaz.Application.Common.Models;

/// <summary>
/// Transport neutral representation of an uploaded file, so Application handlers
/// never depend on IFormFile.
/// </summary>
public sealed class FileUploadRequest
{
    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public long Length { get; init; }

    /// <summary>
    /// The upload's leading bytes, captured at the HTTP boundary before anything reads the
    /// stream. This is what lets validation check the file's real format: the extension and
    /// the content type above are both client-supplied and prove nothing on their own.
    ///
    /// Empty when the request was constructed without a header — a handler unit test, say.
    /// Validation treats that as "nothing to check here" and the storage layer verifies the
    /// bytes it is about to write regardless.
    /// </summary>
    public ReadOnlyMemory<byte> Header { get; init; } = ReadOnlyMemory<byte>.Empty;
}
