package com.gamecollector.app

import android.Manifest
import android.graphics.BitmapFactory
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.activity.viewModels
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.Switch
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.key
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.semantics.LiveRegionMode
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.liveRegion
import androidx.compose.ui.semantics.semantics
import androidx.core.content.ContextCompat
import android.app.Activity
import android.os.SystemClock
import android.widget.Toast
import androidx.activity.compose.BackHandler
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.material3.TextFieldLabelScope
import androidx.compose.ui.text.input.KeyboardType
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.gamecollector.core.designsystem.GameCollectorTheme
import com.gamecollector.core.network.CollectionInvitation
import com.gamecollector.core.network.AdminModerationDecision
import com.gamecollector.core.network.CollectionMember
import com.gamecollector.core.network.CollectionRole
import com.gamecollector.core.network.CollectionSummary
import com.gamecollector.core.network.GameDetails
import com.gamecollector.core.network.GameSummary
import com.gamecollector.core.network.GameSubmission
import com.gamecollector.core.network.GameChangeRequest
import com.gamecollector.core.network.UserProfile
import com.gamecollector.core.network.UserSearchResult
import com.gamecollector.core.database.LocalNotification
import org.json.JSONObject

class MainActivity : ComponentActivity() {
    private val viewModel: MainViewModel by viewModels()
    private val authorization =
        registerForActivityResult(ActivityResultContracts.StartActivityForResult()) {
            viewModel.completeLogin(it.data)
        }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val state by viewModel.state.collectAsStateWithLifecycle()
            val context = LocalContext.current
            var lastBackPress by remember { mutableStateOf(0L) }

            BackHandler {
                if (viewModel.navigateBack()) {
                    lastBackPress = 0L
                } else {
                    val now = SystemClock.elapsedRealtime()

                    if (now - lastBackPress < 2_000L) {
                        (context as? Activity)?.finish()
                    } else {
                        lastBackPress = now
                        Toast.makeText(
                            context,
                            "Press back again to exit",
                            Toast.LENGTH_SHORT,
                        ).show()
                    }
                }
            }
            GameCollectorTheme {
                GameCollectorApp(
                    state = state,
                    actions = AppActions(
                        signIn = { viewModel.beginLogin(authorization::launch) },
                        onboard = viewModel::onboard,
                        home = viewModel::showHome,
                        library = viewModel::showLibrary,
                        searchCollection = viewModel::searchCollection,
                        profile = viewModel::showProfile,
                        settings = viewModel::showSettings,
                        admin = viewModel::showAdmin,
                        openAdminSubmission = viewModel::openAdminSubmission,
                        moderateAdminSubmission = viewModel::moderateAdminSubmission,
                        openAdminChangeRequest = viewModel::openAdminChangeRequest,
                        reviewAdminChangeRequest = viewModel::reviewAdminChangeRequest,
                        deleteAdminGame = viewModel::deleteAdminGame,
                        updateProfile = viewModel::updateProfile,
                        createCollection = viewModel::createCollection,
                        selectCollection = viewModel::selectCollection,
                        manageCollection = viewModel::manageSelectedCollection,
                        updateCollection = viewModel::updateCollection,
                        deleteCollection = viewModel::deleteSelectedCollection,
                        invitations = viewModel::showInvitations,
                        notifications = viewModel::showNotifications,
                        openNotification = viewModel::openNotification,
                        markAllNotificationsRead = viewModel::markAllNotificationsRead,
                        deleteNotification = viewModel::deleteNotification,
                        friends = viewModel::showFriends,
                        searchFriends = viewModel::searchFriends,
                        sendFriendRequest = viewModel::sendFriendRequest,
                        respondToFriendRequest = viewModel::respondToFriendRequest,
                        openFriend = viewModel::openFriend,
                        openFriendCollection = viewModel::openFriendCollection,
                        removeFriend = viewModel::removeFriend,
                        corrections = viewModel::showCorrections,
                        startCorrection = viewModel::startCorrection,
                        submitCorrection = viewModel::submitCorrection,
                        respondToInvitation = viewModel::respondToInvitation,
                        searchUsers = viewModel::searchUsers,
                        invite = viewModel::invite,
                        updateMember = viewModel::updateMember,
                        removeMember = viewModel::removeMember,
                        transferOwnership = viewModel::transferOwnership,
                        searchGames = viewModel::searchGames,
                        scan = viewModel::showScanner,
                        lookupBarcode = viewModel::lookupBarcode,
                        openGame = viewModel::openGame,
                        backFromGame = viewModel::backFromGame,
                        setOwned = viewModel::setOwned,
                        setWishlisted = viewModel::setWishlisted,
                        drafts = viewModel::showDrafts,
                        createDraft = viewModel::createDraft,
                        openDraft = viewModel::openDraft,
                        openServerSubmission = viewModel::openServerSubmission,
                        saveServerSubmission = viewModel::saveServerSubmission,
                        deleteServerSubmission = viewModel::deleteServerSubmission,
                        saveDraft = viewModel::saveDraft,
                        attachDraftImage = viewModel::attachDraftImage,
                        submitDraft = viewModel::submitDraft,
                        deleteDraft = viewModel::deleteDraft,
                        retrySync = viewModel::retrySync,
                        rebuildSync = viewModel::rebuildSyncState,
                        clearCache = viewModel::clearCache,
                        revokeDevice = viewModel::revokeDevice,
                        signOut = viewModel::signOut,
                    ),
                )
            }
        }
        viewModel.handleDeepLink(intent?.data)
    }

    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        viewModel.handleDeepLink(intent.data)
    }

    override fun onResume() {
        super.onResume()
        viewModel.requestSync()
    }
}

private data class AppActions(
    val signIn: () -> Unit,
    val onboard: (String, String, String) -> Unit,
    val home: () -> Unit,
    val library: () -> Unit,
    val searchCollection: (String) -> Unit,
    val profile: () -> Unit,
    val settings: () -> Unit,
    val admin: () -> Unit,
    val openAdminSubmission: (String) -> Unit,
    val moderateAdminSubmission: (AdminModerationDecision, String?) -> Unit,
    val deleteAdminGame: () -> Unit,
    val updateProfile: (String, String) -> Unit,
    val createCollection: (String) -> Unit,
    val selectCollection: (String) -> Unit,
    val manageCollection: () -> Unit,
    val updateCollection: (String, Boolean) -> Unit,
    val deleteCollection: () -> Unit,
    val invitations: () -> Unit,
    val notifications: () -> Unit,
    val openNotification: (LocalNotification) -> Unit,
    val markAllNotificationsRead: () -> Unit,
    val deleteNotification: (String) -> Unit,
    val friends: () -> Unit,
    val searchFriends: (String) -> Unit,
    val sendFriendRequest: (String) -> Unit,
    val respondToFriendRequest: (String, Boolean) -> Unit,
    val openFriend: (String) -> Unit,
    val openFriendCollection: (String) -> Unit,
    val removeFriend: (String) -> Unit,
    val corrections: () -> Unit,
    val startCorrection: () -> Unit,
    val submitCorrection: (CorrectionForm, Uri?, Uri?) -> Unit,
    val openAdminChangeRequest: (String) -> Unit,
    val reviewAdminChangeRequest: (Boolean, String?) -> Unit,
    val respondToInvitation: (String, Boolean) -> Unit,
    val searchUsers: (String) -> Unit,
    val invite: (String, CollectionRole) -> Unit,
    val updateMember: (String, CollectionRole) -> Unit,
    val removeMember: (String) -> Unit,
    val transferOwnership: (String) -> Unit,
    val searchGames: (String) -> Unit,
    val scan: () -> Unit,
    val lookupBarcode: (String) -> Unit,
    val openGame: (String) -> Unit,
    val backFromGame: () -> Unit,
    val setOwned: (Boolean) -> Unit,
    val setWishlisted: (Boolean) -> Unit,
    val drafts: () -> Unit,
    val createDraft: () -> Unit,
    val openDraft: (String) -> Unit,
    val openServerSubmission: (String) -> Unit,
    val saveServerSubmission: (ServerSubmissionForm) -> Unit,
    val deleteServerSubmission: (String) -> Unit,
    val saveDraft: (DraftForm, Int) -> Unit,
    val attachDraftImage: (String, Uri) -> Unit,
    val submitDraft: (DraftForm) -> Unit,
    val deleteDraft: (String) -> Unit,
    val retrySync: () -> Unit,
    val rebuildSync: () -> Unit,
    val clearCache: () -> Unit,
    val revokeDevice: () -> Unit,
    val signOut: () -> Unit,
)

