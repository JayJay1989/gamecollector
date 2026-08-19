package com.gamecollector.core.database

import androidx.room.Dao
import androidx.room.Embedded
import androidx.room.Query
import androidx.room.Relation
import androidx.room.Transaction
import androidx.room.Upsert
import kotlinx.coroutines.flow.Flow

@Dao
interface ProfileDao {
    @Query("SELECT * FROM profiles LIMIT 1") fun observe(): Flow<LocalProfile?>
    @Query("SELECT * FROM profiles LIMIT 1") suspend fun get(): LocalProfile?
    @Upsert suspend fun upsert(profile: LocalProfile)
    @Query("DELETE FROM profiles") suspend fun clear()
}

@Dao
interface CollectionDao {
    @Query("SELECT * FROM collections ORDER BY name COLLATE NOCASE") fun observeCollections(): Flow<List<LocalCollection>>
    @Query("SELECT * FROM collections ORDER BY name COLLATE NOCASE") suspend fun getCollections(): List<LocalCollection>
    @Upsert suspend fun upsertCollections(items: List<LocalCollection>)
    @Query("DELETE FROM collections") suspend fun clearCollections()
    @Query("DELETE FROM collections WHERE id = :id") suspend fun deleteCollection(id: String)
    @Query("SELECT * FROM collection_members WHERE collectionId = :collectionId ORDER BY role DESC, displayName COLLATE NOCASE") fun observeMembers(collectionId: String): Flow<List<LocalCollectionMember>>
    @Query("SELECT * FROM collection_members WHERE collectionId = :collectionId") suspend fun getMembers(collectionId: String): List<LocalCollectionMember>
    @Upsert suspend fun upsertMembers(items: List<LocalCollectionMember>)
    @Query("DELETE FROM collection_members WHERE collectionId = :collectionId") suspend fun clearMembers(collectionId: String)
}

data class LocalGameDetails(
    @Embedded val game: LocalGame,
    @Relation(parentColumn = "id", entityColumn = "gameId") val barcodes: List<LocalGameBarcode>,
    @Relation(parentColumn = "id", entityColumn = "gameId") val languages: List<LocalGameLanguage>,
    @Relation(parentColumn = "id", entityColumn = "gameId") val tags: List<LocalGameTag>,
    @Relation(parentColumn = "id", entityColumn = "gameId") val images: List<LocalGameImage>,
)

@Dao
interface CatalogDao {
    @Transaction
    @Query("SELECT * FROM games WHERE title LIKE '%' || :query || '%' COLLATE NOCASE ORDER BY title COLLATE NOCASE")
    fun observeSearch(query: String): Flow<List<LocalGameDetails>>

    @Transaction
    @Query("SELECT * FROM games WHERE id = :id")
    fun observeGame(id: String): Flow<LocalGameDetails?>

    @Transaction
    @Query("""
        SELECT games.* FROM games
        INNER JOIN collection_games ON collection_games.gameId = games.id
        WHERE collection_games.collectionId = :collectionId
          AND collection_games.isPresent = 1
          AND games.title LIKE '%' || :query || '%' COLLATE NOCASE
        ORDER BY games.title COLLATE NOCASE
    """)
    fun observeCollectionGames(collectionId: String, query: String): Flow<List<LocalGameDetails>>

    @Query("SELECT * FROM games WHERE id = :id") suspend fun getGame(id: String): LocalGame?
    @Query("SELECT gameId FROM game_barcodes WHERE barcode = :barcode LIMIT 1") suspend fun findGameIdByBarcode(barcode: String): String?
    @Upsert suspend fun upsertGames(items: List<LocalGame>)
    @Query("DELETE FROM games") suspend fun clearGames()
    @Query("DELETE FROM games WHERE id = :gameId") suspend fun deleteGame(gameId: String)
    @Upsert suspend fun upsertBarcodes(items: List<LocalGameBarcode>)
    @Upsert suspend fun upsertLanguages(items: List<LocalGameLanguage>)
    @Upsert suspend fun upsertTags(items: List<LocalGameTag>)
    @Upsert suspend fun upsertImages(items: List<LocalGameImage>)
    @Query("DELETE FROM game_barcodes WHERE gameId = :gameId") suspend fun clearBarcodes(gameId: String)
    @Query("DELETE FROM game_languages WHERE gameId = :gameId") suspend fun clearLanguages(gameId: String)
    @Query("DELETE FROM game_tags WHERE gameId = :gameId") suspend fun clearTags(gameId: String)
    @Query("DELETE FROM game_images WHERE gameId = :gameId AND kind = :kind") suspend fun clearImages(gameId: String, kind: String)

