package com.gamecollector.core.network

import com.gamecollector.core.auth.AccessTokenProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrl
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.io.IOException
import java.util.concurrent.TimeUnit
import java.util.UUID

class GameCollectorApi(
    baseUrl: String,
    private val tokens: AccessTokenProvider,
    private val client: OkHttpClient = defaultClient(),
) {
    private val baseUrl = baseUrl.ensureTrailingSlash().toHttpUrl()

    suspend fun getProfile() = request("api/v1/me", parser = ::profile)

    suspend fun onboard(displayName: String, username: String) = request(
        "api/v1/me/onboarding",
        method = "POST",
        body = jsonOf("displayName" to displayName.trim(), "username" to username.trim()),
        expected = setOf(201),
        parser = ::profile,
    )

    suspend fun activateDevice(deviceId: String, fcmToken: String) = request(
        "api/v1/me/device/activate",
        method = "POST",
        body = jsonOf("deviceId" to deviceId, "fcmToken" to fcmToken),
        parser = { Unit },
    )

    suspend fun revokeDevice(deviceId: String) = request(
        "api/v1/me/device",
        method = "DELETE",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun updateProfile(deviceId: String, displayName: String, username: String) = request(
        "api/v1/me",
        method = "PATCH",
        deviceId = deviceId,
        body = jsonOf("displayName" to displayName.trim(), "username" to username.trim()),
        parser = ::profile,
    )

    suspend fun listCollections(deviceId: String) = request(
        "api/v1/collections",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::collection) },
    )

    suspend fun createCollection(deviceId: String, name: String) = request(
        "api/v1/collections",
        method = "POST",
        deviceId = deviceId,
        body = jsonOf("name" to name.trim()),
        expected = setOf(201),
        parser = ::collection,
    )

    suspend fun renameCollection(deviceId: String, collectionId: String, name: String) = request(
        "api/v1/collections/$collectionId",
        method = "PATCH",
        deviceId = deviceId,
        body = jsonOf("name" to name.trim()),
        parser = ::collection,
    )

    suspend fun deleteCollection(deviceId: String, collectionId: String) = request(
        "api/v1/collections/$collectionId",
        method = "DELETE",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun setDefaultCollection(deviceId: String, collectionId: String) = request(
        "api/v1/me/default-collection",
        method = "PUT",
        deviceId = deviceId,
        body = jsonOf("collectionId" to collectionId),
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun listMembers(deviceId: String, collectionId: String) = request(
        "api/v1/collections/$collectionId/members",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::member) },
    )

    suspend fun searchUsers(deviceId: String, query: String) = request(
        path = baseUrl.resolve("api/v1/users/search")!!.newBuilder()
            .addQueryParameter("type", "username")
            .addQueryParameter("q", query.trim().removePrefix("#"))
            .build(),
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::userSearchResult) },
    )

    suspend fun invite(
        deviceId: String,
        collectionId: String,
        userId: String,
        role: CollectionRole,
    ) = request(
        "api/v1/collections/$collectionId/invitations",
        method = "POST",
        deviceId = deviceId,
        body = jsonOf("inviteeUserId" to userId, "role" to role.apiValue),
        expected = setOf(201),
        parser = ::invitation,
    )

    suspend fun updateMember(
        deviceId: String,
        collectionId: String,
        userId: String,
        role: CollectionRole,
    ) = request(
        "api/v1/collections/$collectionId/members/$userId",
        method = "PATCH",
        deviceId = deviceId,
        body = jsonOf("role" to role.apiValue),
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun removeMember(deviceId: String, collectionId: String, userId: String) = request(
        "api/v1/collections/$collectionId/members/$userId",
        method = "DELETE",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun transferOwnership(
        deviceId: String,
        collectionId: String,
        newOwnerUserId: String,
    ) = request(
        "api/v1/collections/$collectionId/transfer-ownership",
        method = "POST",
        deviceId = deviceId,
        body = jsonOf("newOwnerUserId" to newOwnerUserId, "previousOwnerLeaves" to false),
        parser = ::collection,
    )

    suspend fun listInvitations(deviceId: String) = request(
        "api/v1/me/invitations",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::invitation) },
    )

    suspend fun respondToInvitation(deviceId: String, invitationId: String, accept: Boolean) = request(
        "api/v1/invitations/$invitationId/${if (accept) "accept" else "decline"}",
        method = "POST",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun listNotifications(deviceId: String) = request(
        "api/v1/me/notifications",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::notification) },
    )

    suspend fun markNotificationRead(deviceId: String, notificationId: String) = request(
        "api/v1/me/notifications/$notificationId/read",
        method = "POST",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun markAllNotificationsRead(deviceId: String) = request(
        "api/v1/me/notifications/read-all",
        method = "POST",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun searchGames(deviceId: String, query: String) = request(
        path = baseUrl.resolve("api/v1/games/search")!!.newBuilder()
            .apply { if (query.isNotBlank()) addQueryParameter("q", query.trim()) }
            .build(),
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::gameSummary) },
    )

    suspend fun getGame(deviceId: String, gameId: String) = request(
        "api/v1/games/$gameId",
        deviceId = deviceId,
        parser = ::game,
    )

    suspend fun getGameByBarcode(deviceId: String, barcode: String) = request(
        "api/v1/games/barcode/${barcode.filter(Char::isDigit)}",
        deviceId = deviceId,
        parser = ::game,
    )

    suspend fun createChangeRequest(deviceId: String, gameId: String, changes: GameChangePatch) = request(
        "api/v1/games/$gameId/change-requests",
        method = "POST",
        deviceId = deviceId,
        body = JSONObject().put("proposedChanges", changePatchBody(changes)),
        expected = setOf(201),
        parser = ::changeRequest,
    )

    suspend fun listMyChangeRequests(deviceId: String) = request(
        "api/v1/change-requests/mine",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::changeRequest) },
    )

    suspend fun lookupProduct(deviceId: String, barcode: String) = request(
        "api/v1/product-lookup/${barcode.filter(Char::isDigit)}",
        deviceId = deviceId,
        parser = ::productCandidate,
    )

    suspend fun createSubmission(deviceId: String, draft: GameSubmissionDraft) = request(
        "api/v1/game-submissions",
        method = "POST",
        deviceId = deviceId,
        body = submissionBody(draft),
        expected = setOf(201),
        parser = ::submission,
    )

    suspend fun updateSubmission(deviceId: String, gameId: String, draft: GameSubmissionDraft) = request(
        "api/v1/game-submissions/$gameId",
        method = "PUT",
        deviceId = deviceId,
        body = submissionBody(draft),
        parser = ::submission,
    )

    suspend fun getSubmission(deviceId: String, gameId: String) = request(
        "api/v1/game-submissions/$gameId",
        deviceId = deviceId,
        parser = ::submission,
    )

    suspend fun submitGame(deviceId: String, gameId: String) = request(
        "api/v1/game-submissions/$gameId/submit",
        method = "POST",
        deviceId = deviceId,
        parser = ::submission,
    )

    suspend fun createUploadIntent(
        deviceId: String,
        gameId: String,
        imageType: String,
        contentType: String,
        fileSizeBytes: Long,
    ) = request(
        "api/v1/media/upload-intents",
        method = "POST",
        deviceId = deviceId,
        body = jsonOf("gameId" to gameId, "imageType" to imageType, "contentType" to contentType, "fileSizeBytes" to fileSizeBytes),
        expected = setOf(201),
        parser = ::uploadIntent,
    )

    suspend fun uploadToPresignedUrl(uploadUrl: String, contentType: String, bytes: ByteArray): ApiResult<Unit> =
        withContext(Dispatchers.IO) {
            val url = runCatching { uploadUrl.toHttpUrl() }.getOrElse {
                return@withContext ApiResult.NetworkError("The upload URL is invalid.")
            }
            val request = Request.Builder().url(url).put(bytes.toRequestBody(contentType.toMediaType())).build()
            try {
                client.newCall(request).execute().use { response ->
                    if (response.code in setOf(200, 201, 204)) ApiResult.Success(Unit)
                    else ApiResult.Error(response.code, null, "The image upload returned HTTP ${response.code}.")
                }
            } catch (_: IOException) {
                ApiResult.NetworkError("The image upload could not be completed.")
            }
        }

    suspend fun completeMedia(deviceId: String, mediaId: String) = request(
        "api/v1/media/$mediaId/complete",
        method = "POST",
        deviceId = deviceId,
        expected = setOf(202),
        parser = ::gameImage,
    )

    suspend fun getMedia(deviceId: String, mediaId: String) = request(
        "api/v1/media/$mediaId",
        deviceId = deviceId,
        parser = ::gameImage,
    )

    suspend fun listLanguages(deviceId: String) = request(
        "api/v1/languages",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::referenceData) },
    )

    suspend fun listTags(deviceId: String) = request(
        "api/v1/tags",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::referenceData) },
    )

    suspend fun listOwnedGames(deviceId: String, collectionId: String) = request(
        "api/v1/collections/$collectionId/games",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::ownedGame) },
    )

    suspend fun addOwnedGame(deviceId: String, collectionId: String, gameId: String) = request(
        "api/v1/collections/$collectionId/games/$gameId",
        method = "PUT",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun removeOwnedGame(deviceId: String, collectionId: String, gameId: String) = request(
        "api/v1/collections/$collectionId/games/$gameId",
        method = "DELETE",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun listWishlist(deviceId: String) = request(
        "api/v1/me/wishlist",
        deviceId = deviceId,
        parser = { value -> value.arrayObjects().map(::wishlistGame) },
    )

    suspend fun addToWishlist(deviceId: String, gameId: String) = request(
        "api/v1/me/wishlist/$gameId",
        method = "PUT",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun removeFromWishlist(deviceId: String, gameId: String) = request(
        "api/v1/me/wishlist/$gameId",
        method = "DELETE",
        deviceId = deviceId,
        expected = setOf(204),
        parser = { Unit },
    )

    suspend fun pushMutations(deviceId: String, mutations: List<SyncMutation>) = request(
        "api/v1/sync/push",
        method = "POST",
        deviceId = deviceId,
        body = JSONObject().put("mutations", JSONArray().apply {
            mutations.forEach { mutation ->
                put(JSONObject().apply {
                    put("mutationId", mutation.mutationId)
                    put("type", mutation.type)
                    put("gameId", mutation.gameId)
                    put("collectionId", mutation.collectionId ?: JSONObject.NULL)
                })
            }
        }),
        parser = { value ->
            JSONObject(value).getJSONArray("results").objects().map { item ->
                val json = JSONObject(item)
                SyncMutationResult(
                    mutationId = json.getString("mutationId"),
                    applied = json.getBoolean("applied"),
                    duplicate = json.getBoolean("duplicate"),
                    serverSequence = json.optNullableLong("serverSequence"),
                    errorCode = json.optNullableString("errorCode"),
                )
            }
        },
    )

    suspend fun pullSync(deviceId: String, scopes: List<SyncScope>, limit: Int = 500) = request(
        "api/v1/sync/pull",
        method = "POST",
        deviceId = deviceId,
        body = JSONObject().put("limit", limit).put("scopes", JSONArray().apply {
            scopes.forEach { scope ->
                put(JSONObject().apply {
                    put("type", scope.type)
                    put("id", scope.id ?: JSONObject.NULL)
                    put("cursor", scope.cursor)
                })
            }
        }),
        parser = { value ->
            JSONObject(value).getJSONArray("scopes").objects().map(::syncPage)
        },
    )

    suspend fun bootstrapSync(deviceId: String) = request(
        "api/v1/sync/bootstrap",
        deviceId = deviceId,
        parser = { value ->
            val json = JSONObject(value)
            SyncBootstrap(json.getLong("cursor"), json.getJSONArray("snapshot").objects().map(::syncChange))
        },
    )

    suspend fun probeCurrentProfile(): ApiProbeResult = when (val result = getProfile()) {
        is ApiResult.Success -> ApiProbeResult.Authenticated(profileExists = true)
        is ApiResult.Error -> when (result.statusCode) {
            404 -> ApiProbeResult.Authenticated(profileExists = false)
            401, 403 -> ApiProbeResult.Rejected(result.statusCode)
            else -> ApiProbeResult.Failure(result.message)
        }
        is ApiResult.NetworkError -> ApiProbeResult.Failure(result.message)
        ApiResult.SignedOut -> ApiProbeResult.SignedOut
    }

    private suspend fun <T> request(
        path: String,
        method: String = "GET",
        deviceId: String? = null,
        body: JSONObject? = null,
        expected: Set<Int> = setOf(200),
        parser: (String) -> T,
    ): ApiResult<T> = request(
        baseUrl.resolve(path) ?: return ApiResult.NetworkError("The API URL is invalid."),
        method,
        deviceId,
        body,
        expected,
        parser,
    )

    private suspend fun <T> request(
        path: HttpUrl,
        method: String = "GET",
        deviceId: String? = null,
        body: JSONObject? = null,
        expected: Set<Int> = setOf(200),
        parser: (String) -> T,
    ): ApiResult<T> = withContext(Dispatchers.IO) {
        val token = runCatching { tokens.freshAccessToken() }.getOrElse {
            return@withContext ApiResult.NetworkError("Could not refresh the login session.")
        } ?: return@withContext ApiResult.SignedOut
        val builder = Request.Builder()
            .url(path)
            .header("Authorization", "Bearer $token")
            .header("Accept", "application/json")
            .header("X-Correlation-ID", UUID.randomUUID().toString())
        if (deviceId != null) builder.header("X-Device-Id", deviceId)
        val requestBody = body?.toString()?.toRequestBody(JSON)
        builder.method(method, requestBody ?: if (method == "POST" || method == "PUT" || method == "PATCH") EMPTY_BODY else null)
        try {
            client.newCall(builder.build()).execute().use { response ->
                val content = response.body.string()
                if (response.code in expected) {
                    runCatching { ApiResult.Success(parser(content)) }
                        .getOrElse { ApiResult.NetworkError("The API response could not be read.") }
                } else {
                    val problem = runCatching { JSONObject(content) }.getOrNull()
                    ApiResult.Error(
                        statusCode = response.code,
                        code = problem?.optString("code")?.takeIf(String::isNotBlank),
                        message = problem?.optString("detail")?.takeIf(String::isNotBlank)
                            ?: problem?.optString("title")?.takeIf(String::isNotBlank)
                            ?: "The API returned HTTP ${response.code}.",
                        referenceId = response.header("X-Correlation-ID")
                            ?: problem?.optString("correlationId")?.takeIf(String::isNotBlank)
                            ?: problem?.optString("traceId")?.takeIf(String::isNotBlank),
                    )
                }
            }
        } catch (_: IOException) {
            ApiResult.NetworkError("The API could not be reached.")
        }
    }

    private fun profile(value: String): UserProfile {
        val json = JSONObject(value)
        return UserProfile(
            id = json.getString("id"),
            displayName = json.getString("displayName"),
            username = json.getString("username"),
            hasActiveDevice = json.getBoolean("hasActiveDevice"),
            defaultCollectionId = json.optNullableString("defaultCollectionId"),
        )
    }

    private fun collection(value: String): CollectionSummary {
        val json = JSONObject(value)
        return CollectionSummary(
            id = json.getString("id"),
            name = json.getString("name"),
            ownerUserId = json.getString("ownerUserId"),
            myRole = CollectionRole.fromApi(json.get("myRole")),
        )
    }

    private fun member(value: String): CollectionMember {
        val json = JSONObject(value)
        return CollectionMember(
            userId = json.getString("userId"),
            displayName = json.getString("displayName"),
            username = json.getString("username"),
            role = CollectionRole.fromApi(json.get("role")),
        )
    }

    private fun invitation(value: String): CollectionInvitation {
        val json = JSONObject(value)
        return CollectionInvitation(
            id = json.getString("id"),
            collectionId = json.getString("collectionId"),
            collectionName = json.getString("collectionName"),
            inviterUserId = json.getString("inviterUserId"),
            inviteeUserId = json.getString("inviteeUserId"),
            role = CollectionRole.fromApi(json.get("role")),
            status = json.getString("status"),
        )
    }

    private fun notification(value: String): NotificationItem {
        val json = JSONObject(value)
        return NotificationItem(
            id = json.getString("id"),
            type = json.getString("type"),
            payloadJson = json.optJSONObject("payload")?.toString() ?: "{}",
            createdAtUtc = json.getString("createdAtUtc"),
            readAtUtc = json.optNullableString("readAtUtc"),
        )
    }

    private fun userSearchResult(value: String): UserSearchResult {
        val json = JSONObject(value)
        return UserSearchResult(json.getString("id"), json.getString("displayName"), json.getString("username"))
    }

    private fun gameSummary(value: String): GameSummary {
        val json = JSONObject(value)
        return GameSummary(
            id = json.getString("id"),
            title = json.getString("title"),
            publisher = json.optNullableString("publisher"),
            releaseYear = json.optNullableInt("releaseYear"),
            moderationStatus = json.getString("moderationStatus"),
        )
    }

    private fun game(value: String): GameDetails {
        val json = JSONObject(value)
        return GameDetails(
            id = json.getString("id"),
            title = json.getString("title"),
            description = json.optNullableString("description"),
            publisher = json.optNullableString("publisher"),
            releaseYear = json.optNullableInt("releaseYear"),
            minimumPlayers = json.optNullableInt("minimumPlayers"),
            maximumPlayers = json.optNullableInt("maximumPlayers"),
            minimumAge = json.optNullableInt("minimumAge"),
            minimumPlayingTimeMinutes = json.optNullableInt("minimumPlayingTimeMinutes"),
            maximumPlayingTimeMinutes = json.optNullableInt("maximumPlayingTimeMinutes"),
            moderationStatus = json.getString("moderationStatus"),
            revision = json.getLong("revision"),
            barcodes = json.getJSONArray("barcodes").strings(),
            languages = json.getJSONArray("languages").objects().map(::referenceData),
            tags = json.getJSONArray("tags").objects().map(::referenceData),
        )
    }

    private fun productCandidate(value: String): ProductMetadataCandidate {
        val json = JSONObject(value)
        return ProductMetadataCandidate(
            barcode = json.getString("barcode"),
            source = json.getString("source"),
            existingGameId = json.optNullableString("existingGameId"),
            title = json.optNullableString("title"),
            publisher = json.optNullableString("publisher"),
            description = json.optNullableString("description"),
        )
    }

    private fun changeRequest(value: String): GameChangeRequest {
        val json = JSONObject(value)
        val changes = json.getJSONObject("proposedChanges")
        return GameChangeRequest(
            id = json.getString("id"), gameId = json.getString("gameId"), gameTitle = json.getString("gameTitle"),
            proposedChanges = GameChangePatch(
                changes.optNullableString("title"), changes.optNullableString("description"), changes.optNullableString("publisher"),
                changes.optNullableInt("releaseYear"), changes.optNullableInt("minimumPlayers"), changes.optNullableInt("maximumPlayers"),
                changes.optNullableInt("minimumAge"), changes.optNullableInt("minimumPlayingTimeMinutes"), changes.optNullableInt("maximumPlayingTimeMinutes"),
            ),
            status = json.getString("status"), adminComment = json.optNullableString("adminComment"),
            createdAtUtc = json.getString("createdAtUtc"), updatedAtUtc = json.getString("updatedAtUtc"),
        )
    }

    private fun submission(value: String): GameSubmission {
        val json = JSONObject(value)
        return GameSubmission(game(json.getJSONObject("game").toString()), json.optNullableString("moderationComment"))
    }

    private fun uploadIntent(value: String): UploadIntent {
        val json = JSONObject(value)
        return UploadIntent(json.getString("mediaId"), json.getString("uploadUrl"), json.getString("expiresAtUtc"))
    }

    private fun gameImage(value: String): GameImage {
        val json = JSONObject(value)
        return GameImage(
            id = json.getString("id"),
            gameId = json.getString("gameId"),
            imageType = json.getString("imageType"),
            status = json.getString("status"),
            contentType = json.getString("contentType"),
            fileSizeBytes = json.optNullableLong("fileSizeBytes"),
        )
    }

    private fun referenceData(value: String): ReferenceData {
        val json = JSONObject(value)
        return ReferenceData(json.getString("id"), json.getString("name"), json.optNullableString("code"))
    }

    private fun ownedGame(value: String): OwnedGame {
        val json = JSONObject(value)
        return OwnedGame(json.getString("gameId"), json.getString("title"), json.optNullableString("publisher"), json.getString("moderationStatus"))
    }

    private fun wishlistGame(value: String): WishlistGame {
        val json = JSONObject(value)
        return WishlistGame(json.getString("gameId"), json.getString("title"), json.optNullableString("publisher"), json.getString("moderationStatus"))
    }

    private fun syncPage(value: String): SyncScopePage {
        val json = JSONObject(value)
        return SyncScopePage(
            type = json.getString("type"),
            id = json.optNullableString("id"),
            nextCursor = json.getLong("nextCursor"),
            hasMore = json.getBoolean("hasMore"),
            isSnapshot = json.getBoolean("isSnapshot"),
            changes = json.getJSONArray("changes").objects().map(::syncChange),
        )
    }

    private fun syncChange(value: String): SyncChange {
        val json = JSONObject(value)
        return SyncChange(
            sequence = json.getLong("sequence"),
            scopeType = json.getString("scopeType"),
            scopeId = json.optNullableString("scopeId"),
            operation = json.getString("operation"),
            entityId = json.getString("entityId"),
            payloadJson = json.getJSONObject("payload").toString(),
        )
    }

    private fun String.arrayObjects(): List<String> {
        val array = JSONArray(this)
        return List(array.length()) { index -> array.getJSONObject(index).toString() }
    }

    private fun JSONObject.optNullableString(name: String): String? =
        if (isNull(name)) null else optString(name).takeIf(String::isNotBlank)

    private fun JSONObject.optNullableInt(name: String): Int? = if (isNull(name)) null else getInt(name)

    private fun JSONObject.optNullableLong(name: String): Long? = if (isNull(name)) null else getLong(name)

    private fun JSONArray.objects(): List<String> = List(length()) { index -> getJSONObject(index).toString() }

    private fun JSONArray.strings(): List<String> = List(length()) { index -> getString(index) }

    private fun jsonOf(vararg values: Pair<String, Any?>) = JSONObject().apply {
        values.forEach { (key, value) -> put(key, value) }
    }

    private fun submissionBody(draft: GameSubmissionDraft) = JSONObject().apply {
        put("title", draft.title.trim())
        put("description", draft.description?.trim()?.takeIf(String::isNotBlank) ?: JSONObject.NULL)
        put("publisher", draft.publisher?.trim()?.takeIf(String::isNotBlank) ?: JSONObject.NULL)
        put("releaseYear", draft.releaseYear ?: JSONObject.NULL)
        put("minimumPlayers", draft.minimumPlayers ?: JSONObject.NULL)
        put("maximumPlayers", draft.maximumPlayers ?: JSONObject.NULL)
        put("minimumAge", draft.minimumAge ?: JSONObject.NULL)
        put("minimumPlayingTimeMinutes", draft.minimumPlayingTimeMinutes ?: JSONObject.NULL)
        put("maximumPlayingTimeMinutes", draft.maximumPlayingTimeMinutes ?: JSONObject.NULL)
        put("barcodes", JSONArray(draft.barcodes))
        put("languageIds", JSONArray(draft.languageIds))
        put("tagIds", JSONArray(draft.tagIds))
        put("expectedRevision", draft.expectedRevision ?: JSONObject.NULL)
    }

    private fun changePatchBody(changes: GameChangePatch) = JSONObject().apply {
        changes.title?.let { put("title", it.trim()) }
        changes.description?.let { put("description", it.trim()) }
        changes.publisher?.let { put("publisher", it.trim()) }
        changes.releaseYear?.let { put("releaseYear", it) }
        changes.minimumPlayers?.let { put("minimumPlayers", it) }
        changes.maximumPlayers?.let { put("maximumPlayers", it) }
        changes.minimumAge?.let { put("minimumAge", it) }
        changes.minimumPlayingTimeMinutes?.let { put("minimumPlayingTimeMinutes", it) }
        changes.maximumPlayingTimeMinutes?.let { put("maximumPlayingTimeMinutes", it) }
    }

    private fun String.ensureTrailingSlash() = if (endsWith('/')) this else "$this/"

    companion object {
        private val JSON = "application/json; charset=utf-8".toMediaType()
        private val EMPTY_BODY = ByteArray(0).toRequestBody(JSON)

        private fun defaultClient() = OkHttpClient.Builder()
            .connectTimeout(10, TimeUnit.SECONDS)
            .readTimeout(20, TimeUnit.SECONDS)
            .callTimeout(30, TimeUnit.SECONDS)
            .build()
    }
}

sealed interface ApiResult<out T> {
    data class Success<T>(val value: T) : ApiResult<T>
    data class Error(val statusCode: Int, val code: String?, val message: String, val referenceId: String? = null) : ApiResult<Nothing>
    data class NetworkError(val message: String) : ApiResult<Nothing>
    data object SignedOut : ApiResult<Nothing>
}

data class UserProfile(
    val id: String,
    val displayName: String,
    val username: String,
    val hasActiveDevice: Boolean,
    val defaultCollectionId: String?,
)

data class CollectionSummary(
    val id: String,
    val name: String,
    val ownerUserId: String,
    val myRole: CollectionRole,
)

data class CollectionMember(
    val userId: String,
    val displayName: String,
    val username: String,
    val role: CollectionRole,
)

data class CollectionInvitation(
    val id: String,
    val collectionId: String,
    val collectionName: String,
    val inviterUserId: String,
    val inviteeUserId: String,
    val role: CollectionRole,
    val status: String,
)

data class UserSearchResult(val id: String, val displayName: String, val username: String)
data class NotificationItem(val id: String, val type: String, val payloadJson: String, val createdAtUtc: String, val readAtUtc: String?)
data class GameChangePatch(
    val title: String? = null, val description: String? = null, val publisher: String? = null,
    val releaseYear: Int? = null, val minimumPlayers: Int? = null, val maximumPlayers: Int? = null,
    val minimumAge: Int? = null, val minimumPlayingTimeMinutes: Int? = null, val maximumPlayingTimeMinutes: Int? = null,
)
data class GameChangeRequest(
    val id: String, val gameId: String, val gameTitle: String, val proposedChanges: GameChangePatch,
    val status: String, val adminComment: String?, val createdAtUtc: String, val updatedAtUtc: String,
)

data class GameSummary(
    val id: String,
    val title: String,
    val publisher: String?,
    val releaseYear: Int?,
    val moderationStatus: String,
)

data class GameDetails(
    val id: String,
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
    val barcodes: List<String>,
    val languages: List<ReferenceData>,
    val tags: List<ReferenceData>,
)

data class ReferenceData(val id: String, val name: String, val code: String?)
data class OwnedGame(val gameId: String, val title: String, val publisher: String?, val moderationStatus: String)
data class WishlistGame(val gameId: String, val title: String, val publisher: String?, val moderationStatus: String)

data class SyncMutation(val mutationId: String, val type: String, val gameId: String, val collectionId: String?)
data class SyncMutationResult(val mutationId: String, val applied: Boolean, val duplicate: Boolean, val serverSequence: Long?, val errorCode: String?)
data class SyncScope(val type: String, val id: String?, val cursor: Long)
data class SyncChange(val sequence: Long, val scopeType: String, val scopeId: String?, val operation: String, val entityId: String, val payloadJson: String)
data class SyncScopePage(val type: String, val id: String?, val nextCursor: Long, val hasMore: Boolean, val isSnapshot: Boolean, val changes: List<SyncChange>)
data class SyncBootstrap(val cursor: Long, val snapshot: List<SyncChange>)

data class ProductMetadataCandidate(
    val barcode: String,
    val source: String,
    val existingGameId: String?,
    val title: String?,
    val publisher: String?,
    val description: String?,
)

data class GameSubmissionDraft(
    val title: String,
    val description: String?,
    val publisher: String?,
    val releaseYear: Int?,
    val minimumPlayers: Int?,
    val maximumPlayers: Int?,
    val minimumAge: Int?,
    val minimumPlayingTimeMinutes: Int?,
    val maximumPlayingTimeMinutes: Int?,
    val barcodes: List<String>,
    val languageIds: List<String>,
    val tagIds: List<String>,
    val expectedRevision: Long?,
)

data class GameSubmission(val game: GameDetails, val moderationComment: String?)
data class UploadIntent(val mediaId: String, val uploadUrl: String, val expiresAtUtc: String)
data class GameImage(val id: String, val gameId: String, val imageType: String, val status: String, val contentType: String, val fileSizeBytes: Long?)

enum class CollectionRole(val apiValue: Int) {
    Viewer(0), Editor(1), Owner(2);

    companion object {
        fun fromApi(value: Any): CollectionRole = when (value) {
            is Number -> entries.firstOrNull { it.apiValue == value.toInt() }
            else -> entries.firstOrNull { it.name.equals(value.toString(), ignoreCase = true) }
        } ?: Viewer
    }
}

sealed interface ApiProbeResult {
    data object SignedOut : ApiProbeResult
    data class Authenticated(val profileExists: Boolean) : ApiProbeResult
    data class Rejected(val statusCode: Int) : ApiProbeResult
    data class Failure(val message: String) : ApiProbeResult
}
