package com.gamecollector.app

import android.content.Context
import java.util.UUID

class InstallationIdStore(context: Context) {
    private val preferences = context.getSharedPreferences("installation", Context.MODE_PRIVATE)

    val id: String
        get() = preferences.getString(KEY, null) ?: UUID.randomUUID().toString().also {
            preferences.edit().putString(KEY, it).apply()
        }

    private companion object {
        const val KEY = "device_id"
    }
}