@Composable
private fun GameCollectorApp(state: MainUiState, actions: AppActions) {
    val primaryPage =
        state.page in setOf(AppPage.Library, AppPage.Catalog, AppPage.Scanner, AppPage.Home)
    Scaffold(
        bottomBar = {
            if (primaryPage) PrimaryNavigation(state.page, actions)
        },
    ) { insets ->
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxSize()
                .padding(insets),
            contentAlignment = Alignment.TopCenter,
        ) {
            val horizontalPadding = if (maxWidth >= 840.dp) 32.dp else 16.dp
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .widthIn(max = 840.dp)
                    .padding(horizontal = horizontalPadding),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                when (state.page) {
                    AppPage.SignIn -> SignInScreen(actions.signIn)
                    AppPage.Loading -> LoadingScreen()
                    AppPage.Onboarding -> OnboardingScreen(actions.onboard)
                    AppPage.Home -> HomeScreen(state, actions)
                    AppPage.Library -> LibraryScreen(state, actions)
                    AppPage.Profile -> ProfileScreen(state.profile, actions)
                    AppPage.Settings -> SettingsScreen(state, actions)
                    AppPage.Collection -> CollectionScreen(state, actions)
                    AppPage.Invitations -> InvitationsScreen(state.invitations, actions)
                    AppPage.Notifications -> NotificationsScreen(state, actions)
                    AppPage.Corrections -> CorrectionsScreen(state, actions)
                    AppPage.CorrectionEditor -> state.selectedGame?.let {
                        CorrectionEditorScreen(
                            it,
                            actions
                        )
                    } ?: LoadingScreen()

                    AppPage.Catalog -> CatalogScreen(state, actions)
                    AppPage.Game -> GameScreen(state, actions)
                    AppPage.Scanner -> ScannerEntryScreen(actions)
                    AppPage.Drafts -> DraftListScreen(
                        state.drafts,
                        state.serverSubmissions,
                        actions.openDraft,
                        actions.openServerSubmission,
                        actions.createDraft,
                        actions.deleteDraft,
                        actions.deleteServerSubmission,
                        actions.home,
                    )

                    AppPage.DraftEditor -> state.selectedDraft?.let { draft ->
                        DraftEditorScreen(
                            draft,
                            state.draftUploads,
                            state.languages,
                            state.tags,
                            actions.saveDraft,
                            actions.attachDraftImage,
                            actions.submitDraft,
                            actions.drafts,
                        )
                    } ?: LoadingScreen()

                    AppPage.ServerSubmissionEditor -> state.selectedServerSubmission?.let { submission ->
                        ServerSubmissionEditorScreen(
                            submission,
                            state.languages,
                            state.tags,
                            actions.saveServerSubmission,
                            actions.drafts,
                        )
                    } ?: LoadingScreen()

                    AppPage.Admin -> AdminQueueScreen(state, actions)
                    AppPage.AdminSubmission -> state.selectedAdminSubmission?.let {
                        AdminSubmissionScreen(it, state.selectedGameImages, actions)
                    } ?: LoadingScreen()

                    AppPage.AdminCorrection -> state.selectedAdminChangeRequest?.let {
                        AdminCorrectionScreen(it, state.selectedCorrectionImages, actions)
                    } ?: LoadingScreen()
                    AppPage.Friends -> FriendsScreen(state, actions)
                    AppPage.FriendProfile -> FriendProfileScreen(state, actions)
                }
                state.message?.let {
                    Text(
                        it,
                        color = MaterialTheme.colorScheme.primary,
                        modifier = Modifier.semantics { liveRegion = LiveRegionMode.Polite })
                }
            }
            if (state.working && state.page != AppPage.Loading) {
                CircularProgressIndicator(
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(top = 12.dp)
                )
            }
        }
    }
}

@Composable
private fun SignInScreen(onSignIn: () -> Unit) {
    CenteredContent {
        Title("Game Collector")
        Text("Sign in securely with Keycloak to manage your collections.")
        Button(onClick = onSignIn) { Text("Sign in") }
    }
}

@Composable
private fun LoadingScreen() {
    CenteredContent {
        CircularProgressIndicator()
        Text("Loading your collection…")
    }
}

