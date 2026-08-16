# Technical Analysis — Game Collector Android App

## 1. Executive Summary

The application is a shared card-game collection manager primarily designed to solve one problem:

> **“Do we already own this game?”**

The system will consist of three user-facing/system components:

1. A native Android application for normal users.
2. An ASP.NET Core .NET 10 REST API containing all server-side business logic.
3. An admin-only Blazor web portal.

The recommended technology stack is:

| Area | Technology |
|---|---|
| Android | Kotlin |
| Android UI | Jetpack Compose |
| Android minimum version | Android 10 / API 29 |
| Android local database | Room / SQLite |
| Android architecture | Offline-first, MVVM/use-case based |
| Barcode scanning | CameraX + ML Kit |
| Authentication | Keycloak / OpenID Connect |
| Mobile OAuth flow | Authorization Code + PKCE |
| Backend | ASP.NET Core .NET 10 |
| ORM | Entity Framework Core 10 |
| Backend database | SQLite |
| Backend architecture | Clean Architecture |
| Images | MinIO |
| Push notifications | Firebase Cloud Messaging |
| Admin portal | Blazor Web App .NET 10 |
| External product lookup | Provider abstraction, initially UPCitemdb |
| Containerization | Docker |
| Container registry | Private Nexus |
| Deployment | One Portainer stack |

.NET 10 and EF Core 10 are a good match for this project. EF Core 10 is the .NET 10 release and is an LTS release supported until November 2028. Microsoft maintains the EF Core SQLite provider directly.

---

# 2. High-Level Architecture

```text
                         ┌────────────────────┐
                         │     Keycloak       │
                         │   Existing server  │
                         └─────────┬──────────┘
                                   │ OIDC
                   ┌───────────────┴────────────────┐
                   │                                │
            Authorization Code              Authorization Code
                 + PKCE                         + PKCE
                   │                                │
        ┌──────────▼──────────┐          ┌─────────▼──────────┐
        │    Android App      │          │  Blazor Admin Web  │
        │                     │          │     .NET 10        │
        │ Kotlin / Compose    │          └─────────┬──────────┘
        │ Room / SQLite       │                    │
        │ CameraX / ML Kit    │                    │ HTTPS
        │ WorkManager         │                    │
        └──────────┬──────────┘                    │
                   │ HTTPS                          │
                   └──────────────┬─────────────────┘
                                  │
                         ┌────────▼─────────┐
                         │ ASP.NET Core API │
                         │    .NET 10       │
                         │                  │
                         │ Clean Arch.      │
                         │ AuthZ            │
                         │ Sync Engine      │
                         │ Moderation       │
                         └──────┬────┬──────┘
                                │    │
                    ┌───────────┘    └────────────┐
                    │                             │
             ┌──────▼───────┐             ┌──────▼──────┐
             │    SQLite    │             │    MinIO    │
             │              │             │             │
             │ Domain data  │             │ Game images │
             │ Sync data    │             │ Thumbnails  │
             │ Audit logs   │             └─────────────┘
             └──────┬───────┘
                    │
              ┌─────▼───────┐
              │ Background  │
              │ Processing  │
              │             │
              │ FCM         │
              │ Outbox      │
              │ Metadata    │
              └─────────────┘
```

The API is the only component that should directly access the backend SQLite database.

The Blazor portal **must not mount or open the SQLite file directly**. It communicates with the API.

Likewise, background processing should preferably run inside the API process rather than in a second container that independently opens the SQLite file.

That design decision is important because SQLite supports excellent lightweight transactional storage, and WAL mode improves reader/writer concurrency, but writers still serialize.

For the expected scale of a personal/community card-game collection application, this architecture should work very well.

If the application eventually becomes large enough that database concurrency becomes a bottleneck, the Infrastructure layer can later be replaced with PostgreSQL without redesigning the domain or API.

---

# 3. Core Domain Rules

The following rules should be treated as actual domain invariants rather than UI behavior.

## Users

A user authenticates through Keycloak.

The application identifies the person using the Keycloak:

```text
sub
```

claim.

The application must never use username as the actual identity key.

After the first successful Keycloak authentication, the Android app starts application onboarding.

The user enters:

```text
Display name
Username
```

Example:

```text
Display name: John Smith
Username: john
```

The UI renders the username as:

```text
#john
```

The `#` is presentation only and does not need to be stored in the database.

Usernames are globally unique and should be normalized for comparison:

```text
john
JOHN
John
```

must all be considered the same username.

A reasonable database representation is:

```text
Username = "John"
NormalizedUsername = "JOHN"
```

with a unique index on `NormalizedUsername`.

---

# 4. First Login / Onboarding

The first mobile authentication flow should be:

```text
Launch
  ↓
Keycloak login
  ↓
API validates token
  ↓
GET /api/v1/me
  ↓
Profile doesn't exist
  ↓
Onboarding
  ↓
Enter display name
  ↓
Choose unique username
  ↓
Create first collection
  ↓
Set collection as default
  ↓
Register Android device + FCM token
  ↓
Initial synchronization
  ↓
Home screen
```

The Android application never receives or handles the user's Keycloak password.

Keycloak explicitly recommends Authorization Code flow for native/mobile applications, and PKCE provides the appropriate protection for a public native client.

---

# 5. Keycloak Design

I recommend creating three logical Keycloak clients.

## Android client

Example client ID:

```text
gamecollector-android
```

Configuration:

```text
Client type: Public
Standard Flow: Enabled
Authorization Code Flow: Enabled
PKCE: S256 required
Direct Access Grants: Disabled
Implicit Flow: Disabled
```

There must be **no client secret inside the APK**.

Use AppAuth for Android and open the Keycloak authentication page using the system browser/Custom Tabs rather than an embedded WebView. AppAuth is designed around OAuth/OIDC native-app best practices.

Initially a redirect such as:

```text
com.example.gamecollector:/oauth2redirect
```

can be used.

When the app moves to Google Play, Android App Links can be introduced without redesigning the authentication architecture.

---

## Admin portal client

Example:

```text
gamecollector-admin-web
```

This is a confidential server-side client.

Configuration:

```text
Authorization Code Flow
PKCE S256
Client secret
HTTPS redirect URI
```

Example:

```text
https://games-admin.example.com/signin-oidc
```

The secret exists only inside the server environment/Portainer configuration.

Microsoft's current ASP.NET Core guidance recommends confidential OIDC code flow with PKCE for server-side web applications. Blazor Web Apps can use ASP.NET Core's standard OIDC authentication mechanisms.

---

## API

The API validates Keycloak access tokens using:

```text
Issuer
Audience
Expiration
Signature
```

The API should obtain signing keys from Keycloak's OIDC metadata/JWKS endpoint.

Global administrators receive the Keycloak role:

```text
gamecollector-admin
```

The application then has two completely different authorization concepts:

```text
Keycloak role
    ↓
Global administrator permissions
```

versus:

```text
Application database
    ↓
Collection Owner / Editor / Viewer
```

These must remain separate.

---

# 6. Collection Model

Users can belong to multiple collections.

Example:

