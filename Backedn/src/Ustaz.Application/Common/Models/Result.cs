namespace Ustaz.Application.Common.Models;

/// <summary>Lightweight outcome type for operations whose failure is an expected branch.</summary>
public class Result
{
    protected Result(bool succeeded, string? message, IReadOnlyList<string>? errors)
    {
        Succeeded = succeeded;
        Message = message;
        Errors = errors ?? [];
    }

    public bool Succeeded { get; }

    public string? Message { get; }

    public IReadOnlyList<string> Errors { get; }

    public static Result Success(string? message = null) => new(true, message, null);

    public static Result Failure(string message, params string[] errors) => new(false, message, errors);
}

/// <inheritdoc />
public sealed class Result<T> : Result
{
    private Result(bool succeeded, T? data, string? message, IReadOnlyList<string>? errors)
        : base(succeeded, message, errors) => Data = data;

    public T? Data { get; }

    public static Result<T> Success(T data, string? message = null) => new(true, data, message, null);

    public static new Result<T> Failure(string message, params string[] errors) =>
        new(false, default, message, errors);
}
