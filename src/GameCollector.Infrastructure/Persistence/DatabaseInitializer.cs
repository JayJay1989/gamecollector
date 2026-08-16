using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence;

public sealed class DatabaseInitializer(ApplicationDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        EnsureDatabaseDirectoryExists(dbContext.Database.GetConnectionString());
        await dbContext.Database.MigrateAsync(cancellationToken);

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State is not ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await ExecutePragmaAsync(connection, "PRAGMA foreign_keys=ON;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA journal_mode=WAL;", cancellationToken);
            await ExecutePragmaAsync(connection, "PRAGMA busy_timeout=5000;", cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void EnsureDatabaseDirectoryExists(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The SQLite connection string is missing.");
        }

        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) ||
            string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var fullPath = Path.GetFullPath(builder.DataSource);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task ExecutePragmaAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        _ = await command.ExecuteScalarAsync(cancellationToken);
    }
}
