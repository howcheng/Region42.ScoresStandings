# Volunteer Points Auto-Sync

Automated volunteer points import from an external UI-only volunteer tracking application into Region 42 Scores & Standings.

## Architecture

- **Cloud Scheduler** triggers a **Cloud Run Job** (`region42-volunteer-sync`) on a cron schedule.
- The job is a thin client: download Excel export → parse → POST JSON to the web app API.
- **All business logic** (validation, standings refresh, storage writes) runs in the web app via `POST /api/volunteer-points/import`.
- A **GCS/local write lock** (`admin/locks/volunteer-points-import.lock`) prevents overlapping bulk imports and blocks the admin volunteer points grid while a sync is running.

## API

| Method | Path | Auth |
|--------|------|------|
| `POST` | `/api/volunteer-points/import?dryRun=false` | Google identity token (service account) |

Request body: `VolunteerPointsBulkUpdateDto`

```json
{
  "divisionId": 1,
  "entries": [
    { "teamId": 10, "teamName": "10UB01 Sharks", "round": 1, "points": 2, "notes": "" }
  ]
}
```

Use `dryRun=true` to validate without acquiring the write lock or persisting data.

## Authentication

The sync job uses a dedicated service account:

- **Email:** `region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com`
- **Role:** `roles/run.invoker` on Cloud Run service `region42-scores-standings`

The web app validates JWT bearer tokens where:

- `email` claim matches `Authentication:ServiceAccount:AllowedServiceAccountEmail`
- `aud` matches `Authentication:ServiceAccount:JwtAudience` (Cloud Run service URL)

### Manual API test

```powershell
$audience = "https://region42-scores-standings-fcnndtynza-uc.a.run.app"
$token = gcloud auth print-identity-token --audiences=$audience `
  --impersonate-service-account=region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com

curl -X POST "$audience/api/volunteer-points/import?dryRun=true" `
  -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d '{"divisionId":1,"entries":[{"teamId":10,"round":1,"points":2}]}'
```

## GCP setup

### 1. Service account

```powershell
gcloud iam service-accounts create region42-volunteer-sync `
  --display-name="Region 42 Volunteer Points Sync"

gcloud run services add-iam-policy-binding region42-scores-standings `
  --region=us-central1 `
  --member="serviceAccount:region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com" `
  --role="roles/run.invoker"
```

### 2. Secrets (source app credentials)

```powershell
gcloud secrets create volunteer-source-username --replication-policy=automatic
gcloud secrets create volunteer-source-password --replication-policy=automatic
# Your real login for the external volunteer points website
echo -n "your-actual-username" | gcloud secrets versions add volunteer-source-username --data-file=-
echo -n "your-actual-password" | gcloud secrets versions add volunteer-source-password --data-file=-
```

Grant the sync service account `roles/secretmanager.secretAccessor` on those secrets.

### 3. Cloud Run Job

Build and deploy from repository root:

```powershell
docker build -f src/Region42.ScoresStandings.VolunteerSync/Dockerfile -t region42-volunteer-sync .
# push to Artifact Registry, then:

gcloud run jobs create region42-volunteer-sync `
  --image=us-west2-docker.pkg.dev/ayso-region-42/region42/region42-volunteer-sync:latest `
  --region=us-central1 `
  --service-account=region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com `
  --set-env-vars="DOTNET_ENVIRONMENT=Production,VolunteerSync__DivisionId=<id>" `
  --set-secrets="VolunteerSync__SourceUsername=volunteer-source-username:latest,VolunteerSync__SourcePassword=volunteer-source-password:latest"
```

The container Dockerfile also sets `DOTNET_ENVIRONMENT=Production`, which loads [`appsettings.Production.json`](../../src/Region42.ScoresStandings.VolunteerSync/appsettings.Production.json) (production API URL, source app paths, etc.). Cloud Run env vars and secrets override JSON values when set.

Set `VolunteerSync__DryRun=false` once source app mapping is complete and dry-run validation passes.

### 4. Cloud Scheduler

```powershell
gcloud scheduler jobs create http region42-volunteer-sync-weekly `
  --location=us-central1 `
  --schedule="0 6 * * 1" `
  --time-zone="America/Los_Angeles" `
  --uri="https://us-central1-run.googleapis.com/apis/run.googleapis.com/v1/namespaces/ayso-region-42/jobs/region42-volunteer-sync:run" `
  --http-method=POST `
  --oauth-service-account-email=region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com
```

Adjust the cron expression to match when the source volunteer app publishes updated exports.

## Source app integration (TODO)

The following are stubbed until the external volunteer app format is documented:

- `VolunteerPointsSourceClient` — login form field names and download URL
- `VolunteerPointsExcelParser` — worksheet/column mapping
- `VolunteerPointsTeamMatcher` — external team name → internal `TeamId` lookup

## Local development

Run the web app locally and POST JSON directly:

```powershell
cd src/Region42.ScoresStandings.Web
dotnet run
```

For JWT validation in development, set `Authentication:ServiceAccount:JwtAudience` to your local HTTPS URL or test via unit/integration tests with mocked auth.

Run the sync job locally (uses `appsettings.json`; Development is the default environment):

```powershell
cd src/Region42.ScoresStandings.VolunteerSync
dotnet run
```

To test production settings locally:

```powershell
$env:DOTNET_ENVIRONMENT = "Production"
dotnet run
```

Or use user secrets / env vars to override individual `VolunteerSync__*` settings without changing JSON files.

## Concurrency

- **Write lock:** coarse-grained mutex for bulk import; 15-minute TTL handles crashed jobs.
- **Generation-based optimistic concurrency:** still applies to division JSON files on save (existing behavior).

If an admin attempts to save volunteer points while a sync lock is held, the grid POST shows: *"A volunteer points import is currently in progress. Please try again shortly."*
