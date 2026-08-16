# Game Collector Android workflow

Each step must compile and pass its automated tests before work moves to the next step. Room remains the UI source of truth; network calls feed synchronization rather than screens directly.

## 1. Android foundation and OIDC authentication — complete

- Create the Kotlin/Jetpack Compose project and initial module boundaries.
- Configure reproducible Gradle, SDK, Compose, and dependency versions.
- Implement Keycloak Authorization Code + PKCE with browser-based AppAuth.
- Store authentication state with Android Keystore encryption.
- Prove an authenticated request to the API without embedding a client secret.

## 2. Onboarding and collections — complete

- Add onboarding, username, display-name, and profile flows.
- Implement collection listing, creation, switching, and default selection.
- Add Owner, Editor, Viewer, membership, and invitation experiences.

## 3. Catalog and game details — complete

- Model games, barcodes, languages, tags, and image metadata.
- Add title search, barcode lookup, game details, ownership, and wishlist actions.
- Establish the collection-aware home navigation and one-tap scanner entry point.

## 4. Offline Room storage — complete

- Add the Room schema for catalog, collections, ownership, wishlist, invitations, notifications, and drafts.
- Make repositories and observable Room queries the source of truth for Compose.
- Support useful catalog and collection behavior with no network connection.

## 5. Synchronization engine — complete

- Implement pending mutations, idempotency IDs, scopes, cursors, tombstones, push, pull, and bootstrap.
- Apply server changes transactionally to Room.
- Schedule durable background and connectivity-triggered synchronization with WorkManager.

## 6. Barcode scanner — complete

- Add CameraX and ML Kit barcode recognition.
- Resolve scans locally first and display owned/wishlist state immediately.
- Add fast collection and wishlist actions with offline mutation enqueueing.

## 7. Game submission and media — complete

- Implement the durable multi-step unknown-game draft wizard.
- Add external metadata suggestions, camera/gallery images, upload intents, and resumable submission.
- Preserve drafts and local images across process death and offline periods.

## 8. Notifications and sharing — complete

- Add FCM token registration and silent sync wake-ups.
- Implement the Room-backed notification center and read state.
- Complete invitations, user search, collection sharing, and deep links.

## 9. Corrections and settings — complete

- Add approved-game correction requests and status tracking.
- Implement account, device, sync status, cache, logout, and troubleshooting settings.

## 10. Android hardening and release — complete

- Complete accessibility, adaptive layouts, error handling, observability, and offline tests.
- Add lint, static analysis, unit, instrumented, database-migration, and UI tests.
- Configure R8, signing through external secrets, release bundles/APKs, versioning, and rollback guidance.
