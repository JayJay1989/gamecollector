plugins { alias(libs.plugins.android.library) }

android {
    namespace = "com.gamecollector.core.sync"
    compileSdk = 36
    defaultConfig {
        minSdk = 26
        manifestPlaceholders["appAuthRedirectScheme"] = "com.gamecollector.app"
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    testOptions {
        unitTests.isIncludeAndroidResources = true
        unitTests.all {
            it.systemProperty(
                "robolectric.dependency.repo.url",
                "https://repo.maven.apache.org/maven2",
            )
        }
    }
    compileSdkMinor = 1
}

dependencies {
    implementation(project(":core:database"))
    implementation(project(":core:network"))
    implementation(libs.androidx.room.ktx)
    implementation(libs.kotlinx.coroutines.android)
    testImplementation(libs.junit)
    testImplementation(libs.json)
    testImplementation(libs.androidx.room.testing)
    testImplementation(libs.androidx.test.core)
    testImplementation(libs.robolectric)
}
