using FluentValidation;
using Ustaz.Application.Common.Localization;
using Ustaz.Domain.Constants;
using Ustaz.Domain.ValueObjects;

namespace Ustaz.Application.Common.Validation;

/// <summary>
/// Reusable FluentValidation rules so every endpoint validates the same way.
///
/// Every message goes through the <c>Func</c> overload of <c>WithMessage</c> rather than the
/// string one. That is deliberate and load-bearing: the string overload is evaluated once,
/// when the validator is constructed, so a message captured that way would freeze whichever
/// language happened to be ambient on the request that first resolved the validator. The
/// lambda is evaluated per failure, which is what lets
/// <see cref="ValidationMessages"/> answer in the caller's language.
/// </summary>
public static class CommonValidationRules
{
    private const int EmailMaxLength = 256;
    private const int PasswordMinLength = 8;
    private const int PasswordMaxLength = 128;
    private const int PersonNameMinLength = 2;
    private const int PersonNameMaxLength = 100;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(_ => ValidationMessages.EmailRequired())
            .MaximumLength(EmailMaxLength).WithMessage(_ => ValidationMessages.EmailTooLong(EmailMaxLength))
            .EmailAddress().WithMessage(_ => ValidationMessages.EmailInvalid());

    /// <summary>
    /// An email that may be omitted, but must be well formed when supplied. Blank is treated
    /// as "not supplied", matching <see cref="OptionalEgyptianPhone{T}"/>.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> OptionalEmail<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(EmailMaxLength).WithMessage(_ => ValidationMessages.EmailTooLong(EmailMaxLength))
            .Must(value => string.IsNullOrWhiteSpace(value) || BeAValidEmail(value))
            .WithMessage(_ => ValidationMessages.EmailInvalid());

    public static IRuleBuilderOptions<T, string> StrongPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(_ => ValidationMessages.PasswordRequired())
            .MinimumLength(PasswordMinLength).WithMessage(_ => ValidationMessages.PasswordTooShort(PasswordMinLength))
            .MaximumLength(PasswordMaxLength).WithMessage(_ => ValidationMessages.PasswordTooLong(PasswordMaxLength))
            .Matches("[A-Z]").WithMessage(_ => ValidationMessages.PasswordNeedsUppercase())
            .Matches("[a-z]").WithMessage(_ => ValidationMessages.PasswordNeedsLowercase())
            .Matches("[0-9]").WithMessage(_ => ValidationMessages.PasswordNeedsDigit());

    public static IRuleBuilderOptions<T, string> EgyptianPhone<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(_ => ValidationMessages.PhoneRequired())
            .Must(value => EgyptianPhoneNumber.IsValid(value))
            .WithMessage(_ => ValidationMessages.PhoneInvalid());

    public static IRuleBuilderOptions<T, string?> OptionalEgyptianPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(value => string.IsNullOrWhiteSpace(value) || EgyptianPhoneNumber.IsValid(value))
            .WithMessage(_ => ValidationMessages.PhoneInvalid());

    public static IRuleBuilderOptions<T, string?> OptionalHttpUrl<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(BeAValidHttpUrl)
            .WithMessage(_ => ValidationMessages.UrlInvalid())
            .MaximumLength(DomainRules.UrlMaxLength);

    public static IRuleBuilderOptions<T, string> HttpUrl<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(_ => ValidationMessages.UrlRequired())
            .Must(value => BeAValidHttpUrl(value))
            .WithMessage(_ => ValidationMessages.UrlInvalid())
            .MaximumLength(DomainRules.UrlMaxLength);

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage(_ => ValidationMessages.FieldRequired())
            .MinimumLength(PersonNameMinLength).WithMessage(_ => ValidationMessages.FieldTooShort(PersonNameMinLength))
            .MaximumLength(PersonNameMaxLength).WithMessage(_ => ValidationMessages.FieldTooLong(PersonNameMaxLength));

    public static IRuleBuilderOptions<T, decimal> Money<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThanOrEqualTo(DomainRules.MinPrice).WithMessage(_ => ValidationMessages.PriceNegative())
            .LessThanOrEqualTo(DomainRules.MaxPrice).WithMessage(_ => ValidationMessages.PriceTooHigh());

    public static IRuleBuilderOptions<T, int> RatingValue<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(DomainRules.MinRating, DomainRules.MaxRating)
            .WithMessage(_ => ValidationMessages.RatingOutOfRange(DomainRules.MinRating, DomainRules.MaxRating));

    private static bool BeAValidEmail(string value)
    {
        var trimmed = value.Trim();

        // Same shape FluentValidation's EmailAddress() accepts: one "@" with content either
        // side and no whitespace, which is all an address can be checked for without sending.
        var at = trimmed.IndexOf('@');

        return at > 0
               && at == trimmed.LastIndexOf('@')
               && at < trimmed.Length - 1
               && !trimmed.Any(char.IsWhiteSpace);
    }

    private static bool BeAValidHttpUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
