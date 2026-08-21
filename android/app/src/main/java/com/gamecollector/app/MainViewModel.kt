package com.gamecollector.app

import android.app.Application
import android.content.Intent
import android.net.Uri
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import androidx.work.WorkManager
import com.gamecollector.core.auth.OidcSessionManager
import com.gamecollector.core.data.GameCollectorRepository
import com.gamecollector.core.data.GameDraftRepository
import com.gamecollector.core.data.toJsonArray
import com.gamecollector.core.database.GameCollectorDatabase
import com.gamecollector.core.database.LocalGameDraft
import com.gamecollector.core.database.LocalNotification
import com.gamecollector.core.database.LocalGameChangeRequest
import com.gamecollector.core.database.SyncScopeState
import com.gamecollector.core.database.PendingMediaUpload
import com.gamecollector.core.network.ApiResult
import com.gamecollector.core.network.AdminModerationDecision
import com.gamecollector.core.network.CollectionInvitation
import com.gamecollector.core.network.CollectionMember
import com.gamecollector.core.network.CollectionRole
import com.gamecollector.core.network.CollectionSummary
import com.gamecollector.core.network.GameCollectorApi
import com.gamecollector.core.network.GameDetails
import com.gamecollector.core.network.GameSummary
import com.gamecollector.core.network.GameSubmission
import com.gamecollector.core.network.GameChangePatch
import com.gamecollector.core.network.GameChangeRequest
import com.gamecollector.core.network.ReferenceData
import com.gamecollector.core.network.UserProfile
import com.gamecollector.core.network.UserSearchResult
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.flatMapLatest
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.json.JSONObject

@OptIn(ExperimentalCoroutinesApi::class)
class MainViewModel(application: Application) : AndroidViewModel(application) {
    private val app = application
    private val session = OidcSessionManager(
        application,
        Uri.parse(BuildConfig.OIDC_ISSUER),
        BuildConfig.OIDC_CLIENT_ID,
        Uri.parse(BuildConfig.OIDC_REDIRECT_URI),
    )
    private val api = GameCollectorApi(BuildConfig.API_BASE_URL, session)
    private val database = GameCollectorDatabase.get(application)
    private val repository = GameCollectorRepository(database, api)
    private val draftRepository = GameDraftRepository(database)
    private val deviceId = InstallationIdStore(application).id
    private var pendingDeepLink: DeepLinkTarget? = null
    private val mutableState = MutableStateFlow(
        MainUiState(page = if (session.isAuthorized) AppPage.Loading else AppPage.SignIn),
    )
    val state: StateFlow<MainUiState> = mutableState.asStateFlow()

    init {
        observeLocalState()
        if (session.isAuthorized) {
            SyncScheduler.ensurePeriodic(application)
            refreshSession()
        }
    }

    private fun observeLocalState() {
        viewModelScope.launch {
            repository.profile.collect { profile -> if (profile != null) mutableState.update { it.copy(profile = profile) } }
        }
        viewModelScope.launch {
            repository.collections.collect { collections ->
                mutableState.update { current ->
                    val selected = current.selectedCollectionId?.takeIf { id -> collections.any { it.id == id } }
                        ?: current.profile?.defaultCollectionId?.takeIf { id -> collections.any { it.id == id } }
                        ?: collections.firstOrNull()?.id
                    current.copy(collections = collections, selectedCollectionId = selected)
                }
            }
        }
        viewModelScope.launch {
            repository.invitations.collect { invitations -> mutableState.update { it.copy(invitations = invitations) } }
        }
        viewModelScope.launch {
            repository.notifications.collect { notifications -> mutableState.update { it.copy(notifications = notifications) } }
        }
        viewModelScope.launch {
            repository.changeRequests.collect { requests -> mutableState.update { it.copy(changeRequests = requests) } }
        }
        viewModelScope.launch {
            repository.syncScopes.collect { scopes -> mutableState.update { it.copy(syncScopes = scopes) } }
        }
        viewModelScope.launch {
            repository.pendingMutationCount.collect { count -> mutableState.update { it.copy(pendingMutationCount = count) } }
        }
        viewModelScope.launch {
            repository.wishlistIds.collect { ids -> mutableState.update { it.copy(wishlistGameIds = ids) } }
        }
        viewModelScope.launch {
            draftRepository.drafts.collect { drafts -> mutableState.update { it.copy(drafts = drafts) } }
        }
        viewModelScope.launch {
            state.map { it.selectedDraftId }.distinctUntilChanged().flatMapLatest { id ->
                if (id == null) flowOf(null) else draftRepository.draft(id)
            }.collect { draft -> mutableState.update { it.copy(selectedDraft = draft) } }
        }
        viewModelScope.launch {
            state.map { it.selectedDraftId }.distinctUntilChanged().flatMapLatest { id ->
                if (id == null) flowOf(emptyList()) else draftRepository.uploads(id)
            }.collect { uploads -> mutableState.update { it.copy(draftUploads = uploads) } }
        }
        viewModelScope.launch {
            state.map { it.selectedCollectionId }.distinctUntilChanged().flatMapLatest { id ->
                if (id == null) flowOf(emptySet()) else repository.ownedIds(id)
            }.collect { ids -> mutableState.update { it.copy(ownedGameIds = ids) } }
        }
        viewModelScope.launch {
            state.map { it.selectedCollectionId }.distinctUntilChanged().flatMapLatest { id ->
                if (id == null) flowOf(emptyList()) else repository.members(id)
            }.collect { members -> mutableState.update { it.copy(members = members) } }
        }
        viewModelScope.launch {
            state.map { it.catalogQuery }.distinctUntilChanged().flatMapLatest(repository::search)
                .collect { games ->
                    mutableState.update { it.copy(games = games) }
                    val thumbnails = loadFrontThumbnails(games)
                    mutableState.update { it.copy(gameListThumbnails = thumbnails) }
                }
        }
        viewModelScope.launch {
            state.map { it.selectedCollectionId to it.collectionQuery }.distinctUntilChanged().flatMapLatest { (id, query) ->
                if (id == null) flowOf(emptyList()) else repository.collectionGames(id, query)
            }.collect { games ->
                mutableState.update { it.copy(collectionGames = games) }
                val thumbnails = loadFrontThumbnails(games)
                mutableState.update { it.copy(gameListThumbnails = thumbnails) }
            }
        }
        viewModelScope.launch {
            state.map { it.selectedGameId }.distinctUntilChanged().flatMapLatest { id ->
                if (id == null) flowOf(null) else repository.game(id)
            }.collect { game -> if (game != null) mutableState.update { it.copy(selectedGame = game) } }
        }
    }

