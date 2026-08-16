package com.gamecollector.app

internal fun normalizeBarcode(value: String): String? {
    val normalized = value.trim().filter(Char::isDigit)
    return normalized.takeIf { it.length in 8..14 }
}
