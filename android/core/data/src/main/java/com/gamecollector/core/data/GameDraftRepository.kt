package com.gamecollector.core.data

import androidx.room.withTransaction
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.database.LocalGameDraft
import com.gamecollector.core.database.PendingMediaUpload
import com.gamecollector.core.network.GameSubmissionDraft
import com.gamecollector.core.network.ProductMetadataCandidate
import kotlinx.coroutines.flow.Flow
import org.json.JSONArray
import java.time.Instant
import java.util.UUID

class GameDraftRepository(private val database: GameCollectorDatabase) {
    val drafts: Flow<List<LocalGameDraft>> = database.draftDao().observeDrafts()
    fun draft(id: String): Flow<LocalGameDraft?> = database.draftDao().observeDraft(id)
    fun uploads(id: String): Flow<List<PendingMediaUpload>> = database.draftDao().observeUploads(id)

    suspend fun create(barcode: String?, candidate: ProductMetadataCandidate? = null): LocalGameDraft {
        val now = Instant.now().toString()
        return LocalGameDraft(
            id = UUID.randomUUID().toString(),
            serverGameId = null,
            barcode = barcode,
            title = candidate?.title.orEmpty(),
            description = candidate?.description,
            publisher = candidate?.publisher,
            releaseYear = null,
            minimumPlayers = null,
            maximumPlayers = null,
            minimumAge = null,
            minimumPlayingTimeMinutes = null,
            maximumPlayingTimeMinutes = null,
            languageIdsJson = "[]",
            tagIdsJson = "[]",
            source = candidate?.source,
            step = 0,
            status = "Local",
            lastError = null,
            serverRevision = null,
            submitRequested = false,
            createdAtUtc = now,
            updatedAtUtc = now,
        ).also { database.draftDao().upsertDraft(it) }
    }

    suspend fun save(item: LocalGameDraft) = database.draftDao().upsertDraft(item.copy(updatedAtUtc = Instant.now().toString()))

    suspend fun attachMedia(draftId: String, localUri: String, kind: String, contentType: String, fileSizeBytes: Long) {
        database.withTransaction {
            database.draftDao().deleteUpload(draftId, kind)
            database.draftDao().upsertUpload(
                PendingMediaUpload(
                    id = UUID.randomUUID().toString(),
                    draftGameId = draftId,
                    localUri = localUri,
                    kind = kind,
                    state = "Pending",
                    contentType = contentType,
                    fileSizeBytes = fileSizeBytes,
                    serverMediaId = null,
                    attemptCount = 0,
                    lastError = null,
                ),
            )
            database.draftDao().getDraft(draftId)?.let {
                database.draftDao().upsertDraft(it.copy(status = "Local", lastError = null, updatedAtUtc = Instant.now().toString()))
            }
        }
    }

    suspend fun requestSubmission(id: String) {
        database.draftDao().getDraft(id)?.let {
            database.draftDao().upsertDraft(it.copy(submitRequested = true, status = "Queued", lastError = null, updatedAtUtc = Instant.now().toString()))
        }
    }

    suspend fun delete(id: String) = database.withTransaction {
        database.draftDao().deleteUploads(id)
        database.draftDao().deleteDraft(id)
    }
}

fun LocalGameDraft.toNetworkDraft() = GameSubmissionDraft(
    title = title,
    description = description,
    publisher = publisher,
    releaseYear = releaseYear,
    minimumPlayers = minimumPlayers,
    maximumPlayers = maximumPlayers,
    minimumAge = minimumAge,
    minimumPlayingTimeMinutes = minimumPlayingTimeMinutes,
    maximumPlayingTimeMinutes = maximumPlayingTimeMinutes,
    barcodes = listOfNotNull(barcode),
    languageIds = languageIdsJson.stringList(),
    tagIds = tagIdsJson.stringList(),
    expectedRevision = serverRevision,
)

fun List<String>.toJsonArray(): String = JSONArray(this).toString()
fun String.stringList(): List<String> = runCatching {
    val array = JSONArray(this)
    List(array.length()) { array.getString(it) }
}.getOrDefault(emptyList())
