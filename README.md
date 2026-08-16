# Game Collector

The backend is an ASP.NET Core .NET 10 API organized with Clean Architecture. The Android client uses Kotlin and Jetpack Compose. Implementation progress is tracked in `API-WORKFLOW.md` and `ANDROID-WORKFLOW.md`.

## Run the Android app

The Android project is in `android`. Configure these values in your user-level Gradle properties file (`%USERPROFILE%\.gradle\gradle.properties`) so credentials and environment addresses are not committed:

```properties
gamecollector.oidcIssuer=https://sso.buildserver.be/realms/Buildserver
gamecollector.oidcClientId=gamecollector-android
gamecollector.oidcRedirectUri=com.gamecollector.app:/oauth2redirect
gamecollector.apiBaseUrl=https://api.example.com/
gamecollector.firebaseApplicationId=<firebase-mobile-sdk-app-id>
gamecollector.firebaseApiKey=<firebase-web-api-key>
gamecollector.firebaseProjectId=<firebase-project-id>
gamecollector.firebaseSenderId=<firebase-sender-id>
gamecollector.appLinkHost=cards.example.com
```

For temporary local development without a Firebase project, `gamecollector.fcmToken` can still provide a fixed token. Debug builds fall back to an explicitly marked deferred token when neither Firebase configuration nor a fixed token is present; release builds require a real token.

In Keycloak, configure `gamecollector-android` as a public OIDC client with Authorization Code flow, PKCE S256, and the exact redirect URI above. Do not create or embed a client secret for the Android app.

Open the `android` directory in Android Studio, or verify it from PowerShell:

```powershell
Set-Location android
.\gradlew.bat testDebugUnitTest lintDebug assembleDebug
```

The initial screen launches sign-in in the system browser, stores the resulting OIDC state using Android Keystore encryption, and refreshes tokens when required. After authentication, the app supports first-run profile and collection creation, profile editing, collection switching, Owner/Editor/Viewer member management, invitations, and ownership transfer.

The home screen keeps the selected collection visible and provides title search, catalog browsing, and a one-tap scanner entry. Game details include publisher, year, player/age/time facts, languages, tags, and barcodes. Ownership is evaluated against the selected collection while wishlist state remains personal. Owners and Editors can add or remove games; Viewers receive read-only collection status. The barcode entry currently exercises the production lookup path manually; CameraX and ML Kit scanning are added in Android workflow step 6.

Room is the Android UI's read source of truth. Network refreshes persist profile, collections, members, catalog metadata, barcodes, languages, tags, ownership, wishlist, and invitations before observable database flows update Compose. Previously loaded home, collection, member, invitation, title-search, game-detail, ownership, wishlist, and barcode information therefore remains readable without connectivity. Local account data is cleared on sign-out.

The versioned Room schema is exported under `android/core/database/schemas`. Collection ownership and wishlist changes are now optimistic offline writes: Room updates the visible state and stores a UUID-identified pending mutation in one transaction. Synchronization pushes ordered batches of up to 100 mutations before pulling catalog, personal-user, and collection scopes in pages of up to 500 changes. Server sequence numbers provide last-write-wins ordering, while removed ownership and wishlist entries remain as local tombstones so an older response cannot restore them.

The first synchronization bootstraps all authorized scopes. Later runs persist a cursor per scope and apply each page and its cursor in one Room transaction; `sync_reset_required` clears the obsolete cursors and performs a fresh bootstrap. WorkManager requires connectivity and adequate battery, retries transient failures with exponential backoff, and runs after offline changes, at app startup/foreground entry, and periodically every six hours. FCM-triggered wake-ups remain part of Android workflow step 8.

The scanner uses CameraX with the bundled ML Kit barcode model, so recognition is available immediately and does not upload camera images or wait for a model download. It recognizes EAN-8, EAN-13, UPC-A, UPC-E, ITF, and numeric Code 128 values. A detected barcode is checked against Room first; cached game details and current ownership/wishlist state therefore open without connectivity. If it is not cached, the app tries the API. The result screen provides offline-capable collection and wishlist buttons, and manual 8–14 digit entry remains available when camera permission or camera hardware is unavailable.

Unknown games now open a three-stage Room-backed submission wizard for descriptive metadata, gameplay/reference data, and front/back images. External product lookup can prefill a draft, but its source is clearly identified and every value remains editable for user verification. Drafts, selected canonical language/tag IDs, local image URIs, server revisions, media IDs, processing states, retry counts, and errors survive process death. Images selected through Android's photo picker or captured through the device camera are copied into private app storage; only JPEG, PNG, and WebP files from 1 byte through 10 MiB are accepted.