    fun beginLogin(onIntentReady: (Intent) -> Unit) {
        setWorking()
        viewModelScope.launch {
            runCatching { session.authorizationIntent() }
                .onSuccess { onIntentReady(it) }
                .onFailure { showError(it.safeMessage(), AppPage.SignIn) }
        }
    }

    fun completeLogin(data: Intent?) {
        if (data == null) {
            showError("Sign-in was cancelled.", AppPage.SignIn)
            return
        }
        viewModelScope.launch {
            runCatching { session.completeAuthorization(data) }
                .onSuccess { refreshSessionInternal() }
                .onFailure { showError(it.safeMessage(), AppPage.SignIn) }
        }
    }

    fun refreshSession() = viewModelScope.launch { refreshSessionInternal() }

    fun requestSync() {
        if (session.isAuthorized) {
            SyncScheduler.ensurePeriodic(app)
            SyncScheduler.enqueue(app)
        }
    }

    fun onboard(displayName: String, username: String, collectionName: String) {
        if (displayName.isBlank() || username.trim().removePrefix("#").length !in 3..30 || collectionName.isBlank()) {
            showMessage("Enter a display name, a username of 3–30 characters, and a collection name.")
            return
        }
        launchAction {
            when (val profile = api.onboard(displayName, username.removePrefix("#"))) {
                is ApiResult.Success -> {
                    repository.cacheProfile(profile.value)
                    if (!activateCurrentDevice()) return@launchAction
                    when (val collection = api.createCollection(deviceId, collectionName)) {
                        is ApiResult.Success -> {
                            api.setDefaultCollection(deviceId, collection.value.id)
                            repository.refreshCollections(deviceId)
                            loadWorkspace(profile.value.copy(hasActiveDevice = true, defaultCollectionId = collection.value.id))
                        }
                        else -> fail(collection)
                    }
                }
                else -> fail(profile)
            }
        }
    }

    fun showHome() = mutableState.update { it.copy(page = AppPage.Home, message = null) }

    fun navigateBack(): Boolean {
        val destination = when (state.value.page) {
            AppPage.Game ->
                state.value.gameReturnPage

            AppPage.CorrectionEditor ->
                AppPage.Game

            AppPage.DraftEditor,
            AppPage.ServerSubmissionEditor ->
                AppPage.Drafts

            AppPage.AdminSubmission,
            AppPage.AdminCorrection ->
                AppPage.Admin

            AppPage.Profile,
            AppPage.Settings,
            AppPage.Collection,
            AppPage.Invitations,
            AppPage.Notifications,
            AppPage.Corrections,
            AppPage.Drafts,
            AppPage.Admin ->
                AppPage.Home

            AppPage.Home,
            AppPage.Library,
            AppPage.Catalog,
            AppPage.Scanner,
            AppPage.SignIn,
            AppPage.Loading,
            AppPage.Onboarding ->
                null
        }

        if (destination == null) {
            return false
        }

        mutableState.update {
            it.copy(
                page = destination,
                message = null,
            )
        }

        return true
    }

    fun showLibrary() {
        val collectionId = state.value.selectedCollectionId
            ?: return showMessage("Create or select a collection first.")
        mutableState.update { it.copy(page = AppPage.Library, message = null) }
        launchAction {
            when (val result = repository.refreshOwned(deviceId, collectionId)) {
                is ApiResult.Success -> mutableState.update {
                    it.copy(page = AppPage.Library, working = false, message = null)
                }
                is ApiResult.NetworkError -> mutableState.update {
                    it.copy(page = AppPage.Library, working = false, message = "Showing saved collection games offline.")
                }
                else -> fail(result)
            }
        }
    }

    fun searchCollection(query: String) = mutableState.update {
        it.copy(collectionQuery = query, page = AppPage.Library, message = null)
    }

    fun showProfile() = mutableState.update { it.copy(page = AppPage.Profile, message = null) }

    fun showSettings() = mutableState.update {
        it.copy(page = AppPage.Settings, message = null, recentDiagnostics = AppDiagnostics(app).recent())
    }

    fun showAdmin() = launchAction { refreshAdminQueue(showPage = true) }

    fun openAdminSubmission(gameId: String) {
        val submission = state.value.adminSubmissions.firstOrNull { it.game.id == gameId }
            ?: return showMessage("That submission is no longer pending.")
        launchAction {
            mutableState.update {
                it.copy(
                    page = AppPage.AdminSubmission,
                    selectedAdminSubmission = submission,
                    selectedGameImages = emptyMap(),
                    message = null,
                )
            }
            val images = loadGameThumbnails(gameId)
            mutableState.update { it.copy(selectedGameImages = images, working = false) }
        }
    }

    fun moderateAdminSubmission(decision: AdminModerationDecision, comment: String?) {
        val submission = state.value.selectedAdminSubmission ?: return
        if (decision != AdminModerationDecision.Approve && comment.isNullOrBlank()) {
            showMessage("Enter a comment for the submitter.")
            return
        }
        launchAction {
            when (val result = api.moderateAdminSubmission(
                deviceId, submission.game.id, decision, submission.game.revision, comment,
            )) {
                is ApiResult.Success -> {
                    when (val queue = api.listAdminSubmissions(deviceId)) {
                        is ApiResult.Success -> mutableState.update {
                            it.copy(
                                page = AppPage.Admin,
                                isAdministrator = true,
                                adminSubmissions = queue.value,
                                selectedAdminSubmission = null,
                                working = false,
                                message = when (decision) {
                                    AdminModerationDecision.Approve -> "Game approved."
                                    AdminModerationDecision.NeedsChanges -> "Changes requested."
                                    AdminModerationDecision.Reject -> "Submission rejected."
                                },
                            )
                        }
                        else -> fail(queue)
                    }
                }
                else -> fail(result)
            }
        }
    }

    fun updateProfile(displayName: String, username: String) = launchAction {
        when (val result = api.updateProfile(deviceId, displayName, username.removePrefix("#"))) {
            is ApiResult.Success -> {
                repository.cacheProfile(result.value)
                mutableState.update { it.copy(page = AppPage.Home, working = false, message = "Profile updated.") }
            }
            else -> fail(result)
        }
    }

    fun createCollection(name: String) {
        if (name.isBlank()) return showMessage("Enter a collection name.")
        launchAction {
            when (val result = api.createCollection(deviceId, name)) {
                is ApiResult.Success -> {
                    api.setDefaultCollection(deviceId, result.value.id)
                    repository.refreshCollections(deviceId)
                    reloadWorkspace(result.value.id, "Collection created.")
                }
                else -> fail(result)
            }
        }
    }