```text
Our Card Games
My Games
Games at Parents
Work Games
```

Each user has:

```text
DefaultCollectionId
```

When the app launches, that collection is selected automatically.

Users can switch collections, but scanning/searching always operates against the currently selected collection.

---

## Collection roles

Permissions are:

| Action | Owner | Editor | Viewer |
|---|---:|---:|---:|
| View collection | ✓ | ✓ | ✓ |
| Search games | ✓ | ✓ | ✓ |
| Scan games | ✓ | ✓ | ✓ |
| Add game | ✓ | ✓ | |
| Remove game | ✓ | ✓ | |
| Invite members | ✓ | | |
| Change member role | ✓ | | |
| Remove member | ✓ | | |
| Rename collection | ✓ | | |
| Transfer ownership | ✓ | | |
| Delete collection | ✓ | | |

There is exactly **one Owner**.

I recommend storing:

```text
Collection.OwnerUserId
```

rather than trying to maintain multiple Owner membership rows.

Normal membership rows then contain:

```text
Editor
Viewer
```

When returning the collection through the API, the owner is presented as having the `Owner` role.

This naturally guarantees one owner.

---

# 7. Ownership Is Binary

There are deliberately **no quantities**.

A collection/game relationship answers exactly one question:

```text
Does this collection own this game?
```

So:

```text
Collection A + UNO Flip = Owned
```

is valid.

Adding it again does not create another record.

The database enforces uniqueness on:

```text
(CollectionId, GameId)
```

The scanner can therefore immediately display:

```text
✓ Already in Our Card Games
```

This is central to the purpose of the app.

There is no:

```text
Quantity
Purchase price
Condition
Storage location
Personal notes
```

on the collection/game relationship.

---

# 8. Personal Wishlist

Wishlists belong to users, not collections.

Example:

```text
User #john
    UNO No Mercy
    Trio
    Cardia
```

Even if John and another person share a collection, their wishlists remain independent.

Database uniqueness:

```text
(UserId, GameId)
```

A game has three relevant UI states:

```text
Owned in selected collection
On my wishlist
Neither
```

Both can technically be true briefly—for example, if a wishlist item is purchased but not yet removed—but the application should automatically remove the wishlist item when the user adds that game to a collection.

---

# 9. Global Game Catalog

Every variation is treated as a completely separate game.

For example:

```text
UNO
UNO Flip!
UNO All Wild!
UNO Show 'Em No Mercy
UNO Teams
```

are five independent `Game` records.

There is no parent/edition hierarchy in v1.

Each game has exactly one title.

No alternate names/aliases are stored.

---

# 10. Game Data Model

A global game can contain:

```text
Id
Title
Description
Publisher
ReleaseYear
MinimumPlayers
MaximumPlayers
MinimumAge
MinimumPlayingTimeMinutes
MaximumPlayingTimeMinutes
ModerationStatus
SubmittedByUserId
CreatedAtUtc
UpdatedAtUtc
Revision
```

Additional relational information includes:

```text
Barcodes[]
Languages[]
Tags[]
Images[]
```

---

## Languages

A game can have zero or more languages.

Example:

```text
Dutch
French
English
German
```

A multi-language game can therefore contain:

```text
Dutch
French
```

simultaneously.

Languages should be canonical database records rather than arbitrary strings.

---

## Tags

Tags are many-to-many.

Example:

```text
Family
Party
Cooperative
Strategy
Card Game
Fast
Two Player
```

Tags should be centrally managed by admins.

That avoids:

```text
Party
party
Party Game
party-game
```

becoming separate categories.

Users submitting games can select existing tags. Admins can adjust the tags during moderation.

---

# 11. Barcode Model

Barcode should **not** be a column directly on `Game`.

Instead:

```text
Game
  └── GameBarcodes[]
```

A game may therefore have:

```text
EAN-8
EAN-13
UPC-A
GTIN-14
```

or no barcode at all.

Example:

```text
Game: UNO Flip!

Barcodes:
  887961751062
  0887961751062
```

Each normalized barcode should map to only one game.

Database constraint:

```text
UNIQUE NormalizedBarcode
```

Barcodes should be validated both on Android and server-side, including the appropriate check digit.

Games without barcodes remain perfectly valid and can be discovered through manual title search.

---

# 12. Game Images

Each game should support at minimum:

```text
Front
Back
```

For user-created catalog entries, both should be requested.

Images do **not** belong inside SQLite.

SQLite stores only metadata:

```text
GameImage.Id
GameId
ImageType
OriginalObjectKey
ThumbnailObjectKey
ContentType
Width
Height
Checksum
CreatedAtUtc
```

The actual files reside in MinIO.

MinIO's .NET SDK supports pre-signed upload and download URLs, allowing a mobile client to upload directly to a private bucket without receiving MinIO credentials.

A good upload flow is:

```text
Android
  ↓
POST /media/upload-intents
  ↓
API creates secure object name
  ↓
API returns short-lived presigned MinIO PUT URL
  ↓
Android uploads image
  ↓
POST /media/{id}/complete
  ↓
API validates uploaded object
  ↓
Background thumbnail generation
```

The MinIO bucket remains private.

The mobile application never receives:

```text
MinIO access key
MinIO secret key
```

Object keys are generated by the server, for example:

```text
games/{gameId}/front/{imageId}.jpg
```

The app should store an image ID, not a temporary presigned URL.

URLs expire; IDs do not.

---

# 13. Image Capture

New-game creation supports both:

```text
Take photo
Choose from gallery
```

CameraX should be used for camera integration. Google recommends CameraX for most Android camera applications, and CameraX integrates directly with ML Kit analysis.

The Android client should:

```text
correct orientation
resize excessively large photos
compress before upload
```

The server must independently verify:

```text
file size
MIME type
actual image format
maximum dimensions
```

Never trust only the filename or Android-provided MIME type.

---

# 14. Barcode Scanner

Use:

```text
CameraX
+
ML Kit Barcode Scanning
```

ML Kit can decode barcodes locally on Android, including in real-time camera streams, and can be combined with CameraX for a custom scanner interface.

I recommend bundling the ML Kit barcode model instead of requiring a first-time model download.

That slightly increases APK size but means barcode detection is immediately available while offline.

This is important for the application's primary use case:

> standing inside a shop with bad connectivity.

The scanner should restrict detection to formats relevant to product barcodes where possible.

---

# 15. Barcode Scan UX

The scanner workflow should be:

```text
Scan barcode
     ↓
Look up barcode in local Room database
     ↓
Game found?
 ┌───────┴─────────┐
 Yes               No
 │                  │
Check selected      Online?
collection          │
 │            ┌─────┴─────┐
 │           Yes          No
 │            │            │
 │       API lookup     Create game
 │            │         manually
 │       External lookup
 │            │
 │        Found?
 │       ┌────┴────┐
 │      Yes        No
 │       │          │
 │    Prefill    Create
 │   submission   manually
```

If owned:

```text
┌─────────────────────────────┐
│ ✓ ALREADY IN COLLECTION     │
│                             │
│ UNO Flip!                   │
│ Our Card Games              │
│                             │
│ [ View details ]            │
│ [ Remove from collection ]  │
└─────────────────────────────┘
```

