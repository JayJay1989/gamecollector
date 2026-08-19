package com.gamecollector.core.database

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import androidx.sqlite.db.SupportSQLiteOpenHelper
import androidx.sqlite.db.framework.FrameworkSQLiteOpenHelperFactory
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class GameCollectorDatabaseTests {
    private lateinit var database: GameCollectorDatabase

    @Before
    fun createDatabase() {
        database = Room.inMemoryDatabaseBuilder(
            ApplicationProvider.getApplicationContext(),
            GameCollectorDatabase::class.java,
        ).allowMainThreadQueries().build()
    }

    @After
    fun closeDatabase() = database.close()

    @Test
    fun catalogSearchAndBarcodeLookupRemainAvailableLocally() = runBlocking {
        val game = LocalGame(
            id = "game-1",
            title = "UNO Flip!",
            description = "Two-sided UNO.",
            publisher = "Mattel",
            releaseYear = 2019,
            minimumPlayers = 2,
            maximumPlayers = 4,
            minimumAge = 7,
            minimumPlayingTimeMinutes = 15,
            maximumPlayingTimeMinutes = 30,
            moderationStatus = "Approved",
            revision = 1,
            isComplete = true,
        )
        database.catalogDao().upsertGames(listOf(game))
        database.catalogDao().upsertBarcodes(listOf(LocalGameBarcode(game.id, "887961751062")))
        database.catalogDao().upsertLanguages(listOf(LocalGameLanguage(game.id, "language-1", "English", "en")))
        database.catalogDao().upsertTags(listOf(LocalGameTag(game.id, "tag-1", "Card Game")))

        assertEquals("UNO Flip!", database.catalogDao().observeSearch("flip").first().single().game.title)
        assertEquals(game.id, database.catalogDao().findGameIdByBarcode("887961751062"))
        val details = database.catalogDao().observeGame(game.id).first()
        assertEquals("English", details?.languages?.single()?.name)
        assertEquals("Card Game", details?.tags?.single()?.name)
    }

    @Test
    fun collectionOwnershipAndWishlistAreIndependentLocalProjections() = runBlocking {
        database.collectionDao().upsertCollections(listOf(LocalCollection("collection-1", "Our Games", "user-1", 2)))
        database.catalogDao().upsertGames(listOf(
            LocalGame("game-1", "UNO Flip!", null, "Mattel", 2019, null, null, null, null, null, "Approved", 1, false),
        ))
        database.catalogDao().upsertCollectionGames(listOf(LocalCollectionGame("collection-1", "game-1")))
        database.catalogDao().upsertWishlist(listOf(LocalWishlistItem("game-1")))

        assertEquals(setOf("game-1"), database.catalogDao().observeOwnedIds("collection-1").first().toSet())
        assertEquals(setOf("game-1"), database.catalogDao().observeWishlistIds().first().toSet())

        database.catalogDao().removeWishlist("game-1")
        assertTrue(database.catalogDao().observeWishlistIds().first().isEmpty())
        assertEquals(setOf("game-1"), database.catalogDao().observeOwnedIds("collection-1").first().toSet())
    }

    @Test
    fun draftAndMediaCheckpointSurviveIndependentReads() = runBlocking {
        val draft = LocalGameDraft(
            "draft-1", null, "887961751062", "New Game", null, "Publisher", 2026,
            2, 4, 7, 15, 30, "[]", "[]", "upcitemdb", 2, "Queued", null,
            null, true, "2026-08-15T00:00:00Z", "2026-08-15T00:00:00Z",
        )
        val upload = PendingMediaUpload(
            "upload-1", draft.id, "content://images/front", "Front", "Processing", "image/jpeg",
            1234, "media-1", 1, null,
        )

        database.draftDao().upsertDraft(draft)
        database.draftDao().upsertUpload(upload)

        assertEquals("New Game", database.draftDao().getDraft(draft.id)?.title)
        assertEquals("media-1", database.draftDao().getUploads(draft.id).single().serverMediaId)
        assertEquals("Processing", database.draftDao().observeUploads(draft.id).first().single().state)
    }

    @Test
    fun notificationReadStateIsObservableAndPersistent() = runBlocking {
        val item = LocalNotification("notice-1", "CollectionInvitation", "Invitation", "Join us", "{\"collectionId\":\"c1\"}", "2026-08-15T10:00:00Z", null)
        database.notificationDao().upsert(listOf(item))
        assertEquals(1, database.notificationDao().observe().first().count { it.readAtUtc == null })

        database.notificationDao().markRead(item.id, "2026-08-15T10:01:00Z")

        assertEquals("2026-08-15T10:01:00Z", database.notificationDao().get().single().readAtUtc)
        assertEquals("{\"collectionId\":\"c1\"}", database.notificationDao().get().single().payloadJson)
    }

    @Test
    fun migrationThreeToFourAddsNotificationPayloadWithoutLosingRows() {
        val helper = FrameworkSQLiteOpenHelperFactory().create(
            SupportSQLiteOpenHelper.Configuration.builder(ApplicationProvider.getApplicationContext())
                .callback(object : SupportSQLiteOpenHelper.Callback(3) {
                    override fun onCreate(db: androidx.sqlite.db.SupportSQLiteDatabase) {
                        db.execSQL("CREATE TABLE notifications (id TEXT NOT NULL PRIMARY KEY, type TEXT NOT NULL, title TEXT NOT NULL, body TEXT NOT NULL, createdAtUtc TEXT NOT NULL, readAtUtc TEXT)")
                        db.execSQL("INSERT INTO notifications VALUES ('n1', 'Test', 'Title', 'Body', '2026-08-15T00:00:00Z', NULL)")
                    }
                    override fun onUpgrade(db: androidx.sqlite.db.SupportSQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
                }).build(),
        )
        val sqlite = helper.writableDatabase
        GameCollectorDatabase.MIGRATION_3_4.migrate(sqlite)
        sqlite.query("SELECT id, payloadJson FROM notifications").use { cursor ->
            assertTrue(cursor.moveToFirst())
            assertEquals("n1", cursor.getString(0))
            assertEquals("{}", cursor.getString(1))
        }
        helper.close()
    }

    @Test
    fun changeRequestStatusIsStoredForOfflineDisplay() = runBlocking {
        val item = LocalGameChangeRequest("change-1", "game-1", "UNO Flip!", "{\"minimumAge\":10}", "Pending", null,
            "2026-08-16T00:00:00Z", "2026-08-16T00:00:00Z")
        database.changeRequestDao().upsert(listOf(item))

        assertEquals("Pending", database.changeRequestDao().observe().first().single().status)
        database.changeRequestDao().upsert(listOf(item.copy(status = "Approved", adminComment = "Verified.")))
        assertEquals("Verified.", database.changeRequestDao().get(item.id)?.adminComment)
    }

    @Test
    fun migrationFourToFiveCreatesCorrectionStatusProjection() {
        val helper = FrameworkSQLiteOpenHelperFactory().create(
            SupportSQLiteOpenHelper.Configuration.builder(ApplicationProvider.getApplicationContext())
                .callback(object : SupportSQLiteOpenHelper.Callback(4) {
                    override fun onCreate(db: androidx.sqlite.db.SupportSQLiteDatabase) = Unit
                    override fun onUpgrade(db: androidx.sqlite.db.SupportSQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
                }).build(),
        )
        val sqlite = helper.writableDatabase
        GameCollectorDatabase.MIGRATION_4_5.migrate(sqlite)
        sqlite.query("SELECT COUNT(*) FROM game_change_requests").use { cursor ->
            assertTrue(cursor.moveToFirst())
            assertEquals(0, cursor.getInt(0))
        }
        helper.close()
    }

    @Test
    fun migrationTwoToThreePreservesPendingImageSelection() {
        val helper = FrameworkSQLiteOpenHelperFactory().create(
            SupportSQLiteOpenHelper.Configuration.builder(ApplicationProvider.getApplicationContext())
                .callback(object : SupportSQLiteOpenHelper.Callback(2) {
                    override fun onCreate(db: androidx.sqlite.db.SupportSQLiteDatabase) {
                        db.execSQL("CREATE TABLE pending_media_uploads (id TEXT NOT NULL PRIMARY KEY, draftGameId TEXT NOT NULL, localUri TEXT NOT NULL, kind TEXT NOT NULL, state TEXT NOT NULL)")
                        db.execSQL("CREATE INDEX index_pending_media_uploads_draftGameId ON pending_media_uploads(draftGameId)")
                        db.execSQL("INSERT INTO pending_media_uploads VALUES ('upload-1', 'draft-1', 'content://front', 'Front', 'Pending')")
                    }
                    override fun onUpgrade(db: androidx.sqlite.db.SupportSQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit
                })
                .build(),
        )
        val sqlite = helper.writableDatabase

        GameCollectorDatabase.MIGRATION_2_3.migrate(sqlite)

        sqlite.query("SELECT localUri, contentType, fileSizeBytes FROM pending_media_uploads WHERE id = 'upload-1'").use { cursor ->
            assertTrue(cursor.moveToFirst())
            assertEquals("content://front", cursor.getString(0))
            assertEquals("image/jpeg", cursor.getString(1))
            assertEquals(0L, cursor.getLong(2))
        }
        sqlite.query("SELECT COUNT(*) FROM game_drafts").use { cursor ->
            assertTrue(cursor.moveToFirst())
            assertEquals(0, cursor.getInt(0))
        }
        helper.close()
    }
}