@Composable
private fun OnboardingScreen(onSubmit: (String, String, String) -> Unit) {
    var displayName by rememberSaveable { mutableStateOf("") }
    var username by rememberSaveable { mutableStateOf("") }
    var collectionName by rememberSaveable { mutableStateOf("") }
    FormScreen("Welcome") {
        Text("Create your profile and first card-game collection.")
        OutlinedTextField(
            displayName,
            { displayName = it.capitalizeFirstLetter() },
            label = { Text("Display name") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )
        OutlinedTextField(
            username,
            { username = it },
            label = { Text("Username") },
            prefix = { Text("#") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )
        OutlinedTextField(
            collectionName,
            { collectionName = it.capitalizeFirstLetter() },
            label = { Text("First collection") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )
        Button(
            onClick = { onSubmit(displayName, username, collectionName) },
            enabled = displayName.isNotBlank() && username.trim()
                .removePrefix("#").length in 3..30 && collectionName.isNotBlank(),
        ) { Text("Create profile") }
    }
}

@Composable
private fun HomeScreen(state: MainUiState, actions: AppActions) {
    var newCollection by rememberSaveable { mutableStateOf("") }
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Title("More")
            state.profile?.let { Text("${it.displayName}  •  #${it.username}") }
        }
        item {
            state.selectedCollection?.let {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        Modifier.padding(18.dp),
                        verticalArrangement = Arrangement.spacedBy(10.dp)
                    ) {
                        Text(
                            "Current collection",
                            style = MaterialTheme.typography.labelLarge,
                            color = MaterialTheme.colorScheme.primary
                        )
                        Text(
                            it.name,
                            style = MaterialTheme.typography.headlineSmall,
                            fontWeight = FontWeight.SemiBold
                        )
                        Text("${state.ownedGameIds.size} games · ${it.myRole.name}")
                        FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(onClick = actions.library) { Text("Browse games") }
                            OutlinedButton(onClick = actions.manageCollection) { Text("Manage") }
                        }
                    }
                }
            } ?: Text("Create a collection to get started.")
        }
        if (state.collections.size > 1) {
            item {
                Text(
                    "Switch collection",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )
            }
            items(state.collections, key = { it.id }) { collection ->
                Card(
                    onClick = { actions.selectCollection(collection.id) },
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Row(
                        Modifier
                            .fillMaxWidth()
                            .padding(14.dp),
                        horizontalArrangement = Arrangement.SpaceBetween
                    ) {
                        Text(collection.name, fontWeight = FontWeight.SemiBold)
                        Text(if (collection.id == state.selectedCollectionId) "Selected" else collection.myRole.name)
                    }
                }
            }
        }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text(
                        "Activity",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        OutlinedButton(onClick = actions.notifications) {
                            Text(if (state.unreadNotificationCount == 0) "Notifications" else "Notifications (${state.unreadNotificationCount})")
                        }
                        OutlinedButton(onClick = actions.invitations) { Text("Invitations") }
                        OutlinedButton(onClick = actions.friends) { Text("Friends") }
                        OutlinedButton(onClick = actions.drafts) { Text("Submissions") }
                        OutlinedButton(onClick = actions.corrections) { Text("Corrections") }
                        if (state.isAdministrator) {
                            Button(onClick = actions.admin) { Text("Admin (${state.adminSubmissions.size})") }
                        }
                    }
                }
            }
        }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    Text(
                        "Account",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(onClick = actions.profile) { Text("Profile") }
                        OutlinedButton(onClick = actions.settings) { Text("Settings") }
                        TextButton(onClick = actions.signOut) { Text("Sign out") }
                    }
                }
            }
        }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "Create another collection",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    OutlinedTextField(
                        newCollection,
                        { newCollection = it.capitalizeFirstLetter() },
                        label = { Text("Collection name") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth()
                    )
                    Button(onClick = {
                        actions.createCollection(newCollection); newCollection = ""
                    }, enabled = newCollection.isNotBlank()) {
                        Text("Create collection")
                    }
                }
            }
        }
    }
}

@Composable
private fun LibraryScreen(state: MainUiState, actions: AppActions) {
    val collection = state.selectedCollection
    var query by rememberSaveable(collection?.id) { mutableStateOf(state.collectionQuery) }
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Title(collection?.name ?: "My collection")
            Text("${state.ownedGameIds.size} game${if (state.ownedGameIds.size == 1) "" else "s"} in this collection")
            OutlinedTextField(
                value = query,
                onValueChange = {
                    query = it
                    actions.searchCollection(it)
                },
                label = { Text("Search this collection") },
                singleLine = true,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
            )
            if (query.isNotBlank()) {
                Text("${state.collectionGames.size} result${if (state.collectionGames.size == 1) "" else "s"}")
            }
        }
        if (state.collectionGames.isEmpty()) {
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Text(
                        if (query.isBlank()) "This collection does not contain any games yet." else "No games match ‘$query’.",
                        modifier = Modifier.padding(18.dp),
                    )
                }
            }
        }
        items(state.collectionGames, key = { it.id }) { game ->
            GameSummaryRow(
                game = game,
                thumbnail = state.gameListThumbnails[game.id],
                owned = true,
                wishlisted = false,
                open = { actions.openGame(game.id) },
            )
        }
    }
}

@Composable
private fun AdminQueueScreen(state: MainUiState, actions: AppActions) {
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Header("Pending approvals", actions.home)
            Text("${state.adminSubmissions.size} game submission${if (state.adminSubmissions.size == 1) "" else "s"} and ${state.adminChangeRequests.size} correction${if (state.adminChangeRequests.size == 1) "" else "s"} waiting for review")
            OutlinedButton(onClick = actions.admin) { Text("Refresh") }
        }
        item {
            Text(
                "New games",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
        }
        if (state.adminSubmissions.isEmpty()) item { Text("There are no pending game submissions.") }
        items(state.adminSubmissions, key = { it.game.id }) { submission ->
            Card(
                modifier = Modifier.fillMaxWidth(),
                onClick = { actions.openAdminSubmission(submission.game.id) },
            ) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        submission.game.title,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    submission.game.publisher?.let { Text(it) }
                    Text("Revision ${submission.game.revision} · ${submission.game.moderationStatus}")
                    Text("Review submission", color = MaterialTheme.colorScheme.primary)
                }
            }
        }
        item {
            Text(
                "Suggested corrections",
                style = MaterialTheme.typography.titleMedium,
                fontWeight = FontWeight.SemiBold
            )
        }
        if (state.adminChangeRequests.isEmpty()) item { Text("There are no pending corrections.") }
        items(state.adminChangeRequests, key = { it.id }) { request ->
            Card(
                modifier = Modifier.fillMaxWidth(),
                onClick = { actions.openAdminChangeRequest(request.id) }) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        request.gameTitle,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    val fields = proposedFieldLabels(request)
                    Text(if (fields.isEmpty()) "Replacement images" else fields.joinToString(" · "))
                    if (request.proposedImages.isNotEmpty()) Text("${request.proposedImages.size} replacement image${if (request.proposedImages.size == 1) "" else "s"}")
                    Text("Review correction", color = MaterialTheme.colorScheme.primary)
                }
            }
        }
    }
}

@Composable
private fun AdminSubmissionScreen(
    submission: GameSubmission,
    images: Map<String, ByteArray>,
    actions: AppActions,
) {
    var comment by rememberSaveable(submission.game.id) { mutableStateOf("") }
    val game = submission.game
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item { Header("Review game", actions.admin) }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(
                        game.title,
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.SemiBold
                    )
                    game.publisher?.let { Text("Publisher: $it") }
                    game.releaseYear?.let { Text("Release year: $it") }
                    game.description?.let { Text(it) }
                    val players =
                        listOfNotNull(game.minimumPlayers, game.maximumPlayers).joinToString("–")
                    if (players.isNotBlank()) Text("Players: $players")
                    game.minimumAge?.let { Text("Minimum age: $it") }
                    val playingTime = listOfNotNull(
                        game.minimumPlayingTimeMinutes,
                        game.maximumPlayingTimeMinutes
                    ).joinToString("–")
                    if (playingTime.isNotBlank()) Text("Playing time: $playingTime minutes")
                    if (game.barcodes.isNotEmpty()) Text("Barcode: ${game.barcodes.joinToString()}")
                    if (game.languages.isNotEmpty()) Text("Languages: ${game.languages.joinToString { it.name }}")
                    if (game.tags.isNotEmpty()) Text("Tags: ${game.tags.joinToString { it.name }}")
                    Text("Revision ${game.revision} · ${game.moderationStatus}")
                }
            }
        }
        item { GamePhotos(images) }
        item {
            OutlinedTextField(
                value = comment,
                onValueChange = { comment = it },
                label = { Text("Comment to submitter") },
                supportingText = { Text("Required when requesting changes or rejecting.") },
                minLines = 3,
                modifier = Modifier.fillMaxWidth(),
            )
        }
        item {
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(onClick = {
                    actions.moderateAdminSubmission(
                        AdminModerationDecision.Approve,
                        comment
                    )
                }) {
                    Text("Approve")
                }
                OutlinedButton(
                    onClick = {
                        actions.moderateAdminSubmission(
                            AdminModerationDecision.NeedsChanges,
                            comment
                        )
                    },
                    enabled = comment.isNotBlank(),
                ) { Text("Request changes") }
                OutlinedButton(
                    onClick = {
                        actions.moderateAdminSubmission(
                            AdminModerationDecision.Reject,
                            comment
                        )
                    },
                    enabled = comment.isNotBlank(),
                ) { Text("Reject") }
            }
        }
    }
}