If not owned:

```text
┌─────────────────────────────┐
│ NOT IN COLLECTION           │
│                             │
│ UNO Flip!                   │
│                             │
│ [ Add to collection ]       │
│ [ Add to wishlist ]         │
└─────────────────────────────┘
```

The "already owned" state should be intentionally impossible to miss.

That is the app's primary value proposition.

---

# 16. External Product Lookup

External lookup must be considered **enrichment**, not truth.

The provider abstraction should live in the Application/Infrastructure boundary:

```csharp
public interface IProductMetadataProvider
{
    Task<ProductMetadataCandidate?> LookupBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken);
}
```

An initial provider can be UPCitemdb, whose API supports lookup by UPC, EAN, GTIN and ISBN.

Barcode Lookup is another possible provider and exposes product lookup using UPC/EAN identifiers.

The provider chain could eventually be:

```text
Our catalog
   ↓
UPCitemdb
   ↓
Barcode Lookup
   ↓
Manual entry
```

But the application should **never automatically add external data to the approved catalog**.

Instead:

```text
External result
     ↓
Prefill submission form
     ↓
User verifies/corrects
     ↓
User adds own photos
     ↓
Submit
     ↓
Admin moderation
```

This prevents external bad data from polluting your catalog.

It also isolates vendor dependencies.

If UPCitemdb changes pricing or disappears, only:

```text
Infrastructure.ExternalCatalog.UpcItemDb
```

needs replacing.

External API results can be cached server-side for a limited period to reduce repeated API requests.

Third-party images should not automatically be copied into MinIO unless the provider's licensing explicitly permits it.

Your own front/back photographs should remain the canonical image source.

---

# 17. Catalog Moderation

A game has moderation states:

```text
Pending
NeedsChanges
Approved
Rejected
```

Normal user submission:

```text
Unknown game
   ↓
Create game
   ↓
Add metadata
   ↓
Front photo
   ↓
Back photo
   ↓
Submit
   ↓
Pending
```

A pending game is immediately usable by the submitting user in their collection.

It is **not globally discoverable**.

Visibility rules:

```text
Approved
→ searchable by every authenticated user

Pending / NeedsChanges
→ submitter
→ members of a collection already containing it
→ administrators

Rejected
→ submitter and administrators only
→ not discoverable globally
```

---

# 18. Admin Moderation Workflow

Admin portal:

```text
Pending Games
     ↓
Open submission
     ↓
Review:
  title
  publisher
  barcode
  player count
  age
  languages
  tags
  front/back photos
     ↓
Decision
```

Possible decisions:

```text
Approve
Needs Changes
Reject
```

### Approve

```text
Status = Approved
ApprovedBy = Admin
ApprovedAt = UTC
```

A catalog synchronization event is emitted.

The submitting user gets:

```text
Your game "Karakum" has been approved.
```

### Needs Changes

Admin enters a required comment:

```text
The back photo is too blurry to verify the EAN.
```

User gets an in-app + FCM notification.

The submission becomes editable again.

### Reject

Used for:

```text
spam
invalid submission
not actually a game
irrecoverable duplicate
```

The reason is stored.

---

# 19. Suggested Changes to Approved Games

Approved catalog records are never edited directly by ordinary users.

Instead:

```text
Approved Game
     ↓
Suggest correction
     ↓
GameChangeRequest
     ↓
Admin review
     ↓
Approve / Reject
```

The change request should contain a proposed patch rather than changing the active game.

Conceptually:

```text
GameChangeRequest
{
    GameId
    ProposedByUserId
    ProposedChanges
    ProposedImages
    Status
    AdminComment
}
```

The admin portal displays a diff:

```text
Minimum players

Current: 3
Proposed: 2
```

On approval, changes are applied in one transaction.

This preserves catalog integrity and gives you a clean audit history.

---

# 20. Admin-Created Games

Admins can create catalog records directly.

Admin-created games do not require the normal moderation queue.

Flow:

```text
Admin → New Game
      → metadata
      → barcode(s)
      → languages
      → tags
      → images
      → Publish
```

Result:

```text
Status = Approved
```

---

# 21. Android Offline-First Architecture

Android should follow:

```text
Compose UI
    ↓
ViewModel
    ↓
Use cases
    ↓
Repository
    ↓
Room
```

and separately:

```text
Repository
    ↓
Sync engine
    ↓
HTTP API
```

The UI should **never normally read data directly from the network response**.

Instead:

```text
API response
   ↓
Room
   ↓
Flow
   ↓
ViewModel
   ↓
Compose
```

Room is therefore the Android source of truth.

This matches Android's official offline-first architecture guidance, where repositories coordinate local and network data sources and the UI observes local data.

---

# 22. Android Local Database

Suggested Room tables include:

```text
LocalGame
LocalGameBarcode
LocalGameLanguage
LocalGameTag
LocalGameImage
LocalCollection
LocalCollectionGame
LocalWishlistItem
LocalInvitation
LocalNotification

PendingMutation
PendingMediaUpload
SyncScopeState
```

This is a **mobile projection** of the server data model.

It should not attempt to duplicate every server table.

For example:

```text
AuditLog
ModerationHistory
OutboxMessage
```

are unnecessary on the phone.

---

# 23. Offline Search

Approved catalog metadata should be synchronized to Room.

That allows:

```text
barcode lookup
manual title search
owned lookup
wishlist lookup
```

without internet.

Images should not all be downloaded.

Instead:

```text
Metadata → Room
Images → on-demand cache
```

Previously viewed thumbnails can remain available offline using the Android image cache.

This keeps initial synchronization small while preserving the important offline functionality.

---

# 24. Android Project Structure

A modular structure could be:

```text
android/
├── app/
├── core/
│   ├── auth/
│   ├── database/
│   ├── network/
│   ├── data/
│   ├── domain/
│   ├── model/
│   ├── designsystem/
│   └── sync/
│
└── feature/
    ├── onboarding/
    ├── home/
    ├── scanner/
    ├── search/
    ├── game/
    ├── submitgame/
    ├── collections/
    ├── members/
    ├── wishlist/
    ├── notifications/
    └── settings/
```

Recommended Android libraries/components:

```text
Jetpack Compose
Navigation Compose
Room
WorkManager
CameraX
ML Kit
Hilt
Retrofit/OkHttp
Kotlin Coroutines
Flow
```

WorkManager is specifically intended for persistent work that should survive application restarts and device reboots, making it appropriate for deferred synchronization.

---

# 25. Sync Architecture

Synchronization is the technically most important part of the Android application.

I would not implement generic "upload database/downloading database".

Instead, use an **operation + change-feed protocol**.

There are two directions:

```text
ANDROID → SERVER
Pending mutations
```

and:

```text
SERVER → ANDROID
Incremental change feed
```

---

# 26. Local Mutations

Suppose the phone is offline and the user adds UNO Flip.

Room immediately changes:

```text
CollectionGame.IsOwned = true
```

and creates:

```text
PendingMutation
{
    MutationId
    Type = AddCollectionGame
    CollectionId
    GameId
    CreatedAt
}
```

