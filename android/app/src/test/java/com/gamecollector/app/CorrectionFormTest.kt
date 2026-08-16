package com.gamecollector.app

import com.gamecollector.core.network.GameDetails
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class CorrectionFormTest {
    @Test fun unchangedFormProducesNoPatch() {
        assertNull(correctionPatch(game, form()))
    }

    @Test fun patchContainsOnlyChangedValues() {
        val patch = correctionPatch(game, form(minimumAge = 10, publisher = "New Publisher"))!!
        assertEquals(10, patch.minimumAge)
        assertEquals("New Publisher", patch.publisher)
        assertNull(patch.title)
        assertNull(patch.maximumPlayers)
    }

    private fun form(minimumAge: Int = 7, publisher: String = "Mattel") = CorrectionForm(
        "UNO Flip!", "Two-sided UNO.", publisher, 2019, 2, 4, minimumAge, 15, 30,
    )

    private val game = GameDetails(
        "game-1", "UNO Flip!", "Two-sided UNO.", "Mattel", 2019, 2, 4, 7, 15, 30,
        "Approved", 1, emptyList(), emptyList(), emptyList(),
    )
}
