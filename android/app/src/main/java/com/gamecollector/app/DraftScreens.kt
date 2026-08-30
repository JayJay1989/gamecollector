package com.gamecollector.app

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.PickVisualMediaRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.relocation.BringIntoViewRequester
import androidx.compose.foundation.relocation.bringIntoViewRequester
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.foundation.selection.toggleable
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.gamecollector.core.data.stringList
import com.gamecollector.core.database.LocalGameDraft
import com.gamecollector.core.database.PendingMediaUpload
import com.gamecollector.core.network.ReferenceData
import com.gamecollector.core.network.GameSubmission
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.ui.focus.FocusDirection
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@Composable
internal fun DraftListScreen(
    drafts: List<LocalGameDraft>,
    serverSubmissions: List<GameSubmission>,
    onOpen: (String) -> Unit,
    onOpenServer: (String) -> Unit,
    onCreate: () -> Unit,
    onDelete: (String) -> Unit,
    onDeleteServer: (String) -> Unit,
    onBack: () -> Unit,
) {
    LazyColumn(verticalArrangement = Arrangement.spacedBy(12.dp), modifier = Modifier.fillMaxSize()) {
        item {
            DraftHeader("Game submissions", onBack)
            Text("Drafts and selected photos stay on this device until their resumable upload completes.")
            Button(onClick = onCreate, modifier = Modifier.padding(top = 8.dp)) { Text("New game draft") }
        }
        if (serverSubmissions.isNotEmpty()) item { Text("On the server", style = MaterialTheme.typography.titleMedium) }
        items(serverSubmissions, key = { "server-${it.game.id}" }) { submission ->
            var confirmDelete by rememberSaveable(submission.game.id) { mutableStateOf(false) }
            val editable = submission.game.moderationStatus in setOf("Draft", "NeedsChanges")
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(submission.game.title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Text("${submission.game.moderationStatus} · Server revision ${submission.game.revision}")
                    submission.moderationComment?.let { Text("Moderator: $it") }
                    if (editable) {
                        FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(onClick = { onOpenServer(submission.game.id) }) { Text("Edit") }
                            if (!confirmDelete) TextButton(onClick = { confirmDelete = true }) { Text("Delete from server") }
                        }
                        if (confirmDelete) {
                            Text("Permanently delete this server draft and its uploaded images?")
                            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                                Button(onClick = { onDeleteServer(submission.game.id) }) { Text("Confirm delete") }
                                TextButton(onClick = { confirmDelete = false }) { Text("Cancel") }
                            }
                        }
                    } else {
                        Text("This submission cannot be edited while it is ${submission.game.moderationStatus.lowercase()}.")
                    }
                }
            }
        }
        if (drafts.isNotEmpty()) item { Text("On this device", style = MaterialTheme.typography.titleMedium) }
        if (drafts.isEmpty() && serverSubmissions.isEmpty()) item {
            EmptyState("No submissions yet", "Create a draft when a game is missing from the catalog.")
        }
        items(drafts, key = { it.id }) { draft ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(draft.title.ifBlank { "Untitled game" }, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Text("${draft.status} · Step ${draft.step + 1} of 3")
                    draft.lastError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
                    draft.barcode?.let { Text("Barcode $it") }
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        Button(onClick = { onOpen(draft.id) }) { Text(if (draft.status == "Submitted") "View" else "Continue") }
                        if (draft.status != "Submitted") TextButton(onClick = { onDelete(draft.id) }) { Text("Delete") }
                    }
                }
            }
        }
    }
}