    fun selectCollection(collectionId: String) = launchAction {
        when (val result = api.setDefaultCollection(deviceId, collectionId)) {
            is ApiResult.Success -> {
                state.value.profile?.copy(defaultCollectionId = collectionId)?.let { repository.cacheProfile(it) }
                val (owned, wishlist) = libraryIds(collectionId)
                mutableState.update {
                    it.copy(
                        selectedCollectionId = collectionId,
                        profile = it.profile?.copy(defaultCollectionId = collectionId),
                        ownedGameIds = owned,
                        wishlistGameIds = wishlist,
                        working = false,
                        message = null,
                    )
                }
            }
            else -> fail(result)
        }
    }

    fun manageSelectedCollection() {
        val collection = state.value.selectedCollection ?: return
        launchAction {
            when (val result = repository.refreshMembers(deviceId, collection.id)) {
                is ApiResult.Success -> mutableState.update {
                    it.copy(page = AppPage.Collection, searchResults = emptyList(), working = false, message = null)
                }
                is ApiResult.NetworkError -> mutableState.update {
                    it.copy(page = AppPage.Collection, working = false, message = "Showing saved members offline.")
                }
                else -> fail(result)
            }
        }
    }

    fun renameCollection(name: String) {
        val collection = state.value.selectedCollection ?: return
        launchAction {
            when (val result = api.renameCollection(deviceId, collection.id, name)) {
                is ApiResult.Success -> reloadWorkspace(collection.id, "Collection renamed.")
                else -> fail(result)
            }
        }
    }

    fun deleteSelectedCollection() {
        val collection = state.value.selectedCollection ?: return
        launchAction {
            when (val result = api.deleteCollection(deviceId, collection.id)) {
                is ApiResult.Success -> {
                    repository.deleteCollectionLocally(collection.id)
                    reloadWorkspace(message = "Collection deleted.")
                }
                else -> fail(result)
            }
        }
    }

    fun showInvitations() = launchAction {
        when (val result = repository.refreshInvitations(deviceId)) {
            is ApiResult.Success -> mutableState.update { it.copy(page = AppPage.Invitations, working = false, message = null) }
            is ApiResult.NetworkError -> mutableState.update {
                it.copy(page = AppPage.Invitations, working = false, message = "Showing saved invitations offline.")
            }
            else -> fail(result)
        }
    }

    fun showNotifications() = launchAction {
        when (val result = repository.refreshNotifications(deviceId)) {
            is ApiResult.Success -> mutableState.update { it.copy(page = AppPage.Notifications, working = false, message = null) }
            is ApiResult.NetworkError -> mutableState.update { it.copy(page = AppPage.Notifications, working = false, message = "Showing saved notifications offline.") }
            else -> fail(result)
        }
    }

    fun openNotification(notification: LocalNotification) {
        viewModelScope.launch {
            repository.markNotificationReadLocally(notification.id)
            NotificationReadScheduler.enqueue(app, notification.id)
        }
        val payload = runCatching { JSONObject(notification.payloadJson) }.getOrElse { JSONObject() }
        val target = when (notification.type) {
            "CollectionInvitation" -> DeepLinkTarget.Invitation(payload.optString("invitationId").takeIf(String::isNotBlank))
            "InvitationAccepted", "InvitationDeclined", "CollectionMembershipChanged", "CollectionMembershipRemoved" ->
                payload.optString("collectionId").takeIf(String::isNotBlank)?.let(DeepLinkTarget::Collection)
            "GameSubmissionApproved", "GameSubmissionNeedsChanges", "GameSubmissionRejected",
            "SuggestedEditApproved", "SuggestedEditRejected" -> payload.optString("gameId").takeIf(String::isNotBlank)?.let(DeepLinkTarget::Game)
            else -> null
        }
        if (target != null) applyDeepLink(target)
    }

    fun markAllNotificationsRead() = viewModelScope.launch {
        repository.markAllNotificationsReadLocally()
        NotificationReadScheduler.enqueue(app, NotificationReadWorker.ALL)
        showMessage("All notifications marked as read.")
    }

    fun showCorrections() = launchAction {
        when (val result = repository.refreshChangeRequests(deviceId)) {
            is ApiResult.Success -> mutableState.update { it.copy(page = AppPage.Corrections, working = false, message = null) }
            is ApiResult.NetworkError -> mutableState.update { it.copy(page = AppPage.Corrections, working = false, message = "Showing saved correction requests offline.") }
            else -> fail(result)
        }
    }

    fun startCorrection() {
        val game = state.value.selectedGame ?: return
        if (!game.moderationStatus.equals("Approved", true)) return showMessage("Only approved catalog games can be corrected.")
        mutableState.update { it.copy(page = AppPage.CorrectionEditor, message = null) }
    }

    fun submitCorrection(form: CorrectionForm, frontImage: Uri?, backImage: Uri?) {
        val game = state.value.selectedGame ?: return
        val patch = correctionPatch(game, form)
        if (patch == null && frontImage == null && backImage == null) return showMessage("Change at least one field or image before submitting.")
        if (form.title.isBlank()) return showMessage("A game title is required.")
        if (!validPositiveRange(form.minimumPlayers, form.maximumPlayers) || !validPositiveRange(form.minimumPlayingTimeMinutes, form.maximumPlayingTimeMinutes)) {
            return showMessage("Minimum values must not exceed maximum values, and all ranges must be positive.")
        }
        if (form.minimumAge != null && form.minimumAge !in 0..99) return showMessage("Minimum age must be between 0 and 99.")
        launchAction {
            when (val result = api.createChangeRequest(deviceId, game.id, patch ?: GameChangePatch(), frontImage != null || backImage != null)) {
                is ApiResult.Success -> {
                    repository.cacheChangeRequest(result.value)
                    for ((type, uri) in listOf("Front" to frontImage, "Back" to backImage)) {
                        if (uri == null) continue
                        val upload = readCorrectionImage(uri)?.let { (contentType, bytes) ->
                            api.uploadChangeRequestImage(deviceId, result.value.id, type, contentType, bytes)
                        } ?: return@launchAction showError("The $type image could not be read or is larger than 10 MB.")
                        if (upload !is ApiResult.Success) return@launchAction fail(upload)
                    }
                    when (val refreshed = api.listMyChangeRequests(deviceId)) {
                        is ApiResult.Success -> refreshed.value.forEach { repository.cacheChangeRequest(it) }
                        else -> Unit
                    }
                    mutableState.update { it.copy(page = AppPage.Corrections, working = false, message = "Correction submitted for review.") }
                }
                else -> fail(result)
            }
        }
    }

    fun retrySync() {
        SyncScheduler.enqueue(app)
        showMessage("Synchronization queued.")
    }

