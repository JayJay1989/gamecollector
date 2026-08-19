package com.gamecollector.core.network

import com.gamecollector.core.auth.AccessTokenProvider
import kotlinx.coroutines.runBlocking
import okhttp3.OkHttpClient
import okhttp3.Protocol
import okhttp3.Request
import okhttp3.Response
import okhttp3.ResponseBody.Companion.toResponseBody
import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.util.ArrayDeque

class GameCollectorApiTests {
    @Test
    fun authenticatedProbeSendsBearerTokenAndRecognizesMissingProfile() = runBlocking {
        val transport = TestTransport(ResponseSpec(404, "{}"))
        val api = api(transport)

        val result = api.probeCurrentProfile()

        assertEquals("Bearer access-token", transport.requests.single().header("Authorization"))
        assertTrue(result is ApiProbeResult.Authenticated && !result.profileExists)
    }

    @Test
    fun onboardingParsesProfileAndUsesExpectedPayload() = runBlocking {
        val transport = TestTransport(ResponseSpec(201, PROFILE))
        val result = api(transport).onboard("John Smith", "john")

        assertTrue(result is ApiResult.Success && result.value.username == "john")
        val request = transport.requests.single()
        assertEquals("POST", request.method)
        assertEquals("/api/v1/me/onboarding", request.url.encodedPath)
        val body = JSONObject(request.bodyUtf8())
        assertEquals("John Smith", body.getString("displayName"))
        assertEquals("john", body.getString("username"))
    }

    @Test
    fun collectionRequestsCarryDeviceHeaderAndParseRole() = runBlocking {
        val transport = TestTransport(ResponseSpec(201, COLLECTION))
        val result = api(transport).createCollection("device-123", "Our Games")

        assertTrue(result is ApiResult.Success && result.value.myRole == CollectionRole.Owner)
        val request = transport.requests.single()
        assertEquals("device-123", request.header("X-Device-Id"))
        assertEquals("Our Games", JSONObject(request.bodyUtf8()).getString("name"))
    }

    @Test
    fun catalogAndCollectionGamesExposeYearAndFrontThumbnail() = runBlocking {
        val summary = """{"id":"game-1","title":"HeroQuest","publisher":"Milton Bradley","releaseYear":1989,"moderationStatus":"Approved","frontImageId":"media-front"}"""
        val owned = """{"gameId":"game-1","title":"HeroQuest","publisher":"Milton Bradley","releaseYear":1989,"moderationStatus":"Approved","frontImageId":"media-front","addedAtUtc":"2026-08-19T00:00:00Z"}"""
        val transport = TestTransport(ResponseSpec(200, "[$summary]"), ResponseSpec(200, "[$owned]"))
        val api = api(transport)

        val search = api.searchGames("device-123", "HeroQuest")
        val collection = api.listOwnedGames("device-123", "collection-1")

        assertTrue(search is ApiResult.Success && search.value.single().frontImageId == "media-front")
        assertTrue(collection is ApiResult.Success && collection.value.single().releaseYear == 1989)
        assertEquals("media-front", (collection as ApiResult.Success).value.single().frontImageId)
    }

    @Test
    fun invitationWorkflowUsesUsernameQueryAndRoleValue() = runBlocking {
        val transport = TestTransport(
            ResponseSpec(200, "[{\"id\":\"user-2\",\"displayName\":\"Jane\",\"username\":\"jane\"}]"),
            ResponseSpec(201, INVITATION),
        )
        val api = api(transport)

        val users = api.searchUsers("device-123", "#jane")
        val invitation = api.invite("device-123", "collection-1", "user-2", CollectionRole.Editor)

        assertTrue(users is ApiResult.Success && users.value.single().username == "jane")
        assertEquals("username", transport.requests[0].url.queryParameter("type"))
        assertEquals("jane", transport.requests[0].url.queryParameter("q"))
        assertTrue(invitation is ApiResult.Success && invitation.value.role == CollectionRole.Editor)
        assertEquals(1, JSONObject(transport.requests[1].bodyUtf8()).getInt("role"))
    }

