using System.Security.Cryptography;

namespace Ustaz.API.Services;

/// <summary>
/// Resolves the JWT signing key.
///
/// Configuration wins: <c>Jwt__Key</c> from the environment or a secret store is always used
/// when present. When it is absent the key is read from — or created in — a file under the
/// storage root, the same way ASP.NET Core persists its own data-protection key ring.
///
/// That keeps two things true at once: no secret is ever committed to source, and a server
/// that was deployed without the environment variable still starts instead of failing with
/// HTTP 500.30. The file is stable across restarts, so issued tokens keep validating.
/// </summary>
public static class JwtSigningKeyProvider
{
    private const int KeyLengthInBytes = 64;

    public static string Resolve(
        string? configuredKey,
        string storageRootPath,
        string contentRootPath,
        ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(configuredKey) && configuredKey.Length >= 32)
        {
            return configuredKey;
        }

        var directory = ResolveKeyDirectory(storageRootPath, contentRootPath);
        var path = Path.Combine(directory, "jwt-signing.key");

        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();

            if (existing.Length >= 32)
            {
                logger.LogWarning(
                    "Jwt__Key is not configured; using the persisted signing key at {Path}. "
                    + "Set Jwt__Key in the environment to manage it explicitly.",
                    path);

                return existing;
            }
        }

        Directory.CreateDirectory(directory);

        var generated = Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyLengthInBytes));

        File.WriteAllText(path, generated);

        logger.LogWarning(
            "Jwt__Key was not configured, so a new signing key was generated and stored at {Path}. "
            + "Existing tokens are invalidated. Set Jwt__Key in the environment to manage it explicitly.",
            path);

        return generated;
    }

    /// <summary>
    /// Prefers the configured storage root, which is a mounted volume in production. Falls
    /// back to the content root when that path is not writable — on IIS the configured path
    /// may point at a Linux-style location that does not exist on the host.
    /// </summary>
    private static string ResolveKeyDirectory(string storageRootPath, string contentRootPath)
    {
        var candidates = new[]
        {
            Path.IsPathRooted(storageRootPath)
                ? storageRootPath
                : Path.Combine(contentRootPath, storageRootPath),
            Path.Combine(contentRootPath, "storage")
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var directory = Path.Combine(candidate, "keys");
                Directory.CreateDirectory(directory);
                return directory;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Try the next location rather than failing startup over a key file.
            }
        }

        var fallback = Path.Combine(Path.GetTempPath(), "ustaz-keys");
        Directory.CreateDirectory(fallback);

        return fallback;
    }
}