@Composable
private fun AdminCorrectionScreen(
    request: GameChangeRequest,
    images: Map<String, ByteArray>,
    actions: AppActions,
) {
    var comment by rememberSaveable(request.id) { mutableStateOf("") }
    val patch = request.proposedChanges
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item { Header("Review correction", actions.admin) }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(
                        request.gameTitle,
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text("Proposed changes", style = MaterialTheme.typography.titleMedium)
                    patch.title?.let { Text("Title: $it") }
                    patch.description?.let { Text("Description: $it") }
                    patch.publisher?.let { Text("Publisher: $it") }
                    patch.releaseYear?.let { Text("Release year: $it") }
                    patch.minimumPlayers?.let { Text("Minimum players: $it") }
                    patch.maximumPlayers?.let { Text("Maximum players: $it") }
                    patch.minimumAge?.let { Text("Minimum age: $it") }
                    patch.minimumPlayingTimeMinutes?.let { Text("Minimum playing time: $it minutes") }
                    patch.maximumPlayingTimeMinutes?.let { Text("Maximum playing time: $it minutes") }
                    if (proposedFieldLabels(request).isEmpty()) Text("No text fields changed.")
                    Text(
                        "Catalog revision ${request.gameRevision}",
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
        if (request.proposedImages.isNotEmpty()) {
            item {
                Text("Proposed replacement images", style = MaterialTheme.typography.titleMedium)
                Text("These are previews only. The current catalog images remain live until approval.")
            }
            item { GamePhotos(images, request.proposedImages.map { it.imageType }) }
        }
        item {
            OutlinedTextField(
                comment,
                { comment = it },
                label = { Text("Comment to user") },
                supportingText = { Text("Required when rejecting.") },
                minLines = 3,
                modifier = Modifier.fillMaxWidth()
            )
        }
        item {
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = {
                    actions.reviewAdminChangeRequest(
                        true,
                        comment
                    )
                }) { Text("Apply correction") }
                OutlinedButton(
                    onClick = { actions.reviewAdminChangeRequest(false, comment) },
                    enabled = comment.isNotBlank()
                ) {
                    Text("Reject")
                }
            }
        }
    }
}

@Composable
private fun CatalogScreen(state: MainUiState, actions: AppActions) {
    var query by rememberSaveable(state.catalogQuery) { mutableStateOf(state.catalogQuery) }
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Title("Find games")
            Text("Search the full game catalog and add games to your collection.")
            OutlinedTextField(
                query,
                { query = it },
                label = { Text("Game name") },
                singleLine = true,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 8.dp),
            )
            Button(
                onClick = { actions.searchGames(query) },
                modifier = Modifier.padding(top = 8.dp)
            ) { Text("Search") }
            if (state.catalogQuery.isNotBlank()) {
                Text(
                    "${state.games.size} result${if (state.games.size == 1) "" else "s"} for ‘${state.catalogQuery}’",
                    modifier = Modifier.padding(top = 8.dp),
                )
            }
        }
        items(state.games, key = { it.id }) { game ->
            GameSummaryRow(
                game = game,
                thumbnail = state.gameListThumbnails[game.id],
                owned = game.id in state.ownedGameIds,
                wishlisted = game.id in state.wishlistGameIds,
                open = { actions.openGame(game.id) },
            )
        }
    }
}

@Composable
private fun GameSummaryRow(
    game: GameSummary,
    thumbnail: ByteArray?,
    owned: Boolean,
    wishlisted: Boolean,
    open: () -> Unit,
) {
    Card(modifier = Modifier.fillMaxWidth(), onClick = open) {
        Row(
            Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.spacedBy(14.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            GameListThumbnail(thumbnail, game.title)
            Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(3.dp)) {
                Text(
                    game.title,
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )
                Text(
                    game.releaseYear?.toString() ?: "Year unknown",
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
                game.publisher?.takeIf(String::isNotBlank)
                    ?.let { Text(it, style = MaterialTheme.typography.bodySmall) }
                val status = buildList {
                    if (owned) add("In collection")
                    if (wishlisted) add("Wishlist")
                    if (!game.moderationStatus.equals("Approved", true)) add(game.moderationStatus)
                }
                if (status.isNotEmpty()) {
                    Text(
                        status.joinToString(" · "),
                        color = MaterialTheme.colorScheme.primary,
                        style = MaterialTheme.typography.labelLarge
                    )
                }
            }
        }
    }
}

private fun proposedFieldLabels(request: GameChangeRequest): List<String> = buildList {
    val patch = request.proposedChanges
    if (patch.title != null) add("title")
    if (patch.description != null) add("description")
    if (patch.publisher != null) add("publisher")
    if (patch.releaseYear != null) add("year")
    if (patch.minimumPlayers != null || patch.maximumPlayers != null) add("players")
    if (patch.minimumAge != null) add("age")
    if (patch.minimumPlayingTimeMinutes != null || patch.maximumPlayingTimeMinutes != null) add("playing time")
}

@Composable
private fun GameListThumbnail(bytes: ByteArray?, title: String) {
    val bitmap = remember(bytes) { bytes?.let { BitmapFactory.decodeByteArray(it, 0, it.size) } }
    Card(modifier = Modifier
        .width(68.dp)
        .height(92.dp)) {
        if (bitmap == null) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text("No image", style = MaterialTheme.typography.labelSmall)
            }
        } else {
            Image(
                bitmap = bitmap.asImageBitmap(),
                contentDescription = "Front cover of $title",
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
            )
        }
    }
}

