using Ustaz.Application.Common.Models;

namespace Ustaz.Application.Common.Interfaces;

/// <summary>Where a stored file belongs. Everything is scoped by workspace first.</summary>
public enum FileStorageArea
{
    Users = 1,
    Courses = 2,
    Homework = 3,
    Messages = 4,
    Certificates = 5,
    Groups = 6
}

/// <summary>
/// The logical location of a file: the workspace that owns it, the area it belongs to, and
/// optionally the entity it hangs off. Callers never build paths themselves — that is what
/// keeps traversal and cross-tenant writes impossible by construction.
/// </summary>
public sealed record FileStorageScope
{
    public required FileStorageArea Area { get; init; }

    /// <summary>The owning workspace. Null only for genuinely platform-level files.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>The entity the file hangs off: a conversation, a homework, a user.</summary>
    public Guid? OwnerId { get; init; }
}

/// <summary>A file that has been written to storage.</summary>
public sealed record StoredFile
{
    /// <summary>
    /// Opaque handle used to read or delete the file later. It is a relative, sanitised path
    /// inside the storage root and is safe to persist — it is never a physical path and must
    /// never be returned to a client as one.
    /// </summary>
    public required string StorageKey { get; init; }

    /// <summary>The name the uploader used, kept for display only.</summary>
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long SizeInBytes { get; init; }
}

/// <summary>
/// Private, access-controlled file storage.
///
/// This is deliberately separate from <see cref="IMediaService"/>: media is public CDN
/// content (avatars, course thumbnails, lesson video) served by URL, while this holds files
/// that must pass an authorization check on every read — homework answers, message
/// attachments, certificates. Nothing here ever hands out a physical path, so the local
/// implementation can be swapped for S3 or Azure Blob without touching a handler.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        FileUploadRequest file,
        FileStorageScope scope,
        CancellationToken cancellationToken = default);

    /// <summary>Opens a stored file, or returns null when the key no longer resolves.</summary>
    Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>Deletes a stored file. Missing files are not an error.</summary>
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// True when the key belongs to the given workspace. Handlers use this as a second gate
    /// before streaming a file, so a key captured from another tenant cannot be replayed.
    /// </summary>
    bool BelongsToTenant(string storageKey, Guid tenantId);
}