The UI immediately shows the game as owned.

No internet is required.

When connectivity returns, WorkManager sends the mutation to the API.

---

# 27. Mutation IDs and Idempotency

Every operation gets a UUID:

```text
8ecc2177-0ec7-47ad-b699-...
```

API database:

```text
ProcessedMutation
{
    MutationId
    UserId
    ProcessedAt
    Result
}
```

with:

```text
UNIQUE(UserId, MutationId)
```

If Android sends the same operation three times because of network failures:

```text
request 1 → applied
request 2 → recognized duplicate
request 3 → recognized duplicate
```

The game is still added once.

This is essential for reliable offline sync.

---

# 28. Server Ordering / Last Write Wins

You selected:

> Last one added wins.

For this architecture I would define this precisely as:

> **The last valid mutation accepted by the server wins.**

Do not compare phone timestamps.

Phones can have incorrect clocks.

Instead every synchronized server change receives a monotonically increasing:

```text
ServerSequence
```

Example:

```text
15481 → John adds UNO
15482 → Sophie removes UNO
15483 → John adds Trio
```

The higher sequence wins.

Consider:

```text
John offline:
Remove UNO

Sophie online:
Add UNO

John reconnects later:
Remove UNO arrives
```

Because John's remove reaches the API last, it receives the later server sequence and therefore wins.

That gives deterministic conflict resolution without presenting users with a conflict screen.

---

# 29. Tombstones

Deletes cannot simply disappear during synchronization.

Otherwise:

```text
Device A deletes item
Device B still has item
Device B syncs
→ item accidentally comes back
```

Instead collection ownership can internally retain:

```text
IsOwned = false
LastServerSequence = 15482
```

The row acts as a tombstone.

The UI only queries:

```text
IsOwned = true
```

The same approach can be used for wishlist removal.

---

# 30. Sync Scopes

Rather than one massive global cursor, use synchronization scopes.

Examples:

```text
Catalog
User:{UserId}
Collection:{CollectionId}
```

Android stores:

```text
Catalog cursor
User cursor
Collection A cursor
Collection B cursor
```

A request can batch them:

```json
{
  "scopes": [
    { "type": "catalog", "cursor": 1201 },
    { "type": "user", "cursor": 891 },
    { "type": "collection", "id": "...", "cursor": 772 }
  ]
}
```

This solves an important problem when someone joins a new collection.

Their existing global cursor might already be newer than all historical changes in that collection.

Instead:

```text
New collection
→ new scope
→ cursor = 0
→ current collection snapshot
```

No history is missed.

---

# 31. Sync API

Suggested endpoints:

```text
POST /api/v1/sync/push
POST /api/v1/sync/pull
GET  /api/v1/sync/bootstrap
```

### Push

```text
Android pending mutations
        ↓
Authorization validation
        ↓
Idempotency check
        ↓
Application service
        ↓
Database transaction
        ↓
Server sequence
        ↓
SyncEvent
```

### Pull

Returns changes after the client's cursor.

Example:

```json
{
  "changes": [
    {
      "sequence": 15483,
      "scope": "collection",
      "operation": "collectionGameChanged",
      "entityId": "...",
      "payload": {}
    }
  ],
  "nextCursor": 15483
}
```

Changes should be paginated.

For example:

```text
500 events per response
```

The client continues until:

```text
hasMore = false
```

---

# 32. Initial Synchronization

When installing/logging in on a phone:

```text
Authenticate
   ↓
Activate device
   ↓
Bootstrap
```

Bootstrap downloads:

```text
user profile
collections
memberships
owned game relationships
personal wishlist
pending invitations
recent notifications
accessible pending games
approved catalog metadata
barcodes
languages
tags
```

Images remain lazy-loaded.

---

# 33. Sync Event Retention

`SyncEvent` will eventually grow.

It does not have to live forever.

A future retention policy can remove sufficiently old events.

If a phone presents a cursor older than the retained history:

```text
HTTP 409 / SyncResetRequired
```

or equivalent domain response.

Android then performs a fresh scope snapshot.

That prevents an indefinitely growing change-log from becoming mandatory.

---

# 34. Sync Timing

Synchronization runs:

```text
After local changes when online
At app startup
When app returns to foreground
Periodically through WorkManager
After relevant FCM data notifications
```

FCM must be treated as an optimization, not as the synchronization mechanism.

The system remains correct if an FCM message never arrives.

Firebase describes FCM as a way to notify client applications that new data is available to synchronize, which fits this model well.

---

# 35. One Active Device Per User

Each user has at most one active Android installation.

Server:

```text
DeviceRegistration
{
    UserId
    DeviceId
    FcmToken
    ActivatedAt
    LastSeenAt
}
```

with:

```text
UNIQUE(UserId)
```

The phone generates an installation identifier.

Every mobile API request includes:

```text
X-Device-Id
```

The API verifies:

```text
Token subject == User
AND
DeviceId == CurrentActiveDevice
```

If another phone logs in:

```text
New device
   ↓
Activate
   ↓
Previous DeviceRegistration replaced
   ↓
Old FCM token removed
   ↓
Old phone's API requests rejected
```

This implements the single-device requirement independently from Keycloak.

---

# 36. Push Notifications

FCM is responsible only for Android push delivery.

The actual notification is stored first:

```text
Notification
{
    Id
    UserId
    Type
    Payload
    CreatedAtUtc
    ReadAtUtc
}
```

Then FCM sends a notification referring to that ID.

Therefore:

```text
Dismiss Android notification
```

does **not** lose the notification.

It remains visible under:

```text
Notifications
```

inside the application.

Firebase Cloud Messaging supports device-targeted notifications and data messages for Android clients.

---

# 37. Notification Types

Initial notification events should include:

```text
Collection invitation received
Invitation accepted
Invitation declined

Game submission approved
Game submission needs changes
Game submission rejected

Suggested edit approved
Suggested edit rejected
```

Collection add/remove actions should normally **not** produce visible phone notifications.

That would quickly become noisy.

They may cause a silent sync hint instead.

---

# 38. Collection Invitations

Users are already registered before they can be found.

Invite screen:

```text
┌───────────────────────────────┐
│ Invite user                   │
│                               │
│ [ Username ] [ Name ]         │
│                               │
│ Search...                     │
│                               │
│ John Smith                    │
│ #john                         │
│                    [ Invite ] │
└───────────────────────────────┘
```

Two search modes:

```text
Username
Name
```

Username is exact/strongly prioritized.

Name may return multiple users.

The API should not provide a browse-all-users endpoint.

Only limited search results should be returned after entering a search query.

Invitation:

```text
Invitation
{
    CollectionId
    InvitedUserId
    InvitedByUserId
    Role
    Status
    CreatedAtUtc
}
```

Possible statuses:

```text
Pending
Accepted
Declined
Cancelled
```

Invite roles:

```text
Editor
Viewer
```

The Owner role cannot be granted through a normal invitation.

Ownership uses the dedicated transfer workflow.

---

# 39. Account Deletion

Account deletion must first verify:

```text
Does user own any collections?
```

