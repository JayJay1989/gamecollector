package com.gamecollector.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class DeepLinksTest {
    @Test fun parsesCustomAndWebRoutes() {
        assertEquals(DeepLinkTarget.Invitation("invite-1"), parseDeepLinkValue("gamecollector://invitations/invite-1"))
        assertEquals(DeepLinkTarget.Collection("collection-1"), parseDeepLinkValue("https://cards.example/collections/collection-1", "cards.example"))
        assertEquals(DeepLinkTarget.Game("game-1"), parseDeepLinkValue("https://cards.example/games/game-1", "cards.example"))
    }

    @Test fun rejectsUnrecognizedOrUntrustedSchemes() {
        assertNull(parseDeepLinkValue("javascript://games/game-1"))
        assertNull(parseDeepLinkValue("https://cards.example/settings"))
        assertNull(parseDeepLinkValue("https://attacker.example/games/game-1", "cards.example"))
    }
}
