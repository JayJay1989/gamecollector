package com.gamecollector.app

import android.content.Context
import android.net.Uri
import androidx.core.content.FileProvider
import java.io.File
import java.io.FileOutputStream
import java.util.UUID

internal object DraftMediaFiles {
    private const val MAX_BYTES = 10 * 1024 * 1024
    private val acceptedTypes = setOf("image/jpeg", "image/png", "image/webp")

    fun cameraUri(context: Context, draftId: String, kind: String): Uri {
        val file = createFile(context, draftId, kind, "jpg")
        return FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", file)
    }

    fun persist(context: Context, source: Uri, draftId: String, kind: String): StoredDraftImage {
        val contentType = context.contentResolver.getType(source)?.lowercase() ?: "image/jpeg"
        require(contentType in acceptedTypes) { "Choose a JPEG, PNG, or WebP image." }
        val extension = when (contentType) { "image/png" -> "png"; "image/webp" -> "webp"; else -> "jpg" }
        val target = createFile(context, draftId, kind, extension)
        var copied = 0L
        context.contentResolver.openInputStream(source)?.use { input ->
            FileOutputStream(target).use { output ->
                val buffer = ByteArray(DEFAULT_BUFFER_SIZE)
                while (true) {
                    val count = input.read(buffer)
                    if (count < 0) break
                    copied += count
                    require(copied <= MAX_BYTES) { "Images must be no larger than 10 MiB." }
                    output.write(buffer, 0, count)
                }
            }
        } ?: error("The selected image could not be opened.")
        require(copied > 0) { "The selected image is empty." }
        return StoredDraftImage(
            FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", target).toString(),
            contentType,
            copied,
        )
    }

    fun deleteDraft(context: Context, draftId: String) {
        File(context.filesDir, "draft-images/$draftId").deleteRecursively()
    }

    fun clear(context: Context) {
        File(context.filesDir, "draft-images").deleteRecursively()
    }

    private fun createFile(context: Context, draftId: String, kind: String, extension: String): File {
        val directory = File(context.filesDir, "draft-images/$draftId").apply { mkdirs() }
        return File(directory, "${kind.lowercase()}-${UUID.randomUUID()}.$extension")
    }
}

internal data class StoredDraftImage(val uri: String, val contentType: String, val size: Long)
