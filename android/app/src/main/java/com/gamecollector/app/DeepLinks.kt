package com.gamecollector.app

import android.net.Uri
import java.net.URI

sealed interface DeepLinkTarget {
    data class Invitation(val id: String?) : DeepLinkTarget
    data class Collection(val id: String) : DeepLinkTarget
    data class Game(val id: String) : DeepLinkTarget
}

fun parseDeepLink(uri: Uri?): DeepLinkTarget? {
    return uri?.toString()?.let(::parseDeepLinkValue)
}

fun parseDeepLinkValue(value: String, allowedWebHost: String = BuildConfig.APP_LINK_HOST): DeepLinkTarget? {
    val uri = runCatching { URI(value) }.getOrNull() ?: return null
    if (uri.scheme != "gamecollector" && uri.scheme != "https") return null
    if (uri.scheme == "https" && !uri.host.equals(allowedWebHost, ignoreCase = true)) return null
    val path = uri.path.orEmpty().split('/').filter(String::isNotBlank)
    val segments = if (uri.scheme == "gamecollector") listOfNotNull(uri.host) + path else path
    return when (segments.firstOrNull()) {
        "invitations" -> DeepLinkTarget.Invitation(segments.getOrNull(1))
        "collections" -> segments.getOrNull(1)?.let(DeepLinkTarget::Collection)
        "games" -> segments.getOrNull(1)?.let(DeepLinkTarget::Game)
        else -> null
    }
}

fun collectionShareUrl(collectionId: String): String =
    "https://${BuildConfig.APP_LINK_HOST}/collections/${Uri.encode(collectionId)}"
