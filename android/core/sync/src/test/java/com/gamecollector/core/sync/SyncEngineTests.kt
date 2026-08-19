package com.gamecollector.core.sync

import androidx.room.Room
import androidx.test.core.app.ApplicationProvider
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.database.LocalCollection
import com.gamecollector.core.database.LocalCollectionGame
import com.gamecollector.core.database.LocalGame
import com.gamecollector.core.database.PendingMutation
import com.gamecollector.core.database.SyncScopeState
import com.gamecollector.core.network.ApiResult
import com.gamecollector.core.network.SyncBootstrap
import com.gamecollector.core.network.SyncChange
import com.gamecollector.core.network.SyncMutation
import com.gamecollector.core.network.SyncMutationResult
import com.gamecollector.core.network.SyncScope
import com.gamecollector.core.network.SyncScopePage
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

@RunWith(RobolectricTestRunner::class)
class SyncEngineTests {
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
    fun queuedMutationIsPushedOnceAndBootstrapCreatesIndependentScopes() = runBlocking {
        seedOwnedGame(sequence = 0)
        database.syncDao().upsertMutation(PendingMutation(
            id = "mutation-1",
            scopeType = "collection",
            scopeId = COLLECTION_ID,
            operation = "addCollectionGame",
            payloadJson = """{"gameId":"$GAME_ID","collectionId":"$COLLECTION_ID"}""",
            createdAtUtc = "2026-08-15T00:00:00Z",
            attemptCount = 0,
        ))
        val remote = FakeRemote(
            pushResult = ApiResult.Success(listOf(SyncMutationResult("mutation-1", true, false, 20, null))),
            bootstrapResult = ApiResult.Success(bootstrap()),
        )

        val result = SyncEngine(database, remote, "device-1").run()

        assertEquals(SyncRunResult.Success, result)
        assertEquals(listOf("mutation-1"), remote.pushed.single().map(SyncMutation::mutationId))
        assertTrue(database.syncDao().pendingMutations().isEmpty())
        assertEquals(3, database.syncDao().getScopes().size)
        assertEquals(setOf(GAME_ID), database.catalogDao().observeOwnedIds(COLLECTION_ID).first().toSet())
        assertEquals("UNO Flip!", database.catalogDao().observeSearch("flip").first().single().game.title)
    }

    @Test
    fun higherServerSequenceAppliesRemovalAsTombstone() = runBlocking {
        seedOwnedGame(sequence = 20)
        database.syncDao().upsertScope(SyncScopeState("collection:$COLLECTION_ID", "collection", COLLECTION_ID, 20, null))
        val change = SyncChange(
            sequence = 21,
            scopeType = "collection",
            scopeId = COLLECTION_ID,
            operation = "collectionGameChanged",
            entityId = GAME_ID,
            payloadJson = """{"gameId":"$GAME_ID","isPresent":false}""",
        )
        val remote = FakeRemote(
            pullResult = ApiResult.Success(listOf(SyncScopePage("collection", COLLECTION_ID, 21, false, false, listOf(change)))),
        )

        assertEquals(SyncRunResult.Success, SyncEngine(database, remote, "device-1").run())

        assertTrue(database.catalogDao().observeOwnedIds(COLLECTION_ID).first().isEmpty())
        val tombstone = database.catalogDao().getCollectionGame(COLLECTION_ID, GAME_ID)
        assertFalse(tombstone!!.isPresent)
        assertEquals(21, tombstone.lastServerSequence)
        assertEquals(21, database.syncDao().getScopes().single().cursor)
    }

    @Test
    fun catalogDeletionRemovesGameAndCollectionReferences() = runBlocking {
        seedOwnedGame(sequence = 20)
        database.syncDao().upsertScope(SyncScopeState("catalog:", "catalog", null, 20, null))
        val change = SyncChange(
            sequence = 21,
            scopeType = "catalog",
            scopeId = null,
            operation = "gameDeleted",
            entityId = GAME_ID,
            payloadJson = """{"id":"$GAME_ID","isDeleted":true}""",
        )
        val remote = FakeRemote(
            pullResult = ApiResult.Success(listOf(SyncScopePage("catalog", null, 21, false, false, listOf(change)))),
        )

        assertEquals(SyncRunResult.Success, SyncEngine(database, remote, "device-1").run())

        assertTrue(database.catalogDao().observeSearch("").first().isEmpty())
        assertTrue(database.catalogDao().observeOwnedIds(COLLECTION_ID).first().isEmpty())
    }

