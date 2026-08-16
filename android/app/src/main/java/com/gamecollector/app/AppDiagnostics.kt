package com.gamecollector.app

import android.content.Context
import android.util.Log
import java.time.Instant

class AppDiagnostics(context: Context) {
    private val preferences = context.getSharedPreferences("diagnostics", Context.MODE_PRIVATE)

    fun record(category: String, detail: String) {
        val safeCategory = category.filter { it.isLetterOrDigit() || it == '-' }.take(32)
        val safeDetail = detail.replace('|', ' ').replace('\n', ' ').take(160)
        val next = (listOf("${Instant.now()}|$safeCategory|$safeDetail") + recent()).take(LIMIT)
        preferences.edit().putStringSet(KEY, next.mapIndexed { index, value -> "$index:$value" }.toSet()).apply()
        Log.w(TAG, "$safeCategory: $safeDetail")
    }

    fun recent(): List<String> = preferences.getStringSet(KEY, emptySet()).orEmpty()
        .mapNotNull { value -> value.substringAfter(':', "").takeIf(String::isNotBlank) }
        .sortedByDescending { it.substringBefore('|') }
        .take(LIMIT)

    companion object {
        private const val KEY = "recent_events"
        private const val LIMIT = 10
        private const val TAG = "GameCollector"
    }
}
