namespace Ustaz.Domain.Constants;

/// <summary>Hard domain limits shared by validators, configurations and business rules.</summary>
public static class DomainRules
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    public const int MinGroupCapacity = 1;
    public const int MaxGroupCapacity = 500;

    public const decimal MinPrice = 0m;
    public const decimal MaxPrice = 1_000_000m;

    public const int MaxPercentageDiscount = 100;

    public const int NameMaxLength = 200;
    public const int SlugMaxLength = 220;
    public const int ShortTextMaxLength = 500;
    public const int DescriptionMaxLength = 4000;
    public const int UrlMaxLength = 2048;
    public const int PhoneMaxLength = 20;

    public const int MaxYearsOfExperience = 60;
    public const int MinPublishableLessonCount = 1;

    public const int DefaultPageSize = 12;
    public const int MaxPageSize = 100;

    public const decimal MoneyPrecisionScale = 2m;
}
