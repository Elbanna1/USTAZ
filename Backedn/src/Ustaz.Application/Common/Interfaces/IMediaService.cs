using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>
/// Public media storage. The implementation writes to this server's own storage root; the
/// contract never leaks storage specific types, so the backing store can be swapped without
/// touching a handler.
/// </summary>
public interface IMediaService
{
    Task<MediaUploadResult> UploadAsync(
        Stream file,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string publicId,
        CancellationToken cancellationToken = default);
}
