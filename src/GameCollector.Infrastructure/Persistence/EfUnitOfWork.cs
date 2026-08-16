using GameCollector.Application.Abstractions.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameCollector.Infrastructure.Persistence;

public sealed class EfUnitOfWork(ApplicationDbContext dbContext) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        if (dbContext.Database.CurrentTransaction is not null) { await action(cancellationToken); return; }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await action(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new PersistenceConcurrencyException(exception);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteException)
        {
            throw new PersistenceConflictException(
                GetConstraint(sqliteException.Message),
                exception);
        }
    }

    private static string GetConstraint(string message)
    {
        if (message.Contains("UserProfiles.IdentitySubject", StringComparison.Ordinal))
        {
            return PersistenceConstraints.IdentitySubject;
        }

        if (message.Contains("UserProfiles.NormalizedUsername", StringComparison.Ordinal))
        {
            return PersistenceConstraints.NormalizedUsername;
        }

        if (message.Contains("DeviceRegistrations.UserId", StringComparison.Ordinal))
        {
            return PersistenceConstraints.ActiveDeviceUser;
        }

        if (message.Contains("CollectionGames.CollectionId, CollectionGames.GameId", StringComparison.Ordinal)) return PersistenceConstraints.CollectionGame;
        if (message.Contains("WishlistItems.UserId, WishlistItems.GameId", StringComparison.Ordinal)) return PersistenceConstraints.WishlistItem;
        if (message.Contains("GameBarcodes.NormalizedBarcode", StringComparison.Ordinal)) return PersistenceConstraints.GameBarcode;
        if (message.Contains("GameImages.GameId, GameImages.ImageType", StringComparison.Ordinal)) return PersistenceConstraints.GameImageType;
        if (message.Contains("GameChangeRequests.GameId, GameChangeRequests.ProposedByUserId", StringComparison.Ordinal)) return PersistenceConstraints.PendingGameChangeRequest;

        return PersistenceConstraints.ActiveDeviceId;
    }
}
