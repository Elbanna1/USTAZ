using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ustaz.Application.Common.Interfaces;
using Ustaz.Application.Common.Models;
using Ustaz.Application.Common.Validation;
using Ustaz.Infrastructure.Options;

namespace Ustaz.Infrastructure.Services.Media;

/// <summary>
/// Stores public media on this server's own disk. This is the only public media store USTAZ
/// has: there is no third-party image host to configure.
///
/// The files land under the configured storage root (<c>{root}/public</c>), not inside the
/// published application: a redeploy replaces the app directory, and avatars and thumbnails
/// must survive that. The API maps <c>/uploads</c> onto the same directory, so the URLs this
/// returns keep working wherever the root is mounted.
/// </summary>
public sealed class LocalMediaService : IMediaService
{
    /// <summary>The request path the API serves these files under.</summary>
    public const string PublicRequestPath = "/uploads";

    private readonly MediaValidator _validator;
    private readonly ILogger<LocalMediaService> _logger;
    private readonly string _root;

    public LocalMediaService(
        MediaValidator validator,
        IOptions<StorageOptions> storageOptions,
        ILogger<LocalMediaService> logger,
        string contentRootPath)
    {
        _validator = validator;
        _logger = logger;
        _root = ResolvePublicRoot(storageOptions.Value, contentRootPath);

        Directory.CreateDirectory(_root);
    }

    /// <summary>Where public media lives, given the configured storage root.</summary>
    public static string ResolvePublicRoot(StorageOptions options, string contentRootPath)
    {
        var storageRoot = Path.IsPathRooted(options.RootPath)
            ? Path.GetFullPath(options.RootPath)
            : Path.GetFullPath(Path.Combine(contentRootPath, options.RootPath));

        return Path.Combine(storageRoot, "public");
    }

    public async Task<MediaUploadResult> UploadAsync(
        Stream file,
        string fileName,
        string folder,
        CancellationToken cancellationToken = default)
    {
        // Read the leading bytes first, so the validator can check what the file actually is
        // and not only what its name says. The stream is rewound afterwards when it can be.
        var header = await FileSignature.ReadHeaderAsync(file, cancellationToken);

        _validator.Validate(
            fileName,
            contentType: null,
            length: file.CanSeek ? file.Length : 1,
            header: header);

        var safeFolder = string.Join("_", (folder ?? "misc").Split(Path.GetInvalidFileNameChars()));
        var directory = Path.Combine(_root, safeFolder);
        Directory.CreateDirectory(directory);

        // The stored name is generated: the uploader's name never decides a path.
        var extension = Path.GetExtension(fileName);
        var publicId = safeFolder + "/" + Guid.NewGuid().ToString("N");
        var physicalPath = Path.Combine(directory, Path.GetFileName(publicId) + extension);

        await using (var target = File.Create(physicalPath))
        {
            await file.CopyToAsync(target, cancellationToken);
        }

        _logger.LogInformation(
            "Stored an upload as {PublicId} in public media storage.", publicId);

        var url = PublicRequestPath + "/" + publicId + extension;

        return new MediaUploadResult
        {
            Url = url,
            SecureUrl = url,
            PublicId = publicId + extension,
            Format = extension.TrimStart('.'),
            Bytes = new FileInfo(physicalPath).Length,
            ResourceType = _validator.IsImage(fileName) ? "image" : _validator.IsVideo(fileName) ? "video" : "raw"
        };
    }

    public Task DeleteAsync(string publicId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicId))
        {
            return Task.CompletedTask;
        }

        try
        {
            var physicalPath = ResolvePhysicalPath(publicId);

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Never fail the operation that replaced the asset over a stuck old file.
            _logger.LogWarning(exception, "Could not delete local media {PublicId}.", publicId);
        }

        return Task.CompletedTask;
    }

    /// <summary>Resolves an id to a path and refuses anything that escapes the root.</summary>
    private string ResolvePhysicalPath(string publicId)
    {
        var normalised = publicId.Replace('\\', '/');

        if (Path.IsPathRooted(normalised)
            || normalised.Split('/').Any(segment => segment is ".." or "." || string.IsNullOrWhiteSpace(segment)))
        {
            throw new Domain.Exceptions.BusinessRuleException(
                "MEDIA_ID_INVALID", "That media reference is not valid.");
        }

        var candidate = Path.GetFullPath(Path.Combine(_root, normalised));

        if (!candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new Domain.Exceptions.BusinessRuleException(
                "MEDIA_ID_INVALID", "That media reference is not valid.");
        }

        return candidate;
    }
}
