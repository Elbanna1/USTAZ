using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Domain.Exceptions;

namespace Ustaz.API.Extensions;

/// <summary>
/// Converts ASP.NET Core upload primitives into the transport-neutral
/// <see cref="FileUploadRequest"/> the Application layer understands.
/// </summary>
public static class FormFileExtensions
{
    /// <summary>
    /// Synchronous conversion, kept for callers that do not need the signature check — the
    /// storage layer verifies the bytes it writes either way. Prefer
    /// <see cref="ToUploadRequestAsync"/> so validation can reject a mislabelled file with a
    /// field error instead of a storage exception.
    /// </summary>
    public static FileUploadRequest ToUploadRequest(this IFormFile file)
    {
        Require(file);

        return new FileUploadRequest
        {
            Content = file.OpenReadStream(),
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            Length = file.Length
        };
    }

    /// <summary>
    /// Reads the upload's leading bytes before handing the stream on, so validation can see
    /// what the file really is rather than what its name claims.
    ///
    /// The stream from <c>OpenReadStream</c> is seekable (ASP.NET Core has already buffered
    /// the multipart section to memory or disk), so reading the header costs nothing and the
    /// position is restored for whoever writes the file next.
    /// </summary>
    public static async Task<FileUploadRequest> ToUploadRequestAsync(
        this IFormFile file,
        CancellationToken cancellationToken = default)
    {
        Require(file);

        var content = file.OpenReadStream();
        var header = await FileSignature.ReadHeaderAsync(content, cancellationToken);

        return new FileUploadRequest
        {
            Content = content,
            FileName = Path.GetFileName(file.FileName),
            ContentType = file.ContentType,
            Length = file.Length,
            Header = header
        };
    }

    private static void Require(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            throw new BusinessRuleException("FILE_REQUIRED", "A file is required.");
        }
    }
}