If yes:

```text
Transfer ownership first
```

Then:

```text
Remove memberships
Delete personal wishlist
Delete device registration
Remove pending invitations
Remove profile
```

Approved catalog games remain.

Pending submitted games can be deleted where no other active collection depends on them.

Audit records should not silently disappear because that would destroy the audit chain; any retained actor information should instead be anonymized where appropriate.

---

# 40. Backend Clean Architecture

Recommended solution:

```text
GameCollector.sln

src/
├── GameCollector.Api/
├── GameCollector.Application/
├── GameCollector.Domain/
├── GameCollector.Infrastructure/
├── GameCollector.Contracts/
└── GameCollector.AdminWeb/

tests/
├── GameCollector.Domain.Tests/
├── GameCollector.Application.Tests/
├── GameCollector.Infrastructure.Tests/
├── GameCollector.Api.Tests/
└── GameCollector.Architecture.Tests/
```

Dependency direction:

```text
Domain
  ↑
Application
  ↑
Infrastructure
  ↑
API
```

More accurately:

```text
Domain
↑
Application
↑       ↑
API   Infrastructure
```

Infrastructure implements abstractions declared by Application.

The Domain project references no EF Core, ASP.NET Core, MinIO, Firebase or Keycloak packages.

---

# 41. Domain Layer

Contains:

```text
Entities
Value Objects
Enums
Domain Errors
Domain Events
Business invariants
```

Examples:

```text
Game
Collection
CollectionMember
GameBarcode
GameChangeRequest
```

It should not contain:

```text
DbContext
HttpContext
JWT parsing
Firebase
Controllers
MinIO
```

---

# 42. Application Layer

Contains use cases and interfaces.

Examples:

```text
CreateCollection
AddGameToCollection
RemoveGameFromCollection
InviteCollectionMember
AcceptInvitation
TransferCollectionOwnership

SubmitGame
ApproveGame
RejectGame
RequestGameChanges

AddWishlistItem
RemoveWishlistItem

SynchronizeDevice
```

Infrastructure dependencies are represented as interfaces:

```text
IGameRepository
ICollectionRepository
IUnitOfWork
IObjectStorage
IPushNotificationSender
ICurrentUser
IProductMetadataProvider
IAuditWriter
```

---

# 43. Generic Repository Design

You explicitly requested generic repositories/services.

That is reasonable, but I recommend **not making every domain operation generic**.

A good base abstraction is:

```csharp
public interface IRepository<TEntity, TId>
    where TEntity : class
{
    Task<TEntity?> GetByIdAsync(
        TId id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default);

    void Remove(TEntity entity);
}
```

Then specialized repositories extend it.

Example:

```csharp
public interface IGameRepository
    : IRepository<Game, Guid>
{
    Task<Game?> FindByBarcodeAsync(
        string barcode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Game>> SearchApprovedAsync(
        string query,
        int limit,
        CancellationToken cancellationToken = default);
}
```

And:

```csharp
public interface ICollectionRepository
    : IRepository<Collection, Guid>
{
    Task<Collection?> GetWithMembershipsAsync(
        Guid collectionId,
        CancellationToken cancellationToken = default);
}
```

Do **not** expose:

```csharp
IQueryable<TEntity>
```

outside Infrastructure.

That leaks EF Core behavior into Application and defeats part of Clean Architecture.

---

# 44. Generic Services

Generic CRUD services are appropriate for simple admin reference data:

```text
Languages
Tags
```

For example:

```csharp
public interface ICrudService<
    TId,
    TRead,
    TCreate,
    TUpdate>
{
    Task<TRead?> GetAsync(TId id);
    Task<IReadOnlyList<TRead>> GetAllAsync();
    Task<TRead> CreateAsync(TCreate request);
    Task<TRead> UpdateAsync(TId id, TUpdate request);
    Task DeleteAsync(TId id);
}
```

But:

```text
ApproveGame
InviteUser
TransferOwnership
SynchronizeDevice
```

should **not** be forced into generic CRUD.

Those are domain workflows and should have dedicated application services/use cases.

This gives you generic infrastructure without turning the business layer into an abstract framework.

---

# 45. Infrastructure Layer

Contains:

```text
Persistence/
    AppDbContext
    EF configurations
    migrations
    repositories

Authentication/
    Keycloak claims mapping

Storage/
    MinioObjectStorage

Notifications/
    FirebasePushNotificationSender

ExternalCatalog/
    UpcItemDbProvider
    BarcodeLookupProvider

Background/
    OutboxProcessor
    ThumbnailProcessor

Sync/
    SyncEventRepository
```

---

# 46. Backend SQLite Design

SQLite file:

```text
/data/gamecollector.db
```

Recommended configuration:

```text
Foreign keys enabled
WAL journaling
Busy timeout
Short transactions
```

WAL mode allows readers and the writer to operate more effectively concurrently, but SQLite still fundamentally serializes writes.

For this reason:

> **Only the API container should own the backend SQLite file.**

Do not have:

```text
API container → SQLite
Admin container → SQLite
Worker container → SQLite
```

Instead:

```text
Admin → API → SQLite
```

and:

```text
BackgroundService inside API → SQLite
```

This dramatically reduces potential lock contention.

If concurrent writes do become noticeable, an application-level write coordinator can serialize write transactions through one `SemaphoreSlim`.

Given the expected workload, that is preferable to prematurely introducing a larger database server.

---

# 47. EF Core Concurrency

SQLite does not have SQL Server's automatic `rowversion`.

Use an application-managed:

```text
Revision : long
```

on entities where optimistic concurrency matters.

Configure it as an EF concurrency token.

EF Core supports application-managed concurrency tokens for databases where automatic database-generated tokens aren't appropriate.

For example, admin A and admin B edit the same game.

Admin A saves:

```text
Revision 7 → Revision 8
```

Admin B still submits:

```text
Revision 7
```

API returns:

```text
409 Conflict
```

This is different from mobile sync.

Mobile collection conflicts use server-sequence **last-write-wins**.

Admin catalog editing uses optimistic concurrency because silently overwriting an administrator's metadata edit would be undesirable.

---

# 48. Core Server Entities

A practical initial entity set is:

| Entity | Purpose |
|---|---|
| UserProfile | Application identity/profile |
| DeviceRegistration | One active Android phone |
| Collection | Shared collection |
| CollectionMember | Editor/viewer memberships |
| CollectionGame | Owned/not-owned state |
| CollectionInvitation | Pending invites |
| Game | Global catalog game |
| GameBarcode | Multiple barcodes |
| GameImage | MinIO image metadata |
| Language | Canonical languages |
| GameLanguage | Game-language relationship |
| Tag | Canonical category/tag |
| GameTag | Game-tag relationship |
| WishlistItem | Personal wishlist |
| GameChangeRequest | Suggested catalog corrections |
| Notification | In-app notifications |
| AuditLog | Audit history |
| SyncEvent | Incremental synchronization feed |
| ProcessedMutation | Sync idempotency |
| OutboxMessage | Reliable background actions |
| ExternalLookupCache | External provider cache |