@Composable
internal fun ServerSubmissionEditorScreen(
    submission: GameSubmission,
    languages: List<ReferenceData>,
    tags: List<ReferenceData>,
    onSave: (ServerSubmissionForm) -> Unit,
    onBack: () -> Unit,
) {
    val game = submission.game
    var title by rememberSaveable(game.id) { mutableStateOf(game.title) }
    var description by rememberSaveable(game.id) { mutableStateOf(game.description.orEmpty()) }
    var publisher by rememberSaveable(game.id) { mutableStateOf(game.publisher.orEmpty()) }
    var barcodes by rememberSaveable(game.id) { mutableStateOf(game.barcodes.joinToString(", ")) }
    var releaseYear by rememberSaveable(game.id) { mutableStateOf(game.releaseYear?.toString().orEmpty()) }
    var minimumPlayers by rememberSaveable(game.id) { mutableStateOf(game.minimumPlayers?.toString().orEmpty()) }
    var maximumPlayers by rememberSaveable(game.id) { mutableStateOf(game.maximumPlayers?.toString().orEmpty()) }
    var minimumAge by rememberSaveable(game.id) { mutableStateOf(game.minimumAge?.toString().orEmpty()) }
    var minimumTime by rememberSaveable(game.id) { mutableStateOf(game.minimumPlayingTimeMinutes?.toString().orEmpty()) }
    var maximumTime by rememberSaveable(game.id) { mutableStateOf(game.maximumPlayingTimeMinutes?.toString().orEmpty()) }
    var languageIds by remember(game.id) { mutableStateOf(game.languages.map { it.id }.toSet()) }
    var tagIds by remember(game.id) { mutableStateOf(game.tags.map { it.id }.toSet()) }
    val normalizedBarcodes = barcodes.split(',').map { it.filter(Char::isDigit) }.filter { it.length in 8..14 }.distinct()
    val form = ServerSubmissionForm(
        title.trim(), description.trim().takeIf(String::isNotBlank), publisher.trim().takeIf(String::isNotBlank),
        releaseYear.toIntOrNull(), minimumPlayers.toIntOrNull(), maximumPlayers.toIntOrNull(), minimumAge.toIntOrNull(),
        minimumTime.toIntOrNull(), maximumTime.toIntOrNull(), normalizedBarcodes, languageIds, tagIds,
    )
    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        contentPadding = PaddingValues(bottom = 32.dp),
        modifier = Modifier.fillMaxSize().imePadding(),
    ) {
        item {
            DraftHeader("Edit server draft", onBack)
            Text("${game.moderationStatus} · Revision ${game.revision}")
            submission.moderationComment?.let { Text("Moderator: $it", color = MaterialTheme.colorScheme.primary) }
        }
        item {
            DraftSection("Game details") {
                DraftTextField(title, { title = it.take(200) }, "Title")
                DraftTextField(publisher, { publisher = it.take(200) }, "Publisher")
                DraftTextField(barcodes, { barcodes = it.take(80) }, "Barcodes (comma separated)")
                NextAwareTextArea(
                    description,
                    { description = it.take(4000) },
                    "Description",
                    testTag = "server-description",
                )
            }
        }
        item {
            DraftSection("Gameplay") {
                DraftNumberField(releaseYear, { releaseYear = it.filter(Char::isDigit).take(4) }, "Release year")
                NumberPair("Players", minimumPlayers, { minimumPlayers = it }, maximumPlayers, { maximumPlayers = it })
                DraftNumberField(minimumAge, { minimumAge = it.filter(Char::isDigit).take(3) }, "Minimum age")
                NumberPair("Playing time (minutes)", minimumTime, { minimumTime = it }, maximumTime, { maximumTime = it }, ImeAction.Done)
            }
        }
        if (languages.isNotEmpty()) item { SelectionSection("Languages", languages, languageIds) { languageIds = it } }
        if (tags.isNotEmpty()) item { SelectionSection("Tags", tags, tagIds) { tagIds = it } }
        item {
            Text("Existing uploaded images remain attached to this server draft.", style = MaterialTheme.typography.bodySmall)
            Button(onClick = { onSave(form) }, enabled = title.isNotBlank()) { Text("Save server draft") }
        }
    }
}

