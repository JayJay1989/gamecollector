package com.gamecollector.app

import org.junit.Assert.assertEquals
import org.junit.Test

class AppearanceAndNavigationTest {
    @Test
    fun storedThemeModeFallsBackToAutomatic() {
        assertEquals(ThemeMode.Automatic, themeModeFromStoredValue(null))
        assertEquals(ThemeMode.Automatic, themeModeFromStoredValue("Unsupported"))
    }

    @Test
    fun everyThemeModeRoundTripsThroughStorage() {
        ThemeMode.entries.forEach { mode ->
            assertEquals(mode, themeModeFromStoredValue(mode.name))
        }
    }

    @Test
    fun primaryNavigationHasFourUniqueDestinations() {
        assertEquals(listOf("Collection", "Search", "Scan", "More"), primaryDestinationLabels)
        assertEquals(primaryDestinationLabels.size, primaryDestinationLabels.distinct().size)
    }
}