    @Test
    fun notificationInboxAndReadActionsUsePersonalEndpoints() = runBlocking {
        val item = """{"id":"notice-1","type":"CollectionInvitation","payload":{"collectionId":"collection-1"},"createdAtUtc":"2026-08-15T00:00:00Z","readAtUtc":null}"""
        val transport = TestTransport(ResponseSpec(200, "[$item]"), ResponseSpec(204, ""), ResponseSpec(204, ""))
        val api = api(transport)

        val listed = api.listNotifications("device-123")
        assertTrue(listed is ApiResult.Success && listed.value.single().payloadJson.contains("collection-1"))
        assertTrue(api.markNotificationRead("device-123", "notice-1") is ApiResult.Success)
        assertTrue(api.markAllNotificationsRead("device-123") is ApiResult.Success)

        assertEquals("/api/v1/me/notifications", transport.requests[0].url.encodedPath)
        assertEquals("/api/v1/me/notifications/notice-1/read", transport.requests[1].url.encodedPath)
        assertEquals("/api/v1/me/notifications/read-all", transport.requests[2].url.encodedPath)
        assertTrue(transport.requests.all { it.header("X-Device-Id") == "device-123" })
    }

    @Test
    fun correctionRequestSendsOnlyProposedFieldsAndParsesStatus() = runBlocking {
        val response = """{"id":"change-1","gameId":"game-1","gameTitle":"UNO Flip!","proposedByUserId":"user-1","proposedChanges":{"title":null,"description":null,"publisher":null,"releaseYear":null,"minimumPlayers":null,"maximumPlayers":null,"minimumAge":10,"minimumPlayingTimeMinutes":null,"maximumPlayingTimeMinutes":null},"status":"Pending","adminComment":null,"reviewedByUserId":null,"reviewedAtUtc":null,"createdAtUtc":"2026-08-16T00:00:00Z","updatedAtUtc":"2026-08-16T00:00:00Z"}"""
        val transport = TestTransport(ResponseSpec(201, response), ResponseSpec(200, "[$response]"))
        val api = api(transport)

        val created = api.createChangeRequest("device-123", "game-1", GameChangePatch(minimumAge = 10))
        val mine = api.listMyChangeRequests("device-123")

        assertTrue(created is ApiResult.Success && created.value.proposedChanges.minimumAge == 10)
        assertTrue(mine is ApiResult.Success && mine.value.single().status == "Pending")
        val proposed = JSONObject(transport.requests[0].bodyUtf8()).getJSONObject("proposedChanges")
        assertEquals(setOf("minimumAge"), proposed.keys().asSequence().toSet())
        assertEquals("/api/v1/games/game-1/change-requests", transport.requests[0].url.encodedPath)
        assertEquals("/api/v1/change-requests/mine", transport.requests[1].url.encodedPath)
    }

    @Test
    fun deviceRevocationUsesActiveInstallationHeader() = runBlocking {
        val transport = TestTransport(ResponseSpec(204, ""))
        val result = api(transport).revokeDevice("device-123")

        assertTrue(result is ApiResult.Success)
        assertEquals("DELETE", transport.requests.single().method)
        assertEquals("/api/v1/me/device", transport.requests.single().url.encodedPath)
        assertEquals("device-123", transport.requests.single().header("X-Device-Id"))
    }

    @Test
    fun administratorCanHardDeleteGame() = runBlocking {
        val transport = TestTransport(ResponseSpec(204, ""))

        val result = api(transport).deleteAdminGame("device-123", "game-1")

        assertTrue(result is ApiResult.Success)
        assertEquals("DELETE", transport.requests.single().method)
        assertEquals("/api/v1/admin/games/game-1", transport.requests.single().url.encodedPath)
        assertEquals("device-123", transport.requests.single().header("X-Device-Id"))
    }

    @Test
    fun catalogSearchEncodesQueryAndParsesSummary() = runBlocking {
        val transport = TestTransport(ResponseSpec(200, "[$GAME_SUMMARY]"))

        val result = api(transport).searchGames("device-123", "UNO Flip!")

        assertTrue(result is ApiResult.Success && result.value.single().title == "UNO Flip!")
        assertEquals("UNO Flip!", transport.requests.single().url.queryParameter("q"))
        assertEquals("device-123", transport.requests.single().header("X-Device-Id"))
    }