@Composable
private fun GameScreen(state: MainUiState, actions: AppActions) {
    val game = state.selectedGame ?: return
    val collection = state.selectedCollection
    val owned = game.id in state.ownedGameIds
    val wishlisted = game.id in state.wishlistGameIds
    val canEditCollection =
        collection?.myRole == CollectionRole.Owner || collection?.myRole == CollectionRole.Editor
    var confirmDelete by rememberSaveable(game.id) { mutableStateOf(false) }
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Header(game.title, actions.backFromGame)
            val metadata =
                listOfNotNull(game.publisher, game.releaseYear?.toString()).joinToString(" · ")
            if (metadata.isNotBlank()) Text(metadata)
            if (!game.moderationStatus.equals("Approved", true)) Text(
                game.moderationStatus,
                color = MaterialTheme.colorScheme.primary
            )
        }
        item { GamePhotos(state.selectedGameImages) }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                    Text("Collection status", style = MaterialTheme.typography.titleMedium)
                    Text(if (owned) "Owned in ${collection?.name.orEmpty()}" else "Not owned in ${collection?.name.orEmpty()}")
                    if (collection != null) {
                        Button(
                            onClick = { actions.setOwned(!owned) },
                            enabled = canEditCollection
                        ) {
                            Text(if (owned) "Remove from collection" else "Add to collection")
                        }
                    }
                    OutlinedButton(onClick = { actions.setWishlisted(!wishlisted) }) {
                        Text(if (wishlisted) "Remove from wishlist" else "Add to wishlist")
                    }
                }
            }
        }
        item { GameFacts(game) }
        if (game.moderationStatus.equals("Approved", true)) {
            item { OutlinedButton(onClick = actions.startCorrection) { Text("Suggest a correction") } }
        }
        if (state.isAdministrator) {
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        Modifier.padding(14.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text("Administrator", style = MaterialTheme.typography.titleMedium)
                        Text("Permanently deleting this game also removes it from every collection and deletes its images from storage.")
                        if (!confirmDelete) {
                            OutlinedButton(onClick = {
                                confirmDelete = true
                            }) { Text("Delete game permanently") }
                        } else {
                            Text(
                                "This cannot be undone.",
                                color = MaterialTheme.colorScheme.error,
                                fontWeight = FontWeight.SemiBold
                            )
                            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                Button(onClick = actions.deleteAdminGame) { Text("Confirm deletion") }
                                TextButton(onClick = { confirmDelete = false }) { Text("Cancel") }
                            }
                        }
                    }
                }
            }
        }
        game.description?.takeIf(String::isNotBlank)
            ?.let { description -> item { Text(description) } }
        if (game.languages.isNotEmpty()) item { Text("Languages: ${game.languages.joinToString { it.name }}") }
        if (game.tags.isNotEmpty()) item { Text("Tags: ${game.tags.joinToString { it.name }}") }
        if (game.barcodes.isNotEmpty()) item { Text("Barcodes: ${game.barcodes.joinToString()}") }
    }
}

@Composable
private fun GamePhotos(
    images: Map<String, ByteArray>,
    sides: List<String> = listOf("Front", "Back")
) {
    Column(verticalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxWidth()) {
        Text("Images", style = MaterialTheme.typography.titleMedium)
        sides.forEach { side ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(side, fontWeight = FontWeight.SemiBold)
                    val bytes = images[side]
                    val bitmap = remember(bytes) {
                        bytes?.let { BitmapFactory.decodeByteArray(it, 0, it.size) }
                    }
                    if (bitmap == null) {
                        Text("Thumbnail is not available yet.")
                    } else {
                        Image(
                            bitmap = bitmap.asImageBitmap(),
                            contentDescription = "$side side of the game",
                            contentScale = ContentScale.Fit,
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(if (side.equals("Back", true)) 120.dp else 220.dp),
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun PrimaryNavigation(page: AppPage, actions: AppActions) {
    NavigationBar {
        NavigationBarItem(
            selected = page == AppPage.Library,
            onClick = actions.library,
            icon = { Text("▦") },
            label = { Text("Collection") },
        )
        NavigationBarItem(
            selected = page == AppPage.Catalog,
            onClick = { actions.searchGames("") },
            icon = { Text("⌕") },
            label = { Text("Search") },
        )
        NavigationBarItem(
            selected = page == AppPage.Scanner,
            onClick = actions.scan,
            icon = { Text("▣") },
            label = { Text("Scan") },
        )
        NavigationBarItem(
            selected = page == AppPage.Home,
            onClick = actions.home,
            icon = { Text("•••") },
            label = { Text("More") },
        )
    }
}

@Composable
private fun GameFacts(game: GameDetails) {
    val facts = buildList {
        if (game.minimumPlayers != null || game.maximumPlayers != null) add(
            "Players: ${
                range(
                    game.minimumPlayers,
                    game.maximumPlayers
                )
            }"
        )
        game.minimumAge?.let { add("Age: $it+") }
        if (game.minimumPlayingTimeMinutes != null || game.maximumPlayingTimeMinutes != null) add(
            "Playing time: ${
                range(
                    game.minimumPlayingTimeMinutes,
                    game.maximumPlayingTimeMinutes
                )
            } min"
        )
    }
    if (facts.isNotEmpty()) {
        Card(modifier = Modifier.fillMaxWidth()) {
            Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                facts.forEach { Text(it) }
            }
        }
    }
}

private fun range(minimum: Int?, maximum: Int?): String = when {
    minimum != null && maximum != null && minimum != maximum -> "$minimum–$maximum"
    minimum != null -> minimum.toString()
    else -> maximum?.toString().orEmpty()
}

@Composable
private fun ScannerEntryScreen(actions: AppActions) {
    var barcode by rememberSaveable { mutableStateOf("") }
    var scanSession by rememberSaveable { mutableStateOf(0) }
    var detectedBarcode by rememberSaveable { mutableStateOf<String?>(null) }
    var cameraError by rememberSaveable { mutableStateOf<String?>(null) }
    val context = LocalContext.current
    var hasCameraPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(
                context,
                Manifest.permission.CAMERA
            ) == PackageManager.PERMISSION_GRANTED
        )
    }
    val permissionLauncher =
        rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) {
            hasCameraPermission = it
        }

    LaunchedEffect(Unit) {
        if (!hasCameraPermission) permissionLauncher.launch(Manifest.permission.CAMERA)
    }

    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Title("Scan game")
            Text("Align an EAN, UPC, ITF, or numeric Code 128 barcode inside the camera view.")
        }
        item {
            when {
                !hasCameraPermission -> Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text("Camera access is needed to scan a barcode. You can still type it below.")
                        Button(onClick = { permissionLauncher.launch(Manifest.permission.CAMERA) }) {
                            Text(
                                "Allow camera"
                            )
                        }
                    }
                }

                detectedBarcode != null -> Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Text(
                            "Detected: $detectedBarcode",
                            style = MaterialTheme.typography.titleMedium
                        )
                        Button(onClick = {
                            detectedBarcode = null
                            cameraError = null
                            scanSession += 1
                        }) { Text("Scan again") }
                    }
                }

                else -> Card(modifier = Modifier.fillMaxWidth()) {
                    key(scanSession) {
                        BarcodeCamera(
                            onBarcode = {
                                detectedBarcode = it
                                barcode = it
                                actions.lookupBarcode(it)
                            },
                            onError = { cameraError = it },
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(340.dp),
                        )
                    }
                }
            }
            cameraError?.let {
                Text(
                    it,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.padding(top = 8.dp)
                )
            }
            Text(
                "Images stay on this device and are used only for barcode recognition.",
                style = MaterialTheme.typography.bodySmall
            )
        }
        item {
            Text("Enter barcode manually", style = MaterialTheme.typography.titleMedium)
            OutlinedTextField(
                value = barcode,
                onValueChange = { barcode = it.filter(Char::isDigit).take(14) },
                label = { Text("8–14 digits") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(
                    keyboardType = KeyboardType.Number
                ),
                modifier = Modifier.fillMaxWidth(),
            )
            Button(
                onClick = { actions.lookupBarcode(barcode) },
                enabled = normalizeBarcode(barcode) != null,
                modifier = Modifier.padding(top = 8.dp),
            ) { Text("Look up") }
        }
    }
}