Submitting schedules constrained WorkManager processing. It creates or updates the server draft, requests a short-lived upload intent for each image, uploads directly to the presigned object-storage URL without bearer credentials, calls media completion, waits for server validation and thumbnail readiness, and submits for moderation only after both images are ready. Each checkpoint is persisted in Room, so connectivity loss and process restarts resume instead of restarting the entire workflow. Final-state recovery also checks the server if the app stopped after submission was accepted but before the local acknowledgement was saved.

The installation UUID is stable and sent only through `X-Device-Id`. Firebase Messaging is initialized from external Gradle properties, obtains and rotates the device token, and registers token changes through durable WorkManager jobs. Data-only FCM messages never supply UI content; they wake the synchronization worker, after which the notification center reads trusted content and unread state from Room. Read and read-all actions update Room immediately and use durable connectivity-constrained jobs to reconcile with the API.

The notification center routes invitations, shared collections, and games from payload identifiers. Collection owners can search by username and invite Viewers or Editors, while every member can share a collection link through Android's share sheet. The app handles both `gamecollector://` links and verified `https://<gamecollector.appLinkHost>/` links. To enable verified production links, host `/.well-known/assetlinks.json` on that domain with the `com.gamecollector.app` package and production signing-certificate SHA-256 fingerprint; the manifest already declares `android:autoVerify="true"`.

Approved games now offer a correction editor. It compares the edited form with the current catalog record and sends only changed fields as a proposed patch; the approved record is never altered directly. Personal correction requests, proposed fields, Pending/Approved/Rejected status, moderator comments, and timestamps are persisted in Room for offline status viewing. Opening the correction history refreshes it from the API before Compose renders the database projection.

Settings shows the signed-in account and active-device state, queued offline mutations, synchronized scope count, latest successful synchronization time, and configured API address. Troubleshooting actions can enqueue synchronization, discard local cursors to request a full bootstrap, or clear downloaded catalog/notification/correction content and rebuild it from the server. Cache clearing keeps authentication and submission drafts, requires confirmation, and is blocked while collection or wishlist mutations are waiting to synchronize. Device revocation calls the API before removing local account data; ordinary sign-out only clears local authentication and account data.

The Android release build is R8-optimized, resource-shrunk, externally signed, and guarded by an explicit production-configuration check. The Compose shell adapts its width and wrapping for compact and tablet layouts, exposes headings and live status messages to accessibility services, and records a small local diagnostic history without credentials or response bodies. API failures carry correlation references into safe user-visible troubleshooting information. Versioning, signing, device coverage, staged rollout, artifact retention, and forward-only rollback are documented in [docs/ANDROID-RELEASE.md](docs/ANDROID-RELEASE.md).

Verify the complete local Android build with:

```powershell
Set-Location android
.\gradlew.bat testDebugUnitTest compileDebugAndroidTestKotlin lintDebug assembleDebug assembleRelease bundleRelease
```

## Run the API

Configure the Keycloak values, then start the API project:

```powershell
$env:Authentication__Keycloak__Authority = "https://sso.buildserver.be/realms/Buildserver"
$env:Authentication__Keycloak__Audience = "gamecollector-api"
$env:Authentication__Keycloak__AdminRole = "gamecollector-admin"
$env:ConnectionStrings__GameCollector = "Data Source=data/gamecollector.db;Foreign Keys=True;Default Timeout=5;Pooling=True"
$env:MediaStorage__Endpoint = "minio.example.com"
$env:MediaStorage__AccessKey = "<access-key>"
$env:MediaStorage__SecretKey = "<secret-key>"
$env:MediaStorage__Bucket = "gamecollector-media"
$env:MediaStorage__UseSsl = "true"
$env:Firebase__ProjectId = "your-firebase-project-id"
$env:Firebase__CredentialsPath = "C:\secrets\firebase-service-account.json"
$env:ApiHardening__MaximumRequestBodyBytes = "1048576"
$env:ApiHardening__RateLimitPermitCount = "120"
$env:ApiHardening__RateLimitWindowSeconds = "60"
$env:ApiHardening__RequestTimeoutSeconds = "30"
dotnet run --project src/GameCollector.Api
```

The committed authority and MinIO credentials are example values and must be overridden outside local development. HTTPS metadata validation is enabled by default. Create `gamecollector-media` as a private MinIO bucket; clients receive only short-lived signed URLs and never receive storage credentials.