    fun rebuildSyncState() = viewModelScope.launch {
        database.syncDao().clearScopes()
        SyncScheduler.enqueue(app)
        showMessage("A full synchronization has been queued.")
    }

    fun clearCache() = viewModelScope.launch {
        if (state.value.pendingMutationCount > 0) {
            showMessage("Synchronize queued collection or wishlist changes before clearing cached content.")
            return@launch
        }
        repository.clearCachedContent()
        SyncScheduler.enqueue(app)
        showMessage("Cached content cleared. A fresh synchronization has been queued.")
    }

    fun revokeDevice() = launchAction {
        when (val result = api.revokeDevice(deviceId)) {
            is ApiResult.Success -> signOutWithMessage("This device was revoked and local account data was cleared.")
            else -> fail(result)
        }
    }

    fun handleDeepLink(uri: Uri?) {
        val target = parseDeepLink(uri) ?: return
        if (!session.isAuthorized || state.value.profile == null) pendingDeepLink = target else applyDeepLink(target)
    }

    private fun applyDeepLink(target: DeepLinkTarget) {
        when (target) {
            is DeepLinkTarget.Invitation -> showInvitations()
            is DeepLinkTarget.Game -> openGame(target.id)
            is DeepLinkTarget.Collection -> {
                if (state.value.collections.none { it.id == target.id }) {
                    showMessage("This collection is not currently shared with you. Check invitations.")
                    showInvitations()
                } else {
                    mutableState.update { it.copy(selectedCollectionId = target.id) }
                    manageSelectedCollection()
                }
            }
        }
    }

    private fun applyPendingDeepLink() {
        pendingDeepLink?.let { target -> pendingDeepLink = null; applyDeepLink(target) }
    }

    fun searchGames(query: String) = launchAction {
        mutableState.update { it.copy(page = AppPage.Catalog, catalogQuery = query.trim(), working = true, message = null) }
        when (val result = repository.refreshCatalog(deviceId, query)) {
            is ApiResult.Success -> {
                val (owned, wishlist) = libraryIds(state.value.selectedCollectionId)
                mutableState.update {
                    it.copy(
                        page = AppPage.Catalog,
                        catalogQuery = query.trim(),
                        ownedGameIds = owned,
                        wishlistGameIds = wishlist,
                        working = false,
                        message = if (result.value.isEmpty()) "No games found." else null,
                    )
                }
            }
            is ApiResult.NetworkError -> mutableState.update {
                it.copy(page = AppPage.Catalog, catalogQuery = query.trim(), working = false, message = "Showing saved catalog results offline.")
            }
            else -> fail(result)
        }
    }

    fun showScanner() = mutableState.update { it.copy(page = AppPage.Scanner, message = null) }

    fun lookupBarcode(barcode: String) {
        val normalized = normalizeBarcode(barcode)
            ?: return showMessage("Enter a barcode containing 8–14 digits.")
        launchAction {
            val cachedId = repository.cachedGameByBarcode(normalized)
            if (cachedId != null) {
                mutableState.update {
                    it.copy(
                        page = AppPage.Game,
                        gameReturnPage = AppPage.Scanner,
                        selectedGameId = cachedId,
                        selectedGameImages = emptyMap(),
                        working = false,
                        message = "Found instantly in the saved catalog.",
                    )
                }
                val images = loadGameThumbnails(cachedId)
                mutableState.update { it.copy(selectedGameImages = images) }
                return@launchAction
            }
            when (val result = repository.refreshGameByBarcode(deviceId, normalized)) {
                is ApiResult.Success -> showGame(result.value, AppPage.Scanner)
                is ApiResult.Error -> if (result.statusCode == 404) startDraftForBarcode(normalized) else fail(result)
                else -> fail(result)
            }
        }
    }

    fun openGame(gameId: String) = launchAction {
        val returnPage = if (state.value.page == AppPage.Library) AppPage.Library else AppPage.Catalog
        mutableState.update {
            it.copy(
                page = AppPage.Game,
                gameReturnPage = returnPage,
                selectedGameId = gameId,
                selectedGameImages = emptyMap(),
                working = true,
                message = null,
            )
        }
        when (val result = repository.refreshGame(deviceId, gameId)) {
            is ApiResult.Success -> showGame(result.value, returnPage)
            is ApiResult.NetworkError -> mutableState.update { it.copy(working = false, message = "Showing saved game details offline.") }
            else -> fail(result)
        }
    }

    fun backFromGame() = mutableState.update { it.copy(page = it.gameReturnPage, message = null) }

    fun showDrafts() = launchAction {
        when (val result = api.listMySubmissions(deviceId)) {
            is ApiResult.Success -> mutableState.update {
                it.copy(
                    page = AppPage.Drafts,
                    selectedDraftId = null,
                    selectedServerSubmission = null,
                    serverSubmissions = result.value,
                    working = false,
                    message = null,
                )
            }
            is ApiResult.NetworkError -> mutableState.update {
                it.copy(page = AppPage.Drafts, selectedDraftId = null, working = false, message = "Server submissions could not be refreshed.")
            }
            else -> fail(result)
        }
        loadReferenceData()
    }

    fun openServerSubmission(gameId: String) = mutableState.update { current ->
        val submission = current.serverSubmissions.firstOrNull { it.game.id == gameId }
        if (submission == null) current.copy(message = "That server submission is no longer available.")
        else current.copy(page = AppPage.ServerSubmissionEditor, selectedServerSubmission = submission, message = null)
    }

    fun saveServerSubmission(form: ServerSubmissionForm) {
        val current = state.value.selectedServerSubmission ?: return
        if (form.title.isBlank()) return showMessage("Enter a game title.")
        launchAction {
            val request = com.gamecollector.core.network.GameSubmissionDraft(
                title = form.title,
                description = form.description,
                publisher = form.publisher,
                releaseYear = form.releaseYear,
                minimumPlayers = form.minimumPlayers,
                maximumPlayers = form.maximumPlayers,
                minimumAge = form.minimumAge,
                minimumPlayingTimeMinutes = form.minimumPlayingTimeMinutes,
                maximumPlayingTimeMinutes = form.maximumPlayingTimeMinutes,
                barcodes = form.barcodes,
                languageIds = form.languageIds.toList(),
                tagIds = form.tagIds.toList(),
                expectedRevision = current.game.revision,
            )
            when (val result = api.updateSubmission(deviceId, current.game.id, request)) {
                is ApiResult.Success -> mutableState.update { state ->
                    state.copy(
                        serverSubmissions = state.serverSubmissions.map { if (it.game.id == result.value.game.id) result.value else it },
                        selectedServerSubmission = result.value,
                        working = false,
                        message = "Server draft updated.",
                    )
                }
                else -> fail(result)
            }
        }
    }

