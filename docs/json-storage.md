# JSON File Storage (Google Cloud Storage)

The application stores season data as JSON files in Google Cloud Storage instead of PostgreSQL.

## Bucket layout

```
gs://region42-storage/
  index.json
  admin/
    users.json
  {year}/
    core-season/
      season.json
      10u-boys.json
      10u-girls.json
      12u-boys.json
      12u-girls.json
      14u-boys.json
      14u-girls.json
```

- **season.json** — season metadata, league settings, divisions, and team rosters (authoritative source for team names)
- **{division-key}.json** — games (with embedded scores), volunteer points, and pre-calculated standings by round
- **index.json** — catalog of all seasons/competitions in the bucket

### Standings snapshots

Each division file stores pre-calculated standings under `standingsByRound`, keyed by round number. Each team row stores only `teamId` plus stats (wins, points, etc.) — **not** team names. Names are resolved at display time by looking up the team ID in the season roster, the same way game scores reference teams by ID only.

Existing files that still contain `teamName`/`teamShortName` in standings rows remain readable (extra JSON fields are ignored). Those fields are dropped the next time standings are recalculated and saved.

## Configuration

| Setting | Description |
|---------|-------------|
| `Storage:Provider` | `LocalFile` (development) or `Gcs` (production) |
| `Storage:BucketName` | GCS bucket name (`region42-storage`) |
| `Storage:CompetitionSlug` | Folder name under each year (`core-season`) |
| `Storage:LocalRoot` | Local filesystem root when using `LocalFile` |

## Local development

By default, `appsettings.json` uses `LocalFile` with data under `../../.local-data/storage` (solution-root `.local-data/storage`). No database or GCS credentials are required for normal local development.

## Production (Cloud Run)

Set `Storage:Provider` to `Gcs`. The Cloud Run service account needs **Storage Object Admin** on `region42-storage`.

## One-time migration from PostgreSQL

If you still have data in Cloud SQL, export it before decommissioning the database:

```powershell
cd src/Region42.ScoresStandings.Web

# Ensure DefaultConnection points at Cloud SQL (user secrets or env var)
dotnet user-secrets set "Storage:Provider" "Gcs"
dotnet user-secrets set "Storage:BucketName" "region42-storage"

dotnet run -- --export-from-postgres
```

To export to local files first for inspection:

```powershell
dotnet user-secrets set "Storage:Provider" "LocalFile"
dotnet user-secrets set "Storage:LocalRoot" "../../.local-data/storage"
dotnet run -- --export-from-postgres
```

Then upload `./data/storage` to the bucket with `gcloud storage cp -r data/storage/* gs://region42-storage/`.

## Concurrency

Each JSON file has a `version` field and GCS generation-based optimistic concurrency. If two admins save simultaneously, the second save retries automatically.
