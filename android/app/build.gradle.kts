plugins {
    alias(libs.plugins.android.application)
    alias(libs.plugins.compose.compiler)
}

fun quotedProperty(name: String, fallback: String): String =
    "\"${providers.gradleProperty(name).orElse(fallback).get().replace("\\", "\\\\").replace("\"", "\\\"")}\""

val releaseStoreFile = providers.gradleProperty("gamecollector.signing.storeFile")
val releaseStorePassword = providers.gradleProperty("gamecollector.signing.storePassword")
val releaseKeyAlias = providers.gradleProperty("gamecollector.signing.keyAlias")
val releaseKeyPassword = providers.gradleProperty("gamecollector.signing.keyPassword")
val hasReleaseSigning = listOf(releaseStoreFile, releaseStorePassword, releaseKeyAlias, releaseKeyPassword).all { it.isPresent }
val hasProductionApi = providers.gradleProperty("gamecollector.apiBaseUrl")
    .map { it.isNotBlank() && !it.contains("example.com") }
    .getOrElse(false)
val hasFirebaseApp = providers.gradleProperty("gamecollector.firebaseApplicationId").map(String::isNotBlank).getOrElse(false)
val hasAppLinkHost = providers.gradleProperty("gamecollector.appLinkHost").map(String::isNotBlank).getOrElse(false)
val hasReleaseVersionCode = providers.gradleProperty("gamecollector.versionCode").isPresent
val hasReleaseVersionName = providers.gradleProperty("gamecollector.versionName").isPresent

android {
    namespace = "com.gamecollector.app"
    compileSdk = 36
    compileSdkExtension = 1

    defaultConfig {
        applicationId = "com.gamecollector.app"
        minSdk = 26
        targetSdk = 36
        versionCode = providers.gradleProperty("gamecollector.versionCode").orElse("1").get().toInt()
        versionName = providers.gradleProperty("gamecollector.versionName").orElse("0.1.0").get()
        testInstrumentationRunner = "androidx.test.runner.AndroidJUnitRunner"
        manifestPlaceholders["appAuthRedirectScheme"] = "com.gamecollector.app"
        manifestPlaceholders["appLinkHost"] = providers.gradleProperty("gamecollector.appLinkHost").orElse("app.example.com").get()
        buildConfigField("String", "OIDC_ISSUER", quotedProperty("gamecollector.oidcIssuer", "https://sso.buildserver.be/realms/Buildserver"))
        buildConfigField("String", "OIDC_CLIENT_ID", quotedProperty("gamecollector.oidcClientId", "gamecollector-android"))
        buildConfigField("String", "OIDC_REDIRECT_URI", quotedProperty("gamecollector.oidcRedirectUri", "com.gamecollector.app:/oauth2redirect"))
        buildConfigField("String", "API_BASE_URL", quotedProperty("gamecollector.apiBaseUrl", "https://gc.lateur.pro/"))
        buildConfigField("String", "FCM_TOKEN", quotedProperty("gamecollector.fcmToken", ""))
        buildConfigField("String", "FIREBASE_APPLICATION_ID", quotedProperty("gamecollector.firebaseApplicationId", ""))
        buildConfigField("String", "FIREBASE_API_KEY", quotedProperty("gamecollector.firebaseApiKey", ""))
        buildConfigField("String", "FIREBASE_PROJECT_ID", quotedProperty("gamecollector.firebaseProjectId", ""))
        buildConfigField("String", "FIREBASE_SENDER_ID", quotedProperty("gamecollector.firebaseSenderId", ""))
        buildConfigField("String", "APP_LINK_HOST", quotedProperty("gamecollector.appLinkHost", "app.example.com"))
        buildConfigField("String", "BUILD_REVISION", quotedProperty("gamecollector.buildRevision", "local"))
    }

    signingConfigs {
        if (hasReleaseSigning) {
            create("release") {
                storeFile = file(releaseStoreFile.get())
                storePassword = releaseStorePassword.get()
                keyAlias = releaseKeyAlias.get()
                keyPassword = releaseKeyPassword.get()
                enableV1Signing = true
                enableV2Signing = true
                enableV3Signing = true
                enableV4Signing = true
            }
        }
    }

    buildTypes {
        debug { applicationIdSuffix = ".debug" }
        release {
            isMinifyEnabled = true
            isShrinkResources = true
            isDebuggable = false
            proguardFiles(getDefaultProguardFile("proguard-android-optimize.txt"), "proguard-rules.pro")
            if (hasReleaseSigning) signingConfig = signingConfigs.getByName("release")
        }
    }

    buildFeatures {
        compose = true
        buildConfig = true
    }
    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    packaging.resources.excludes += "/META-INF/{AL2.0,LGPL2.1}"
    lint {
        abortOnError = true
        checkDependencies = true
        checkReleaseBuilds = true
    }
    testOptions { animationsDisabled = true }
    sourceSets.getByName("androidTest").assets.directories.add(
        project(":core:database").file("schemas").path,
    )
}

