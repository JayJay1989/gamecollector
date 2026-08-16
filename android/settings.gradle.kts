pluginManagement {
    repositories {
        google()
        mavenCentral()
        gradlePluginPortal()
    }
}

dependencyResolutionManagement {
    repositoriesMode.set(RepositoriesMode.FAIL_ON_PROJECT_REPOS)
    repositories {
        google()
        mavenCentral()
    }
}

rootProject.name = "GameCollectorAndroid"
include(":app")
include(":core:auth")
include(":core:network")
include(":core:designsystem")
include(":core:database")
include(":core:data")
include(":core:sync")
