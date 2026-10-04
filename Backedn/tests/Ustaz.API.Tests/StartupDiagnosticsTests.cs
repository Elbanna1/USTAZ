using Shouldly;
using Ustaz.API.Services;

namespace Ustaz.API.Tests;

/// <summary>
/// The startup failure report is written to a file under the content root, which on IIS sits
/// inside the deployed application. So it has to be safe to read, safe to email to support, and
/// safe to leave on disk — which means it must never contain a credential.
///
/// This matters more than it looks. A startup exception is precisely the kind that quotes the
/// configuration that caused it: a <c>SqlException</c> names the server, an options validator
/// echoes the value it rejected. The whole point of the report is to name what went wrong
/// without becoming the leak it is describing.
/// </summary>
public sealed class StartupDiagnosticsTests
{
    [Theory]
    [InlineData("Password=Qd3?6-hJaX+9;")]
    [InlineData("password=hunter2")]
    [InlineData("PWD=hunter2")]
    [InlineData("User Id=db70576;")]
    [InlineData("uid=admin")]
    [InlineData("ApiSecret=abc123")]
    [InlineData("api_key=sk-live-123")]
    [InlineData("ApiKey=sk-live-123")]
    [InlineData("token=eyJhbGciOiJIUzI1NiJ9")]
    [InlineData("Jwt__Key=super-secret-signing-key-0123456789")]
    public void A_secret_value_is_redacted(string fragment)
    {
        var secret = fragment.Split('=', 2)[1].TrimEnd(';');

        var redacted = StartupDiagnostics.Redact("Failed while reading " + fragment + " from configuration.");

        redacted.ShouldNotContain(secret);
        redacted.ShouldContain("***REDACTED***");
    }

    [Fact]
    public void A_whole_connection_string_is_redacted()
    {
        const string report =
            "SqlException: cannot connect using "
            + "Server=db70576.public.databaseasp.net;Database=db70576;User Id=db70576;Password=Qd3?6-hJaX+9;Encrypt=True;";

        var redacted = StartupDiagnostics.Redact(report);

        // Neither the credential nor the host that would let someone try it elsewhere.
        redacted.ShouldNotContain("Qd3?6-hJaX+9");
        redacted.ShouldNotContain("db70576.public.databaseasp.net");
        redacted.ShouldNotContain("databaseasp");
    }

    [Fact]
    public void The_surrounding_diagnostic_text_survives_redaction()
    {
        // Over-redacting into uselessness would defeat the purpose just as thoroughly as
        // under-redacting: the operator still has to be able to read what failed.
        const string report =
            "USTAZ failed to start.\n"
            + "Microsoft.Data.SqlClient.SqlException\n"
            + "  A network-related error occurred while establishing a connection to SQL Server.\n"
            + "  Password=secret123;\n"
            + "Stack trace:\n"
            + "   at Ustaz.Persistence.DependencyInjection.AddPersistenceLayer(...)";

        var redacted = StartupDiagnostics.Redact(report);

        redacted.ShouldNotContain("secret123");

        redacted.ShouldContain("USTAZ failed to start.");
        redacted.ShouldContain("SqlException");
        redacted.ShouldContain("A network-related error occurred");
        redacted.ShouldContain("AddPersistenceLayer");
    }

    [Fact]
    public void Text_with_nothing_to_hide_is_left_alone()
    {
        const string report = "USTAZ cannot start: required configuration is missing.\n"
            + "  - ConnectionStrings__DefaultConnection — the SQL Server connection string.";

        StartupDiagnostics.Redact(report).ShouldBe(report);
    }

    [Fact]
    public void An_unexpected_failure_is_described_with_its_cause_chain()
    {
        var inner = new IOException("The directory is read-only.");
        var outer = new InvalidOperationException("Could not create the storage root.", inner);

        var report = StartupDiagnostics.DescribeUnexpectedFailure(
            outer, "Production", @"C:\inetpub\wwwroot\ustaz-api");

        // An operator needs the whole chain: the outer message says what the application was
        // doing, the inner one says why the operating system refused.
        report.ShouldContain("InvalidOperationException");
        report.ShouldContain("Could not create the storage root.");
        report.ShouldContain("IOException");
        report.ShouldContain("The directory is read-only.");

        // And enough context to know which deployment produced it.
        report.ShouldContain("Production");
        report.ShouldContain(@"C:\inetpub\wwwroot\ustaz-api");

        // It must say plainly that this is not the missing-configuration case, because that is
        // the first thing anyone will assume after reading the audit report.
        report.ShouldContain("not a missing-configuration failure");
    }
}
