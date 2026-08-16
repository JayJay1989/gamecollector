package com.gamecollector.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DraftFormTest {
    @Test
    fun acceptsValidSubmissionMetadata() {
        assertNull(validateDraftForm(form()))
    }

    @Test
    fun rejectsInvertedPlayerRange() {
        assertEquals("Enter a valid player range.", validateDraftForm(form(minimumPlayers = 5, maximumPlayers = 2)))
    }

    @Test
    fun rejectsInvalidReleaseYear() {
        assertEquals("Enter a release year from 1800 to 2200.", validateDraftForm(form(releaseYear = 1700)))
    }

    private fun form(
        releaseYear: Int? = 2024,
        minimumPlayers: Int? = 2,
        maximumPlayers: Int? = 4,
    ) = DraftForm(
        title = "Test Game",
        description = null,
        publisher = null,
        barcode = "887961751062",
        releaseYear = releaseYear,
        minimumPlayers = minimumPlayers,
        maximumPlayers = maximumPlayers,
        minimumAge = 7,
        minimumPlayingTimeMinutes = 10,
        maximumPlayingTimeMinutes = 30,
        languageIds = emptySet(),
        tagIds = emptySet(),
    )
}
