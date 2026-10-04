namespace Ustaz.Infrastructure.Options;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string? ConnectionString { get; set; }

    public string InstanceName { get; set; } = "ustaz:";

    public int DefaultExpirationMinutes { get; set; } = 10;

    public bool Enabled => !string.IsNullOrWhiteSpace(ConnectionString);
}
