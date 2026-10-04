namespace Ustaz.Application.Common.Localization;

/// <summary>
/// The error codes the API attaches to a failure that is not already carrying one of the
/// domain's own <c>BusinessRuleException</c> codes.
///
/// These are part of the public contract: the client switches on them to decide what to do
/// (re-authenticate, highlight a field, show a retry). They are never renamed once shipped,
/// and they are never translated — the human-readable text beside them is what gets localised.
/// </summary>
public static class ErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string DuplicateValue = "DUPLICATE_VALUE";
    public const string Forbidden = "FORBIDDEN";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string TokenExpired = "TOKEN_EXPIRED";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountInactive = "ACCOUNT_INACTIVE";
    public const string RefreshTokenInvalid = "REFRESH_TOKEN_INVALID";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string RequestCancelled = "REQUEST_CANCELLED";
    public const string RateLimited = "RATE_LIMITED";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string InternalError = "INTERNAL_ERROR";
}
