package com.gamecollector.app

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.hasText
import androidx.compose.ui.test.isHeading
import androidx.compose.ui.test.junit4.v2.createComposeRule
import com.gamecollector.core.designsystem.GameCollectorTheme
import org.junit.Rule
import org.junit.Test

class AccessibilityUiTest {
    @get:Rule val compose = createComposeRule()

    @Test fun screenTitleExposesHeadingSemantics() {
        compose.setContent { GameCollectorTheme(darkTheme = false) { Title("Accessible screen") } }
        compose.onNode(hasText("Accessible screen") and isHeading()).assertIsDisplayed()
    }
}
