using Microsoft.EntityFrameworkCore;
using ScanNow.Domain.Abstractions;
using ScanNow.Infrastructure;

namespace ScanNow.Tests.Infrastructure;

/// <summary>
/// Creates an ApplicationDbContext pointed at the real PostgreSQL database.
///
/// WHY real PostgreSQL:
///   - Both bugs were specific to Npgsql / EF Core 10 query translation.
///   - EF Core InMemory does not apply global query filters the same way,
///     so it would silently hide the original bugs and give false-green results.
///
/// ISOLATION strategy:
///   - Every test that writes data wraps its work in a transaction and rolls back
///     at the end (see IAsyncLifetime in each test class). Nothing is committed
///     to production data.
/// </summary>
public static class TestDbContextFactory
{
    // Connection string for the real PostgreSQL — read from env var so CI can override it.
    private const string DefaultConnStr =
        "Host=dpg-d87eu857vvec739015jg-a.singapore-postgres.render.com;" +
        "Port=5432;Database=scannow_db;Username=scannow_user;" +
        "Password=saqlBZoMyiaO2trxXgzfvDoUMOQ19dzL;" +
        "SSL Mode=Require;Trust Server Certificate=true";

    public static string ConnectionString =>
        Environment.GetEnvironmentVariable("SCANNOW_TEST_CONN") ?? DefaultConnStr;

    public static ApplicationDbContext Create(ITenantContext tenantContext, bool logSql = false)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString);

        if (logSql)
            builder.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
                   .EnableSensitiveDataLogging();

        return new ApplicationDbContext(builder.Options, tenantContext);
    }
}
