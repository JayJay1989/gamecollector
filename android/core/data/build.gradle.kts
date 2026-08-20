plugins { alias(libs.plugins.android.library) }

android {
    namespace = "com.gamecollector.core.data"
    compileSdk = 36
    defaultConfig { minSdk = 26 }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    compileSdkMinor = 1
}

dependencies {
    implementation(project(":core:database"))
    implementation(project(":core:network"))
    implementation(libs.androidx.room.ktx)
    implementation(libs.kotlinx.coroutines.android)
    testImplementation(libs.junit)
}