tasks.register("verifyProductionRelease") {
    group = "verification"
    description = "Fails unless production endpoints, Firebase, versioning, and external signing are configured."
    inputs.property("releaseSigningConfigured", hasReleaseSigning)
    inputs.property("productionApiConfigured", hasProductionApi)
    inputs.property("firebaseConfigured", hasFirebaseApp)
    inputs.property("appLinkConfigured", hasAppLinkHost)
    inputs.property("releaseVersionCodeConfigured", hasReleaseVersionCode)
    inputs.property("releaseVersionNameConfigured", hasReleaseVersionName)
    doLast {
        val configured = inputs.properties
        check(configured["releaseSigningConfigured"] == true) { "Configure all gamecollector.signing.* properties outside the repository." }
        check(configured["productionApiConfigured"] == true) { "Configure the production API URL." }
        check(configured["firebaseConfigured"] == true) { "Configure Firebase before release." }
        check(configured["appLinkConfigured"] == true) { "Configure the verified App Link host." }
        check(configured["releaseVersionCodeConfigured"] == true) { "Set a monotonically increasing versionCode." }
        check(configured["releaseVersionNameConfigured"] == true) { "Set a release versionName." }
    }
}

dependencies {
    implementation(project(":core:auth"))
    implementation(project(":core:network"))
    implementation(project(":core:designsystem"))
    implementation(project(":core:database"))
    implementation(project(":core:data"))
    implementation(project(":core:sync"))
    implementation(libs.androidx.core.ktx)
    implementation(libs.appauth)
    implementation(libs.androidx.activity.compose)
    implementation(libs.androidx.lifecycle.runtime.compose)
    implementation(libs.androidx.lifecycle.viewmodel.compose)
    implementation(platform(libs.androidx.compose.bom))
    implementation(libs.androidx.compose.ui)
    implementation(libs.androidx.compose.ui.tooling.preview)
    implementation(libs.androidx.compose.material3)
    implementation(libs.kotlinx.coroutines.android)
    implementation(libs.androidx.work.runtime)
    implementation(libs.androidx.room.runtime)
    implementation(libs.androidx.camera.core)
    implementation(libs.androidx.camera.camera2)
    implementation(libs.androidx.camera.lifecycle)
    implementation(libs.androidx.camera.view)
    implementation(libs.mlkit.barcode.scanning)
    implementation(platform(libs.firebase.bom))
    implementation(libs.firebase.messaging)
    debugImplementation(libs.androidx.compose.ui.tooling)
    debugImplementation(libs.androidx.compose.ui.test.manifest)
    testImplementation(libs.junit)
    androidTestImplementation(platform(libs.androidx.compose.bom))
    androidTestImplementation(libs.androidx.compose.ui.test.junit4)
    androidTestImplementation(libs.androidx.test.ext.junit)
    androidTestImplementation(libs.androidx.test.espresso.core)
    androidTestImplementation(libs.androidx.room.testing)
}