The API applies checked-in EF Core migrations at startup. SQLite is configured with foreign-key enforcement, a five-second busy timeout, and WAL journaling. Use one API process for each SQLite file.

Create a migration after changing the persistence model with:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations add <MigrationName> --project src/GameCollector.Infrastructure --startup-project src/GameCollector.Infrastructure --context ApplicationDbContext --output-dir Persistence/Migrations
```

Useful endpoints:

```text
GET /openapi/v1.json
GET /health/live
GET /health/ready
```

Implemented authenticated user endpoints:

```text
GET   /api/v1/me
POST  /api/v1/me/onboarding
PATCH /api/v1/me
POST  /api/v1/me/device/activate
DELETE /api/v1/me/device

GET    /api/v1/collections
POST   /api/v1/collections
GET    /api/v1/collections/{id}
PATCH  /api/v1/collections/{id}
DELETE /api/v1/collections/{id}
POST   /api/v1/collections/{id}/transfer-ownership
GET    /api/v1/collections/{id}/members
PATCH  /api/v1/collections/{id}/members/{userId}
DELETE /api/v1/collections/{id}/members/{userId}
POST   /api/v1/collections/{id}/invitations

GET  /api/v1/me/invitations
POST /api/v1/invitations/{id}/accept
POST /api/v1/invitations/{id}/decline
GET  /api/v1/users/search

GET /api/v1/games
GET /api/v1/games/search?q=uno
GET /api/v1/games/{id}
GET /api/v1/games/barcode/{barcode}
GET /api/v1/product-lookup/{barcode}
GET /api/v1/languages
GET /api/v1/tags

POST /api/v1/media/upload-intents
POST /api/v1/media/{id}/complete
GET  /api/v1/media/{id}

POST /api/v1/game-submissions
GET  /api/v1/game-submissions/mine
GET  /api/v1/game-submissions/{id}
PUT  /api/v1/game-submissions/{id}
POST /api/v1/game-submissions/{id}/submit

POST /api/v1/games/{gameId}/change-requests
GET  /api/v1/change-requests/mine

GET    /api/v1/collections/{id}/games
PUT    /api/v1/collections/{id}/games/{gameId}
DELETE /api/v1/collections/{id}/games/{gameId}
GET    /api/v1/me/wishlist
PUT    /api/v1/me/wishlist/{gameId}
DELETE /api/v1/me/wishlist/{gameId}

POST /api/v1/sync/push
POST /api/v1/sync/pull
GET  /api/v1/sync/bootstrap

GET  /api/v1/me/notifications
POST /api/v1/me/notifications/{id}/read
POST /api/v1/me/notifications/read-all
```

Endpoints protected by the active-device policy require `X-Device-Id` with the currently registered installation UUID. Activating a new device replaces the previous registration.

Media uploads accept JPEG, PNG, and WebP up to 10 MiB. Completion independently checks the stored byte count, decoded format, MIME type, dimensions, pixel count, and animation state before queueing a 480-pixel JPEG thumbnail. Thumbnail jobs are stored in the database outbox, retried with exponential backoff after transient failures, and marked complete only after successful processing.

Offline clients send at most 100 ordered, uniquely identified collection or wishlist mutations per sync push. Replaying a mutation ID returns its original result without applying it twice. Pull accepts catalog, personal-user, and authorized-collection scopes, returns changes in server-sequence order, and limits each page to 500 changes. A zero cursor returns a scope snapshot; `/sync/bootstrap` returns all currently accessible scopes at one consistent cursor. Removed ownership and wishlist rows remain as tombstones for synchronization. If a cursor predates retained history, the API returns `409 sync_reset_required` so the client can discard its local projection and bootstrap again. All sync endpoints require the active `X-Device-Id`.

In-app notification records are committed with the invitation, membership, moderation, correction, or device-registration change that created them. Read state is durable and included in the personal sync scope. FCM is only a wake-up channel: its data payload contains the notification ID and type, while the app obtains actual content from its local synchronized data or the API. Push delivery uses Firebase HTTP v1 with OAuth service-account credentials and runs through the durable outbox; temporary failures retain their error and retry schedule without rolling back the domain operation. Leave `Firebase:ProjectId` empty to disable external push delivery in local development.

Unknown barcodes are enriched through the replaceable UPCitemdb adapter with a five-second timeout and a one-hour server cache. Local catalog matches always win, and external metadata is returned only as a candidate for user review; it is never inserted into the catalog or copied into MinIO automatically.

User-created games begin as private drafts. Ready front and back images are required before submission. Pending and Needs Changes games are visible only to their submitter, administrators, and members of collections already containing them; rejected games remain visible only to their submitter and administrators. Approved games become globally discoverable.

Approved games cannot be edited directly by ordinary users. Corrections are stored as proposed patches until an administrator approves or rejects them. Catalog revisions are concurrency tokens, so stale administrator decisions return `409 Conflict` instead of overwriting newer data.

Implemented administrator moderation endpoints:

```text
GET  /api/v1/admin/submissions
GET  /api/v1/admin/submissions/{id}
POST /api/v1/admin/submissions/{id}/approve
POST /api/v1/admin/submissions/{id}/needs-changes
POST /api/v1/admin/submissions/{id}/reject
GET  /api/v1/admin/change-requests
POST /api/v1/admin/change-requests/{id}/approve
POST /api/v1/admin/change-requests/{id}/reject
```

Implemented administration and diagnostics endpoints:

```text
GET  /api/v1/admin/users
GET  /api/v1/admin/users/{id}
POST /api/v1/admin/users/{id}/disable
POST /api/v1/admin/users/{id}/enable
POST /api/v1/admin/users/{id}/revoke-device

