namespace Ustaz.Application.Common.Validation;

/// <summary>
/// Reads the first bytes of an upload and says what the file actually is.
///
/// Why this is necessary: an extension and a <c>Content-Type</c> are both supplied by the
/// client, so together they prove nothing. A caller can rename <c>payload.html</c> to
/// <c>avatar.png</c>, declare <c>image/png</c>, and pass an allow-list check on both counts.
/// The bytes are the only part of an upload the uploader cannot simply assert.
///
/// Scope, deliberately: this verifies that a file claiming to be one of the formats USTAZ
/// serves back to a browser — the images, and PDF — really is that format. It is not a virus
/// scanner and does not try to parse Office documents, whose ZIP container looks identical to
/// any other ZIP. Those are stored as private attachments and only ever handed back through an
/// authorised download endpoint with <c>Content-Disposition: attachment</c>, so a mislabelled
/// one is a broken file rather than a vector.
/// </summary>
public static class FileSignature
{
    /// <summary>
    /// How many bytes have to be readable for a verdict. Twelve covers the longest signature
    /// here (WEBP's "RIFF....WEBP") with room to spare.
    /// </summary>
    public const int HeaderLength = 32;

    /// <summary>
    /// Extensions whose content this class can actually verify. Anything outside this set is
    /// accepted on its extension alone, which the allow lists have already constrained.
    /// </summary>
    public static readonly IReadOnlySet<string> VerifiableExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".pdf"
        };

    /// <summary>
    /// True when <paramref name="header"/> is consistent with <paramref name="extension"/>.
    /// An extension this class cannot verify returns true: the decision belongs to the
    /// allow list, not here.
    /// </summary>
    public static bool Matches(ReadOnlySpan<byte> header, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => IsJpeg(header),
            ".png" => IsPng(header),
            ".gif" => IsGif(header),
            ".webp" => IsWebp(header),
            ".bmp" => IsBmp(header),
            ".pdf" => IsPdf(header),
            _ => true
        };
    }

    /// <summary>True when the bytes are one of the image formats USTAZ serves publicly.</summary>
    public static bool IsImage(ReadOnlySpan<byte> header) =>
        IsJpeg(header) || IsPng(header) || IsGif(header) || IsWebp(header) || IsBmp(header);

    /// <summary>
    /// Reads the header without consuming the stream for the caller that writes it next.
    /// A seekable stream is rewound; a non-seekable one is buffered by the caller before it
    /// gets here, so this never silently returns an empty header for a real upload.
    /// </summary>
    public static async Task<byte[]> ReadHeaderAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanRead)
        {
            return [];
        }

        var start = stream.CanSeek ? stream.Position : 0L;
        var buffer = new byte[HeaderLength];

        var read = await stream.ReadAtLeastAsync(
            buffer, minimumBytes: HeaderLength, throwOnEndOfStream: false, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = start;
        }

        return read == HeaderLength ? buffer : buffer[..read];
    }

    // JPEG: FF D8 FF.
    private static bool IsJpeg(ReadOnlySpan<byte> h) =>
        h.Length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF;

    // PNG: 89 'P' 'N' 'G' 0D 0A 1A 0A.
    private static bool IsPng(ReadOnlySpan<byte> h) =>
        h.Length >= 8
        && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
        && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A;

    // GIF: "GIF87a" or "GIF89a".
    private static bool IsGif(ReadOnlySpan<byte> h) =>
        h.Length >= 6
        && h[0] == (byte)'G' && h[1] == (byte)'I' && h[2] == (byte)'F'
        && h[3] == (byte)'8' && (h[4] == (byte)'7' || h[4] == (byte)'9') && h[5] == (byte)'a';

    // WEBP: "RIFF" then four size bytes then "WEBP".
    private static bool IsWebp(ReadOnlySpan<byte> h) =>
        h.Length >= 12
        && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
        && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P';

    // BMP: "BM".
    private static bool IsBmp(ReadOnlySpan<byte> h) =>
        h.Length >= 2 && h[0] == (byte)'B' && h[1] == (byte)'M';

    // PDF: "%PDF-".
    private static bool IsPdf(ReadOnlySpan<byte> h) =>
        h.Length >= 5
        && h[0] == (byte)'%' && h[1] == (byte)'P' && h[2] == (byte)'D' && h[3] == (byte)'F'
        && h[4] == (byte)'-';
}