@Composable
private fun ProfileScreen(profile: UserProfile?, actions: AppActions) {
    var displayName by rememberSaveable(profile?.id) { mutableStateOf(profile?.displayName.orEmpty()) }
    var username by rememberSaveable(profile?.id) { mutableStateOf(profile?.username.orEmpty()) }
    FormScreen("Profile") {
        OutlinedTextField(
            displayName,
            { displayName = it.capitalizeFirstLetter() },
            label = { Text("Display name") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )
        OutlinedTextField(
            username,
            { username = it },
            label = { Text("Username") },
            prefix = { Text("#") },
            singleLine = true,
            modifier = Modifier.fillMaxWidth()
        )
        FlowRow(
            horizontalArrangement = Arrangement.spacedBy(8.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Button(onClick = { actions.updateProfile(displayName, username) }) { Text("Save") }
            TextButton(onClick = actions.home) { Text("Cancel") }
        }
    }
}

@Composable
private fun CollectionScreen(state: MainUiState, actions: AppActions) {
    val collection = state.selectedCollection ?: return
    val context = LocalContext.current
    val isOwner = collection.myRole == CollectionRole.Owner
    var name by rememberSaveable(collection.id) { mutableStateOf(collection.name) }
    var isPublic by rememberSaveable(collection.id) { mutableStateOf(collection.isPublic) }
    var query by rememberSaveable(collection.id) { mutableStateOf("") }
    var confirmDelete by rememberSaveable(collection.id) { mutableStateOf(false) }
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Header(collection.name, actions.home)
            Text("Your role: ${collection.myRole.name}")
            OutlinedButton(onClick = {
                val link = collectionShareUrl(collection.id)
                val share = Intent(Intent.ACTION_SEND).apply {
                    type = "text/plain"
                    putExtra(Intent.EXTRA_SUBJECT, "${collection.name} on Game Collector")
                    putExtra(Intent.EXTRA_TEXT, "Open ${collection.name} in Game Collector: $link")
                }
                context.startActivity(Intent.createChooser(share, "Share collection"))
            }) { Text("Share collection link") }
        }
        if (isOwner) {
            item {
                OutlinedTextField(
                    name,
                    { name = it.capitalizeFirstLetter() },
                    label = { Text("Collection name") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalAlignment = Alignment.CenterVertically) {
                    Column(Modifier.weight(1f)) {
                        Text("Public collection", fontWeight = FontWeight.SemiBold)
                        Text("Accepted friends can browse its games.", style = MaterialTheme.typography.bodySmall)
                    }
                    Switch(checked = isPublic, onCheckedChange = { isPublic = it })
                }
                Button(
                    onClick = { actions.updateCollection(name, isPublic) },
                    enabled = name.isNotBlank() && (name != collection.name || isPublic != collection.isPublic)
                ) { Text("Save collection") }
            }
            item {
                Text("Invite by username", style = MaterialTheme.typography.titleMedium)
                OutlinedTextField(
                    query,
                    { query = it },
                    label = { Text("Username") },
                    prefix = { Text("#") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth()
                )
                OutlinedButton(onClick = { actions.searchUsers(query) }) { Text("Search") }
            }
            items(state.searchResults, key = { it.id }) { user ->
                SearchResultRow(
                    user,
                    actions.invite
                )
            }
        }
        item { Text("Members", style = MaterialTheme.typography.titleMedium) }
        items(state.members, key = { it.userId }) { member -> MemberRow(member, isOwner, actions) }
        if (isOwner) {
            item {
                HorizontalDivider()
                if (!confirmDelete) {
                    TextButton(onClick = { confirmDelete = true }) { Text("Delete collection") }
                } else {
                    Text("Delete this collection permanently?")
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(onClick = actions.deleteCollection) { Text("Confirm delete") }
                        TextButton(onClick = { confirmDelete = false }) { Text("Cancel") }
                    }
                }
            }
        }
    }
}

@Composable
private fun SearchResultRow(user: UserSearchResult, invite: (String, CollectionRole) -> Unit) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(user.displayName, fontWeight = FontWeight.SemiBold)
            Text("#${user.username}")
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(onClick = {
                    invite(
                        user.id,
                        CollectionRole.Viewer
                    )
                }) { Text("Invite viewer") }
                OutlinedButton(onClick = {
                    invite(
                        user.id,
                        CollectionRole.Editor
                    )
                }) { Text("Invite editor") }
            }
        }
    }
}

@Composable
private fun MemberRow(member: CollectionMember, canManage: Boolean, actions: AppActions) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(member.displayName, fontWeight = FontWeight.SemiBold)
            Text("#${member.username} · ${member.role.name}")
            if (canManage && member.role != CollectionRole.Owner) {
                FlowRow(
                    horizontalArrangement = Arrangement.spacedBy(4.dp),
                    verticalArrangement = Arrangement.spacedBy(4.dp)
                ) {
                    TextButton(onClick = {
                        actions.updateMember(
                            member.userId,
                            if (member.role == CollectionRole.Viewer) CollectionRole.Editor else CollectionRole.Viewer
                        )
                    }) {
                        Text(if (member.role == CollectionRole.Viewer) "Make editor" else "Make viewer")
                    }
                    TextButton(onClick = { actions.transferOwnership(member.userId) }) { Text("Transfer ownership") }
                    TextButton(onClick = { actions.removeMember(member.userId) }) { Text("Remove") }
                }
            }
        }
    }
}

@Composable
private fun InvitationsScreen(invitations: List<CollectionInvitation>, actions: AppActions) {
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item { Header("Invitations", actions.home) }
        if (invitations.isEmpty()) item { Text("No pending invitations.") }
        items(invitations, key = { it.id }) { invitation ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(invitation.collectionName, fontWeight = FontWeight.SemiBold)
                    Text("Role: ${invitation.role.name}")
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(onClick = {
                            actions.respondToInvitation(
                                invitation.id,
                                true
                            )
                        }) { Text("Accept") }
                        OutlinedButton(onClick = {
                            actions.respondToInvitation(
                                invitation.id,
                                false
                            )
                        }) { Text("Decline") }
                    }
                }
            }
        }
    }
}

@Composable
private fun FriendsScreen(state: MainUiState, actions: AppActions) {
    var query by rememberSaveable { mutableStateOf("") }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxSize()) {
        item {
            Header("Friends", actions.home)
            OutlinedTextField(query, { query = it }, label = { Text("Find by username") }, prefix = { Text("#") },
                singleLine = true, modifier = Modifier.fillMaxWidth())
            Button(onClick = { actions.searchFriends(query) }, modifier = Modifier.padding(top = 8.dp)) { Text("Search users") }
        }
        items(state.friendSearchResults, key = { "search-${it.id}" }) { user ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Row(Modifier.fillMaxWidth().padding(14.dp), horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically) {
                    Column { Text(user.displayName, fontWeight = FontWeight.SemiBold); Text("#${user.username}") }
                    Button(onClick = { actions.sendFriendRequest(user.id) }) { Text("Add friend") }
                }
            }
        }
        if (state.friendRequests.isNotEmpty()) item { Text("Friend requests", style = MaterialTheme.typography.titleMedium) }
        items(state.friendRequests, key = { "request-${it.id}" }) { request ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(request.displayName, fontWeight = FontWeight.SemiBold); Text("#${request.username}")
                    if (request.incoming) FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(onClick = { actions.respondToFriendRequest(request.id, true) }) { Text("Accept") }
                        OutlinedButton(onClick = { actions.respondToFriendRequest(request.id, false) }) { Text("Decline") }
                    } else Text("Request sent", color = MaterialTheme.colorScheme.primary)
                }
            }
        }
        item { Text("Your friends", style = MaterialTheme.typography.titleMedium) }
        if (state.friends.isEmpty()) item { Text("No friends added yet.") }
        items(state.friends, key = { it.userId }) { friend ->
            Card(modifier = Modifier.fillMaxWidth(), onClick = { actions.openFriend(friend.userId) }) {
                Column(Modifier.padding(14.dp)) {
                    Text(friend.displayName, fontWeight = FontWeight.SemiBold); Text("#${friend.username}")
                    Text("View public collections and wishlist", color = MaterialTheme.colorScheme.primary)
                }
            }
        }
    }
}

