using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameCollector.Infrastructure.Persistence;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "ConnectionStrings__GameCollector");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                "Data Source=data/gamecollector.db;Foreign Keys=True;Default Timeout=5;Pooling=True";
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new ApplicationDbContext(options);
    }
}