@Composable
internal fun DraftEditorScreen(
    draft: LocalGameDraft,
    uploads: List<PendingMediaUpload>,
    languages: List<ReferenceData>,
    tags: List<ReferenceData>,
    onSave: (DraftForm, Int) -> Unit,
    onAttach: (String, Uri) -> Unit,
    onSubmit: (DraftForm) -> Unit,
    onBack: () -> Unit,
) {
    var step by rememberSaveable(draft.id) { mutableStateOf(draft.step) }
    var title by rememberSaveable(draft.id) { mutableStateOf(draft.title) }
    var description by rememberSaveable(draft.id) { mutableStateOf(draft.description.orEmpty()) }
    var publisher by rememberSaveable(draft.id) { mutableStateOf(draft.publisher.orEmpty()) }
    var barcode by rememberSaveable(draft.id) { mutableStateOf(draft.barcode.orEmpty()) }
    var releaseYear by rememberSaveable(draft.id) { mutableStateOf(draft.releaseYear?.toString().orEmpty()) }
    var minimumPlayers by rememberSaveable(draft.id) { mutableStateOf(draft.minimumPlayers?.toString().orEmpty()) }
    var maximumPlayers by rememberSaveable(draft.id) { mutableStateOf(draft.maximumPlayers?.toString().orEmpty()) }
    var minimumAge by rememberSaveable(draft.id) { mutableStateOf(draft.minimumAge?.toString().orEmpty()) }
    var minimumTime by rememberSaveable(draft.id) { mutableStateOf(draft.minimumPlayingTimeMinutes?.toString().orEmpty()) }
    var maximumTime by rememberSaveable(draft.id) { mutableStateOf(draft.maximumPlayingTimeMinutes?.toString().orEmpty()) }
    var languageIds by remember(draft.id) { mutableStateOf(draft.languageIdsJson.stringList().toSet()) }
    var tagIds by remember(draft.id) { mutableStateOf(draft.tagIdsJson.stringList().toSet()) }
    val form = DraftForm(
        title, description, publisher, barcode, releaseYear.toIntOrNull(), minimumPlayers.toIntOrNull(),
        maximumPlayers.toIntOrNull(), minimumAge.toIntOrNull(), minimumTime.toIntOrNull(), maximumTime.toIntOrNull(),
        languageIds, tagIds,
    )

    LazyColumn(
        verticalArrangement = Arrangement.spacedBy(12.dp),
        contentPadding = PaddingValues(bottom = 32.dp),
        modifier = Modifier.fillMaxSize().imePadding(),
    ) {
        item {
            DraftHeader("New game · ${step + 1}/3", onBack)
            draft.source?.let { Text("Suggested by $it — verify every field before submitting.", color = MaterialTheme.colorScheme.primary) }
            if (draft.status == "Submitted") Text("Submitted for moderation.", color = MaterialTheme.colorScheme.primary)
            draft.lastError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        }
        when (step) {
            0 -> item {
                DraftSection("Game details") {
                    DraftTextField(title, { title = it.take(200) }, "Title")
                    DraftTextField(publisher, { publisher = it.take(200) }, "Publisher")
                    DraftTextField(barcode, { barcode = it.filter(Char::isDigit).take(14) }, "Barcode (8–14 digits)")
                    NextAwareTextArea(
                        description,
                        { description = it.take(4000) },
                        "Description",
                        ImeAction.Done,
                        "draft-description",
                    )
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        TextButton(onClick = { onSave(form, 0) }) { Text("Save") }
                        Button(onClick = { onSave(form, 1); step = 1 }, enabled = title.isNotBlank()) { Text("Next") }
                    }
                }
            }
            1 -> {
                item {
                    DraftSection("Gameplay") {
                        DraftNumberField(releaseYear, { releaseYear = it.filter(Char::isDigit).take(4) }, "Release year")
                        NumberPair("Players", minimumPlayers, { minimumPlayers = it }, maximumPlayers, { maximumPlayers = it })
                        DraftNumberField(minimumAge, { minimumAge = it.filter(Char::isDigit).take(3) }, "Minimum age")
                        NumberPair("Playing time (minutes)", minimumTime, { minimumTime = it }, maximumTime, { maximumTime = it }, ImeAction.Done)
                    }
                }
                if (languages.isNotEmpty()) item { SelectionSection("Languages", languages, languageIds) { languageIds = it } }
                if (tags.isNotEmpty()) item { SelectionSection("Tags", tags, tagIds) { tagIds = it } }
                item {
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        TextButton(onClick = { onSave(form, 0); step = 0 }) { Text("Previous") }
                        Button(onClick = { onSave(form, 2); step = 2 }) { Text("Save and add photos") }
                    }
                }
            }
            else -> item {
                PhotoStep(draft, uploads, form, onAttach, onSubmit) {
                    onSave(form, 1)
                    step = 1
                }
            }
        }
    }
}

