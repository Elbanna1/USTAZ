using System.ComponentModel.DataAnnotations;

namespace Ustaz.Infrastructure.Options;

/// <summary>
/// The request budgets the API enforces. The defaults are the production values; they are
/// configuration driven so an operator can loosen or tighten them for a particular deployment
/// — and so a test host can set a budget it can actually drive to its limit — without a
/// rebuild.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// The ceiling every request passes through. Generous: ordinary browsing must never be
    /// throttled, and this exists to blunt a flood, not to shape traffic.
    /// </summary>
    [Range(1, 1_000_000)]
    public int GlobalPermitLimit { get; set; } = 300;

    [Range(1, 3600)]
    public int GlobalWindowSeconds { get; set; } = 60;

    /// <summary>
    /// The credential endpoints: register, login, refresh, password reset. Much tighter,
    /// because the cost of a wrong answer here is a guessed password.
    /// </summary>
    [Range(1, 1_000_000)]
    public int AuthPermitLimit { get; set; } = 10;

    [Range(1, 3600)]
    public int AuthWindowSeconds { get; set; } = 60;

    /// <summary>
    /// Uploads cost disk and bandwidth: enough for someone handing in a few files, useless as
    /// an abuse channel.
    /// </summary>
    [Range(1, 1_000_000)]
    public int UploadPermitLimit { get; set; } = 20;

    [Range(1, 3600)]
    public int UploadWindowSeconds { get; set; } = 300;
}