    @Test
    fun gameDetailsIncludeFactsBarcodesLanguagesAndTags() = runBlocking {
        val transport = TestTransport(ResponseSpec(200, GAME))

        val result = api(transport).getGame("device-123", "game-1")

        assertTrue(result is ApiResult.Success)
        val game = (result as ApiResult.Success).value
        assertEquals(4, game.maximumPlayers)
        assertEquals(listOf("887961751062"), game.barcodes)
        assertEquals("English", game.languages.single().name)
        assertEquals("Card Game", game.tags.single().name)
    }

    @Test
    fun ownershipAndWishlistMutationsUseSeparateResources() = runBlocking {
        val transport = TestTransport(ResponseSpec(204, ""), ResponseSpec(204, ""))
        val api = api(transport)

        assertTrue(api.addOwnedGame("device-123", "collection-1", "game-1") is ApiResult.Success)
        assertTrue(api.addToWishlist("device-123", "game-1") is ApiResult.Success)

        assertEquals("PUT", transport.requests[0].method)
        assertEquals("/api/v1/collections/collection-1/games/game-1", transport.requests[0].url.encodedPath)
        assertEquals("/api/v1/me/wishlist/game-1", transport.requests[1].url.encodedPath)
    }

    @Test
    fun syncPushCarriesStableMutationIdAndParsesDuplicateResult() = runBlocking {
        val transport = TestTransport(ResponseSpec(200, """{"results":[{"mutationId":"mutation-1","applied":true,"duplicate":true,"serverSequence":42,"errorCode":null}]}"""))

        val result = api(transport).pushMutations("device-123", listOf(
            SyncMutation("mutation-1", "addCollectionGame", "game-1", "collection-1"),
        ))

        assertTrue(result is ApiResult.Success && result.value.single().duplicate)
        val mutation = JSONObject(transport.requests.single().bodyUtf8()).getJSONArray("mutations").getJSONObject(0)
        assertEquals("mutation-1", mutation.getString("mutationId"))
        assertEquals("addCollectionGame", mutation.getString("type"))
    }

    @Test
    fun syncPullParsesScopeCursorTombstoneAndPagination() = runBlocking {
        val body = """{"scopes":[{"type":"collection","id":"collection-1","nextCursor":43,"hasMore":false,"isSnapshot":false,"changes":[{"sequence":43,"scopeType":"collection","scopeId":"collection-1","operation":"collectionGameChanged","entityId":"game-1","payload":{"gameId":"game-1","isPresent":false},"occurredAtUtc":"2026-08-15T00:00:00Z"}]}]}"""
        val transport = TestTransport(ResponseSpec(200, body))

        val result = api(transport).pullSync("device-123", listOf(SyncScope("collection", "collection-1", 42)))

        assertTrue(result is ApiResult.Success)
        val page = (result as ApiResult.Success).value.single()
        assertEquals(43, page.nextCursor)
        assertEquals("collectionGameChanged", page.changes.single().operation)
        assertTrue(!JSONObject(page.changes.single().payloadJson).getBoolean("isPresent"))
    }

    @Test
    fun productLookupReturnsReviewableCandidate() = runBlocking {
        val transport = TestTransport(ResponseSpec(200, """{"barcode":"887961751062","source":"upcitemdb","existingGameId":null,"title":"Suggested title","publisher":"Suggested publisher","description":null}"""))

        val result = api(transport).lookupProduct("device-123", "887961751062")

        assertTrue(result is ApiResult.Success && result.value.source == "upcitemdb")
        assertEquals("/api/v1/product-lookup/887961751062", transport.requests.single().url.encodedPath)
    }