    fun deleteServerSubmission(gameId: String) = launchAction {
        when (val result = api.deleteSubmission(deviceId, gameId)) {
            is ApiResult.Success -> mutableState.update {
                it.copy(
                    page = AppPage.Drafts,
                    serverSubmissions = it.serverSubmissions.filterNot { submission -> submission.game.id == gameId },
                    selectedServerSubmission = null,
                    working = false,
                    message = "Server draft permanently deleted.",
                )
            }
            else -> fail(result)
        }
    }

    fun createDraft() = launchAction {
        val draft = draftRepository.create(null)
        mutableState.update { it.copy(page = AppPage.DraftEditor, selectedDraftId = draft.id, working = false, message = null) }
        loadReferenceData()
    }

    fun openDraft(id: String) {
        mutableState.update { it.copy(page = AppPage.DraftEditor, selectedDraftId = id, message = null) }
        loadReferenceData()
    }

    fun saveDraft(form: DraftForm, step: Int) = launchAction {
        val draft = state.value.selectedDraft ?: return@launchAction showMessage("The draft is no longer available.")
        val updated = draft.withForm(form, step)
        draftRepository.save(updated)
        mutableState.update { it.copy(working = false, message = "Draft saved on this device.") }
    }

    fun attachDraftImage(kind: String, uri: Uri) = launchAction {
        val draft = state.value.selectedDraft ?: return@launchAction showMessage("The draft is no longer available.")
        val stored = runCatching {
            withContext(Dispatchers.IO) { DraftMediaFiles.persist(app, uri, draft.id, kind) }
        }.getOrElse { return@launchAction showError(it.message ?: "The image could not be saved.") }
        draftRepository.attachMedia(draft.id, stored.uri, kind, stored.contentType, stored.size)
        mutableState.update { it.copy(working = false, message = "$kind image saved locally.") }
    }

    fun submitDraft(form: DraftForm) = launchAction {
        val draft = state.value.selectedDraft ?: return@launchAction showMessage("The draft is no longer available.")
        validateDraftForm(form)?.let { return@launchAction showMessage(it) }
        val kinds = state.value.draftUploads.map { it.kind }.toSet()
        if ("Front" !in kinds || "Back" !in kinds) return@launchAction showMessage("Add both front and back images first.")
        draftRepository.save(draft.withForm(form, 2))
        draftRepository.requestSubmission(draft.id)
        DraftUploadWorker.enqueue(app, draft.id)
        mutableState.update { it.copy(working = false, message = "Submission queued and will resume automatically when online.") }
    }

    fun deleteDraft(id: String) = launchAction {
        draftRepository.delete(id)
        withContext(Dispatchers.IO) { DraftMediaFiles.deleteDraft(app, id) }
        mutableState.update { it.copy(page = AppPage.Drafts, selectedDraftId = null, working = false, message = "Draft removed.") }
    }

    fun setOwned(owned: Boolean) {
        val collection = state.value.selectedCollection ?: return showMessage("Select a collection first.")
        val game = state.value.selectedGame ?: return
        launchAction {
            repository.enqueueOwnershipMutation(collection.id, game.id, owned)
            SyncScheduler.enqueue(app)
            mutableState.update {
                it.copy(working = false, message = if (owned) "Added to ${collection.name}; synchronization queued." else "Removed from ${collection.name}; synchronization queued.")
            }
        }
    }

    fun setWishlisted(wishlisted: Boolean) {
        val game = state.value.selectedGame ?: return
        launchAction {
            repository.enqueueWishlistMutation(game.id, wishlisted)
            SyncScheduler.enqueue(app)
            mutableState.update {
                it.copy(working = false, message = if (wishlisted) "Added to your wishlist; synchronization queued." else "Removed from your wishlist; synchronization queued.")
            }
        }
    }

    fun respondToInvitation(invitationId: String, accept: Boolean) = launchAction {
        when (val result = api.respondToInvitation(deviceId, invitationId, accept)) {
            is ApiResult.Success -> {
                repository.removeInvitationLocally(invitationId)
                reloadWorkspace(message = if (accept) "Invitation accepted." else "Invitation declined.")
            }
            else -> fail(result)
        }
    }

    fun searchUsers(query: String) {
        if (query.trim().removePrefix("#").length < 2) return showMessage("Enter at least two username characters.")
        launchAction {
            when (val result = api.searchUsers(deviceId, query)) {
                is ApiResult.Success -> mutableState.update { it.copy(searchResults = result.value, working = false, message = null) }
                else -> fail(result)
            }
        }
    }

    fun invite(userId: String, role: CollectionRole) {
        val collection = state.value.selectedCollection ?: return
        launchAction {
            when (val result = api.invite(deviceId, collection.id, userId, role)) {
                is ApiResult.Success -> mutableState.update { it.copy(searchResults = emptyList(), working = false, message = "Invitation sent.") }
                else -> fail(result)
            }
        }
    }

    fun updateMember(userId: String, role: CollectionRole) = memberAction {
        api.updateMember(deviceId, it.id, userId, role)
    }

    fun removeMember(userId: String) = memberAction { api.removeMember(deviceId, it.id, userId) }

    fun transferOwnership(userId: String) = memberAction { api.transferOwnership(deviceId, it.id, userId) }

    fun signOut() {
        signOutWithMessage("Local authentication state cleared.")
    }

    private fun signOutWithMessage(message: String) {
        mutableState.value = MainUiState(page = AppPage.Loading, message = "Signing out…")
        viewModelScope.launch {
            session.signOut()
            withContext(Dispatchers.IO) {
                runCatching { WorkManager.getInstance(app).cancelAllWork().result.get() }
                repository.clearAll()
                DraftMediaFiles.clear(app)
            }
            mutableState.value = MainUiState(page = AppPage.SignIn, message = message)
        }
    }

    fun openAdminChangeRequest(id: String) {
        val request = state.value.adminChangeRequests.firstOrNull { it.id == id }
            ?: return showMessage("That correction is no longer pending.")
        launchAction {
            mutableState.update {
                it.copy(page = AppPage.AdminCorrection, selectedAdminChangeRequest = request,
                    selectedCorrectionImages = emptyMap(), message = null)
            }
            val thumbnails = linkedMapOf<String, ByteArray>()
            for (image in request.proposedImages) {
                val result = api.downloadChangeRequestThumbnail(deviceId, image.id)
                if (result is ApiResult.Success) thumbnails[image.imageType] = result.value
            }
            mutableState.update { it.copy(selectedCorrectionImages = thumbnails, working = false) }
        }
    }

