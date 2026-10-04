using FluentValidation.Results;

namespace Ustaz.Application.Common.Exceptions;

/// <summary>
/// Aggregates FluentValidation failures into the shape the API returns
/// (a dictionary of property name to messages).
/// </summary>
public sealed class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures occurred.") => Errors = new Dictionary<string, string[]>();

    public ValidationException(IEnumerable<ValidationFailure> failures) : this() =>
        Errors = failures
            .GroupBy(failure => failure.PropertyName, failure => failure.ErrorMessage)
            .ToDictionary(group => group.Key, group => group.Distinct().ToArray());

    public ValidationException(string propertyName, string errorMessage) : this() =>
        Errors = new Dictionary<string, string[]> { [propertyName] = [errorMessage] };

    public IDictionary<string, string[]> Errors { get; }
}
