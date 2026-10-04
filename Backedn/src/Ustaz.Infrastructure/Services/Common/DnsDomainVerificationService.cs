using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Ustaz.Application.Features.Tenants;

namespace Ustaz.Infrastructure.Services.Common;

/// <summary>
/// Verifies custom-domain ownership by looking for a DNS TXT record.
///
/// .NET has no built-in TXT lookup, so this shells out to the platform resolver
/// (nslookup). If the lookup cannot run at all, verification fails closed: a domain is
/// never marked verified on the strength of an error.
/// </summary>
public sealed class DnsDomainVerificationService(ILogger<DnsDomainVerificationService> logger)
    : IDomainVerificationService
{
    private const string RecordPrefix = "_ustaz-verify";
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(10);

    public string RecordName(string host) => RecordPrefix + "." + host.Trim().ToLowerInvariant();

    public async Task<bool> IsVerifiedAsync(
        string host,
        string expectedToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(expectedToken))
        {
            return false;
        }

        try
        {
            var records = await LookupTxtAsync(RecordName(host), cancellationToken);

            return records.Any(record =>
                record.Contains(expectedToken, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "DNS verification lookup failed for {Host}.", host);
            return false;
        }
    }

    private static async Task<IReadOnlyList<string>> LookupTxtAsync(
        string recordName,
        CancellationToken cancellationToken)
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var startInfo = new ProcessStartInfo
        {
            FileName = isWindows ? "nslookup" : "dig",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (isWindows)
        {
            startInfo.ArgumentList.Add("-type=TXT");
            startInfo.ArgumentList.Add(recordName);
        }
        else
        {
            startInfo.ArgumentList.Add("+short");
            startInfo.ArgumentList.Add("TXT");
            startInfo.ArgumentList.Add(recordName);
        }

        using var process = Process.Start(startInfo);

        if (process is null)
        {
            return [];
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LookupTimeout);

        var output = await process.StandardOutput.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token);

        return [.. output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.Length > 0)];
    }
}