    fun reviewAdminChangeRequest(approve: Boolean, comment: String?) {
        val request = state.value.selectedAdminChangeRequest ?: return
        if (!approve && comment.isNullOrBlank()) return showMessage("Enter a rejection reason.")
        launchAction {
            when (val result = api.reviewAdminChangeRequest(
                deviceId, request.id, approve, request.gameRevision, comment,
            )) {
                is ApiResult.Success -> {
                    val queue = api.listAdminChangeRequests(deviceId)
                    mutableState.update {
                        it.copy(
                            page = AppPage.Admin,
                            adminChangeRequests = (queue as? ApiResult.Success)?.value
                                ?: it.adminChangeRequests.filterNot { item -> item.id == request.id },
                            selectedAdminChangeRequest = null,
                            selectedCorrectionImages = emptyMap(),
                            working = false,
                            message = if (approve) "Correction approved." else "Correction rejected.",
                        )
                    }
                }
                else -> fail(result)
            }
        }
    }

    private suspend fun readCorrectionImage(uri: Uri): Pair<String, ByteArray>? = withContext(Dispatchers.IO) {
        val contentType = app.contentResolver.getType(uri)?.lowercase()
            ?.takeIf { it in setOf("image/jpeg", "image/png", "image/webp") } ?: return@withContext null
        val bytes = app.contentResolver.openInputStream(uri)?.use { it.readBytes() } ?: return@withContext null
        bytes.takeIf { it.isNotEmpty() && it.size <= 10 * 1024 * 1024 }?.let { contentType to it }
    }

    fun deleteAdminGame() {
        val game = state.value.selectedGame ?: return
        if (!state.value.isAdministrator) return showMessage("Administrator access is required.")
        val returnPage = state.value.gameReturnPage
        launchAction {
            when (val result = api.deleteAdminGame(deviceId, game.id)) {
                is ApiResult.Success -> {
                    repository.deleteGameLocally(game.id)
                    mutableState.update {
                        it.copy(
                            page = returnPage,
                            selectedGameId = null,
                            selectedGame = null,
                            selectedGameImages = emptyMap(),
                            gameListThumbnails = it.gameListThumbnails - game.id,
                            working = false,
                            message = "${game.title} was permanently deleted.",
                        )
                    }
                }
                else -> fail(result)
            }
        }
    }

    private suspend fun refreshSessionInternal() {
        mutableState.update { it.copy(page = AppPage.Loading, working = true, message = null) }
        when (val result = repository.refreshProfile()) {
            is ApiResult.Success -> {
                if (activateCurrentDevice()) loadWorkspace(result.value.copy(hasActiveDevice = true))
            }
            is ApiResult.Error -> if (result.statusCode == 404) {
                mutableState.update { it.copy(page = AppPage.Onboarding, working = false, message = null) }
            } else fail(result)
            is ApiResult.NetworkError -> {
                val cached = repository.cachedProfile()
                if (cached != null) loadWorkspace(cached, refresh = false, offline = true) else fail(result)
            }
            else -> fail(result)
        }
    }

    private suspend fun activateCurrentDevice(): Boolean {
        val token = FirebaseBootstrap.token(app) ?: if (BuildConfig.DEBUG) {
            "debug-deferred-$deviceId"
        } else {
            showError("A push token is required before this release build can activate the device.")
            return false
        }
        return when (val result = api.activateDevice(deviceId, token)) {
            is ApiResult.Success -> true
            else -> { fail(result); false }
        }
    }

    private suspend fun loadWorkspace(profile: UserProfile, refresh: Boolean = true, offline: Boolean = false) {
        repository.cacheProfile(profile)
        val result = if (refresh) repository.refreshCollections(deviceId) else ApiResult.Success(repository.cachedCollections())
        when (result) {
            is ApiResult.Success -> {
                val selected = profile.defaultCollectionId?.takeIf { id -> result.value.any { it.id == id } }
                    ?: result.value.firstOrNull()?.id
                val (owned, wishlist) = libraryIds(selected)
                mutableState.value = MainUiState(
                    page = if (selected == null) AppPage.Home else AppPage.Library,
                    profile = profile,
                    collections = result.value,
                    selectedCollectionId = selected,
                    ownedGameIds = owned,
                    wishlistGameIds = wishlist,
                    message = if (offline) "Showing saved data offline." else null,
                )
                if (!offline) refreshAdminQueue(showPage = false)
                requestSync()
                applyPendingDeepLink()
            }
            is ApiResult.NetworkError -> {
                val cached = repository.cachedCollections()
                if (cached.isNotEmpty()) {
                    val selected = profile.defaultCollectionId?.takeIf { id -> cached.any { it.id == id } } ?: cached.first().id
                    val (owned, wishlist) = libraryIds(selected, refresh = false)
                    mutableState.value = MainUiState(
                        page = AppPage.Library,
                        profile = profile,
                        collections = cached,
                        selectedCollectionId = selected,
                        ownedGameIds = owned,
                        wishlistGameIds = wishlist,
                        message = "Showing saved data offline.",
                    )
                    applyPendingDeepLink()
                } else fail(result)
            }
            else -> fail(result)
        }
    }

    private suspend fun reloadWorkspace(preferredId: String? = null, message: String? = null) {
        val profileResult = repository.refreshProfile()
        val collectionsResult = repository.refreshCollections(deviceId)
        if (profileResult is ApiResult.Success && collectionsResult is ApiResult.Success) {
            val selected = preferredId?.takeIf { id -> collectionsResult.value.any { it.id == id } }
                ?: profileResult.value.defaultCollectionId?.takeIf { id -> collectionsResult.value.any { it.id == id } }
                ?: collectionsResult.value.firstOrNull()?.id
            val (owned, wishlist) = libraryIds(selected)
            mutableState.value = MainUiState(
                page = if (selected == null) AppPage.Home else AppPage.Library,
                profile = profileResult.value,
                collections = collectionsResult.value,
                selectedCollectionId = selected,
                ownedGameIds = owned,
                wishlistGameIds = wishlist,
                message = message,
            )
            refreshAdminQueue(showPage = false)
        } else fail(if (profileResult !is ApiResult.Success) profileResult else collectionsResult)
    }

