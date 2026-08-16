# Game Collector API deployment and rollback

## Release image

Build from the repository root and use an explicit semantic version. An immutable commit suffix is recommended:

```powershell
$version = "1.0.0-a83fc91"
$image = "nexus.example.com/gamecollector/api:$version"
docker build --build-arg APP_VERSION=$version --tag $image .
docker push $image
```

Do not deploy only `latest`. Keep the previously deployed image tag available in Nexus until the new release and its database backup have been verified.

The runtime image listens on port 8080, runs as the non-root user supplied by the official .NET image, drops diagnostics, stores SQLite under `/data`, and checks `/health/ready`.

## Required configuration

Set these values in Portainer's protected environment or secret management. Never place their real values in the stack file, image, source control, or logs.

| Portainer variable | API setting | Purpose |
|---|---|---|
| `GAMECOLLECTOR_API_IMAGE` | — | Full immutable Nexus image and tag |
| `KEYCLOAK_AUTHORITY` | `Authentication__Keycloak__Authority` | Realm issuer URL |
| `KEYCLOAK_AUDIENCE` | `Authentication__Keycloak__Audience` | API audience |
| `KEYCLOAK_ADMIN_ROLE` | `Authentication__Keycloak__AdminRole` | Administrator role |
| `MINIO_ACCESS_KEY` | `MediaStorage__AccessKey` | Private object-storage identity |
| `MINIO_SECRET_KEY` | `MediaStorage__SecretKey` | Private object-storage secret |
| `MINIO_IMAGE` | — | Explicit MinIO release image |
| `FIREBASE_PROJECT_ID` | `Firebase__ProjectId` | FCM project; leave empty to disable push |
| `FIREBASE_CREDENTIALS_HOST_PATH` | — | Read-only service-account JSON host path |
| `FIREBASE_CREDENTIALS_PATH` | `Firebase__CredentialsPath` | Container path, normally `/run/secrets/firebase.json` |

The stack binds API and MinIO console ports to loopback by default. Put TLS at the trusted reverse proxy and expose only the API route needed by clients. Create `gamecollector-media` as a private MinIO bucket before accepting uploads.

## Portainer deployment

1. Back up the SQLite and MinIO volumes before changing the image tag.
2. Set `GAMECOLLECTOR_API_IMAGE` to the new immutable Nexus tag.
3. Deploy [compose.portainer.yml](../compose.portainer.yml) as one stack with exactly one API replica.
4. Wait for `/health/live` and `/health/ready` to return HTTP 200.
5. Verify onboarding/login, catalog lookup, one write operation, thumbnail outbox processing, sync bootstrap, and an administrator query.
6. Confirm logs contain correlation IDs but no authorization headers, FCM tokens, service-account JSON, MinIO secrets, or request bodies.

The API applies checked-in EF Core migrations before it begins serving traffic. SQLite supports one API process per database volume; do not scale this service horizontally. Move persistence to PostgreSQL before introducing multiple API replicas.

## Backup

Use a storage-level snapshot that is consistent across the named volumes. If snapshots are unavailable, stop the API container, copy the complete `gamecollector-db` volume while no process is writing, back up the `gamecollector-minio` volume, and then restart the API. Test restoration regularly on a separate host.

Keep at least:

- the pre-deployment database and object-storage backup;
- the corresponding immutable API image tag;
- the exact Portainer variables used for that release, excluding secret values from ordinary documentation.

## Rollback

Application rollback is safe only when the old application understands the current schema.

1. Stop the API container to prevent new writes.
2. If the release applied a migration that the old image cannot read, restore the matching pre-deployment SQLite and MinIO backups together.
3. Change `GAMECOLLECTOR_API_IMAGE` back to the previous immutable tag.
4. Redeploy one API replica and wait for readiness.
5. Repeat the smoke tests and inspect outbox failures.

Never run ad-hoc destructive migration commands against the only production database. Practice backup restoration and rollback before the first production upgrade.