@Composable
private fun PhotoStep(
    draft: LocalGameDraft,
    uploads: List<PendingMediaUpload>,
    form: DraftForm,
    onAttach: (String, Uri) -> Unit,
    onSubmit: (DraftForm) -> Unit,
    previous: () -> Unit,
) {
    val context = LocalContext.current
    var targetKind by remember { mutableStateOf("Front") }
    var captureUri by remember { mutableStateOf<Uri?>(null) }
    val picker = rememberLauncherForActivityResult(ActivityResultContracts.PickVisualMedia()) { uri ->
        uri?.let { onAttach(targetKind, it) }
    }
    val camera = rememberLauncherForActivityResult(ActivityResultContracts.TakePicture()) { saved ->
        if (saved) captureUri?.let { onAttach(targetKind, it) }
    }
    DraftSection("Front and back photos") {
        listOf("Front", "Back").forEach { kind ->
            val upload = uploads.firstOrNull { it.kind == kind }
            Text("$kind: ${upload?.state ?: "not selected"}", fontWeight = FontWeight.SemiBold)
            upload?.lastError?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = {
                    targetKind = kind
                    picker.launch(PickVisualMediaRequest(ActivityResultContracts.PickVisualMedia.ImageOnly))
                }, enabled = draft.status != "Submitted") { Text("Choose $kind") }
                OutlinedButton(onClick = {
                    targetKind = kind
                    captureUri = DraftMediaFiles.cameraUri(context, draft.id, kind)
                    camera.launch(captureUri!!)
                }, enabled = draft.status != "Submitted") { Text("Photograph $kind") }
            }
        }
        Text("JPEG, PNG, or WebP · maximum 10 MiB each", style = MaterialTheme.typography.bodySmall)
        FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            TextButton(onClick = previous, enabled = draft.status != "Submitted") { Text("Previous") }
            Button(
                onClick = { onSubmit(form) },
                enabled = draft.status != "Submitted" && uploads.any { it.kind == "Front" },
            ) { Text(if (draft.status == "Failed") "Retry submission" else "Submit for review") }
        }
    }
}

@Composable
private fun SelectionSection(title: String, values: List<ReferenceData>, selected: Set<String>, update: (Set<String>) -> Unit) {
    DraftSection(title) {
        values.forEach { value ->
            val checked = value.id in selected
            Row(modifier = Modifier.fillMaxWidth().toggleable(checked, role = Role.Checkbox, onValueChange = { isChecked ->
                update(if (isChecked) selected + value.id else selected - value.id)
            })) {
                Checkbox(checked = checked, onCheckedChange = null)
                Text(value.name, modifier = Modifier.padding(top = 12.dp))
            }
        }
    }
}

