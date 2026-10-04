using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ustaz.Domain.ValueObjects;

/// <summary>
/// URL friendly identifier. Arabic letters are preserved (they are valid in modern URLs)
/// while whitespace and punctuation collapse into single hyphens.
/// </summary>
public sealed partial class Slug : ValueObject
{
    private Slug(string value) => Value = value;

    public string Value { get; }

    public static Slug Create(string source, string? uniqueSuffix = null)
    {
        var slug = Normalize(source);

        if (string.IsNullOrEmpty(slug))
        {
            slug = "item";
        }

        if (!string.IsNullOrWhiteSpace(uniqueSuffix))
        {
            slug = slug + "-" + Normalize(uniqueSuffix);
        }

        return new Slug(slug);
    }

    /// <summary>Creates a slug with a short random suffix so collisions are practically impossible.</summary>
    public static Slug CreateUnique(string source) =>
        Create(source, Guid.NewGuid().ToString("N")[..8]);

    private static string Normalize(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return string.Empty;
        }

        var normalized = source.Trim().ToLower(CultureInfo.InvariantCulture);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized.Normalize(NormalizationForm.FormC))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (char.IsLetterOrDigit(ch) && category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        return CollapseHyphens().Replace(builder.ToString(), "-").Trim('-');
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    public static implicit operator string(Slug slug) => slug.Value;

    [GeneratedRegex("-{2,}")]
    private static partial Regex CollapseHyphens();
}
