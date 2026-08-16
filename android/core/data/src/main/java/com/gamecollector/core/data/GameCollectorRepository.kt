package com.gamecollector.core.data

import androidx.room.withTransaction
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.database.LocalCollection
import com.gamecollector.core.database.LocalCollectionGame
import com.gamecollector.core.database.LocalCollectionMember
import com.gamecollector.core.database.LocalGame
import com.gamecollector.core.database.LocalGameChangeRequest
import com.gamecollector.core.database.LocalGameBarcode
import com.gamecollector.core.database.LocalGameDetails
import com.gamecollector.core.database.LocalGameLanguage
import com.gamecollector.core.database.LocalGameTag
import com.gamecollector.core.database.LocalInvitation
import com.gamecollector.core.database.LocalNotification
import com.gamecollector.core.database.LocalProfile
import com.gamecollector.core.database.LocalWishlistItem
import com.gamecollector.core.database.PendingMutation
import com.gamecollector.core.network.ApiResult
import com.gamecollector.core.network.CollectionInvitation
import com.gamecollector.core.network.CollectionMember
import com.gamecollector.core.network.CollectionRole
import com.gamecollector.core.network.CollectionSummary
import com.gamecollector.core.network.GameCollectorApi
import com.gamecollector.core.network.GameDetails
import com.gamecollector.core.network.GameSummary
import com.gamecollector.core.network.GameChangeRequest
import com.gamecollector.core.network.NotificationItem
import com.gamecollector.core.network.ReferenceData
import com.gamecollector.core.network.UserProfile
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import java.time.Instant
import java.util.UUID
import org.json.JSONObject

