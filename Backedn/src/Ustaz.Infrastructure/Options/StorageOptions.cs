using System.ComponentModel.DataAnnotations;

namespace Ustaz.Infrastructure.Options;

/// <summary>
/// Where USTAZ keeps uploaded files on the deployed machine, and what it accepts.
///
/// Everything is configuration driven so a production host can point this at a mounted volume
/// without a code change; the default is a <c>storage</c> folder beside the application, which
/// works on a fresh server as-is. Set it through <c>Storage__RootPath</c> in production.
///
/// Layout under the root:
/// <code>
/// {root}/public/{folder}/{id}{ext}                  served at /uploads, cacheable
/// {root}/tenants/{tenantId}/{area}/{ownerId}/...    private, authorised download only
/// {root}/shared/{area}/...                          private, authorised download only
/// {root}/keys/jwt-signing.key                       never served
/// </code>
///
/// The storage root is deliberately outside the published application directory's control: a
/// redeploy replaces the app folder, and a student's homework attachment must survive that.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>
    /// Absolute path, or a path relative to the application's content root. Set it through
    /// <c>Storage__RootPath</c> in production.
    /// </summary>
    public string RootPath { get; set; } = "storage";

    /// <summary>Hard ceiling enforced before a byte is written.</summary>
    [Range(1, 512)]
    public int MaxFileSizeInMegabytes { get; set; } = 25;

    public long MaxFileSizeInBytes => MaxFileSizeInMegabytes * 1024L * 1024L;

    /// <summary>
    /// How long a browser may cache a public asset. Safe to set high because the URL contains
    /// a generated id: replacing an avatar produces a new URL rather than mutating one.
    /// </summary>
    [Range(0, 31_536_000)]
    public int PublicCacheSeconds { get; set; } = 604_800;

    public string[] AllowedImageExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    public string[] AllowedDocumentExtensions { get; set; } =
        [".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx"];

    public string[] AllowedVideoExtensions { get; set; } = [".mp4", ".mov", ".webm"];

    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg", "image/png", "image/webp", "image/gif",
        "application/pdf", "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "video/mp4", "video/quicktime", "video/webm"
    ];
}
