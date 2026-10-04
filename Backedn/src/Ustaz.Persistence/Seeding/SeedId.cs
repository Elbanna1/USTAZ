using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ustaz.Persistence.Seeding;

/// <summary>
/// Derives a stable identifier from a name.
///
/// Every seeded row needs an id that is the same on every run, in every database, on every
/// developer's machine — that is what makes "insert the rows that are missing" a correct
/// idempotency check rather than a guess. Writing several hundred literal GUIDs by hand
/// would do it too, and be unreadable and easy to duplicate by accident; a name-based UUID
/// (RFC 4122 version 5) gives the same guarantee from a key that says what the row is.
///
/// The hash is SHA-1 because that is what the version 5 scheme specifies. Nothing here is a
/// security decision: these ids are primary keys, never secrets, and are not derived from
/// anything confidential.
/// </summary>
internal static class SeedId
{
    /// <summary>
    /// The namespace for every USTAZ seeded id. Fixed forever: changing it would orphan every
    /// row a previous run inserted and make the next run insert the whole dataset again.
    /// </summary>
    private static readonly Guid SeedNamespace = Guid.Parse("4f3b9a16-5c2d-4f8e-9b61-0d7a2e5c8f31");

    public static Guid For(string key) => Create(SeedNamespace, key);

    public static Guid For(string key, int index) =>
        Create(SeedNamespace, key + ":" + index.ToString(CultureInfo.InvariantCulture));

    public static Guid For(string key, string qualifier) => Create(SeedNamespace, key + ":" + qualifier);

    public static Guid For(string key, Guid parentId) =>
        Create(SeedNamespace, key + ":" + parentId.ToString("N"));

    public static Guid For(string key, Guid parentId, int index) =>
        Create(SeedNamespace, key + ":" + parentId.ToString("N") + ":" + index.ToString(CultureInfo.InvariantCulture));

    public static Guid For(string key, Guid parentId, string qualifier) =>
        Create(SeedNamespace, key + ":" + parentId.ToString("N") + ":" + qualifier);

    public static Guid For(string key, Guid firstId, Guid secondId) =>
        Create(SeedNamespace, key + ":" + firstId.ToString("N") + ":" + secondId.ToString("N"));

    public static Guid For(string key, Guid firstId, Guid secondId, int index) =>
        Create(
            SeedNamespace,
            key + ":" + firstId.ToString("N") + ":" + secondId.ToString("N") + ":"
            + index.ToString(CultureInfo.InvariantCulture));

    private static Guid Create(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapToBigEndian(namespaceBytes);

        var nameBytes = Encoding.UTF8.GetBytes(name);

        var payload = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, payload, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, payload, namespaceBytes.Length, nameBytes.Length);

        var hash = SHA1.HashData(payload);

        var result = new byte[16];
        Array.Copy(hash, 0, result, 0, 16);

        // Version 5 in the high nibble of octet 6, RFC 4122 variant in the top bits of octet 8.
        result[6] = (byte)((result[6] & 0x0F) | 0x50);
        result[8] = (byte)((result[8] & 0x3F) | 0x80);

        SwapToBigEndian(result);
        return new Guid(result);
    }

    /// <summary>
    /// .NET lays the first three GUID fields out little-endian; RFC 4122 hashes them
    /// big-endian. The swap is its own inverse, so the same call serves both directions.
    /// </summary>
    private static void SwapToBigEndian(byte[] guidBytes)
    {
        (guidBytes[0], guidBytes[3]) = (guidBytes[3], guidBytes[0]);
        (guidBytes[1], guidBytes[2]) = (guidBytes[2], guidBytes[1]);
        (guidBytes[4], guidBytes[5]) = (guidBytes[5], guidBytes[4]);
        (guidBytes[6], guidBytes[7]) = (guidBytes[7], guidBytes[6]);
    }
}
