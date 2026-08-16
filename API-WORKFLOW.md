# Game Collector API workflow

Each step must build successfully and pass its automated tests before work moves to the next step.

## 1. API foundation — complete

- Create the .NET 10 solution and Clean Architecture projects.
- Enforce the intended dependency direction between projects.
- Add consistent compiler and repository settings.
- Expose `/health/live` and `/health/ready`.
- Add the first API integration tests.

## 2. API conventions and Keycloak authentication — complete

- Add versioned `/api/v1` routing and OpenAPI output.
- Adopt RFC 9457 Problem Details plus stable application error codes.
- Add correlation IDs, request logging, and safe exception handling.
- Validate Keycloak JWT issuer, audience, signature, and expiration.
- Add the `gamecollector-admin` authorization policy.
- Test unauthenticated, user, and administrator access.

## 3. SQLite persistence foundation — complete

- Add EF Core 10 with the Microsoft SQLite provider.
- Create the application database context and design-time factory.
- Configure WAL, foreign keys, UTC timestamps, and short transactions.
- Add migrations, startup migration handling, and real-SQLite integration tests.
- Make `/health/ready` verify SQLite without depending on optional providers.

## 4. Profiles, onboarding, and devices — complete

- Implement the user profile keyed by Keycloak `sub`.
- Add normalized, globally unique usernames and application disable state.
- Implement `GET`, `POST`, and `PATCH /api/v1/me`.
- Implement one-active-device registration and revocation.
- Cover validation, uniqueness, authorization, and replacement behavior.

## 5. Collections, membership, and invitations — complete

- Implement collections, one owner, Editor/Viewer memberships, and default collection.
- Add collection CRUD, ownership transfer, and member management.
- Add invitation create, accept, and decline flows.
- Enforce resource-level authorization and the full role security matrix.

## 6. Catalog and reference data — complete

- Implement games, globally unique normalized barcodes, languages, tags, and revisions.
- Add game details, title search, and barcode lookup.
- Apply moderation visibility rules to every query.
- Seed canonical reference data and test barcode validation/uniqueness.

## 7. Collection ownership and personal wishlists — complete

- Implement binary collection/game ownership with a unique `(CollectionId, GameId)` key.
- Add idempotent add/remove endpoints and selected-collection status.
- Implement personal wishlists with automatic removal when a game is acquired.
- Test Editor/Viewer permissions and concurrency conflicts.

## 8. Media and external product lookup — complete

- Add private MinIO integration and short-lived upload intents.
- Validate completed uploads by content, size, format, and dimensions.
- Generate thumbnails in background processing.
- Add a replaceable, timeout-protected metadata provider abstraction and cache.

## 9. Submissions, moderation, and change requests — complete

- Implement Pending, NeedsChanges, Approved, and Rejected state transitions.
- Enforce submitter, collection-member, global, and administrator visibility.
- Add administrator moderation endpoints and user correction requests.
- Record state-changing administrator actions in the audit log.

## 10. Offline synchronization and outbox — complete

- Implement idempotent mutation push, ordered pull, bootstrap, scopes, and tombstones.
- Add server sequences and stale-cursor reset behavior.
- Use an outbox for reliable post-commit background work.
- Test retries, duplicate and out-of-order mutations, conflicts, and device replacement.

## 11. Notifications and FCM — complete

- Implement durable in-app notifications and read state.
- Send minimal FCM wake-up payloads through the outbox worker.
- Add invitation, moderation, membership, and device-revocation notifications.
- Track delivery failures without losing domain operations.

## 12. Administration API — complete

- Complete the visibly separate `/api/v1/admin` surface.
- Add user disable/enable, device revoke, collection inspection, catalog editing, audit search, and sync diagnostics.
- Require the administrator policy independently on every admin endpoint.

## 13. Hardening, packaging, and release — complete

- Add rate and request-size limits, security headers, timeouts, and secret-safe logging.
- Complete domain, application, architecture, SQLite, API, and security tests.
- Add production Docker image, non-root execution, persistent `/data`, and health checks.
- Document environment variables, migrations, Nexus version tags, Portainer deployment, and rollback.
