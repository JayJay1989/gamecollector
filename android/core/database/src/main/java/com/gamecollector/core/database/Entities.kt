package com.gamecollector.core.database

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index

@Entity(tableName = "profiles")
data class LocalProfile(
    @androidx.room.PrimaryKey val id: String,
    val displayName: String,
    val username: String,
    val hasActiveDevice: Boolean,
    val defaultCollectionId: String?,
)

@Entity(tableName = "collections", indices = [Index("name")])
data class LocalCollection(
    @androidx.room.PrimaryKey val id: String,
    val name: String,
    val ownerUserId: String,
    val myRole: Int,
    val isPublic: Boolean = false,
)

@Entity(
    tableName = "collection_members",
    primaryKeys = ["collectionId", "userId"],
    foreignKeys = [ForeignKey(
        entity = LocalCollection::class,
        parentColumns = ["id"],
        childColumns = ["collectionId"],
        onDelete = ForeignKey.CASCADE,
    )],
    indices = [Index("collectionId")],
)
data class LocalCollectionMember(
    val collectionId: String,
    val userId: String,
    val displayName: String,
    val username: String,
    val role: Int,
)

@Entity(tableName = "games", indices = [Index("title"), Index("moderationStatus")])
data class LocalGame(
    @androidx.room.PrimaryKey val id: String,
    val title: String,
    val description: String?,
    val publisher: String?,
    val releaseYear: Int?,
    val minimumPlayers: Int?,
    val maximumPlayers: Int?,
    val minimumAge: Int?,
    val minimumPlayingTimeMinutes: Int?,
    val maximumPlayingTimeMinutes: Int?,
    val moderationStatus: String,
    val revision: Long,
    val isComplete: Boolean,
)

@Entity(
    tableName = "game_barcodes",
    primaryKeys = ["gameId", "barcode"],
    foreignKeys = [ForeignKey(entity = LocalGame::class, parentColumns = ["id"], childColumns = ["gameId"], onDelete = ForeignKey.CASCADE)],
    indices = [Index(value = ["barcode"], unique = true), Index("gameId")],
)
data class LocalGameBarcode(val gameId: String, val barcode: String)

@Entity(
    tableName = "game_languages",
    primaryKeys = ["gameId", "referenceId"],
    foreignKeys = [ForeignKey(entity = LocalGame::class, parentColumns = ["id"], childColumns = ["gameId"], onDelete = ForeignKey.CASCADE)],
    indices = [Index("gameId")],
)
data class LocalGameLanguage(val gameId: String, val referenceId: String, val name: String, val code: String?)

@Entity(
    tableName = "game_tags",
    primaryKeys = ["gameId", "referenceId"],
    foreignKeys = [ForeignKey(entity = LocalGame::class, parentColumns = ["id"], childColumns = ["gameId"], onDelete = ForeignKey.CASCADE)],
    indices = [Index("gameId")],
)
data class LocalGameTag(val gameId: String, val referenceId: String, val name: String)

@Entity(
    tableName = "game_images",
    foreignKeys = [ForeignKey(entity = LocalGame::class, parentColumns = ["id"], childColumns = ["gameId"], onDelete = ForeignKey.CASCADE)],
    indices = [Index("gameId")],
)
data class LocalGameImage(
    @androidx.room.PrimaryKey val id: String,
    val gameId: String,
    val kind: String,
    val thumbnailUrl: String?,
    val fullUrl: String?,
)

@Entity(
    tableName = "collection_games",
    primaryKeys = ["collectionId", "gameId"],
    foreignKeys = [
        ForeignKey(entity = LocalCollection::class, parentColumns = ["id"], childColumns = ["collectionId"], onDelete = ForeignKey.CASCADE),
        ForeignKey(entity = LocalGame::class, parentColumns = ["id"], childColumns = ["gameId"], onDelete = ForeignKey.CASCADE),
    ],
    indices = [Index("collectionId"), Index("gameId")],
)
data class LocalCollectionGame(
    val collectionId: String,
    val gameId: String,
    val isPresent: Boolean = true,
    val lastServerSequence: Long = 0,
)

@Entity(
    tableName = "wishlist_items",
    foreignKeys = [ForeignKey(entity = LocalGame::class, parentColumns = ["id"], childColumns = ["gameId"], onDelete = ForeignKey.CASCADE)],
    indices = [Index("gameId")],
)
data class LocalWishlistItem(
    @androidx.room.PrimaryKey val gameId: String,
    val isPresent: Boolean = true,
    val lastServerSequence: Long = 0,
)

@Entity(tableName = "invitations", indices = [Index("status")])
data class LocalInvitation(
    @androidx.room.PrimaryKey val id: String,
    val collectionId: String,
    val collectionName: String,
    val inviterUserId: String,
    val inviteeUserId: String,
    val role: Int,
    val status: String,
)

@Entity(tableName = "notifications", indices = [Index("createdAtUtc"), Index("readAtUtc")])
data class LocalNotification(
    @androidx.room.PrimaryKey val id: String,
    val type: String,
    val title: String,
    val body: String,
    val payloadJson: String,
    val createdAtUtc: String,
    val readAtUtc: String?,
)

@Entity(tableName = "game_change_requests", indices = [Index("gameId"), Index("status"), Index("updatedAtUtc")])
data class LocalGameChangeRequest(
    @androidx.room.PrimaryKey val id: String,
    val gameId: String,
    val gameTitle: String,
    val proposedChangesJson: String,
    val status: String,
    val adminComment: String?,
    val createdAtUtc: String,
    val updatedAtUtc: String,
)

@Entity(tableName = "pending_mutations", indices = [Index("createdAtUtc"), Index("scopeType", "scopeId")])
data class PendingMutation(
    @androidx.room.PrimaryKey val id: String,
    val scopeType: String,
    val scopeId: String?,
    val operation: String,
    val payloadJson: String,
    val createdAtUtc: String,
    val attemptCount: Int,
)

@Entity(tableName = "game_drafts", indices = [Index("status"), Index("updatedAtUtc")])
data class LocalGameDraft(
    @androidx.room.PrimaryKey val id: String,
    val serverGameId: String?,
    val barcode: String?,
    val title: String,
    val description: String?,
    val publisher: String?,
    val releaseYear: Int?,
    val minimumPlayers: Int?,
    val maximumPlayers: Int?,
    val minimumAge: Int?,
    val minimumPlayingTimeMinutes: Int?,
    val maximumPlayingTimeMinutes: Int?,
    val languageIdsJson: String,
    val tagIdsJson: String,
    val source: String?,
    val step: Int,
    val status: String,
    val lastError: String?,
    val serverRevision: Long?,
    val submitRequested: Boolean,
    val createdAtUtc: String,
    val updatedAtUtc: String,
)

@Entity(
    tableName = "pending_media_uploads",
    indices = [Index("draftGameId"), Index(value = ["draftGameId", "kind"], unique = true)],
)
data class PendingMediaUpload(
    @androidx.room.PrimaryKey val id: String,
    val draftGameId: String,
    val localUri: String,
    val kind: String,
    val state: String,
    val contentType: String,
    val fileSizeBytes: Long,
    val serverMediaId: String?,
    val attemptCount: Int,
    val lastError: String?,
)

@Entity(tableName = "sync_scope_states")
data class SyncScopeState(
    @androidx.room.PrimaryKey val key: String,
    val scopeType: String,
    val scopeId: String?,
    val cursor: Long,
    val lastSyncedAtUtc: String?,
)