    private suspend fun refreshAdminQueue(showPage: Boolean) {
        when (val result = api.listAdminSubmissions(deviceId)) {
            is ApiResult.Success -> {
                val correctionResult = api.listAdminChangeRequests(deviceId)
                if (correctionResult !is ApiResult.Success && showPage) return fail(correctionResult)
                mutableState.update {
                    it.copy(
                    page = if (showPage) AppPage.Admin else it.page,
                    isAdministrator = true,
                    adminSubmissions = result.value,
                    adminChangeRequests = (correctionResult as? ApiResult.Success)?.value ?: it.adminChangeRequests,
                    selectedAdminSubmission = if (showPage) null else it.selectedAdminSubmission,
                    selectedAdminChangeRequest = if (showPage) null else it.selectedAdminChangeRequest,
                    working = false,
                    message = null,
                )
                }
            }
            is ApiResult.Error -> if (result.statusCode == 403) {
                mutableState.update {
                    it.copy(
                        page = if (showPage) AppPage.Home else it.page,
                        isAdministrator = false,
                        adminSubmissions = emptyList(),
                        adminChangeRequests = emptyList(),
                        selectedAdminSubmission = null,
                        selectedAdminChangeRequest = null,
                        working = false,
                        message = if (showPage) "Your current login does not have the gamecollector-admin role." else it.message,
                    )
                }
            } else if (showPage) fail(result) else mutableState.update { it.copy(working = false) }
            is ApiResult.NetworkError -> if (showPage) fail(result) else mutableState.update { it.copy(working = false) }
            ApiResult.SignedOut -> if (showPage) fail(result) else Unit
        }
    }

    private suspend fun showGame(game: GameDetails, returnPage: AppPage = AppPage.Catalog) {
        val (owned, wishlist) = libraryIds(state.value.selectedCollectionId)
        val images = loadGameThumbnails(game.id)
        mutableState.update {
            it.copy(
                page = AppPage.Game,
                gameReturnPage = returnPage,
                selectedGameId = game.id,
                selectedGame = game,
                selectedGameImages = images,
                ownedGameIds = owned,
                wishlistGameIds = wishlist,
                working = false,
                message = null,
            )
        }
    }

    private suspend fun loadGameThumbnails(gameId: String): Map<String, ByteArray> {
        val media = api.listGameMedia(deviceId, gameId)
        if (media !is ApiResult.Success) return emptyMap()
        val result = linkedMapOf<String, ByteArray>()
        for (item in media.value.filter { it.status == "Ready" }) {
            val thumbnail = api.downloadMediaThumbnail(deviceId, item.id)
            if (thumbnail is ApiResult.Success) result[item.imageType] = thumbnail.value
        }
        return result
    }

    private suspend fun loadFrontThumbnails(games: List<GameSummary>): Map<String, ByteArray> {
        val result = state.value.gameListThumbnails.toMutableMap()
        for (game in games) {
            if (game.id in result) continue
            val mediaId = game.frontImageId ?: continue
            val thumbnail = api.downloadMediaThumbnail(deviceId, mediaId)
            if (thumbnail is ApiResult.Success) result[game.id] = thumbnail.value
        }
        return result
    }

    private suspend fun startDraftForBarcode(barcode: String) {
        when (val candidate = api.lookupProduct(deviceId, barcode)) {
            is ApiResult.Success -> {
                candidate.value.existingGameId?.let { id -> openGame(id); return }
                val draft = draftRepository.create(barcode, candidate.value)
                mutableState.update {
                    it.copy(page = AppPage.DraftEditor, selectedDraftId = draft.id, working = false, message = "Review the suggested details before submitting.")
                }
            }
            is ApiResult.Error -> if (candidate.statusCode == 404) {
                val draft = draftRepository.create(barcode)
                mutableState.update { it.copy(page = AppPage.DraftEditor, selectedDraftId = draft.id, working = false, message = "No metadata suggestion was found. Enter the game details manually.") }
            } else fail(candidate)
            is ApiResult.NetworkError -> {
                val draft = draftRepository.create(barcode)
                mutableState.update { it.copy(page = AppPage.DraftEditor, selectedDraftId = draft.id, working = false, message = "Draft created offline. Metadata can be entered manually.") }
            }
            ApiResult.SignedOut -> fail(candidate)
        }
        loadReferenceData()
    }

    private fun loadReferenceData() {
        viewModelScope.launch {
            val languages = api.listLanguages(deviceId)
            val tags = api.listTags(deviceId)
            mutableState.update {
                it.copy(
                    languages = (languages as? ApiResult.Success)?.value ?: it.languages,
                    tags = (tags as? ApiResult.Success)?.value ?: it.tags,
                )
            }
        }
    }

    private suspend fun libraryIds(collectionId: String?, refresh: Boolean = true): Pair<Set<String>, Set<String>> {
        if (refresh) {
            if (collectionId != null) repository.refreshOwned(deviceId, collectionId)
            repository.refreshWishlist(deviceId)
        }
        val owned = if (collectionId == null) emptySet() else repository.ownedIds(collectionId).first()
        val wishlist = repository.wishlistIds.first()
        return owned to wishlist
    }

    private fun memberAction(action: suspend (CollectionSummary) -> ApiResult<*>) {
        val collection = state.value.selectedCollection ?: return
        launchAction {
            when (val result = action(collection)) {
                is ApiResult.Success -> when (val members = repository.refreshMembers(deviceId, collection.id)) {
                    is ApiResult.Success -> mutableState.update { it.copy(working = false, message = "Collection members updated.") }
                    else -> fail(members)
                }
                else -> fail(result)
            }
        }
    }

    private fun launchAction(action: suspend () -> Unit) {
        setWorking()
        viewModelScope.launch { action() }
    }

    private fun setWorking() = mutableState.update { it.copy(working = true, message = null) }

    private fun showMessage(message: String) = mutableState.update { it.copy(working = false, message = message) }

    private fun showError(message: String, page: AppPage? = null) = mutableState.update {
        it.copy(page = page ?: it.page, working = false, message = message)
    }

    private fun fail(result: ApiResult<*>) {
        when (result) {
            is ApiResult.Error -> {
                AppDiagnostics(app).record("api-${result.statusCode}", result.code ?: "request-failed")
                showError(result.message + (result.referenceId?.let { " Reference: $it" } ?: ""))
            }
            is ApiResult.NetworkError -> {
                AppDiagnostics(app).record("network", result.message)
                showError(result.message)
            }
            ApiResult.SignedOut -> showError("Your session has ended. Sign in again.", AppPage.SignIn)
            is ApiResult.Success -> Unit
        }
    }

    override fun onCleared() {
        session.close()
        super.onCleared()
    }

    private fun Throwable.safeMessage() = message?.take(200) ?: "Authentication failed."
}

enum class AppPage { SignIn, Loading, Onboarding, Home, Library, Profile, Settings, Collection, Invitations, Notifications, Corrections, CorrectionEditor, Catalog, Game, Scanner, Drafts, DraftEditor, ServerSubmissionEditor, Admin, AdminSubmission, AdminCorrection }

