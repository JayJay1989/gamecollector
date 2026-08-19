package com.gamecollector.core.sync

import androidx.room.withTransaction
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.database.LocalCollection
import com.gamecollector.core.database.LocalCollectionGame
import com.gamecollector.core.database.LocalCollectionMember
import com.gamecollector.core.database.LocalGame
import com.gamecollector.core.database.LocalGameBarcode
import com.gamecollector.core.database.LocalGameLanguage
import com.gamecollector.core.database.LocalGameTag
import com.gamecollector.core.database.LocalInvitation
import com.gamecollector.core.database.LocalNotification
import com.gamecollector.core.database.LocalProfile
import com.gamecollector.core.database.LocalWishlistItem
import com.gamecollector.core.database.PendingMutation
import com.gamecollector.core.database.SyncScopeState
import com.gamecollector.core.network.ApiResult
import com.gamecollector.core.network.GameCollectorApi
import com.gamecollector.core.network.SyncBootstrap
import com.gamecollector.core.network.SyncChange
import com.gamecollector.core.network.SyncMutation
import com.gamecollector.core.network.SyncScope
import com.gamecollector.core.network.SyncScopePage
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant

class SyncEngine(
    private val database: GameCollectorDatabase,
    private val remote: SyncRemoteDataSource,
    private val deviceId: String,
) {
    suspend fun run(): SyncRunResult {
        when (val push = pushPending()) {
            SyncRunResult.Success -> Unit
            else -> return push
        }
        val scopes = database.syncDao().getScopes()
        if (scopes.isEmpty()) return bootstrap()
        return pullUntilComplete()
    }

    private suspend fun pushPending(): SyncRunResult {
        val pending = database.syncDao().pendingMutations(100)
        if (pending.isEmpty()) return SyncRunResult.Success
        val mutations = pending.map { it.toNetwork() }
        return when (val response = remote.push(deviceId, mutations)) {
            is ApiResult.Success -> {
                database.withTransaction {
                    val byId = pending.associateBy(PendingMutation::id)
                    response.value.forEach { result ->
                        val mutation = byId[result.mutationId]
                        val sequence = result.serverSequence
                        if (mutation != null && sequence != null && (result.applied || result.duplicate)) {
                            applyAcceptedMutation(mutation, sequence)
                        }
                    }
                    val completed = response.value
                        .filter { it.applied || it.duplicate || it.errorCode != null }
                        .map { it.mutationId }
                    if (completed.isNotEmpty()) database.syncDao().deleteMutations(completed)
                }
                if (database.syncDao().pendingMutations(1).isNotEmpty()) pushPending() else SyncRunResult.Success
            }
            is ApiResult.NetworkError -> {
                database.syncDao().incrementAttempts(pending.map(PendingMutation::id))
                SyncRunResult.Retry
            }
            is ApiResult.Error -> if (response.statusCode == 401 || response.statusCode == 403) SyncRunResult.SignedOut
                else if (response.statusCode >= 500 || response.statusCode == 429) SyncRunResult.Retry else SyncRunResult.Failure
            ApiResult.SignedOut -> SyncRunResult.SignedOut
        }
    }

    private suspend fun pullUntilComplete(): SyncRunResult {
        repeat(MAX_PULL_PAGES) {
            val scopes = database.syncDao().getScopes().map { SyncScope(it.scopeType, it.scopeId, it.cursor) }
            if (scopes.isEmpty()) return bootstrap()
            when (val response = remote.pull(deviceId, scopes)) {
                is ApiResult.Success -> {
                    response.value.forEach { applyPage(it) }
                    if (response.value.none(SyncScopePage::hasMore)) return SyncRunResult.Success
                }
                is ApiResult.Error -> {
                    if (response.code == "sync_reset_required") return bootstrap()
                    return if (response.statusCode == 401 || response.statusCode == 403) SyncRunResult.SignedOut
                    else if (response.statusCode >= 500 || response.statusCode == 429) SyncRunResult.Retry else SyncRunResult.Failure
                }
                is ApiResult.NetworkError -> return SyncRunResult.Retry
                ApiResult.SignedOut -> return SyncRunResult.SignedOut
            }
        }
        return SyncRunResult.Retry
    }

    private suspend fun bootstrap(): SyncRunResult = when (val response = remote.bootstrap(deviceId)) {
        is ApiResult.Success -> {
            applyBootstrap(response.value)
            SyncRunResult.Success
        }
        is ApiResult.NetworkError -> SyncRunResult.Retry
        is ApiResult.Error -> if (response.statusCode == 401 || response.statusCode == 403) SyncRunResult.SignedOut
            else if (response.statusCode >= 500 || response.statusCode == 429) SyncRunResult.Retry else SyncRunResult.Failure
        ApiResult.SignedOut -> SyncRunResult.SignedOut
    }

    private suspend fun applyBootstrap(bootstrap: SyncBootstrap) {
        database.withTransaction {
            database.syncDao().clearScopes()
            bootstrap.snapshot.sortedBy { scopeOrder(it.scopeType) }.forEach { change ->
                applyChange(change)
                database.syncDao().upsertScope(scopeState(change.scopeType, change.scopeId, bootstrap.cursor))
            }
        }
    }

    private suspend fun applyPage(page: SyncScopePage) {
        database.withTransaction {
            page.changes.forEach { applyChange(it) }
            database.syncDao().upsertScope(scopeState(page.type, page.id, page.nextCursor))
        }
    }

    private suspend fun applyChange(change: SyncChange) {
        if (change.operation == "snapshot") {
            applySnapshot(change)
            return
        }
        val payload = JSONObject(change.payloadJson)
        when (change.operation) {
            "collectionGameChanged" -> applyCollectionGame(change.scopeId ?: return, payload.string("gameId") ?: change.entityId, payload.optBoolean("isPresent"), change.sequence)
            "wishlistGameChanged" -> applyWishlist(payload.string("gameId") ?: change.entityId, payload.optBoolean("isPresent"), change.sequence)
            "profileChanged" -> applyProfile(payload)
            "collectionChanged" -> applyCollection(payload, change.scopeId)
            "membershipChanged" -> applyMembership(change.scopeId ?: return, payload)
            "invitationChanged" -> applyInvitation(payload)
            "notificationChanged" -> applyNotification(payload)
            "changeRequestChanged" -> applyChangeRequest(payload)
            "gameChanged" -> applyGame(payload)
            "gameDeleted" -> database.catalogDao().deleteGame(payload.string("id") ?: change.entityId)
        }
    }

    private suspend fun applySnapshot(change: SyncChange) {
        val payload = JSONObject(change.payloadJson)
        when (change.scopeType) {
            "catalog" -> applyCatalogSnapshot(payload)
            "user" -> applyUserSnapshot(payload)
            "collection" -> applyCollectionSnapshot(change.scopeId ?: return, payload)
        }
    }

    private suspend fun applyCatalogSnapshot(payload: JSONObject) {
        val languageObjects = payload.array("languages").objects()
        val tagObjects = payload.array("tags").objects()
        val languages = languageObjects.associateBy { it.getString("id") }
        val tags = tagObjects.associateBy { it.getString("id") }
        database.catalogDao().clearGames()
        payload.array("games").objects().forEach { game ->
            val id = game.getString("id")
            database.catalogDao().upsertGames(listOf(game.toLocalGame(complete = true)))
            database.catalogDao().upsertBarcodes(game.array("barcodes").strings().map { LocalGameBarcode(id, it) })
            database.catalogDao().upsertLanguages(game.array("languageIds").strings().mapNotNull { referenceId ->
                languages[referenceId]?.let { LocalGameLanguage(id, referenceId, it.getString("name"), it.nullableString("code")) }
            })
            database.catalogDao().upsertTags(game.array("tagIds").strings().mapNotNull { referenceId ->
                tags[referenceId]?.let { LocalGameTag(id, referenceId, it.getString("name")) }
            })
        }
    }

    private suspend fun applyUserSnapshot(payload: JSONObject) {
        val profileJson = payload.getJSONObject("profile")
        val oldProfile = database.profileDao().get()
        val profile = LocalProfile(
            id = profileJson.getString("id"),
            displayName = profileJson.getString("displayName"),
            username = profileJson.getString("username"),
            hasActiveDevice = oldProfile?.hasActiveDevice ?: true,
            defaultCollectionId = profileJson.nullableString("defaultCollectionId"),
        )
        database.profileDao().upsert(profile)
        val existing = database.collectionDao().getCollections().associateBy(LocalCollection::id)
        database.collectionDao().clearCollections()
        val collections = payload.array("collections").objects().map { item ->
            LocalCollection(
                id = item.getString("id"),
                name = item.getString("name"),
                ownerUserId = item.getString("ownerUserId"),
                myRole = if (item.getString("ownerUserId") == profile.id) 2 else existing[item.getString("id")]?.myRole ?: 0,
            )
        }
        database.collectionDao().upsertCollections(collections)
        database.catalogDao().clearWishlist()
        database.catalogDao().upsertWishlist(payload.array("wishlist").objects().map {
            LocalWishlistItem(it.getString("gameId"), it.optBoolean("isPresent"), it.optLong("lastServerSequence"))
        })
        database.invitationDao().clear()
        val collectionNames = collections.associate { it.id to it.name }
        database.invitationDao().upsert(payload.array("invitations").objects().map { invitation ->
            LocalInvitation(
                id = invitation.getString("id"),
                collectionId = invitation.getString("collectionId"),
                collectionName = collectionNames[invitation.getString("collectionId")] ?: "Shared collection",
                inviterUserId = invitation.getString("inviterUserId"),
                inviteeUserId = profile.id,
                role = roleValue(invitation.get("role")),
                status = invitation.getString("status"),
            )
        })
        database.notificationDao().clear()
        database.notificationDao().upsert(payload.array("notifications").objects().map(::notification))
    }

    private suspend fun applyCollectionSnapshot(collectionId: String, payload: JSONObject) {
        val collectionJson = payload.getJSONObject("collection")
        val profileId = database.profileDao().get()?.id
        val members = payload.array("members").objects()
        val role = if (collectionJson.getString("ownerUserId") == profileId) 2
            else members.firstOrNull { it.getString("userId") == profileId }?.let { roleValue(it.get("role")) } ?: 0
        database.collectionDao().upsertCollections(listOf(
            LocalCollection(collectionId, collectionJson.getString("name"), collectionJson.getString("ownerUserId"), role),
        ))
        val previousMembers = database.collectionDao().getMembers(collectionId).associateBy(LocalCollectionMember::userId)
        database.collectionDao().clearMembers(collectionId)
        database.collectionDao().upsertMembers(members.map { member ->
            val old = previousMembers[member.getString("userId")]
            LocalCollectionMember(collectionId, member.getString("userId"), old?.displayName.orEmpty(), old?.username.orEmpty(), roleValue(member.get("role")))
        })
        database.catalogDao().clearCollectionGames(collectionId)
        database.catalogDao().upsertCollectionGames(payload.array("games").objects().map {
            LocalCollectionGame(collectionId, it.getString("gameId"), it.optBoolean("isOwned"), it.optLong("lastServerSequence"))
        })
    }

    private suspend fun applyAcceptedMutation(mutation: PendingMutation, sequence: Long) {
        val payload = JSONObject(mutation.payloadJson)
        val gameId = payload.getString("gameId")
        when (mutation.operation) {
            "addCollectionGame", "removeCollectionGame" -> applyCollectionGame(
                payload.getString("collectionId"), gameId, mutation.operation == "addCollectionGame", sequence,
            )
            "addWishlistGame", "removeWishlistGame" -> applyWishlist(gameId, mutation.operation == "addWishlistGame", sequence)
        }
    }

    private suspend fun applyCollectionGame(collectionId: String, gameId: String, present: Boolean, sequence: Long) {
        val previous = database.catalogDao().getCollectionGame(collectionId, gameId)
        if (previous == null || sequence >= previous.lastServerSequence) {
            database.catalogDao().upsertCollectionGames(listOf(LocalCollectionGame(collectionId, gameId, present, sequence)))
            if (present) database.catalogDao().removeWishlist(gameId)
        }
    }

    private suspend fun applyWishlist(gameId: String, present: Boolean, sequence: Long) {
        val previous = database.catalogDao().getWishlist(gameId)
        if (previous == null || sequence >= previous.lastServerSequence) {
            database.catalogDao().upsertWishlist(listOf(LocalWishlistItem(gameId, present, sequence)))
        }
    }

    private suspend fun applyProfile(payload: JSONObject) {
        val old = database.profileDao().get() ?: return
        database.profileDao().upsert(old.copy(
            displayName = payload.nullableString("displayName") ?: old.displayName,
            username = payload.nullableString("username") ?: old.username,
            defaultCollectionId = if (payload.has("defaultCollectionId")) payload.nullableString("defaultCollectionId") else old.defaultCollectionId,
        ))
    }

    private suspend fun applyCollection(payload: JSONObject, scopeId: String?) {
        val id = payload.string("id") ?: scopeId ?: return
        if (payload.optBoolean("isDeleted")) {
            database.collectionDao().deleteCollection(id)
            return
        }
        val old = database.collectionDao().getCollections().firstOrNull { it.id == id }
        val owner = payload.string("ownerUserId") ?: old?.ownerUserId ?: return
        val profileId = database.profileDao().get()?.id
        database.collectionDao().upsertCollections(listOf(LocalCollection(
            id, payload.string("name") ?: old?.name ?: "Collection", owner,
            if (owner == profileId) 2 else old?.myRole ?: 0,
        )))
    }

    private suspend fun applyMembership(collectionId: String, payload: JSONObject) {
        val userId = payload.string("userId") ?: return
        if (payload.optBoolean("isDeleted")) {
            val remaining = database.collectionDao().getMembers(collectionId).filterNot { it.userId == userId }
            database.collectionDao().clearMembers(collectionId)
            database.collectionDao().upsertMembers(remaining)
            return
        }
        val old = database.collectionDao().getMembers(collectionId).firstOrNull { it.userId == userId }
        val role = roleValue(payload.get("role"))
        database.collectionDao().upsertMembers(listOf(
            LocalCollectionMember(collectionId, userId, old?.displayName.orEmpty(), old?.username.orEmpty(), role),
        ))
        if (database.profileDao().get()?.id == userId) {
            val collection = database.collectionDao().getCollections().firstOrNull { it.id == collectionId }
            if (collection != null) database.collectionDao().upsertCollections(listOf(collection.copy(myRole = role)))
        }
    }

    private suspend fun applyInvitation(payload: JSONObject) {
        val id = payload.string("id") ?: return
        val status = payload.string("status") ?: "Pending"
        if (!status.equals("Pending", true)) {
            database.invitationDao().delete(id)
            return
        }
        val collectionId = payload.string("collectionId") ?: return
        val oldName = database.collectionDao().getCollections().firstOrNull { it.id == collectionId }?.name ?: "Shared collection"
        database.invitationDao().upsert(listOf(LocalInvitation(
            id, collectionId, oldName, payload.string("inviterUserId").orEmpty(), database.profileDao().get()?.id.orEmpty(),
            payload.opt("role")?.let(::roleValue) ?: 0, status,
        )))
    }

    private suspend fun applyNotification(payload: JSONObject) = database.notificationDao().upsert(listOf(notification(payload)))

    private suspend fun applyChangeRequest(payload: JSONObject) {
        val id = payload.string("id") ?: return
        val previous = database.changeRequestDao().get(id) ?: return
        database.changeRequestDao().upsert(listOf(previous.copy(
            status = payload.string("status") ?: previous.status,
            updatedAtUtc = Instant.now().toString(),
        )))
    }

    private suspend fun applyGame(payload: JSONObject) {
        if (!payload.has("title")) return
        database.catalogDao().upsertGames(listOf(payload.toLocalGame(complete = false)))
    }

    private fun notification(json: JSONObject): LocalNotification {
        val payload = json.optJSONObject("payload") ?: JSONObject()
        val copy = payload.toString()
        val text = notificationText(json.getString("type"), payload)
        return LocalNotification(
            id = json.getString("id"),
            type = json.getString("type"),
            title = text.first,
            body = text.second,
            payloadJson = copy,
            createdAtUtc = json.optString("createdAtUtc", Instant.now().toString()),
            readAtUtc = json.nullableString("readAtUtc"),
        )
    }

    private fun notificationText(type: String, payload: JSONObject): Pair<String, String> = when (type) {
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

    private fun scopeState(type: String, id: String?, cursor: Long) = SyncScopeState(
        key = "$type:${id.orEmpty()}", scopeType = type, scopeId = id, cursor = cursor, lastSyncedAtUtc = Instant.now().toString(),
    )

    private fun PendingMutation.toNetwork(): SyncMutation {
        val payload = JSONObject(payloadJson)
        return SyncMutation(id, operation, payload.getString("gameId"), payload.nullableString("collectionId"))
    }

    private fun JSONObject.toLocalGame(complete: Boolean): LocalGame = LocalGame(
        id = getString("id"),
        title = optString("title", "Unknown game"),
        description = nullableString("description"),
        publisher = nullableString("publisher"),
        releaseYear = nullableInt("releaseYear"),
        minimumPlayers = nullableInt("minimumPlayers"),
        maximumPlayers = nullableInt("maximumPlayers"),
        minimumAge = nullableInt("minimumAge"),
        minimumPlayingTimeMinutes = nullableInt("minimumPlayingTimeMinutes"),
        maximumPlayingTimeMinutes = nullableInt("maximumPlayingTimeMinutes"),
        moderationStatus = optString("moderationStatus", "Approved"),
        revision = optLong("revision", 0),
        isComplete = complete,
    )

    private fun JSONObject.array(name: String): JSONArray = optJSONArray(name) ?: JSONArray()
    private fun JSONObject.string(name: String): String? = nullableString(name)
    private fun JSONObject.nullableString(name: String): String? = if (!has(name) || isNull(name)) null else optString(name).takeIf(String::isNotBlank)
    private fun JSONObject.nullableInt(name: String): Int? = if (!has(name) || isNull(name)) null else getInt(name)
    private fun JSONArray.objects(): List<JSONObject> = List(length()) { getJSONObject(it) }
    private fun JSONArray.strings(): List<String> = List(length()) { getString(it) }
    private fun roleValue(value: Any): Int = when (value) {
        is Number -> value.toInt()
        else -> when (value.toString().lowercase()) { "owner" -> 2; "editor" -> 1; else -> 0 }
    }
    private fun scopeOrder(type: String) = when (type) { "catalog" -> 0; "user" -> 1; else -> 2 }

    private companion object { const val MAX_PULL_PAGES = 100 }
}

sealed interface SyncRunResult {
    data object Success : SyncRunResult
    data object Retry : SyncRunResult
    data object SignedOut : SyncRunResult
    data object Failure : SyncRunResult
}

interface SyncRemoteDataSource {
    suspend fun push(deviceId: String, mutations: List<SyncMutation>): ApiResult<List<com.gamecollector.core.network.SyncMutationResult>>
    suspend fun pull(deviceId: String, scopes: List<SyncScope>): ApiResult<List<SyncScopePage>>
    suspend fun bootstrap(deviceId: String): ApiResult<SyncBootstrap>
}

class GameCollectorSyncRemote(private val api: GameCollectorApi) : SyncRemoteDataSource {
    override suspend fun push(deviceId: String, mutations: List<SyncMutation>) = api.pushMutations(deviceId, mutations)
    override suspend fun pull(deviceId: String, scopes: List<SyncScope>) = api.pullSync(deviceId, scopes)
    override suspend fun bootstrap(deviceId: String) = api.bootstrapSync(deviceId)
}