    @Test
    fun submissionAndPresignedUploadUseTheirDistinctSecurityContexts() = runBlocking {
        val submission = """{"game":$GAME,"submittedByUserId":"user-1","moderationComment":null,"approvedByUserId":null,"approvedAtUtc":null,"createdAtUtc":"2026-01-01T00:00:00Z","updatedAtUtc":"2026-01-01T00:00:00Z"}"""
        val transport = TestTransport(ResponseSpec(201, submission), ResponseSpec(200, ""))
        val api = api(transport)

        val created = api.createSubmission("device-123", GameSubmissionDraft(
            "New Game", null, "Publisher", 2026, 2, 4, 7, 15, 30,
            listOf("887961751062"), emptyList(), emptyList(), null,
        ))
        val uploaded = api.uploadToPresignedUrl("https://uploads.example.test/object?signature=secret", "image/jpeg", byteArrayOf(1, 2, 3))

        assertTrue(created is ApiResult.Success)
        assertTrue(uploaded is ApiResult.Success)
        assertEquals("Bearer access-token", transport.requests[0].header("Authorization"))
        assertEquals("device-123", transport.requests[0].header("X-Device-Id"))
        assertEquals(2, JSONObject(transport.requests[0].bodyUtf8()).getInt("minimumPlayers"))
        assertEquals(null, transport.requests[1].header("Authorization"))
        assertEquals("secret", transport.requests[1].url.queryParameter("signature"))
        assertEquals("image/jpeg", transport.requests[1].body?.contentType().toString())
    }

    @Test
    fun proxiedMediaUploadUsesApiAuthenticationAndDeviceHeader() = runBlocking {
        val image = """{"id":"media-1","gameId":"game-1","imageType":"Front","status":"Processing","contentType":"image/jpeg","fileSizeBytes":3,"width":null,"height":null,"checksum":null,"originalUrl":null,"thumbnailUrl":null}"""
        val transport = TestTransport(ResponseSpec(202, image))

        val result = api(transport).uploadMedia("device-123", "media-1", "image/jpeg", byteArrayOf(1, 2, 3))

        assertTrue(result is ApiResult.Success && result.value.status == "Processing")
        val request = transport.requests.single()
        assertEquals("PUT", request.method)
        assertEquals("/api/v1/media/media-1/content", request.url.encodedPath)
        assertEquals("Bearer access-token", request.header("Authorization"))
        assertEquals("device-123", request.header("X-Device-Id"))
        assertEquals("image/jpeg", request.body?.contentType().toString())
    }

    @Test
    fun gameThumbnailsAreListedAndDownloadedThroughAuthenticatedApi() = runBlocking {
        val image = """{"id":"media-1","gameId":"game-1","imageType":"Front","status":"Ready","contentType":"image/jpeg","fileSizeBytes":300,"width":480,"height":320,"checksum":"abc","originalUrl":null,"thumbnailUrl":null}"""
        val transport = TestTransport(ResponseSpec(200, "[$image]"), ResponseSpec(200, "jpeg-thumbnail"))
        val api = api(transport)

        val listed = api.listGameMedia("device-123", "game-1")
        val downloaded = api.downloadMediaThumbnail("device-123", "media-1")

        assertTrue(listed is ApiResult.Success && listed.value.single().imageType == "Front")
        assertTrue(downloaded is ApiResult.Success && downloaded.value.decodeToString() == "jpeg-thumbnail")
        assertEquals("/api/v1/media/games/game-1", transport.requests[0].url.encodedPath)
        assertEquals("/api/v1/media/media-1/thumbnail", transport.requests[1].url.encodedPath)
        assertTrue(transport.requests.all { it.header("Authorization") == "Bearer access-token" })
        assertTrue(transport.requests.all { it.header("X-Device-Id") == "device-123" })
    }

