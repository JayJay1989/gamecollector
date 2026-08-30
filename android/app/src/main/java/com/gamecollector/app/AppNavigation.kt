package com.gamecollector.app

import androidx.annotation.DrawableRes
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.scaleIn
import androidx.compose.animation.scaleOut
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.navigationBarsPadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp

private data class PrimaryDestination(
    val page: AppPage,
    val label: String,
    @param:DrawableRes val icon: Int,
    val action: (AppActions) -> Unit,
)

private val primaryDestinations = listOf(
    PrimaryDestination(AppPage.Library, "Collection", R.drawable.ic_collection) { it.library() },
    PrimaryDestination(AppPage.Catalog, "Search", R.drawable.ic_search) { it.searchGames("") },
    PrimaryDestination(AppPage.Scanner, "Scan", R.drawable.ic_scan) { it.scan() },
    PrimaryDestination(AppPage.Home, "More", R.drawable.ic_more) { it.home() },
)

internal val primaryDestinationLabels: List<String>
    get() = primaryDestinations.map { it.label }

@Composable
internal fun PrimaryNavigation(page: AppPage, actions: AppActions) {
    Surface(
        color = MaterialTheme.colorScheme.surfaceContainer,
        tonalElevation = 6.dp,
        shadowElevation = 8.dp,
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .navigationBarsPadding()
                .height(72.dp)
                .padding(horizontal = 10.dp, vertical = 8.dp)
                .selectableGroup(),
            horizontalArrangement = Arrangement.spacedBy(6.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            primaryDestinations.forEach { destination ->
                val selected = page == destination.page
                val weight = animateFloatAsState(
                    targetValue = if (selected) 1.65f else 1f,
                    label = "navigation item width",
                )
                Surface(
                    color = if (selected) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.surfaceContainer,
                    contentColor = if (selected) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onSurfaceVariant,
                    shape = MaterialTheme.shapes.extraLarge,
                    modifier = Modifier
                        .weight(weight.value)
                        .height(52.dp)
                        .selectable(
                            selected = selected,
                            onClick = { destination.action(actions) },
                            role = Role.Tab,
                        )
                        .testTag("primary-${destination.label.lowercase()}"),
                ) {
                    Row(
                        horizontalArrangement = Arrangement.Center,
                        verticalAlignment = Alignment.CenterVertically,
                        modifier = Modifier.padding(horizontal = 12.dp),
                    ) {
                        Image(
                            painter = painterResource(destination.icon),
                            contentDescription = destination.label,
                            colorFilter = ColorFilter.tint(
                                if (selected) MaterialTheme.colorScheme.onPrimaryContainer
                                else MaterialTheme.colorScheme.onSurfaceVariant,
                            ),
                            modifier = Modifier.size(24.dp),
                        )
                        AnimatedVisibility(
                            visible = selected,
                            enter = fadeIn() + scaleIn(initialScale = 0.8f),
                            exit = fadeOut() + scaleOut(targetScale = 0.8f),
                        ) {
                            Text(
                                text = destination.label,
                                style = MaterialTheme.typography.labelLarge,
                                modifier = Modifier.padding(start = 8.dp),
                                maxLines = 1,
                            )
                        }
                    }
                }
            }
        }
    }
}