All primary IDs should be globally unique UUIDs.

Using client-generatable UUIDs is particularly useful offline because Android can create records before contacting the API.

---

# 49. Outbox Pattern

Consider approving a game.

The system must:

```text
Update game
Write audit record
Write sync event
Notify user
Send FCM
```

Do not perform the Firebase call inside the database transaction.

Instead:

```text
Transaction:
    Update Game
    Insert AuditLog
    Insert SyncEvent
    Insert Notification
    Insert OutboxMessage
COMMIT
```

Then a hosted background service reads:

```text
OutboxMessage
```

and sends FCM.

If Firebase is temporarily unavailable:

```text
database transaction remains successful
notification remains pending
background service retries
```

This is much more reliable.

The same pattern can handle:

```text
image processing
external side effects
MinIO cleanup
```

---

# 50. API Design

Use:

```text
/api/v1/
```

from day one.

Logical route groups:

## Profile

```text
GET    /api/v1/me
POST   /api/v1/me/onboarding
PATCH  /api/v1/me
DELETE /api/v1/me

PUT    /api/v1/me/default-collection
```

## Device

```text
POST   /api/v1/me/device/activate
DELETE /api/v1/me/device
```

## Users

```text
GET /api/v1/users/search?type=username&q=john
GET /api/v1/users/search?type=name&q=John
```

## Collections

```text
GET    /api/v1/collections
POST   /api/v1/collections
GET    /api/v1/collections/{id}
PATCH  /api/v1/collections/{id}
DELETE /api/v1/collections/{id}

POST /api/v1/collections/{id}/transfer-ownership
```

## Collection games

```text
GET    /api/v1/collections/{id}/games
PUT    /api/v1/collections/{id}/games/{gameId}
DELETE /api/v1/collections/{id}/games/{gameId}
```

## Members

```text
GET    /api/v1/collections/{id}/members
PATCH  /api/v1/collections/{id}/members/{userId}
DELETE /api/v1/collections/{id}/members/{userId}
```

## Invitations

```text
POST /api/v1/collections/{id}/invitations

GET  /api/v1/me/invitations

POST /api/v1/invitations/{id}/accept
POST /api/v1/invitations/{id}/decline
```

## Catalog

```text
GET /api/v1/games
GET /api/v1/games/{id}
GET /api/v1/games/search?q=uno
GET /api/v1/games/barcode/{barcode}
```

## Submission

```text
POST /api/v1/game-submissions
GET  /api/v1/game-submissions/mine
GET  /api/v1/game-submissions/{id}
PUT  /api/v1/game-submissions/{id}
POST /api/v1/game-submissions/{id}/submit
```

## Suggested corrections

```text
POST /api/v1/games/{gameId}/change-requests
GET  /api/v1/change-requests/mine
```

## Wishlist

```text
GET    /api/v1/me/wishlist
PUT    /api/v1/me/wishlist/{gameId}
DELETE /api/v1/me/wishlist/{gameId}
```

## Notifications

```text
GET  /api/v1/me/notifications
POST /api/v1/me/notifications/{id}/read
POST /api/v1/me/notifications/read-all
```

## Sync

```text
POST /api/v1/sync/push
POST /api/v1/sync/pull
GET  /api/v1/sync/bootstrap
```

---

# 51. Admin API

Keep admin routes visibly separate:

```text
/api/v1/admin/
```

Examples:

```text
GET  /admin/submissions
GET  /admin/submissions/{id}
POST /admin/submissions/{id}/approve
POST /admin/submissions/{id}/needs-changes
POST /admin/submissions/{id}/reject

GET  /admin/change-requests
POST /admin/change-requests/{id}/approve
POST /admin/change-requests/{id}/reject

POST /admin/games
PUT  /admin/games/{id}

GET  /admin/users
GET  /admin/users/{id}
POST /admin/users/{id}/disable
POST /admin/users/{id}/enable
POST /admin/users/{id}/revoke-device

GET /admin/collections
GET /admin/collections/{id}

GET /admin/audit
```

Every `/admin` endpoint independently requires:

```text
gamecollector-admin
```

Do not rely on Blazor hiding a page.

The API is the security boundary.

---

# 52. Admin Blazor Portal

Recommended pages:

```text
Dashboard

Catalog
    Games
    Create game
    Edit game

Moderation
    Pending submissions
    Needs changes
    Rejected
    Change requests

Reference Data
    Tags
    Languages

Users
    Search
    User details
    Collections
    Active device
    Disable account
    Revoke device

Collections
    Search
    Members
    Games
    Owner

Audit
    Search
    Filters
    Event details

Diagnostics
    Recent sync failures
    Notification failures
    External metadata lookup failures
```

The admin portal communicates only with the REST API.

It should not reference `Infrastructure` or `DbContext`.

---

# 53. Audit Log

Important events should generate append-only audit entries.

Example:

```text
2026-08-15 18:42 UTC
Actor: admin-user-id
Action: GameApproved
Entity: Game/1842
```

Record:

```text
AuditLog.Id
ActorUserId
Action
EntityType
EntityId
TimestampUtc
CorrelationId
DeviceId
IPAddress
BeforeJson
AfterJson
```

Not every operation needs before/after JSON.

For example:

```text
Login
Device revoked
Invitation accepted
```

can use structured metadata.

Sensitive values such as:

```text
JWT
Refresh token
Keycloak secret
Firebase credentials
MinIO secret
```

must never enter the audit payload.

---

# 54. Sync Diagnostics

Do not mix synchronization diagnostics with the domain audit log.

Have separate diagnostic information such as:

```text
User
Device
Last successful sync
Last cursor
Number of uploaded mutations
Number of downloaded events
Last error
```

This allows an admin to answer:

> “Why isn't my girlfriend's phone seeing the game I just added?”

without filling the security audit log with thousands of routine synchronization entries.

---

# 55. Notifications and FCM Security

FCM payloads should contain minimal information.

Prefer:

```json
{
  "notificationId": "...",
  "type": "CollectionInvitation"
}
```

instead of sending all sensitive application data through the push payload.

When opened:

```text
Android → API/Room → actual notification data
```

The backend should use the current Firebase HTTP v1 mechanism or Firebase Admin SDK rather than embedding obsolete server keys in the application. Firebase documents the HTTP v1 endpoint for server-side message delivery.

---

# 56. Security Requirements

All external communication must use HTTPS.

The API must perform authorization at the resource level.

For example:

```text
DELETE /collections/{id}/games/{gameId}
```

must verify:

```text
Authenticated
AND
User has access to Collection
AND
Role = Owner OR Editor
```

Never trust:

```text
role
collectionId
userId
```

simply because Android supplied them.

Other protections should include:

```text
request size limits
image size limits
rate limiting
barcode validation
username normalization
input length validation
external API timeouts
MinIO private buckets
short-lived signed URLs
structured security logging
```

Secrets belong only in server configuration.

---

# 57. Android Token Security

Do not store tokens in Room.

Authentication state should be stored separately using Android secure/Keystore-backed storage.

Never write access or refresh tokens to:

```text
Logcat
Crash reports
Analytics
SQLite
```