    @Test
    fun adminModerationListsPendingGamesAndSendsRevisionProtectedDecision() = runBlocking {
        val submission = """{"game":$GAME,"submittedByUserId":"user-1","moderationComment":null,"approvedByUserId":null,"approvedAtUtc":null,"createdAtUtc":"2026-01-01T00:00:00Z","updatedAtUtc":"2026-01-01T00:00:00Z"}"""
        val transport = TestTransport(ResponseSpec(200, "[$submission]"), ResponseSpec(200, submission))
        val api = api(transport)

        val queue = api.listAdminSubmissions("device-123")
        val reviewed = api.moderateAdminSubmission(
            "device-123", "game-1", AdminModerationDecision.NeedsChanges, 7, "Add a back image.",
        )

        assertTrue(queue is ApiResult.Success && queue.value.single().game.title == "UNO Flip!")
        assertTrue(reviewed is ApiResult.Success)
        assertEquals("Pending", transport.requests[0].url.queryParameter("status"))
        assertEquals("/api/v1/admin/submissions/game-1/needs-changes", transport.requests[1].url.encodedPath)
        val body = JSONObject(transport.requests[1].bodyUtf8())
        assertEquals(7, body.getLong("expectedRevision"))
        assertEquals("Add a back image.", body.getString("comment"))
        assertTrue(transport.requests.all { it.header("X-Device-Id") == "device-123" })
    }

    @Test
    fun personalSubmissionListAndDeleteUseServerResources() = runBlocking {
        val submission = """{"game":$GAME,"submittedByUserId":"user-1","moderationComment":null,"approvedByUserId":null,"approvedAtUtc":null,"createdAtUtc":"2026-01-01T00:00:00Z","updatedAtUtc":"2026-01-01T00:00:00Z"}"""
        val transport = TestTransport(ResponseSpec(200, "[$submission]"), ResponseSpec(204, ""))
        val api = api(transport)

        val listed = api.listMySubmissions("device-123")
        val deleted = api.deleteSubmission("device-123", "game-1")

        assertTrue(listed is ApiResult.Success && listed.value.single().game.id == "game-1")
        assertTrue(deleted is ApiResult.Success)
        assertEquals("/api/v1/game-submissions/mine", transport.requests[0].url.encodedPath)
        assertEquals("DELETE", transport.requests[1].method)
        assertEquals("/api/v1/game-submissions/game-1", transport.requests[1].url.encodedPath)
    }

    private fun api(transport: TestTransport) = GameCollectorApi(
        "https://api.example.test",
        AccessTokenProvider { "access-token" },
        OkHttpClient.Builder().addInterceptor(transport::intercept).build(),
    )

    private fun Request.bodyUtf8(): String {
        val buffer = okio.Buffer()
        body?.writeTo(buffer)
        return buffer.readUtf8()
    }

    private class TestTransport(vararg responses: ResponseSpec) {
        private val responses = ArrayDeque(responses.toList())
        val requests = mutableListOf<Request>()

        fun intercept(chain: okhttp3.Interceptor.Chain): Response {
            requests += chain.request()
            val response = responses.removeFirst()
            return Response.Builder()
                .request(chain.request())
                .protocol(Protocol.HTTP_1_1)
                .code(response.code)
                .message("Test")
                .body(response.body.toResponseBody())
                .build()
        }
    }

    private data class ResponseSpec(val code: Int, val body: String)

    private companion object {
        const val PROFILE = """{"id":"user-1","displayName":"John Smith","username":"john","hasActiveDevice":false,"defaultCollectionId":null}"""
        const val COLLECTION = """{"id":"collection-1","name":"Our Games","ownerUserId":"user-1","myRole":2,"createdAtUtc":"2026-01-01T00:00:00Z","updatedAtUtc":"2026-01-01T00:00:00Z"}"""
        const val INVITATION = """{"id":"invite-1","collectionId":"collection-1","collectionName":"Our Games","inviterUserId":"user-1","inviteeUserId":"user-2","role":1,"status":"Pending","createdAtUtc":"2026-01-01T00:00:00Z"}"""
        const val GAME_SUMMARY = """{"id":"game-1","title":"UNO Flip!","publisher":"Mattel","releaseYear":2019,"moderationStatus":"Approved"}"""
        const val GAME = """{"id":"game-1","title":"UNO Flip!","description":"Two-sided UNO.","publisher":"Mattel","releaseYear":2019,"minimumPlayers":2,"maximumPlayers":4,"minimumAge":7,"minimumPlayingTimeMinutes":15,"maximumPlayingTimeMinutes":30,"moderationStatus":"Approved","revision":1,"barcodes":["887961751062"],"languages":[{"id":"language-1","name":"English","code":"en"}],"tags":[{"id":"tag-1","name":"Card Game","code":null}]}"""
    }
}