@Composable
private fun FriendProfileScreen(state: MainUiState, actions: AppActions) {
    val friend = state.selectedFriendProfile ?: return
    var confirmRemove by rememberSaveable(friend.userId) { mutableStateOf(false) }
    LazyColumn(verticalArrangement = Arrangement.spacedBy(10.dp), modifier = Modifier.fillMaxSize()) {
        item {
            Header(friend.displayName, actions.friends)
            Text("#${friend.username}")
        }
        item { Text("Public collections", style = MaterialTheme.typography.titleMedium) }
        if (friend.publicCollections.isEmpty()) item { Text("This friend has no public collections.") }
        items(friend.publicCollections, key = { it.id }) { collection ->
            Card(modifier = Modifier.fillMaxWidth(), onClick = { actions.openFriendCollection(collection.id) }) {
                Row(Modifier.fillMaxWidth().padding(14.dp), horizontalArrangement = Arrangement.SpaceBetween) {
                    Text(collection.name, fontWeight = FontWeight.SemiBold); Text("${collection.gameCount} games")
                }
            }
        }
        if (state.friendCollectionGames.isNotEmpty()) {
            item { Text("Collection games", style = MaterialTheme.typography.titleMedium) }
            items(state.friendCollectionGames, key = { "friend-game-${it.gameId}" }) { game ->
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(Modifier.padding(12.dp)) { Text(game.title, fontWeight = FontWeight.SemiBold); game.releaseYear?.let { Text(it.toString()) } }
                }
            }
        }
        item { Text("Wishlist", style = MaterialTheme.typography.titleMedium) }
        if (friend.wishlist.isEmpty()) item { Text("The wishlist is empty.") }
        items(friend.wishlist, key = { "wish-${it.gameId}" }) { game ->
            Card(modifier = Modifier.fillMaxWidth()) { Column(Modifier.padding(12.dp)) {
                Text(game.title, fontWeight = FontWeight.SemiBold); game.publisher?.let { Text(it) }
            } }
        }
        item {
            if (!confirmRemove) TextButton(onClick = { confirmRemove = true }) { Text("Remove friend") }
            else FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = { actions.removeFriend(friend.userId) }) { Text("Confirm remove") }
                TextButton(onClick = { confirmRemove = false }) { Text("Cancel") }
            }
        }
    }
}

@Composable
private fun NotificationsScreen(state: MainUiState, actions: AppActions) {
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Header("Notifications", actions.home)
            if (state.unreadNotificationCount > 0) {
                TextButton(onClick = actions.markAllNotificationsRead) { Text("Mark all as read") }
            }
        }
        if (state.notifications.isEmpty()) item { Text("No notifications yet.") }
        items(state.notifications, key = { it.id }) { notification ->
            Card(
                modifier = Modifier.fillMaxWidth(),
                onClick = { actions.openNotification(notification) }) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        notification.title,
                        fontWeight = if (notification.readAtUtc == null) FontWeight.Bold else FontWeight.Normal
                    )
                    Text(notification.body)
                    Text(
                        notification.createdAtUtc.take(16).replace('T', ' '),
                        style = MaterialTheme.typography.bodySmall
                    )
                    TextButton(onClick = { actions.deleteNotification(notification.id) }) { Text("Delete") }
                }
            }
        }
    }
}

@Composable
private fun CorrectionsScreen(state: MainUiState, actions: AppActions) {
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item { Header("My corrections", actions.home) }
        if (state.changeRequests.isEmpty()) item { Text("No correction requests yet. Open an approved game to suggest one.") }
        items(state.changeRequests, key = { it.id }) { request ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(
                        request.gameTitle,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold
                    )
                    Text(request.status, color = MaterialTheme.colorScheme.primary)
                    val changes =
                        runCatching { JSONObject(request.proposedChangesJson) }.getOrNull()
                    changes?.keys()?.asSequence()?.toList()?.takeIf(List<String>::isNotEmpty)
                        ?.let { keys ->
                            Text(
                                "Proposed: ${
                                    keys.joinToString {
                                        it.replace(
                                            Regex("([a-z])([A-Z])"),
                                            "$1 $2"
                                        ).lowercase()
                                    }
                                }"
                            )
                        }
                    request.adminComment?.let { Text("Moderator: $it") }
                    Text(
                        "Updated ${request.updatedAtUtc.take(16).replace('T', ' ')}",
                        style = MaterialTheme.typography.bodySmall
                    )
                }
            }
        }
    }
}