    @Query("SELECT gameId FROM collection_games WHERE collectionId = :collectionId AND isPresent = 1") fun observeOwnedIds(collectionId: String): Flow<List<String>>
    @Query("SELECT gameId FROM wishlist_items WHERE isPresent = 1") fun observeWishlistIds(): Flow<List<String>>
    @Upsert suspend fun upsertCollectionGames(items: List<LocalCollectionGame>)
    @Query("DELETE FROM collection_games WHERE collectionId = :collectionId") suspend fun clearCollectionGames(collectionId: String)
    @Query("DELETE FROM collection_games WHERE collectionId = :collectionId AND gameId = :gameId") suspend fun removeCollectionGame(collectionId: String, gameId: String)
    @Query("SELECT * FROM collection_games WHERE collectionId = :collectionId AND gameId = :gameId") suspend fun getCollectionGame(collectionId: String, gameId: String): LocalCollectionGame?
    @Upsert suspend fun upsertWishlist(items: List<LocalWishlistItem>)
    @Query("DELETE FROM wishlist_items") suspend fun clearWishlist()
    @Query("DELETE FROM wishlist_items WHERE gameId = :gameId") suspend fun removeWishlist(gameId: String)
    @Query("SELECT * FROM wishlist_items WHERE gameId = :gameId") suspend fun getWishlist(gameId: String): LocalWishlistItem?
}

@Dao
interface InvitationDao {
    @Query("SELECT * FROM invitations WHERE status = 'Pending' COLLATE NOCASE ORDER BY collectionName COLLATE NOCASE")
    fun observePending(): Flow<List<LocalInvitation>>
    @Upsert suspend fun upsert(items: List<LocalInvitation>)
    @Query("DELETE FROM invitations") suspend fun clear()
    @Query("DELETE FROM invitations WHERE id = :id") suspend fun delete(id: String)
}

@Dao
interface SyncDao {
    @Query("SELECT * FROM pending_mutations ORDER BY createdAtUtc LIMIT :limit") suspend fun pendingMutations(limit: Int = 100): List<PendingMutation>
    @Upsert suspend fun upsertMutation(item: PendingMutation)
    @Query("DELETE FROM pending_mutations WHERE id IN (:ids)") suspend fun deleteMutations(ids: List<String>)
    @Query("UPDATE pending_mutations SET attemptCount = attemptCount + 1 WHERE id IN (:ids)") suspend fun incrementAttempts(ids: List<String>)
    @Query("SELECT * FROM sync_scope_states") fun observeScopes(): Flow<List<SyncScopeState>>
    @Query("SELECT * FROM sync_scope_states") suspend fun getScopes(): List<SyncScopeState>
    @Upsert suspend fun upsertScope(item: SyncScopeState)
    @Query("DELETE FROM sync_scope_states") suspend fun clearScopes()
    @Query("SELECT COUNT(*) FROM pending_mutations") fun observePendingCount(): Flow<Int>
}

@Dao
interface NotificationDao {
    @Query("SELECT * FROM notifications ORDER BY createdAtUtc DESC") fun observe(): Flow<List<LocalNotification>>
    @Query("SELECT * FROM notifications ORDER BY createdAtUtc DESC") suspend fun get(): List<LocalNotification>
    @Upsert suspend fun upsert(items: List<LocalNotification>)
    @Query("UPDATE notifications SET readAtUtc = :readAtUtc WHERE id = :id") suspend fun markRead(id: String, readAtUtc: String)
    @Query("UPDATE notifications SET readAtUtc = :readAtUtc WHERE readAtUtc IS NULL") suspend fun markAllRead(readAtUtc: String)
    @Query("DELETE FROM notifications") suspend fun clear()
}

@Dao
interface ChangeRequestDao {
    @Query("SELECT * FROM game_change_requests ORDER BY updatedAtUtc DESC") fun observe(): Flow<List<LocalGameChangeRequest>>
    @Query("SELECT * FROM game_change_requests WHERE id = :id") suspend fun get(id: String): LocalGameChangeRequest?
    @Upsert suspend fun upsert(items: List<LocalGameChangeRequest>)
    @Query("DELETE FROM game_change_requests") suspend fun clear()
}

@Dao
interface DraftDao {
    @Query("SELECT * FROM game_drafts ORDER BY updatedAtUtc DESC") fun observeDrafts(): Flow<List<LocalGameDraft>>
    @Query("SELECT * FROM game_drafts WHERE id = :id") fun observeDraft(id: String): Flow<LocalGameDraft?>
    @Query("SELECT * FROM game_drafts WHERE id = :id") suspend fun getDraft(id: String): LocalGameDraft?
    @Query("SELECT * FROM game_drafts WHERE status != 'Submitted' ORDER BY updatedAtUtc") suspend fun getActiveDrafts(): List<LocalGameDraft>
    @Upsert suspend fun upsertDraft(item: LocalGameDraft)
    @Query("DELETE FROM game_drafts WHERE id = :id") suspend fun deleteDraft(id: String)

    @Query("SELECT * FROM pending_media_uploads WHERE draftGameId = :draftId ORDER BY kind") fun observeUploads(draftId: String): Flow<List<PendingMediaUpload>>
    @Query("SELECT * FROM pending_media_uploads WHERE draftGameId = :draftId ORDER BY kind") suspend fun getUploads(draftId: String): List<PendingMediaUpload>
    @Query("DELETE FROM pending_media_uploads WHERE draftGameId = :draftId") suspend fun deleteUploads(draftId: String)
    @Query("DELETE FROM pending_media_uploads WHERE draftGameId = :draftId AND kind = :kind") suspend fun deleteUpload(draftId: String, kind: String)
    @Upsert suspend fun upsertUpload(item: PendingMediaUpload)
}
