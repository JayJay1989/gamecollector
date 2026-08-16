using GameCollector.Infrastructure.Persistence.Converters;
using GameCollector.Domain.Users;
using GameCollector.Domain.Collections;
using GameCollector.Domain.Catalog;
using Microsoft.EntityFrameworkCore;
using GameCollector.Domain.Auditing;
using GameCollector.Domain.Sync;
using GameCollector.Domain.Background;
using GameCollector.Domain.Notifications;

namespace GameCollector.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();

    public DbSet<DeviceRegistration> DeviceRegistrations => Set<DeviceRegistration>();
    public DbSet<Collection> Collections => Set<Collection>();
    public DbSet<CollectionMember> CollectionMembers => Set<CollectionMember>();
    public DbSet<CollectionInvitation> CollectionInvitations => Set<CollectionInvitation>();
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameBarcode> GameBarcodes => Set<GameBarcode>();
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<GameLanguage> GameLanguages => Set<GameLanguage>();
    public DbSet<GameTag> GameTags => Set<GameTag>();
    public DbSet<CollectionGame> CollectionGames => Set<CollectionGame>();
    public DbSet<WishlistItem> WishlistItems => Set<WishlistItem>();
    public DbSet<GameImage> GameImages => Set<GameImage>();
    public DbSet<GameChangeRequest> GameChangeRequests => Set<GameChangeRequest>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SyncEvent> SyncEvents => Set<SyncEvent>();
    public DbSet<ProcessedMutation> ProcessedMutations => Set<ProcessedMutation>();
    public DbSet<SyncRetentionState> SyncRetentionStates => Set<SyncRetentionState>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SyncDiagnostic> SyncDiagnostics => Set<SyncDiagnostic>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