class GameCollectorRepository(
    private val database: GameCollectorDatabase,
    private val api: GameCollectorApi,
) {
    val profile: Flow<UserProfile?> = database.profileDao().observe().map { it?.toModel() }
    val collections: Flow<List<CollectionSummary>> = database.collectionDao().observeCollections().map { items -> items.map(LocalCollection::toModel) }
    val invitations: Flow<List<CollectionInvitation>> = database.invitationDao().observePending().map { items -> items.map(LocalInvitation::toModel) }
    val notifications: Flow<List<LocalNotification>> = database.notificationDao().observe()
    val changeRequests: Flow<List<LocalGameChangeRequest>> = database.changeRequestDao().observe()
    val syncScopes = database.syncDao().observeScopes()
    val pendingMutationCount = database.syncDao().observePendingCount()
    val wishlistIds: Flow<Set<String>> = database.catalogDao().observeWishlistIds().map(List<String>::toSet)

    fun members(collectionId: String): Flow<List<CollectionMember>> =
        database.collectionDao().observeMembers(collectionId).map { items -> items.map(LocalCollectionMember::toModel) }

    fun search(query: String): Flow<List<GameSummary>> =
        database.catalogDao().observeSearch(query).map { items -> items.map(LocalGame::toSummary) }

    fun game(gameId: String): Flow<GameDetails?> =
        database.catalogDao().observeGame(gameId).map { it?.toModel() }

    fun ownedIds(collectionId: String): Flow<Set<String>> =
        database.catalogDao().observeOwnedIds(collectionId).map(List<String>::toSet)

    suspend fun cachedProfile(): UserProfile? = database.profileDao().get()?.toModel()

    suspend fun cachedCollections(): List<CollectionSummary> = database.collectionDao().getCollections().map(LocalCollection::toModel)

    suspend fun cachedGameByBarcode(barcode: String): String? = database.catalogDao().findGameIdByBarcode(barcode)

    suspend fun cacheProfile(value: UserProfile) = database.profileDao().upsert(value.toLocal())

    suspend fun clearProfile() = database.profileDao().clear()

    suspend fun clearAll() = database.clearAllTables()

    suspend fun refreshProfile(): ApiResult<UserProfile> = api.getProfile().also { result ->
        if (result is ApiResult.Success) cacheProfile(result.value)
    }

    suspend fun refreshCollections(deviceId: String): ApiResult<List<CollectionSummary>> = api.listCollections(deviceId).also { result ->
        if (result is ApiResult.Success) database.collectionDao().upsertCollections(result.value.map(CollectionSummary::toLocal))
    }

    suspend fun refreshMembers(deviceId: String, collectionId: String): ApiResult<List<CollectionMember>> =
        api.listMembers(deviceId, collectionId).also { result ->
            if (result is ApiResult.Success) database.withTransaction {
                database.collectionDao().clearMembers(collectionId)
                database.collectionDao().upsertMembers(result.value.map { it.toLocal(collectionId) })
            }
        }

    suspend fun refreshInvitations(deviceId: String): ApiResult<List<CollectionInvitation>> = api.listInvitations(deviceId).also { result ->
        if (result is ApiResult.Success) database.withTransaction {
            database.invitationDao().clear()
            database.invitationDao().upsert(result.value.map(CollectionInvitation::toLocal))
        }
    }

    suspend fun refreshNotifications(deviceId: String): ApiResult<List<NotificationItem>> = api.listNotifications(deviceId).also { result ->
        if (result is ApiResult.Success) database.withTransaction {
            val locallyRead = database.notificationDao().get().filter { it.readAtUtc != null }.associate { it.id to it.readAtUtc }
            database.notificationDao().clear()
            database.notificationDao().upsert(result.value.map { item ->
                item.toLocal().let { local -> local.copy(readAtUtc = local.readAtUtc ?: locallyRead[local.id]) }
            })
        }
    }

    suspend fun refreshChangeRequests(deviceId: String): ApiResult<List<GameChangeRequest>> = api.listMyChangeRequests(deviceId).also { result ->
        if (result is ApiResult.Success) database.withTransaction {
            database.changeRequestDao().clear()
            database.changeRequestDao().upsert(result.value.map(GameChangeRequest::toLocal))
        }
    }

    suspend fun cacheChangeRequest(value: GameChangeRequest) = database.changeRequestDao().upsert(listOf(value.toLocal()))

    suspend fun clearCachedContent() = database.withTransaction {
        database.catalogDao().clearGames()
        database.invitationDao().clear()
        database.notificationDao().clear()
        database.changeRequestDao().clear()
        database.syncDao().clearScopes()
    }

    suspend fun markNotificationReadLocally(id: String) {
        database.notificationDao().markRead(id, Instant.now().toString())
    }

    suspend fun markAllNotificationsReadLocally() {
        database.notificationDao().markAllRead(Instant.now().toString())
    }

    suspend fun refreshCatalog(deviceId: String, query: String): ApiResult<List<GameSummary>> = api.searchGames(deviceId, query).also { result ->
        if (result is ApiResult.Success) database.withTransaction {
            result.value.forEach { summary ->
                val previous = database.catalogDao().getGame(summary.id)
                database.catalogDao().upsertGames(listOf(summary.toLocal(previous)))
            }
        }
    }

    suspend fun refreshGame(deviceId: String, gameId: String): ApiResult<GameDetails> = api.getGame(deviceId, gameId).also { result ->
        if (result is ApiResult.Success) cacheGame(result.value)
    }

    suspend fun refreshGameByBarcode(deviceId: String, barcode: String): ApiResult<GameDetails> = api.getGameByBarcode(deviceId, barcode).also { result ->
        if (result is ApiResult.Success) cacheGame(result.value)
    }

    suspend fun refreshOwned(deviceId: String, collectionId: String): ApiResult<Set<String>> {
        return when (val result = api.listOwnedGames(deviceId, collectionId)) {
            is ApiResult.Success -> {
                database.withTransaction {
                    result.value.forEach { item ->
                        val previous = database.catalogDao().getGame(item.gameId)
                        database.catalogDao().upsertGames(listOf(
                            LocalGame(
                                id = item.gameId,
                                title = item.title,
                                description = previous?.description,
                                publisher = item.publisher,
                                releaseYear = previous?.releaseYear,
                                minimumPlayers = previous?.minimumPlayers,
                                maximumPlayers = previous?.maximumPlayers,
                                minimumAge = previous?.minimumAge,
                                minimumPlayingTimeMinutes = previous?.minimumPlayingTimeMinutes,
                                maximumPlayingTimeMinutes = previous?.maximumPlayingTimeMinutes,
                                moderationStatus = item.moderationStatus,
                                revision = previous?.revision ?: 0,
                                isComplete = previous?.isComplete ?: false,
                            ),
                        ))
                    }
                    database.catalogDao().clearCollectionGames(collectionId)
                    database.catalogDao().upsertCollectionGames(result.value.map { LocalCollectionGame(collectionId, it.gameId) })
                }
                ApiResult.Success<Set<String>>(result.value.mapTo(mutableSetOf()) { it.gameId })
            }
            is ApiResult.Error -> ApiResult.Error(result.statusCode, result.code, result.message)
            is ApiResult.NetworkError -> ApiResult.NetworkError(result.message)
            ApiResult.SignedOut -> ApiResult.SignedOut
        }
    }

    suspend fun refreshWishlist(deviceId: String): ApiResult<Set<String>> {
        return when (val result = api.listWishlist(deviceId)) {
            is ApiResult.Success -> {
                database.withTransaction {
                    result.value.forEach { item ->
                        val previous = database.catalogDao().getGame(item.gameId)
                        database.catalogDao().upsertGames(listOf(
                            LocalGame(
                                id = item.gameId,
                                title = item.title,
                                description = previous?.description,
                                publisher = item.publisher,
                                releaseYear = previous?.releaseYear,
                                minimumPlayers = previous?.minimumPlayers,
                                maximumPlayers = previous?.maximumPlayers,
                                minimumAge = previous?.minimumAge,
                                minimumPlayingTimeMinutes = previous?.minimumPlayingTimeMinutes,
                                maximumPlayingTimeMinutes = previous?.maximumPlayingTimeMinutes,
                                moderationStatus = item.moderationStatus,
                                revision = previous?.revision ?: 0,
                                isComplete = previous?.isComplete ?: false,
                            ),
                        ))
                    }
                    database.catalogDao().clearWishlist()
                    database.catalogDao().upsertWishlist(result.value.map { LocalWishlistItem(it.gameId) })
                }
                ApiResult.Success<Set<String>>(result.value.mapTo(mutableSetOf()) { it.gameId })
            }
            is ApiResult.Error -> ApiResult.Error(result.statusCode, result.code, result.message)
            is ApiResult.NetworkError -> ApiResult.NetworkError(result.message)
            ApiResult.SignedOut -> ApiResult.SignedOut
        }
    }

    suspend fun setOwnedLocally(collectionId: String, gameId: String, owned: Boolean) {
        val previous = database.catalogDao().getCollectionGame(collectionId, gameId)
        database.catalogDao().upsertCollectionGames(listOf(
            LocalCollectionGame(collectionId, gameId, owned, previous?.lastServerSequence ?: 0),
        ))
        if (owned) database.catalogDao().removeWishlist(gameId)
    }

    suspend fun setWishlistedLocally(gameId: String, wishlisted: Boolean) {
        val previous = database.catalogDao().getWishlist(gameId)
        database.catalogDao().upsertWishlist(listOf(LocalWishlistItem(gameId, wishlisted, previous?.lastServerSequence ?: 0)))
    }

    suspend fun enqueueOwnershipMutation(collectionId: String, gameId: String, owned: Boolean): String = database.withTransaction {
        setOwnedLocally(collectionId, gameId, owned)
        val id = UUID.randomUUID().toString()
        database.syncDao().upsertMutation(PendingMutation(
            id = id,
            scopeType = "collection",
            scopeId = collectionId,
            operation = if (owned) "addCollectionGame" else "removeCollectionGame",
            payloadJson = "{\"gameId\":\"$gameId\",\"collectionId\":\"$collectionId\"}",
            createdAtUtc = Instant.now().toString(),
            attemptCount = 0,
        ))
        id
    }

    suspend fun enqueueWishlistMutation(gameId: String, wishlisted: Boolean): String = database.withTransaction {
        setWishlistedLocally(gameId, wishlisted)
        val id = UUID.randomUUID().toString()
        database.syncDao().upsertMutation(PendingMutation(
            id = id,
            scopeType = "user",
            scopeId = null,
            operation = if (wishlisted) "addWishlistGame" else "removeWishlistGame",
            payloadJson = "{\"gameId\":\"$gameId\"}",
            createdAtUtc = Instant.now().toString(),
            attemptCount = 0,
        ))
        id
    }

    suspend fun deleteCollectionLocally(id: String) = database.collectionDao().deleteCollection(id)

    suspend fun removeInvitationLocally(id: String) = database.invitationDao().delete(id)

    private suspend fun cacheGame(value: GameDetails) = database.withTransaction {
        database.catalogDao().upsertGames(listOf(value.toLocal()))
        database.catalogDao().clearBarcodes(value.id)
        database.catalogDao().clearLanguages(value.id)
        database.catalogDao().clearTags(value.id)
        database.catalogDao().upsertBarcodes(value.barcodes.map { LocalGameBarcode(value.id, it) })
        database.catalogDao().upsertLanguages(value.languages.map { LocalGameLanguage(value.id, it.id, it.name, it.code) })
        database.catalogDao().upsertTags(value.tags.map { LocalGameTag(value.id, it.id, it.name) })
    }
}

