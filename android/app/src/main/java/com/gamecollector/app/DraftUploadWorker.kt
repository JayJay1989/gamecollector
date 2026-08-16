package com.gamecollector.app

import android.content.Context
import android.net.Uri
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.Data
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import com.gamecollector.core.auth.OidcSessionManager
import com.gamecollector.core.data.GameCollectorRepository
import com.gamecollector.core.data.toNetworkDraft
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.database.LocalGameDraft
import com.gamecollector.core.database.PendingMediaUpload
import com.gamecollector.core.network.ApiResult
import com.gamecollector.core.network.GameCollectorApi
import java.time.Duration
import java.time.Instant

class DraftUploadWorker(context: Context, params: WorkerParameters) : CoroutineWorker(context, params) {
    override suspend fun doWork(): Result {
        val draftId = inputData.getString(DRAFT_ID) ?: return Result.failure()
        val database = GameCollectorDatabase.get(applicationContext)
        val dao = database.draftDao()
        var draft = dao.getDraft(draftId) ?: return Result.success()
        if (!draft.submitRequested || draft.status == "Submitted") return Result.success()

        val session = OidcSessionManager(
            applicationContext,
            Uri.parse(BuildConfig.OIDC_ISSUER),
            BuildConfig.OIDC_CLIENT_ID,
            Uri.parse(BuildConfig.OIDC_REDIRECT_URI),
        )
        try {
            val api = GameCollectorApi(BuildConfig.API_BASE_URL, session)
            val deviceId = InstallationIdStore(applicationContext).id
            val submission = when {
                draft.serverGameId == null -> api.createSubmission(deviceId, draft.toNetworkDraft())
                draft.status in setOf("Local", "Queued", "Failed") -> api.updateSubmission(deviceId, draft.serverGameId!!, draft.toNetworkDraft())
                else -> null
            }
            when (submission) {
                is ApiResult.Success -> {
                    draft = draft.copy(
                        serverGameId = submission.value.game.id,
                        serverRevision = submission.value.game.revision,
                        status = "Uploading",
                        lastError = null,
                        updatedAtUtc = Instant.now().toString(),
                    )
                    dao.upsertDraft(draft)
                }
                is ApiResult.NetworkError -> return retryDraft(dao, draft, submission.message)
                is ApiResult.Error -> return failDraft(dao, draft, submission.message)
                ApiResult.SignedOut -> return retryDraft(dao, draft, "Sign in again to resume this submission.")
                null -> Unit
            }

            for (upload in dao.getUploads(draft.id)) {
                when (val outcome = processUpload(api, deviceId, draft.serverGameId!!, upload, dao)) {
                    UploadOutcome.Ready -> Unit
                    UploadOutcome.Retry -> return retryDraft(dao, draft, "Waiting for image processing or connectivity.")
                    is UploadOutcome.Failed -> return failDraft(dao, draft, outcome.message)
                }
            }

            val uploads = dao.getUploads(draft.id)
            if (uploads.none { it.kind == "Front" && it.state == "Ready" } || uploads.none { it.kind == "Back" && it.state == "Ready" }) {
                return retryDraft(dao, draft, "Front and back images are still required.")
            }
            return when (val submitted = api.submitGame(deviceId, draft.serverGameId!!)) {
                is ApiResult.Success -> {
                    completeDraft(dao, draft, submitted.value.game.revision, database, api, deviceId)
                }
                is ApiResult.NetworkError -> retryDraft(dao, draft, submitted.message)
                is ApiResult.Error -> when (val current = api.getSubmission(deviceId, draft.serverGameId!!)) {
                    is ApiResult.Success -> if (current.value.game.moderationStatus !in setOf("Draft", "NeedsChanges")) {
                        completeDraft(dao, draft, current.value.game.revision, database, api, deviceId)
                    } else failDraft(dao, draft, submitted.message)
                    is ApiResult.NetworkError -> retryDraft(dao, draft, current.message)
                    else -> failDraft(dao, draft, submitted.message)
                }
                ApiResult.SignedOut -> retryDraft(dao, draft, "Sign in again to resume this submission.")
            }
        } finally {
            session.close()
        }
    }

