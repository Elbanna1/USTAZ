using System.Text.RegularExpressions;
using Ustaz.Domain.Exceptions;

namespace Ustaz.Domain.ValueObjects;

/// <summary>
/// Egyptian mobile number normalised to international E.164 form (+20 1X XXXX XXXX).
/// Accepts the common local spellings: 01012345678, 201012345678, +201012345678.
/// </summary>
public sealed partial class EgyptianPhoneNumber : ValueObject
{
    public const string CountryCode = "+20";

    private EgyptianPhoneNumber(string value) => Value = value;

    /// <summary>Normalised number, e.g. +201012345678.</summary>
    public string Value { get; }

    /// <summary>National significant number without the country code, e.g. 1012345678.</summary>
    public string NationalNumber => Value[3..];

    public static EgyptianPhoneNumber Create(string? raw)
    {
        if (!TryCreate(raw, out var phone))
        {
            throw new BusinessRuleException($"The value '{raw}' is not a valid Egyptian mobile number.");
        }

        return phone;
    }

    public static bool TryCreate(string? raw, out EgyptianPhoneNumber phone)
    {
        phone = null!;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var trimmed = raw.Trim();

        // A leading "+" or "00" means the caller already supplied a country code, so the
        // number must not be reinterpreted as a local Egyptian one.
        var isInternational = trimmed.StartsWith('+') || trimmed.StartsWith("00", StringComparison.Ordinal);

        var digits = NonDigits().Replace(trimmed, string.Empty);

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (!isInternational)
        {
            if (digits.StartsWith("0", StringComparison.Ordinal))
            {
                digits = "20" + digits[1..];
            }
            else if (digits.Length == 10 && digits.StartsWith("1", StringComparison.Ordinal))
            {
                digits = "20" + digits;
            }
        }

        if (!EgyptianMobile().IsMatch(digits))
        {
            return false;
        }

        phone = new EgyptianPhoneNumber("+" + digits);
        return true;
    }

    public static bool IsValid(string? raw) => TryCreate(raw, out _);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"[^\d]")]
    private static partial Regex NonDigits();

    [GeneratedRegex(@"^201[0125]\d{8}$")]
    private static partial Regex EgyptianMobile();
}