@Composable
private fun CorrectionEditorScreen(game: GameDetails, actions: AppActions) {
    var title by rememberSaveable(game.id) { mutableStateOf(game.title) }
    var description by rememberSaveable(game.id) { mutableStateOf(game.description.orEmpty()) }
    var publisher by rememberSaveable(game.id) { mutableStateOf(game.publisher.orEmpty()) }
    var year by rememberSaveable(game.id) { mutableStateOf(game.releaseYear?.toString().orEmpty()) }
    var minPlayers by rememberSaveable(game.id) {
        mutableStateOf(
            game.minimumPlayers?.toString().orEmpty()
        )
    }
    var maxPlayers by rememberSaveable(game.id) {
        mutableStateOf(
            game.maximumPlayers?.toString().orEmpty()
        )
    }
    var minAge by rememberSaveable(game.id) {
        mutableStateOf(
            game.minimumAge?.toString().orEmpty()
        )
    }
    var minTime by rememberSaveable(game.id) {
        mutableStateOf(
            game.minimumPlayingTimeMinutes?.toString().orEmpty()
        )
    }
    var maxTime by rememberSaveable(game.id) {
        mutableStateOf(
            game.maximumPlayingTimeMinutes?.toString().orEmpty()
        )
    }
    var frontImageUri by rememberSaveable(game.id) { mutableStateOf<String?>(null) }
    var backImageUri by rememberSaveable(game.id) { mutableStateOf<String?>(null) }
    val frontPicker =
        rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
            frontImageUri = uri?.toString()
        }
    val backPicker =
        rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri ->
            backImageUri = uri?.toString()
        }
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(10.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item {
            Header(
                "Suggest correction",
                { actions.openGame(game.id) }); Text("Only changed fields are sent for moderator review. The current catalog entry stays visible until approval.")
        }
        item {
            OutlinedTextField(
                title,
                { title = it.capitalizeFirstLetter() },
                label = { Text("Title") },
                modifier = Modifier.fillMaxWidth()
            )
        }
        item {
            OutlinedTextField(
                description,
                { description = it.capitalizeFirstLetter() },
                label = { Text("Description") },
                modifier = Modifier.fillMaxWidth()
            )
        }
        item {
            OutlinedTextField(
                publisher,
                { publisher = it.capitalizeFirstLetter() },
                label = { Text("Publisher") },
                modifier = Modifier.fillMaxWidth()
            )
        }
        item {
            NumberField("Release year", year) { year = it }
            NumberField("Minimum players", minPlayers) { minPlayers = it }
            NumberField("Maximum players", maxPlayers) { maxPlayers = it }
            NumberField("Minimum age", minAge) { minAge = it }
            NumberField("Minimum playing time", minTime) { minTime = it }
            NumberField("Maximum playing time", maxTime) { maxTime = it }
        }
        item {
            Text("Replacement images (optional)", style = MaterialTheme.typography.titleMedium)
            Text("The existing image remains visible until an administrator approves this correction.")
            FlowRow(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.padding(top = 8.dp)
            ) {
                OutlinedButton(onClick = { frontPicker.launch("image/*") }) { Text(if (frontImageUri == null) "Replace front" else "Front selected") }
                OutlinedButton(onClick = { backPicker.launch("image/*") }) { Text(if (backImageUri == null) "Replace back" else "Back selected") }
                if (frontImageUri != null) TextButton(onClick = {
                    frontImageUri = null
                }) { Text("Clear front") }
                if (backImageUri != null) TextButton(onClick = {
                    backImageUri = null
                }) { Text("Clear back") }
            }
        }
        item {
            Button(onClick = {
                actions.submitCorrection(
                    CorrectionForm(
                        title,
                        description,
                        publisher,
                        year.toIntOrNull(),
                        minPlayers.toIntOrNull(),
                        maxPlayers.toIntOrNull(),
                        minAge.toIntOrNull(),
                        minTime.toIntOrNull(),
                        maxTime.toIntOrNull()
                    ),
                    frontImageUri?.let(Uri::parse), backImageUri?.let(Uri::parse),
                )
            }) {
                Text("Submit for review")
            }
        }
    }
}

@Composable
private fun NumberField(label: String, value: String, onValue: (String) -> Unit) {
    OutlinedTextField(
        value,
        { onValue(it.filter(Char::isDigit).take(4)) },
        label = { Text(label) },
        singleLine = true,
        keyboardOptions = KeyboardOptions(
            keyboardType = KeyboardType.Number
        ),
        modifier = Modifier
            .fillMaxWidth()
            .padding(bottom = 8.dp)
    )
}

private fun String.capitalizeFirstLetter(): String {
    val index = indexOfFirst(Char::isLetter)
    if (index < 0 || !this[index].isLowerCase()) return this
    return replaceRange(index, index + 1, this[index].uppercase())
}

@Composable
private fun SettingsScreen(state: MainUiState, actions: AppActions) {
    var confirmRevoke by rememberSaveable { mutableStateOf(false) }
    var confirmClear by rememberSaveable { mutableStateOf(false) }
    val latest = state.syncScopes.mapNotNull { it.lastSyncedAtUtc }.maxOrNull()
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        modifier = Modifier.fillMaxSize()
    ) {
        item { Header("Settings", actions.home) }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                    Text("Account", style = MaterialTheme.typography.titleMedium)
                    state.profile?.let { Text("${it.displayName} · #${it.username}"); Text(if (it.hasActiveDevice) "This device is active" else "Device activation unavailable") }
                    TextButton(onClick = actions.profile) { Text("Edit profile") }
                }
            }
        }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(5.dp)) {
                    Text("Synchronization", style = MaterialTheme.typography.titleMedium)
                    Text("${state.syncScopes.size} synchronized scopes · ${state.pendingMutationCount} queued changes")
                    Text(latest?.let {
                        "Last successful update: ${
                            it.take(16).replace('T', ' ')
                        } UTC"
                    } ?: "A full synchronization has not completed yet.")
                    Text(
                        "Server: ${BuildConfig.API_BASE_URL}",
                        style = MaterialTheme.typography.bodySmall
                    )
                    Text(
                        "App ${BuildConfig.VERSION_NAME} (${BuildConfig.VERSION_CODE}) · ${BuildConfig.BUILD_REVISION}",
                        style = MaterialTheme.typography.bodySmall
                    )
                    FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(onClick = actions.retrySync) { Text("Sync now") }
                        OutlinedButton(onClick = actions.rebuildSync) { Text("Rebuild sync") }
                    }
                }
            }
        }
        item {
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(
                        "Storage and troubleshooting",
                        style = MaterialTheme.typography.titleMedium
                    )
                    Text("Clearing cached content keeps your login and drafts, then downloads trusted server data again.")
                    if (!confirmClear) OutlinedButton(onClick = {
                        confirmClear = true
                    }) { Text("Clear cached content") }
                    else FlowRow(
                        horizontalArrangement = Arrangement.spacedBy(8.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(onClick = {
                            confirmClear = false; actions.clearCache()
                        }) { Text("Confirm clear") }
                        TextButton(onClick = { confirmClear = false }) { Text("Cancel") }
                    }
                }
            }
        }
        if (state.recentDiagnostics.isNotEmpty()) {
            item {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        Modifier.padding(14.dp),
                        verticalArrangement = Arrangement.spacedBy(4.dp)
                    ) {
                        Text("Recent diagnostics", style = MaterialTheme.typography.titleMedium)
                        Text(
                            "These entries contain event categories and reference IDs, not account content.",
                            style = MaterialTheme.typography.bodySmall
                        )
                        state.recentDiagnostics.take(5).forEach { event ->
                            val parts = event.split('|', limit = 3)
                            Text(
                                parts.drop(1).joinToString(" · ").ifBlank { event },
                                style = MaterialTheme.typography.bodySmall
                            )
                        }
                    }
                }
            }
        }
        item {
            Text("Device and session", style = MaterialTheme.typography.titleMedium)
            if (!confirmRevoke) OutlinedButton(onClick = {
                confirmRevoke = true
            }) { Text("Revoke this device") }
            else {
                Text("Revoking signs this device out and requires activation on the next sign-in.")
                FlowRow(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Button(onClick = actions.revokeDevice) { Text("Revoke and sign out") }
                    TextButton(onClick = { confirmRevoke = false }) { Text("Cancel") }
                }
            }
            TextButton(onClick = actions.signOut) { Text("Sign out") }
        }
    }
}

@Composable
private fun Header(title: String, back: () -> Unit) {
    FlowRow(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Title(title)
        TextButton(onClick = back) { Text("Back") }
    }
}

@Composable
private fun FormScreen(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = 32.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Title(title)
        content()
    }
}

@Composable
private fun CenteredContent(content: @Composable ColumnScope.() -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize(),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(16.dp, Alignment.CenterVertically),
        content = content,
    )
}

@Composable
internal fun Title(value: String) = Text(
    value,
    style = MaterialTheme.typography.headlineMedium,
    fontWeight = FontWeight.Bold,
    modifier = Modifier.semantics { heading() },
)
