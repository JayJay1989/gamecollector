# Android release runbook

This runbook covers the production gate, signing, artifacts, rollout, and recovery for the Game Collector Android app. Release credentials must stay outside the repository.

## 1. Prepare the release

Start from the last verified commit and choose a `versionCode` greater than every build previously uploaded to Google Play. Set a user-facing `versionName` and an immutable source revision. Add the environment and signing values to the releasing account's user-level Gradle properties file (`%USERPROFILE%\.gradle\gradle.properties`):

```properties
gamecollector.versionCode=42
gamecollector.versionName=1.4.0
gamecollector.buildRevision=<full-commit-sha>

gamecollector.oidcIssuer=https://sso.buildserver.be/realms/Buildserver
gamecollector.oidcClientId=gamecollector-android
gamecollector.oidcRedirectUri=com.gamecollector.app:/oauth2redirect
gamecollector.apiBaseUrl=https://gc.lateur.pro/
gamecollector.appLinkHost=cards.example.com

gamecollector.signing.storeFile=C:/secure/gamecollector-upload.jks
gamecollector.signing.storePassword=<secret>
gamecollector.signing.keyAlias=<alias>
gamecollector.signing.keyPassword=<secret>
```

Restrict access to this file and the keystore. Keep encrypted, access-tested backups of the keystore and credentials in separate locations. Never copy them into the repository, CI logs, or release artifacts.

The production Firebase client configuration is read from `android/app/src/release/google-services.json`; the debug configuration at `android/app/src/debug/google-services.json` is not used in release builds. The API service-account JSON is a separate secret and must never be placed in either Android source directory.

## 2. Verify and build

From the `android` directory, run:

```powershell
.\gradlew.bat verifyProductionRelease testDebugUnitTest compileDebugAndroidTestKotlin lintDebug lintRelease assembleRelease bundleRelease
```

`verifyProductionRelease` rejects missing signing, version, Firebase, App Link, or production API configuration. R8 code shrinking, resource shrinking, and optimization are enabled for release builds.

Run the device suite on supported phones and a tablet before promotion:

```powershell
.\gradlew.bat connectedDebugAndroidTest
```

Cover API 26 and the current stable Android API, portrait/landscape, a tablet width, large font and display scaling, TalkBack, offline restart, Room migration, scanner permission denial, background synchronization, notification links, media upload resumption, and sign-out. The checked-in instrumented suite includes Compose accessibility semantics and Room 4-to-5 migration coverage.

The primary outputs are:

- `app/build/outputs/bundle/release/app-release.aab` for Google Play.
- `app/build/outputs/apk/release/app-release.apk` for signed APK distribution.
- `app/build/outputs/mapping/release/mapping.txt` for de-obfuscating this exact release.

Retain the AAB/APK, mapping file, source revision, dependency lock state, and SHA-256 checksums together. Confirm the package name, version, certificate fingerprint, permissions, and App Link verification from the final signed artifact.

## 3. Production smoke test and rollout

Use an internal testing track first, then a small staged production rollout. With a non-administrator and administrator account, verify OIDC login, active-device registration, catalog/search, collection mutation, offline mutation recovery, barcode scanning, draft image upload, notifications/deep links, corrections, cache rebuilding, and logout. Confirm the API sees correlation IDs and no client secrets or tokens appear in logs.

Before increasing the rollout, monitor API error rate, authentication failures, sync retries, FCM registration, upload completion, crash/ANR reports, and user reports. Pause the rollout when any release-specific regression exceeds the team's agreed threshold.

## 4. Rollback and recovery

Google Play does not install a lower `versionCode` over a newer build. To roll back behavior, build the last stable source with a new, higher `versionCode`, the same application ID and signing key, then pass the complete verification and staged rollout again.

Room migrations are forward-only. A corrective release must continue to understand schema version 5 and preserve queued mutations and local drafts. Do not distribute an older binary that expects an earlier database schema. If an emergency sideload downgrade is unavoidable, export or synchronize recoverable user data first; clearing app data removes local-only drafts and queued offline changes.

Coordinate Android and API recovery. Do not restore an API/database version whose contract or schema is incompatible with clients already installed. Prefer a forward API fix or compatibility layer. Follow the server backup and schema-aware recovery procedure in `docs/DEPLOYMENT.md`.

If the upload key is lost or compromised, stop releases, restrict the affected credentials, use the Google Play upload-key reset process where applicable, rotate external secrets, and revalidate App Link certificate fingerprints. Loss of the app-signing key outside Play-managed signing can prevent trusted updates, so its backup must be tested before the first production release.