    private suspend fun processUpload(
        api: GameCollectorApi,
        deviceId: String,
        gameId: String,
        upload: PendingMediaUpload,
        dao: com.gamecollector.core.database.DraftDao,
    ): UploadOutcome {
        if (upload.state == "Ready") return UploadOutcome.Ready
        upload.serverMediaId?.let { mediaId ->
            when (val current = api.getMedia(deviceId, mediaId)) {
                is ApiResult.Success -> when (current.value.status) {
                    "Ready" -> {
                        dao.upsertUpload(upload.copy(state = "Ready", lastError = null))
                        return UploadOutcome.Ready
                    }
                    "Processing" -> return UploadOutcome.Retry
                    "PendingUpload" -> when (val completed = api.completeMedia(deviceId, mediaId)) {
                        is ApiResult.Success -> {
                            dao.upsertUpload(upload.copy(state = completed.value.status, lastError = null))
                            return if (completed.value.status == "Ready") UploadOutcome.Ready else UploadOutcome.Retry
                        }
                        is ApiResult.NetworkError -> return UploadOutcome.Retry
                        else -> Unit
                    }
                }
                is ApiResult.NetworkError -> return UploadOutcome.Retry
                else -> Unit
            }
        }

        val bytes = runCatching {
            applicationContext.contentResolver.openInputStream(Uri.parse(upload.localUri))?.use { it.readBytes() }
        }.getOrNull() ?: return updateUploadFailure(dao, upload, "The saved image can no longer be read.")
        if (bytes.isEmpty() || bytes.size > MAX_IMAGE_BYTES) return updateUploadFailure(dao, upload, "Images must be between 1 byte and 10 MiB.")
        val intent = api.createUploadIntent(deviceId, gameId, upload.kind, upload.contentType, bytes.size.toLong())
        if (intent !is ApiResult.Success) return if (intent is ApiResult.NetworkError || intent == ApiResult.SignedOut) UploadOutcome.Retry
        else updateUploadFailure(dao, upload, (intent as ApiResult.Error).message)
        val withIntent = upload.copy(serverMediaId = intent.value.mediaId, state = "Uploading", fileSizeBytes = bytes.size.toLong(), lastError = null)
        dao.upsertUpload(withIntent)
        when (val sent = api.uploadToPresignedUrl(intent.value.uploadUrl, upload.contentType, bytes)) {
            is ApiResult.Success -> Unit
            is ApiResult.NetworkError -> return UploadOutcome.Retry
            is ApiResult.Error -> return updateUploadFailure(dao, withIntent, sent.message)
            ApiResult.SignedOut -> return UploadOutcome.Retry
        }
        return when (val completed = api.completeMedia(deviceId, intent.value.mediaId)) {
            is ApiResult.Success -> {
                dao.upsertUpload(withIntent.copy(state = completed.value.status, lastError = null))
                if (completed.value.status == "Ready") UploadOutcome.Ready else UploadOutcome.Retry
            }
            is ApiResult.NetworkError -> UploadOutcome.Retry
            is ApiResult.Error -> updateUploadFailure(dao, withIntent, completed.message)
            ApiResult.SignedOut -> UploadOutcome.Retry
        }
    }

    private suspend fun updateUploadFailure(
        dao: com.gamecollector.core.database.DraftDao,
        upload: PendingMediaUpload,
        message: String,
    ): UploadOutcome.Failed {
        dao.upsertUpload(upload.copy(state = "Failed", attemptCount = upload.attemptCount + 1, lastError = message.take(300)))
        return UploadOutcome.Failed(message)
    }

    private suspend fun retryDraft(dao: com.gamecollector.core.database.DraftDao, draft: LocalGameDraft, message: String): Result {
        dao.upsertDraft(draft.copy(status = if (draft.serverGameId == null) "Queued" else "Uploading", lastError = message.take(300), updatedAtUtc = Instant.now().toString()))
        return Result.retry()
    }

    private suspend fun failDraft(dao: com.gamecollector.core.database.DraftDao, draft: LocalGameDraft, message: String): Result {
        dao.upsertDraft(draft.copy(status = "Failed", lastError = message.take(300), updatedAtUtc = Instant.now().toString()))
        return Result.failure(Data.Builder().putString("error", message.take(300)).build())
    }

    private suspend fun completeDraft(
        dao: com.gamecollector.core.database.DraftDao,
        draft: LocalGameDraft,
        revision: Long,
        database: GameCollectorDatabase,
        api: GameCollectorApi,
        deviceId: String,
    ): Result {
        dao.upsertDraft(draft.copy(status = "Submitted", lastError = null, serverRevision = revision, updatedAtUtc = Instant.now().toString()))
        GameCollectorRepository(database, api).refreshGame(deviceId, draft.serverGameId!!)
        DraftMediaFiles.deleteDraft(applicationContext, draft.id)
        return Result.success()
    }

    private sealed interface UploadOutcome {
        data object Ready : UploadOutcome
        data object Retry : UploadOutcome
        data class Failed(val message: String) : UploadOutcome
    }

    companion object {
        private const val DRAFT_ID = "draft_id"
        private const val MAX_IMAGE_BYTES = 10 * 1024 * 1024

        fun enqueue(context: Context, draftId: String) {
            val request = OneTimeWorkRequestBuilder<DraftUploadWorker>()
                .setInputData(Data.Builder().putString(DRAFT_ID, draftId).build())
                .setConstraints(Constraints.Builder().setRequiredNetworkType(NetworkType.CONNECTED).setRequiresBatteryNotLow(true).build())
                .setBackoffCriteria(BackoffPolicy.EXPONENTIAL, Duration.ofSeconds(10))
                .build()
            WorkManager.getInstance(context).enqueueUniqueWork("draft-upload-$draftId", ExistingWorkPolicy.REPLACE, request)
        }
    }
}
