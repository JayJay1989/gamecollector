package com.gamecollector.app

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

private val Context.gameCollectorPreferences by preferencesDataStore(name = "gamecollector_preferences")

enum class ThemeMode {
    Automatic,
    Light,
    Dark,
}

internal fun themeModeFromStoredValue(value: String?): ThemeMode =
    value?.let { stored -> ThemeMode.entries.firstOrNull { it.name == stored } }
        ?: ThemeMode.Automatic

internal class AppPreferences(private val context: Context) {
    private val themeModeKey = stringPreferencesKey("theme_mode")

    val themeMode: Flow<ThemeMode> = context.gameCollectorPreferences.data.map { preferences ->
        themeModeFromStoredValue(preferences[themeModeKey])
    }

    suspend fun setThemeMode(themeMode: ThemeMode) {
        context.gameCollectorPreferences.edit { preferences ->
            preferences[themeModeKey] = themeMode.name
        }
    }
}