private fun UserProfile.toLocal() = LocalProfile(id, displayName, username, hasActiveDevice, defaultCollectionId)
private fun LocalProfile.toModel() = UserProfile(id, displayName, username, hasActiveDevice, defaultCollectionId)
private fun CollectionSummary.toLocal() = LocalCollection(id, name, ownerUserId, myRole.apiValue)
private fun LocalCollection.toModel() = CollectionSummary(id, name, ownerUserId, CollectionRole.fromApi(myRole))
private fun CollectionMember.toLocal(collectionId: String) = LocalCollectionMember(collectionId, userId, displayName, username, role.apiValue)
private fun LocalCollectionMember.toModel() = CollectionMember(userId, displayName, username, CollectionRole.fromApi(role))
private fun CollectionInvitation.toLocal() = LocalInvitation(id, collectionId, collectionName, inviterUserId, inviteeUserId, role.apiValue, status)
private fun LocalInvitation.toModel() = CollectionInvitation(id, collectionId, collectionName, inviterUserId, inviteeUserId, CollectionRole.fromApi(role), status)

private fun NotificationItem.toLocal(): LocalNotification {
    val payload = runCatching { JSONObject(payloadJson) }.getOrElse { JSONObject() }
    val text = when (type) {
        "CollectionInvitation" -> "Collection invitation" to "You were invited to ${payload.optString("collectionName", "a shared collection")}."
        "InvitationAccepted" -> "Invitation accepted" to "A user joined your shared collection."
        "InvitationDeclined" -> "Invitation declined" to "A user declined your collection invitation."
        "CollectionMembershipChanged" -> "Collection access updated" to "Your role in a shared collection changed."
        "CollectionMembershipRemoved" -> "Collection access removed" to "You no longer have access to a shared collection."
        "GameSubmissionApproved" -> "Submission approved" to "Your game submission is now in the catalog."
        "GameSubmissionNeedsChanges" -> "Submission needs changes" to "Review the moderator feedback and update your submission."
        "GameSubmissionRejected" -> "Submission rejected" to "Your game submission was not approved."
        "SuggestedEditApproved" -> "Correction approved" to "Your suggested catalog correction was approved."
        "SuggestedEditRejected" -> "Correction rejected" to "Your suggested catalog correction was not approved."
        "DeviceRegistrationReplaced" -> "Device registration changed" to "Push delivery moved to a newer registration for this installation."
        "DeviceRegistrationRevoked" -> "Device registration revoked" to "This installation can no longer make active-device requests."
        else -> "Game Collector update" to "Open the app to see what changed."
    }
    return LocalNotification(id, type, text.first, text.second, payloadJson, createdAtUtc, readAtUtc)
}