@Composable
private fun NumberPair(
    label: String,
    minimum: String,
    setMinimum: (String) -> Unit,
    maximum: String,
    setMaximum: (String) -> Unit,
    finalImeAction: ImeAction = ImeAction.Next,
) {
    val focusManager = LocalFocusManager.current
    Text(label, style = MaterialTheme.typography.labelLarge)
    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
        OutlinedTextField(minimum, { setMinimum(it.filter(Char::isDigit).take(4)) }, label = { Text("Minimum") }, singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = ImeAction.Next),
            keyboardActions = KeyboardActions(onNext = { focusManager.moveFocus(FocusDirection.Next) }), modifier = Modifier.weight(1f).keepVisibleAboveDraftKeyboard())
        OutlinedTextField(maximum, { setMaximum(it.filter(Char::isDigit).take(4)) }, label = { Text("Maximum") }, singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = finalImeAction),
            keyboardActions = keyboardActions(focusManager), modifier = Modifier.weight(1f).keepVisibleAboveDraftKeyboard())
    }
}

@Composable
private fun DraftTextField(value: String, update: (String) -> Unit, label: String, imeAction: ImeAction = ImeAction.Next) {
    val focusManager = LocalFocusManager.current
    OutlinedTextField(value, { update(it.capitalizeDraftText()) }, label = { Text(label) }, singleLine = true,
        keyboardOptions = KeyboardOptions(imeAction = imeAction), keyboardActions = keyboardActions(focusManager),
        modifier = Modifier.fillMaxWidth().keepVisibleAboveDraftKeyboard())
}

@Composable
private fun NextAwareTextArea(
    value: String,
    update: (String) -> Unit,
    label: String,
    imeAction: ImeAction = ImeAction.Next,
    testTag: String,
) {
    val focusManager = LocalFocusManager.current
    val bringIntoViewRequester = remember { BringIntoViewRequester() }
    val coroutineScope = rememberCoroutineScope()
    OutlinedTextField(
        value,
        { update(it.capitalizeDraftText()) },
        label = { Text(label) },
        modifier = Modifier
            .fillMaxWidth()
            .bringIntoViewRequester(bringIntoViewRequester)
            .onFocusChanged { focusState ->
                if (focusState.isFocused) {
                    coroutineScope.launch {
                        delay(250)
                        bringIntoViewRequester.bringIntoView()
                    }
                }
            }
            .testTag(testTag),
        minLines = 4,
        maxLines = 8,
        keyboardOptions = KeyboardOptions(imeAction = imeAction),
        keyboardActions = keyboardActions(focusManager),
    )
}

private fun String.capitalizeDraftText(): String {
    val index = indexOfFirst(Char::isLetter)
    if (index < 0 || !this[index].isLowerCase()) return this
    return replaceRange(index, index + 1, this[index].uppercase())
}

@Composable
private fun DraftNumberField(value: String, update: (String) -> Unit, label: String, imeAction: ImeAction = ImeAction.Next) {
    val focusManager = LocalFocusManager.current
    OutlinedTextField(value, update, label = { Text(label) }, singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number, imeAction = imeAction),
        keyboardActions = keyboardActions(focusManager), modifier = Modifier.fillMaxWidth().keepVisibleAboveDraftKeyboard())
}

@Composable
private fun Modifier.keepVisibleAboveDraftKeyboard(): Modifier {
    val requester = remember { BringIntoViewRequester() }
    val coroutineScope = rememberCoroutineScope()
    return bringIntoViewRequester(requester).onFocusChanged { focusState ->
        if (focusState.isFocused) {
            coroutineScope.launch {
                delay(300)
                requester.bringIntoView()
            }
        }
    }
}

private fun keyboardActions(focusManager: androidx.compose.ui.focus.FocusManager) = KeyboardActions(
    onNext = { focusManager.moveFocus(FocusDirection.Next) },
    onDone = { focusManager.clearFocus() },
)

@Composable
private fun DraftSection(title: String, content: @Composable ColumnScope.() -> Unit) {
    AppSectionCard {
        Text(title, style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
        content()
    }
}

@Composable
private fun DraftHeader(title: String, back: () -> Unit) {
    FlowRow(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween, verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Title(title)
        TextButton(onClick = back) { Text("Back") }
    }
}
