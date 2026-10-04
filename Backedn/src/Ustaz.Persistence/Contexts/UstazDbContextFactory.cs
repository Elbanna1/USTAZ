using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ustaz.Persistence.Contexts;

/// <summary>
/// Used by "dotnet ef" when no host is available. The connection string comes from
/// USTAZ_CONNECTIONSTRING (or ConnectionStrings__DefaultConnection) so credentials
/// never live in source control.
/// </summary>
public sealed class UstazDbContextFactory : IDesignTimeDbContextFactory<UstazDbContext>
{
    /// <summary>
    /// Windows-authenticated local SQL Server: the same string the Development settings use,
    /// and one that carries no password to leak.
    /// </summary>
    private const string LocalFallback =
        "Server=(local);Database=UstazDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";

    public UstazDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("USTAZ_CONNECTIONSTRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? LocalFallback;

        var optionsBuilder = new DbContextOptionsBuilder<UstazDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
            sql.MigrationsAssembly(typeof(UstazDbContext).Assembly.FullName));

        return new UstazDbContext(optionsBuilder.Options);
    }
}