private fun GameChangeRequest.toLocal(): LocalGameChangeRequest {
    val patch = JSONObject().apply {
        proposedChanges.title?.let { put("title", it) }
        proposedChanges.description?.let { put("description", it) }
        proposedChanges.publisher?.let { put("publisher", it) }
        proposedChanges.releaseYear?.let { put("releaseYear", it) }
        proposedChanges.minimumPlayers?.let { put("minimumPlayers", it) }
        proposedChanges.maximumPlayers?.let { put("maximumPlayers", it) }
        proposedChanges.minimumAge?.let { put("minimumAge", it) }
        proposedChanges.minimumPlayingTimeMinutes?.let { put("minimumPlayingTimeMinutes", it) }
        proposedChanges.maximumPlayingTimeMinutes?.let { put("maximumPlayingTimeMinutes", it) }
    }
    return LocalGameChangeRequest(id, gameId, gameTitle, patch.toString(), status, adminComment, createdAtUtc, updatedAtUtc)
}

private fun GameSummary.toLocal(previous: LocalGame?) = LocalGame(
    id = id,
    title = title,
    description = previous?.description,
    publisher = publisher,
    releaseYear = releaseYear,
    minimumPlayers = previous?.minimumPlayers,
    maximumPlayers = previous?.maximumPlayers,
    minimumAge = previous?.minimumAge,
    minimumPlayingTimeMinutes = previous?.minimumPlayingTimeMinutes,
    maximumPlayingTimeMinutes = previous?.maximumPlayingTimeMinutes,
    moderationStatus = moderationStatus,
    revision = previous?.revision ?: 0,
    isComplete = previous?.isComplete ?: false,
)

private fun GameDetails.toLocal() = LocalGame(
    id, title, description, publisher, releaseYear, minimumPlayers, maximumPlayers, minimumAge,
    minimumPlayingTimeMinutes, maximumPlayingTimeMinutes, moderationStatus, revision, true,
)

private fun LocalGame.toSummary() = GameSummary(id, title, publisher, releaseYear, moderationStatus)

private fun LocalGameDetails.toModel() = GameDetails(
    id = game.id,
    title = game.title,
    description = game.description,
    publisher = game.publisher,
    releaseYear = game.releaseYear,
    minimumPlayers = game.minimumPlayers,
    maximumPlayers = game.maximumPlayers,
    minimumAge = game.minimumAge,
    minimumPlayingTimeMinutes = game.minimumPlayingTimeMinutes,
    maximumPlayingTimeMinutes = game.maximumPlayingTimeMinutes,
    moderationStatus = game.moderationStatus,
    revision = game.revision,
    barcodes = barcodes.map { it.barcode },
    languages = languages.map { ReferenceData(it.referenceId, it.name, it.code) },
    tags = tags.map { ReferenceData(it.referenceId, it.name, null) },
)
