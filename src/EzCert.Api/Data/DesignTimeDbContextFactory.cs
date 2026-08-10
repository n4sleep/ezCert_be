using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EzCert.Api.Data;

// Used by `dotnet ef` at design time so migration commands don't boot the full app
// (and therefore don't require a live database to *create* a migration).
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<EzCertDbContext>
{
    public EzCertDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("EZCERT_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=ezcert;Username=ezcert;Password=ezcert";
        var options = new DbContextOptionsBuilder<EzCertDbContext>().UseNpgsql(cs).Options;
        return new EzCertDbContext(options);
    }
}
