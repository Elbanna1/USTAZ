using Ustaz.Application.Common.Interfaces;

namespace Ustaz.Infrastructure.Services.Common;

/// <summary>
/// Used outside a request — background sweeps, tests — where there is no correlation id to
/// carry. Registered as the fallback so nothing has to null-check the accessor.
/// </summary>
public sealed class NullCorrelationContext : ICorrelationContext
{
    public string? CorrelationId => null;
}
