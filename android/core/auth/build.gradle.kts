plugins { alias(libs.plugins.android.library) }

android {
    namespace = "com.gamecollector.core.auth"
    compileSdk = 36
    compileSdkExtension = 1
    defaultConfig { minSdk = 26 }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
}

dependencies {
    implementation(libs.androidx.core.ktx)
    implementation(libs.appauth)
    implementation(libs.kotlinx.coroutines.android)
    testImplementation(libs.junit)
}
