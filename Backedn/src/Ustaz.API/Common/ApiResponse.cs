namespace Ustaz.API.Common;

/// <summary>
/// The envelope every successful USTAZ endpoint returns, so the React client can rely on
/// one consistent shape.
/// </summary>
public class ApiResponse
{
    public bool Success { get; init; } = true;

    public string Message { get; init; } = "Operation completed successfully.";

    public static ApiResponse Ok(string? message = null) =>
        new() { Success = true, Message = message ?? "Operation completed successfully." };
}

/// <inheritdoc />
public sealed class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null) =>
        new()
        {
            Success = true,
            Message = message ?? "Operation completed successfully.",
            Data = data
        };
}