data class CorrectionForm(
    val title: String, val description: String?, val publisher: String?, val releaseYear: Int?,
    val minimumPlayers: Int?, val maximumPlayers: Int?, val minimumAge: Int?,
    val minimumPlayingTimeMinutes: Int?, val maximumPlayingTimeMinutes: Int?,
)

fun correctionPatch(game: GameDetails, form: CorrectionForm): GameChangePatch? {
    val patch = GameChangePatch(
        title = form.title.trim().takeIf { it != game.title },
        description = form.description?.trim()?.takeIf { it != game.description.orEmpty() },
        publisher = form.publisher?.trim()?.takeIf { it != game.publisher.orEmpty() },
        releaseYear = form.releaseYear.takeIf { it != game.releaseYear },
        minimumPlayers = form.minimumPlayers.takeIf { it != game.minimumPlayers },
        maximumPlayers = form.maximumPlayers.takeIf { it != game.maximumPlayers },
        minimumAge = form.minimumAge.takeIf { it != game.minimumAge },
        minimumPlayingTimeMinutes = form.minimumPlayingTimeMinutes.takeIf { it != game.minimumPlayingTimeMinutes },
        maximumPlayingTimeMinutes = form.maximumPlayingTimeMinutes.takeIf { it != game.maximumPlayingTimeMinutes },
    )
    return patch.takeIf { listOf(it.title, it.description, it.publisher, it.releaseYear, it.minimumPlayers, it.maximumPlayers,
        it.minimumAge, it.minimumPlayingTimeMinutes, it.maximumPlayingTimeMinutes).any { value -> value != null } }
}

data class DraftForm(
    val title: String,
    val description: String?,
    val publisher: String?,
    val barcode: String?,
    val releaseYear: Int?,
    val minimumPlayers: Int?,
    val maximumPlayers: Int?,
    val minimumAge: Int?,
    val minimumPlayingTimeMinutes: Int?,
    val maximumPlayingTimeMinutes: Int?,
    val languageIds: Set<String>,
    val tagIds: Set<String>,
)

data class ServerSubmissionForm(
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
    val languageIds: Set<String>,
    val tagIds: Set<String>,
)

private fun LocalGameDraft.withForm(form: DraftForm, nextStep: Int) = copy(
    title = form.title.trim(),
    description = form.description?.trim()?.takeIf(String::isNotBlank),
    publisher = form.publisher?.trim()?.takeIf(String::isNotBlank),
    barcode = form.barcode?.filter(Char::isDigit)?.takeIf { it.length in 8..14 },
    releaseYear = form.releaseYear,
    minimumPlayers = form.minimumPlayers,
    maximumPlayers = form.maximumPlayers,
    minimumAge = form.minimumAge,
    minimumPlayingTimeMinutes = form.minimumPlayingTimeMinutes,
    maximumPlayingTimeMinutes = form.maximumPlayingTimeMinutes,
    languageIdsJson = form.languageIds.sorted().toJsonArray(),
    tagIdsJson = form.tagIds.sorted().toJsonArray(),
    step = nextStep.coerceIn(0, 2),
    status = if (status == "Submitted") status else "Local",
    lastError = null,
)

internal fun validateDraftForm(form: DraftForm): String? = when {
    form.title.isBlank() || form.title.trim().length > 200 -> "Enter a valid game title."
    (form.description?.length ?: 0) > 4000 -> "The description is too long."
    (form.publisher?.length ?: 0) > 200 -> "The publisher is too long."
    !form.barcode.isNullOrBlank() && normalizeBarcode(form.barcode) == null -> "Enter a barcode containing 8–14 digits."
    form.releaseYear != null && form.releaseYear !in 1800..2200 -> "Enter a release year from 1800 to 2200."
    !validPositiveRange(form.minimumPlayers, form.maximumPlayers) -> "Enter a valid player range."
    !validPositiveRange(form.minimumPlayingTimeMinutes, form.maximumPlayingTimeMinutes) -> "Enter a valid playing-time range."
    form.minimumAge != null && form.minimumAge !in 0..100 -> "Enter a valid minimum age."
    else -> null
}

private fun validPositiveRange(minimum: Int?, maximum: Int?): Boolean =
    (minimum == null || minimum >= 1) && (maximum == null || maximum >= 1) &&
        (minimum == null || maximum == null || minimum <= maximum)

data class MainUiState(
    val page: AppPage,
    val profile: UserProfile? = null,
    val collections: List<CollectionSummary> = emptyList(),
    val selectedCollectionId: String? = null,
    val members: List<CollectionMember> = emptyList(),
    val invitations: List<CollectionInvitation> = emptyList(),
    val notifications: List<LocalNotification> = emptyList(),
    val changeRequests: List<LocalGameChangeRequest> = emptyList(),
    val syncScopes: List<SyncScopeState> = emptyList(),
    val pendingMutationCount: Int = 0,
    val recentDiagnostics: List<String> = emptyList(),
    val searchResults: List<UserSearchResult> = emptyList(),
    val games: List<GameSummary> = emptyList(),
    val collectionGames: List<GameSummary> = emptyList(),
    val collectionQuery: String = "",
    val gameListThumbnails: Map<String, ByteArray> = emptyMap(),
    val catalogQuery: String = "",
    val selectedGameId: String? = null,
    val selectedGame: GameDetails? = null,
    val selectedGameImages: Map<String, ByteArray> = emptyMap(),
    val gameReturnPage: AppPage = AppPage.Catalog,
    val ownedGameIds: Set<String> = emptySet(),
    val wishlistGameIds: Set<String> = emptySet(),
    val drafts: List<LocalGameDraft> = emptyList(),
    val selectedDraftId: String? = null,
    val selectedDraft: LocalGameDraft? = null,
    val draftUploads: List<PendingMediaUpload> = emptyList(),
    val languages: List<ReferenceData> = emptyList(),
    val tags: List<ReferenceData> = emptyList(),
    val isAdministrator: Boolean = false,
    val adminSubmissions: List<GameSubmission> = emptyList(),
    val selectedAdminSubmission: GameSubmission? = null,
    val adminChangeRequests: List<GameChangeRequest> = emptyList(),
    val selectedAdminChangeRequest: GameChangeRequest? = null,
    val selectedCorrectionImages: Map<String, ByteArray> = emptyMap(),
    val serverSubmissions: List<GameSubmission> = emptyList(),
    val selectedServerSubmission: GameSubmission? = null,
    val working: Boolean = false,
    val message: String? = null,
) {
    val unreadNotificationCount: Int get() = notifications.count { it.readAtUtc == null }
    val selectedCollection: CollectionSummary?
        get() = collections.firstOrNull { it.id == selectedCollectionId }
}
