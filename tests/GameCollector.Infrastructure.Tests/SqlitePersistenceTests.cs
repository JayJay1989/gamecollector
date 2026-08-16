using System.Data.Common;
using GameCollector.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameCollector.Infrastructure.Tests;

public sealed class SqlitePersistenceTests
{
    [Fact]
    public async Task InitializerAppliesMigrationsAndSqlitePragmas()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "GameCollector.Infrastructure.Tests",
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "integration.db");

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:GameCollector"] =
                        $"Data Source={databasePath};Foreign Keys=True;Default Timeout=5;Pooling=False"
                })
                .Build();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddLogging();
            serviceCollection.AddInfrastructure(configuration);

            await using var serviceProvider = serviceCollection.BuildServiceProvider();
            await serviceProvider.InitializeDatabaseAsync();

            await using var scope = serviceProvider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

            Assert.Contains(appliedMigrations, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
            Assert.True(await dbContext.Database.CanConnectAsync());

            var connection = dbContext.Database.GetDbConnection();
            await dbContext.Database.OpenConnectionAsync();
            Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "PRAGMA foreign_keys;"));
            Assert.Equal("wal", await ExecuteScalarAsync<string>(connection, "PRAGMA journal_mode;"));
            Assert.Equal(5000L, await ExecuteScalarAsync<long>(connection, "PRAGMA busy_timeout;"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static async Task<T> ExecuteScalarAsync<T>(DbConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var result = await command.ExecuteScalarAsync();

        return Assert.IsType<T>(result);
    }
}
