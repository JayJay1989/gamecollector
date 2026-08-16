package com.gamecollector.core.database

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase
import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

@Database(
    entities = [
        LocalProfile::class,
        LocalCollection::class,
        LocalCollectionMember::class,
        LocalGame::class,
        LocalGameBarcode::class,
        LocalGameLanguage::class,
        LocalGameTag::class,
        LocalGameImage::class,
        LocalCollectionGame::class,
        LocalWishlistItem::class,
        LocalInvitation::class,
        LocalNotification::class,
        LocalGameChangeRequest::class,
        PendingMutation::class,
        PendingMediaUpload::class,
        LocalGameDraft::class,
        SyncScopeState::class,
    ],
    version = 5,
    exportSchema = true,
)
abstract class GameCollectorDatabase : RoomDatabase() {
    abstract fun profileDao(): ProfileDao
    abstract fun collectionDao(): CollectionDao
    abstract fun catalogDao(): CatalogDao
    abstract fun invitationDao(): InvitationDao
    abstract fun syncDao(): SyncDao
    abstract fun notificationDao(): NotificationDao
    abstract fun changeRequestDao(): ChangeRequestDao
    abstract fun draftDao(): DraftDao

    companion object {
        @Volatile private var instance: GameCollectorDatabase? = null

        fun get(context: Context): GameCollectorDatabase = instance ?: synchronized(this) {
            instance ?: Room.databaseBuilder(
                context.applicationContext,
                GameCollectorDatabase::class.java,
                "game-collector.db",
            ).addMigrations(MIGRATION_1_2, MIGRATION_2_3, MIGRATION_3_4, MIGRATION_4_5).build().also { instance = it }
        }

        val MIGRATION_1_2 = object : Migration(1, 2) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE collection_games ADD COLUMN isPresent INTEGER NOT NULL DEFAULT 1")
                db.execSQL("ALTER TABLE collection_games ADD COLUMN lastServerSequence INTEGER NOT NULL DEFAULT 0")
                db.execSQL("ALTER TABLE wishlist_items ADD COLUMN isPresent INTEGER NOT NULL DEFAULT 1")
                db.execSQL("ALTER TABLE wishlist_items ADD COLUMN lastServerSequence INTEGER NOT NULL DEFAULT 0")
            }
        }

        val MIGRATION_2_3 = object : Migration(2, 3) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("""CREATE TABLE IF NOT EXISTS game_drafts (id TEXT NOT NULL PRIMARY KEY, serverGameId TEXT, barcode TEXT, title TEXT NOT NULL, description TEXT, publisher TEXT, releaseYear INTEGER, minimumPlayers INTEGER, maximumPlayers INTEGER, minimumAge INTEGER, minimumPlayingTimeMinutes INTEGER, maximumPlayingTimeMinutes INTEGER, languageIdsJson TEXT NOT NULL, tagIdsJson TEXT NOT NULL, source TEXT, step INTEGER NOT NULL, status TEXT NOT NULL, lastError TEXT, serverRevision INTEGER, submitRequested INTEGER NOT NULL, createdAtUtc TEXT NOT NULL, updatedAtUtc TEXT NOT NULL)""")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_game_drafts_status ON game_drafts(status)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_game_drafts_updatedAtUtc ON game_drafts(updatedAtUtc)")
                db.execSQL("""CREATE TABLE pending_media_uploads_new (id TEXT NOT NULL PRIMARY KEY, draftGameId TEXT NOT NULL, localUri TEXT NOT NULL, kind TEXT NOT NULL, state TEXT NOT NULL, contentType TEXT NOT NULL, fileSizeBytes INTEGER NOT NULL, serverMediaId TEXT, attemptCount INTEGER NOT NULL, lastError TEXT)""")
                db.execSQL("""INSERT INTO pending_media_uploads_new (id, draftGameId, localUri, kind, state, contentType, fileSizeBytes, serverMediaId, attemptCount, lastError) SELECT id, draftGameId, localUri, kind, state, 'image/jpeg', 0, NULL, 0, NULL FROM pending_media_uploads""")
                db.execSQL("DROP TABLE pending_media_uploads")
                db.execSQL("ALTER TABLE pending_media_uploads_new RENAME TO pending_media_uploads")
                db.execSQL("DELETE FROM pending_media_uploads WHERE rowid NOT IN (SELECT MAX(rowid) FROM pending_media_uploads GROUP BY draftGameId, kind)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_pending_media_uploads_draftGameId ON pending_media_uploads(draftGameId)")
                db.execSQL("CREATE UNIQUE INDEX IF NOT EXISTS index_pending_media_uploads_draftGameId_kind ON pending_media_uploads(draftGameId, kind)")
            }
        }

        val MIGRATION_3_4 = object : Migration(3, 4) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("ALTER TABLE notifications ADD COLUMN payloadJson TEXT NOT NULL DEFAULT '{}'")
            }
        }


        val MIGRATION_4_5 = object : Migration(4, 5) {
            override fun migrate(db: SupportSQLiteDatabase) {
                db.execSQL("""CREATE TABLE IF NOT EXISTS game_change_requests (id TEXT NOT NULL PRIMARY KEY, gameId TEXT NOT NULL, gameTitle TEXT NOT NULL, proposedChangesJson TEXT NOT NULL, status TEXT NOT NULL, adminComment TEXT, createdAtUtc TEXT NOT NULL, updatedAtUtc TEXT NOT NULL)""")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_game_change_requests_gameId ON game_change_requests(gameId)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_game_change_requests_status ON game_change_requests(status)")
                db.execSQL("CREATE INDEX IF NOT EXISTS index_game_change_requests_updatedAtUtc ON game_change_requests(updatedAtUtc)")
            }
        }
    }
}