    private suspend fun seedOwnedGame(sequence: Long) {
        database.collectionDao().upsertCollections(listOf(LocalCollection(COLLECTION_ID, "Our Games", USER_ID, 2)))
        database.catalogDao().upsertGames(listOf(
            LocalGame(GAME_ID, "UNO Flip!", null, "Mattel", 2019, null, null, null, null, null, "Approved", 1, true),
        ))
        database.catalogDao().upsertCollectionGames(listOf(LocalCollectionGame(COLLECTION_ID, GAME_ID, true, sequence)))
    }

    private fun bootstrap(): SyncBootstrap = SyncBootstrap(30, listOf(
        SyncChange(30, "catalog", null, "snapshot", USER_ID, CATALOG_SNAPSHOT),
        SyncChange(30, "user", USER_ID, "snapshot", USER_ID, USER_SNAPSHOT),
        SyncChange(30, "collection", COLLECTION_ID, "snapshot", COLLECTION_ID, COLLECTION_SNAPSHOT),
    ))

    private class FakeRemote(
        private val pushResult: ApiResult<List<SyncMutationResult>> = ApiResult.Success(emptyList()),
        private val pullResult: ApiResult<List<SyncScopePage>> = ApiResult.Success(emptyList()),
        private val bootstrapResult: ApiResult<SyncBootstrap> = ApiResult.NetworkError("Not configured"),
    ) : SyncRemoteDataSource {
        val pushed = mutableListOf<List<SyncMutation>>()
        override suspend fun push(deviceId: String, mutations: List<SyncMutation>): ApiResult<List<SyncMutationResult>> {
            pushed += mutations
            return pushResult
        }
        override suspend fun pull(deviceId: String, scopes: List<SyncScope>) = pullResult
        override suspend fun bootstrap(deviceId: String) = bootstrapResult
    }

    private companion object {
        const val USER_ID = "00000000-0000-0000-0000-000000000001"
        const val COLLECTION_ID = "00000000-0000-0000-0000-000000000002"
        const val GAME_ID = "00000000-0000-0000-0000-000000000003"
        const val LANGUAGE_ID = "00000000-0000-0000-0000-000000000004"
        const val TAG_ID = "00000000-0000-0000-0000-000000000005"
        const val CATALOG_SNAPSHOT = """{"games":[{"id":"$GAME_ID","title":"UNO Flip!","description":null,"publisher":"Mattel","releaseYear":2019,"minimumPlayers":2,"maximumPlayers":4,"minimumAge":7,"minimumPlayingTimeMinutes":15,"maximumPlayingTimeMinutes":30,"moderationStatus":"Approved","revision":1,"barcodes":["887961751062"],"languageIds":["$LANGUAGE_ID"],"tagIds":["$TAG_ID"]}],"languages":[{"id":"$LANGUAGE_ID","code":"en","name":"English"}],"tags":[{"id":"$TAG_ID","name":"Card Game"}]}"""
        const val USER_SNAPSHOT = """{"profile":{"id":"$USER_ID","displayName":"John","username":"john","defaultCollectionId":"$COLLECTION_ID"},"collections":[{"id":"$COLLECTION_ID","name":"Our Games","ownerUserId":"$USER_ID"}],"wishlist":[],"invitations":[],"notifications":[]}"""
        const val COLLECTION_SNAPSHOT = """{"collection":{"id":"$COLLECTION_ID","name":"Our Games","ownerUserId":"$USER_ID"},"members":[],"games":[{"gameId":"$GAME_ID","isOwned":true,"lastServerSequence":20}]}"""
    }
}