Logout should:

```text
remove local tokens
revoke/clear local device session
clear user-specific Room data
perform Keycloak logout when appropriate
```

---

# 58. SQLite and Scaling Limit

The chosen backend database is perfectly reasonable for this initial self-hosted application, provided its limits are respected.

Specifically:

```text
one API replica
one SQLite file
short write transactions
images outside SQLite
WAL enabled
```

The architecture should intentionally **not** support:

```text
5 API replicas sharing /data/gamecollector.db
```

If that becomes necessary, it is the signal to move the Infrastructure persistence implementation to PostgreSQL.

The repositories/application layer mean that migration does not require redesigning:

```text
Android
API contracts
Domain
Application services
Keycloak
MinIO
FCM
Admin UI
```

although database migrations and provider-specific persistence code obviously still need changing.

---

# 59. Portainer Deployment

The application should be deployed as one Portainer stack.

Conceptually:

```yaml
services:

  gamecollector-api:
    image: nexus/.../gamecollector-api:<version>

  gamecollector-admin:
    image: nexus/.../gamecollector-admin:<version>

  minio:
    image: minio/...

volumes:

  gamecollector-db:
  gamecollector-minio:
```

Keycloak is external.

Nexus is external.

Firebase is external.

External product metadata services are external.

---

# 60. Container Responsibilities

## gamecollector-api

Contains:

```text
ASP.NET Core REST API
EF Core
SQLite access
Sync engine
Outbox worker
FCM sender
MinIO integration
External metadata integration
```

Persistent mount:

```text
/data/
```

SQLite:

```text
/data/gamecollector.db
```

---

## gamecollector-admin

Contains:

```text
Blazor Web App
OIDC client
API client
```

No database volume.

No MinIO credentials unless absolutely necessary.

Prefer:

```text
Admin → API → MinIO
```

---

## MinIO

Persistent volume:

```text
/data
```

Bucket:

```text
gamecollector-media
```

Private access.

---

# 61. Nexus Image Strategy

Example repository names:

```text
nexus.example.com/gamecollector/api
nexus.example.com/gamecollector/admin
```

Tags:

```text
1.0.0
1.0.1
1.1.0
```

and optionally immutable commit tags:

```text
1.0.0-a83fc91
```

Avoid deploying only:

```text
latest
```

because rolling back becomes ambiguous.

Portainer should reference an explicit release version.

---

# 62. Configuration

API environment/secrets will require values conceptually such as:

```text
KEYCLOAK_AUTHORITY
KEYCLOAK_AUDIENCE

MINIO_ENDPOINT
MINIO_ACCESS_KEY
MINIO_SECRET_KEY
MINIO_BUCKET

FIREBASE_PROJECT_ID
FIREBASE_CREDENTIALS

UPCITEMDB_API_KEY

SQLITE_CONNECTION_STRING
```

The credentials must be supplied through deployment configuration/secret management and never baked into Docker images.

---

# 63. Health Endpoints

Expose:

```text
/health/live
/health/ready
```

`live` means:

```text
ASP.NET process functioning
```

`ready` can check essential infrastructure such as:

```text
SQLite
MinIO
```

Portainer/Docker health checks can then detect failed containers.

Do not make readiness depend on every external metadata provider; a UPC API outage should not make the entire Game Collector API unavailable.

---

# 64. Android User Experience

The primary home screen should optimize for the actual problem, not catalog administration.

I would make the hierarchy:

```text
Selected Collection ▼

[ Search games... ]

        SCAN GAME

Owned games

Wishlist shortcut

Notifications
```

The barcode scanner should be reachable in one tap.

Collection selection should remain visible.

A small persistent label such as:

```text
Our Card Games ▼
```

helps prevent accidentally adding a game to the wrong collection.

---

# 65. New Game Wizard

If a barcode is unknown:

```text
Step 1
Barcode
→ automatically populated

Step 2
Basic information
→ Title
→ Publisher
→ Release year

Step 3
Game information
→ Players
→ Playing time
→ Minimum age

Step 4
Languages
→ multi-select

Step 5
Tags
→ existing canonical tags

Step 6
Front photo

Step 7
Back photo

Step 8
Review

Submit
```

If an external provider found metadata, those fields are prefilled.

The user still reviews them.

The entire draft can be stored in Room, meaning someone can start creating a game without network connectivity.

Images remain local pending upload.

Once online:

```text
upload media
submit metadata
create game submission
add to selected collection
sync
```

---

# 66. Manual Search

Manual search should search local Room first.

Results can show:

```text
Thumbnail
Title
Owned status
Wishlist status
```

Example:

```text
UNO Flip!
✓ In Our Card Games

UNO Teams
Not owned

UNO No Mercy
♡ Wishlist
```

If online, a server search can augment local results if the catalog has changed but has not yet synchronized.

---

# 67. Default Collection

`DefaultCollectionId` belongs to the user profile.

Rules:

```text
must reference a collection the user can access
```

If that collection is deleted or the user is removed:

```text
DefaultCollectionId = another accessible collection
```

or:

```text
null
```

until the user chooses one.

When creating the first collection during onboarding, set it automatically as default.

---

# 68. Owner Transfer

Because a collection has exactly one owner, leaving/deleting an account requires explicit transfer.

Flow:

```text
Owner
  ↓
Transfer ownership
  ↓
Select existing Editor/Viewer
  ↓
Confirm
  ↓
Transaction:
   New owner assigned
   Old owner becomes Editor or leaves
```

The exact action is explicit in the UI.

An owner cannot delete their user account while owning collections.

---

# 69. Admin User Management

An application administrator should be able to:

```text
search users
view profile
view memberships
view owned collections
view active device
view submissions
disable application access
re-enable application access
revoke active device
```

Application disable should live in your database:

```text
UserProfile.IsDisabled
```

The user may technically still authenticate with Keycloak, but the Game Collector API responds with forbidden access.

That avoids giving your application broad Keycloak administration credentials unnecessarily.

---

# 70. API Contracts

Create:

```text
GameCollector.Contracts
```

for stable API DTOs.

Example:

```text
GameDto
CollectionDto
CollectionMemberDto
NotificationDto
GameSubmissionDto
```

Generate an OpenAPI specification from the ASP.NET API.

Android can then either:

```text
generate a Kotlin client
```

or implement the contract using Retrofit.

The admin application can consume the same contract through a generated/typed C# client.

This reduces accidental differences between:

```text
Android assumptions
Admin assumptions
API reality
```

---

# 71. Error Model

Use one consistent error format.

Examples:

```text
400 Invalid request
401 Not authenticated
403 Not allowed
404 Entity missing
409 Conflict
422 Domain validation error
429 Too many requests
500 Unexpected error
```

Examples of domain codes:

```text
username_already_exists
game_already_owned
collection_access_denied
owner_transfer_required
barcode_already_exists
device_not_active
sync_reset_required
submission_not_editable
```

Android should react to error codes, not parse English error messages.

---

# 72. Testing Strategy

The backend needs several layers of automated tests.

### Domain tests

Examples:

```text
Editor can remove game
Viewer cannot add game
Only owner can invite
Owner cannot leave without transfer
Barcode cannot belong to two games
```

### Application tests

Examples:

```text
Approve submission
Accept invitation
Transfer ownership
Delete account
```

### SQLite integration tests

Use a real SQLite database.

Do not substitute an unrelated EF in-memory provider for all persistence tests, because SQLite-specific constraints and transaction behavior matter.

### API integration tests

Test complete endpoints including:

```text
authentication
authorization
validation
serialization
database writes
```

### Sync tests

This area deserves extensive coverage:

```text
duplicate mutation
mutation retry
offline add
offline delete
two users editing same collection
out-of-order network delivery
stale cursor
scope reset
bootstrap
tombstone
device replacement
```

### Android tests

Cover:

```text
Room DAOs
repositories
sync engine
ViewModels
Compose screens
offline behavior
collection switching
scanner state machine
```

### Security matrix

Explicitly test:

```text
Viewer → DELETE game → 403
Editor → DELETE game → success
Editor → invite → 403
Owner → invite → success
Normal user → admin API → 403
Admin → admin API → success
```

---

# 73. Logging and Observability

Use structured server logs.

Every request should have a correlation ID.

Important log properties:

```text
CorrelationId
UserId
DeviceId
RequestPath
HTTP status
Duration
```

Never log:

```text
Authorization header
JWT
Refresh token
Passwords
MinIO credentials
Firebase credentials
Keycloak secret
```

Useful metrics include:

```text
API request duration
SQLite lock failures
sync mutations processed
sync failures
outbox backlog
FCM failures
MinIO failures
external lookup latency
```

---

# 74. Recommended Implementation Order

## Phase 1 — Foundation

Build:

```text
.NET solution
Clean Architecture projects
EF Core/SQLite
Docker images
Keycloak authentication
Android Compose project
Android OIDC authentication
```

Goal:

```text
Android login → authenticated API request
Admin login → authenticated admin API request
```

---

## Phase 2 — Users and Collections

Implement:

```text
onboarding
username
display name
collections
default collection
Owner/Editor/Viewer
collection switching
```

At this point two users can share a collection.

---

## Phase 3 — Catalog

Implement:

```text
Game
Barcode
Languages
Tags
Search
Game detail
```

Seed an initial catalog manually.

---

## Phase 4 — Offline Storage

Implement:

```text
Room
local catalog
local collections
local ownership
local wishlist
repository layer
```

App should remain usable with airplane mode enabled.

---

## Phase 5 — Synchronization

Implement:

```text
pending mutations
idempotency
server sequences
sync scopes
push
pull
bootstrap
tombstones
WorkManager
```

This phase is critical and should be completed before adding many secondary features.

---

## Phase 6 — Barcode Scanner

Implement:

```text
CameraX
ML Kit
barcode lookup
Already Owned UI
Add To Collection
Wishlist
```

This creates the application's main user experience.

---

## Phase 7 — New Game Submission

Implement:

```text
unknown barcode
manual game form
external metadata provider
camera/gallery photos
MinIO upload
pending game
```

---

## Phase 8 — Admin Moderation

Implement:

```text
Blazor admin UI
moderation queue
approve
needs changes
reject
admin-created games
edit approved games
```

---

## Phase 9 — Notifications and Sharing

Implement:

```text
user search
collection invitations
in-app notifications
FCM
invitation accept/decline
```

---

## Phase 10 — Change Requests and Administration

Implement:

```text
suggest game correction
admin diff
user management
collection inspection
device revoke
audit log
sync diagnostics
```

---

## Phase 11 — Hardening

Finish:

```text
rate limiting
security tests
image validation
health checks
structured logging
error handling
migration handling
release Docker builds
signed Android APK
```

---

# 75. V1 Definition

The first complete version should allow this exact scenario:

```text
John opens Game Collector.

Default:
"Our Card Games"

John enters a store.

He sees UNO Teams.

He scans the EAN.

The phone checks Room.

Result:

    UNO Teams

    ✓ Already in
    Our Card Games

John doesn't accidentally buy it again.
```

And:

```text
John scans Karakum.

Unknown barcode.

External provider has no useful result.

John selects:
"Create game"

He enters metadata.

He photographs front and back.

The app adds it to:
"Our Card Games"

When online:
images upload
submission synchronizes

Admin receives:
Pending game

Admin approves it.

Sophie later scans the same EAN.

Karakum is now immediately recognized.
```

That end-to-end journey should be the primary acceptance test for the system.

---

# 76. Features Explicitly Out of V1

Based on the requirements, do **not** add complexity for:

```text
game quantities
individual physical copies
purchase history
condition
storage location
personal collection notes
multiple game titles/aliases
multiple owners
multiple active devices per account
public user web portal
automated server backups
```

Keeping these out materially reduces complexity.

---

# 77. Good Future Enhancements

After v1 is stable, several additions would fit naturally.

### Rapid Scan Mode

Stay inside the scanner and scan several boxes in a shop:

```text
UNO Flip      ✓ OWNED
Trio          ✓ OWNED
Cardia        NOT OWNED
Bandido       ✓ OWNED
```

This could become one of the most useful features.

### Collection statistics

Examples:

```text
Total games
Games by tag
Games by publisher
Games by player count
```

### Export

Allow collection export as:

```text
CSV
JSON
```

Useful even if the application disappears someday.

### Better duplicate catalog detection

During moderation:

```text
same EAN
similar title
same publisher
similar box image
```

can warn an admin before approving a duplicate game.

### Catalog merge

Admin can merge:

```text
Duplicate Game B
→ Game A
```

and automatically migrate:

```text
collection ownership
wishlists
barcodes
images
```

to the canonical game.

This will probably become important once many users can submit games.

### Store / Shopping Mode

A simplified screen focused entirely on:

```text
scan
owned/not-owned
wishlist
```

without navigating through the rest of the app.

---

# 78. Overall Recommendation

The architecture I would implement is:

```text
Native Kotlin / Compose Android application
        ↓
Room as local source of truth
        ↓
Operation-based offline synchronization
        ↓
ASP.NET Core .NET 10 REST API
        ↓
Clean Architecture
        ↓
Application/domain-specific services
        ↓
Generic repository foundation
        ↓
EF Core 10
        ↓
SQLite
```

with:

```text
Keycloak
→ identity

Application database
→ collection authorization

MinIO
→ images

FCM
→ push delivery

Blazor
→ administrator interface

Nexus
→ Docker images

Portainer
→ deployment
```

The two design choices I consider most important are:

**First:** don't make the Android application a thin online API client. Make Room authoritative for the UI and synchronization invisible in the background. Android's architecture guidance explicitly supports local-first/offline-first repository patterns, and WorkManager provides durable background scheduling.

**Second:** because the backend database is SQLite, make the ASP.NET Core API the sole database owner. Keep transactions small, store images in MinIO, use WAL, and make the Blazor portal communicate only through the API. SQLite's WAL design improves concurrent reading/writing while still maintaining serialized writes.

With those constraints, this is a technically clean architecture for the application you described and leaves clear migration paths if the catalog or user base becomes substantially larger later.