GET /api/v1/admin/collections
GET /api/v1/admin/collections/{id}

GET  /api/v1/admin/games
GET  /api/v1/admin/games/{id}
POST /api/v1/admin/games
PUT  /api/v1/admin/games/{id}

GET /api/v1/admin/audit
GET /api/v1/admin/diagnostics/sync
```

User and collection searches accept `q` and a bounded `limit`. Audit search supports action, entity type/ID, actor, time range, and limit filters. Catalog updates require the current revision and return `409 Conflict` for stale edits. Sync diagnostics are stored separately from security audit entries and report the device, last successful sync and cursor, aggregate upload/download counts, and last synchronization error.

Every route under `/api/v1/admin` independently requires the configured Keycloak administrator role. A disabled application profile remains forbidden even if its Keycloak token still carries that role. Administrator state changes write append-only audit entries without FCM tokens or other credentials.

Every administrator moderation decision writes an append-only audit entry in the same SQLite transaction, including the actor, correlation ID, optional device/IP context, and safe before/after summaries.

## Production hardening and deployment

The API applies a bounded request-body size, per-user/IP fixed-window rate limiting, a default request timeout, HSTS outside development, no-store caching, and defensive content, frame, referrer, permissions, and CSP headers. Rate-limit, oversized-request, and timeout responses use the same stable Problem Details format as the rest of the API. Health endpoints are not rate limited.

The request log records method, path, status, duration, correlation ID, user subject, and device ID. It deliberately excludes query strings, authorization headers, cookies, request/response bodies, FCM tokens, and configured credentials.

Build the non-root release image from [Dockerfile](Dockerfile). The image exposes port 8080, persists SQLite at `/data/gamecollector.db`, and contains a readiness health check. Validate and deploy [compose.portainer.yml](compose.portainer.yml) with explicit immutable Nexus and MinIO image tags; never operate multiple API replicas against the same SQLite volume.

Release tagging, configuration, Portainer deployment, backup, smoke testing, and schema-aware rollback are documented in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

Run all checks with:

```powershell
dotnet build GameCollector.slnx
dotnet test GameCollector.slnx --no-build
```

## GitHub Actions

The [API container workflow](.github/workflows/api-container.yml) restores, builds, and tests the .NET solution. After a successful run on the repository's default branch, a `v*` tag, or a manual dispatch, it builds the API image and pushes commit and release tags to Nexus. Configure these in the GitHub repository settings:

- Variable `NEXUS_REGISTRY`: the Docker registry host and optional port, without `https://` (for example `nexus.buildserver.be:5000`).
- Variable `NEXUS_IMAGE`: the Nexus image path (for example `gamecollector/gamecollector-api`); it defaults to `gamecollector-api`.
- Secret `NEXUS_USERNAME`: a Nexus account limited to pushing this repository.
- Secret `NEXUS_PASSWORD`: that account's token or password.

The Nexus Docker endpoint must be reachable from GitHub-hosted runners and use a publicly trusted TLS certificate. Use a current self-hosted runner when Nexus is private or uses an internal certificate authority.

The [Android build workflow](.github/workflows/android-build.yml) runs unit tests, compiles the instrumented tests, performs debug and release lint, and produces a debug APK, unsigned optimized release APK, unsigned AAB, R8 mapping, and lint reports. It deliberately receives no keystore or signing credentials. Artifacts remain downloadable from the workflow run for 14 days; device-based instrumented tests still require an emulator or connected-device job.
