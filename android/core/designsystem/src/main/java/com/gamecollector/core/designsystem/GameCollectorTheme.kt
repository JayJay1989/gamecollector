package com.gamecollector.core.designsystem

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Typography
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val LightColors = lightColorScheme(
    primary = Color(0xFF385CA8),
    onPrimary = Color.White,
    primaryContainer = Color(0xFFD9E2FF),
    onPrimaryContainer = Color(0xFF0F2E66),
    secondary = Color(0xFF006B5F),
    onSecondary = Color.White,
    secondaryContainer = Color(0xFF9EF2E1),
    onSecondaryContainer = Color(0xFF00201B),
    tertiary = Color(0xFF765A00),
    tertiaryContainer = Color(0xFFFFDF8C),
    background = Color(0xFFF9F9FF),
    surface = Color(0xFFF9F9FF),
    surfaceVariant = Color(0xFFE1E2EC),
    outline = Color(0xFF757780),
)

private val DarkColors = darkColorScheme(
    primary = Color(0xFFB1C6FF),
    onPrimary = Color(0xFF082F6D),
    primaryContainer = Color(0xFF204580),
    onPrimaryContainer = Color(0xFFD9E2FF),
    secondary = Color(0xFF82D5C5),
    onSecondary = Color(0xFF00372F),
    secondaryContainer = Color(0xFF005047),
    onSecondaryContainer = Color(0xFF9EF2E1),
    tertiary = Color(0xFFEAC247),
    tertiaryContainer = Color(0xFF594400),
    background = Color(0xFF111318),
    surface = Color(0xFF111318),
    surfaceVariant = Color(0xFF44464F),
    outline = Color(0xFF8E9099),
)

@Composable
fun GameCollectorTheme(
    darkTheme: Boolean,
    content: @Composable () -> Unit,
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        typography = Typography(),
        content = content,
    )
}
