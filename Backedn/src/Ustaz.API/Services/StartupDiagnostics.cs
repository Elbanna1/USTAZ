using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Ustaz.API.Services;

/// <summary>
/// Makes a failed startup readable, wherever the application happens to be hosted.
///
/// The problem this solves: when startup throws, the ASP.NET Core Module replaces everything with
/// a generic <c>HTTP Error 500.30</c> page. There is no request, so there is no request log; the
/// host was never built, so Serilog's file sink was never configured; and <c>stdoutLogEnabled</c>
/// is <c>false</c> in a default publish, so stderr goes nowhere. The operator is told only that
/// the app "failed to start".
///
/// Three sinks are written, because which ones are available depends entirely on the hosting:
///
/// 1. <b>stderr</b> — picked up by the IIS stdout log when it is switched on.
/// 2. <b>The Windows event log</b> — needs no deployment change, but a shared-hosting app pool
///    identity usually cannot write it, and the customer usually cannot read it.
/// 3. <b>A file under the content root</b> — the one an operator on shared Windows hosting can
///    actually reach, over FTP or a control-panel file manager. This is the sink that matters
///    most in practice and was the one missing.
///
/// Everything written here is redacted first. A startup exception is exactly the kind that tends
/// to quote the configuration that caused it, so the report must never be the thing that leaks
/// the connection string it is complaining about.
/// </summary>
public static class StartupDiagnostics
{
    private const string EventLogSource = "Ustaz.API";

    /// <summary>Relative to the content root, so it sits beside the ordinary Serilog output.</summary>
    public const string ReportFileName = "logs/startup-error.txt";

    /// <summary>
    /// Patterns whose *values* must never be written out. Matched case-insensitively against
    /// <c>key=value</c> pairs, which is the shape a connection string and most secret-bearing
    /// configuration take.
    /// </summary>
    private static readonly Regex SecretPattern = new(
        @"(?<key>password|pwd|user\s*id|uid|secret|api[_-]?key|apikey|token|jwt__?key|jwt:key)\s*=\s*(?<value>[^;,\s""']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>A whole connection string, which is worth removing wholesale rather than per-field.</summary>
    private static readonly Regex ConnectionStringPattern = new(
        @"(Server|Data\s*Source|Initial\s*Catalog|Database)\s*=\s*[^;""']+;?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Reports a startup failure to every sink that will accept it, then returns. Never throws:
    /// a failure to report must not replace the failure being reported.
    /// </summary>
    public static void Report(string report, string? contentRootPath)
    {
        var safe = Redact(report);

        WriteToStandardError(safe);
        WriteToFile(safe, contentRootPath);
        WriteToEventLog(safe);
    }

    /// <summary>
    /// Builds the report for an unexpected startup exception — anything that is not the
    /// missing-configuration case <see cref="StartupConfigurationCheck"/> already explains.
    ///
    /// The stack trace is included deliberately. This file is only ever written when the
    /// application is already refusing to serve traffic, and without a frame to look at an
    /// operator cannot tell a bad storage path from an unreachable database.
    /// </summary>
    public static string DescribeUnexpectedFailure(
        Exception exception,
        string environmentName,
        string contentRootPath)
    {
        var builder = new StringBuilder();

        builder.AppendLine("USTAZ failed to start.");
        builder.AppendLine();
        builder.AppendLine("Time (UTC):   " + DateTime.UtcNow.ToString("u"));
        builder.AppendLine("Environment:  " + environmentName);
        builder.AppendLine("Content root: " + contentRootPath);
        builder.AppendLine("Process:      " + Environment.ProcessPath);
        builder.AppendLine(".NET version: " + RuntimeInformation.FrameworkDescription);
        builder.AppendLine();
        builder.AppendLine("This was not a missing-configuration failure. The exception below "
            + "escaped startup, so the ASP.NET Core Module reported HTTP 500.30 instead.");
        builder.AppendLine();

        var depth = 0;

        for (var current = exception; current is not null; current = current.InnerException)
        {
            builder.AppendLine(new string(' ', depth * 2) + current.GetType().FullName);
            builder.AppendLine(new string(' ', depth * 2) + "  " + current.Message);
            builder.AppendLine();
            depth++;
        }

        builder.AppendLine("Stack trace:");
        builder.AppendLine(exception.StackTrace ?? "(none)");
        builder.AppendLine();
        builder.AppendLine("Values in this report are redacted. If a configuration value is "
            + "implicated, its key is named but never its contents.");

        return builder.ToString();
    }

    /// <summary>
    /// Removes anything that looks like a credential. Deliberately blunt: over-redacting a
    /// diagnostic costs an operator one more look at the configuration, while under-redacting
    /// one writes a password into a file served from a web root.
    /// </summary>
    public static string Redact(string text)
    {
        var redacted = SecretPattern.Replace(text, match => match.Groups["key"].Value + "=***REDACTED***");

        redacted = ConnectionStringPattern.Replace(
            redacted, match => match.Groups[1].Value + "=***REDACTED***;");

        return redacted;
    }

    private static void WriteToStandardError(string report)
    {
        try
        {
            Console.Error.WriteLine(report);
            Console.Error.Flush();
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // No console attached under IIS in-process hosting.
        }
    }

    /// <summary>
    /// The sink a shared-hosting operator can actually read. Overwritten rather than appended:
    /// the question being answered is always "why did it fail *this* time".
    /// </summary>
    private static void WriteToFile(string report, string? contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            return;
        }

        try
        {
            var path = Path.Combine(contentRootPath, ReportFileName.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, report, Encoding.UTF8);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            // An unwritable application directory is itself a plausible cause of the failure
            // being reported, so this sink failing is expected rather than exceptional.
        }
    }

    private static void WriteToEventLog(string report)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        try
        {
            EventLog.WriteEntry(EventLogSource, report, EventLogEntryType.Error);
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or System.Security.SecurityException
            or PlatformNotSupportedException
            or ArgumentException)
        {
            // The app pool identity is frequently not allowed to write the event log.
        }
    }
}
