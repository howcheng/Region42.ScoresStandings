# Volunteer Points Auto-Sync



Automated volunteer points import from the cgisports referee assignment log into Region 42 Scores & Standings.



## Architecture



- **Cloud Scheduler** triggers a **Cloud Run Job** (`region42-volunteer-sync`) on a cron schedule.

- The job downloads the legacy **`.xls` assignment log** from cgisports, parses the **`team_log`** sheet, and POSTs per-division payloads to the web app API.

- **All business logic** (validation, team name matching, authoritative reconciliation, standings refresh, storage writes) runs in the web app via `POST /api/volunteer-points/import`.

- A **GCS/local write lock** (`admin/locks/volunteer-points-import.lock`) prevents overlapping bulk imports and blocks the admin volunteer points grid while a sync is running.



## API



| Method | Path | Auth |

|--------|------|------|

| `GET` | `/api/volunteer-points/sync-context` | Google identity token (service account) |

| `POST` | `/api/volunteer-points/import?dryRun=false&authoritativeSync=true` | Google identity token (service account) |



### Sync context



Returns the active season, each division’s **`cgiDivisionCode`** (`10UB`, `12UG`, …), team roster metadata, and **`roundByDate`** (game date → round) used to map export dates to standings rounds.



### Import



Request body: `VolunteerPointsBulkUpdateDto`



```json

{

  "divisionId": 1,

  "entries": [

    { "teamName": "10UB04 (Timen)", "round": 1, "points": 1.5 }

  ]

}

```



- **`dryRun=true`** — validate without acquiring the write lock or persisting data.

- **`authoritativeSync=true`** — treat the payload as the assignment-log snapshot for that division: upsert all entries, zero out prior **sync-owned** `(team, round)` cells that are absent from the payload, and overwrite manual values for cells included in the payload.



Volunteer points support **half increments** (`0.5`) as `decimal` values.

**Team labels:** cgisports uses short names like `10UB04 (Timen)`; the API resolves them to roster names via `VolunteerPointsTeamNameMatcher` (division + team number, bare `10UB04`, or team number alone such as `04` within the division import). Labels that are only letters (likely a coach last name with no team number) are **not** auto-matched because roster names use full coach names and false positives are too likely. When preparing a division import, rows whose label starts with another division code (for example `12UB02` under the `10UB` column) are dropped. Any remaining unmatched labels are logged by the sync job; the run continues.



## cgisports source integration



1. **Login:** `POST https://cgisports.com/ref/2130/` with form fields `userid`, `password`, `login=Login`, and empty `view_schedule`.

2. **Session:** Read `user={login}.{sessionId}` from the post-login redirect URL or HTML (`name="user" value="..."`).

3. **Download:** `POST` to `/ref/2130?user={sessionUser}&get_logs=1` with `user` and `ck=ok` in the form body.



VolunteerSync configuration ([`appsettings.json`](../../src/Region42.ScoresStandings.VolunteerSync/appsettings.json)):



| Setting | Example |

|---------|---------|

| `SourceLoginPath` | `/ref/2130/` |

| `SourceAssignmentLogPath` | `/ref/2130?user={0}&assignment_log=1` |

| `SourceDownloadPath` | `/ref/2130?user={0}&get_logs=1` |



`{0}` is the **full session user token**, not the bare login name.



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



curl "$audience/api/volunteer-points/sync-context" `

  -H "Authorization: Bearer $token"



curl -X POST "$audience/api/volunteer-points/import?dryRun=true&authoritativeSync=true" `

  -H "Authorization: Bearer $token" `

  -H "Content-Type: application/json" `

  -d '{"divisionId":1,"entries":[{"teamName":"10UB01 Sharks (Smith)","round":1,"points":1.5}]}'

```



## GCP setup



See previous sections in this file for service account, secrets (`volunteer-source-username` / `volunteer-source-password` with **userid** and password for cgisports), Cloud Run Job, and Cloud Scheduler. Set `VolunteerSync__DryRun=false` once dry-run validation passes.



The job imports **all divisions** in the active season from one Excel file. Optional `VolunteerSync__DivisionId` limits a run to a single division for debugging.



## Local development



```powershell
cd src/Region42.ScoresStandings.VolunteerSync
# Same UserSecretsId as the Web project — secrets can be set from either project directory.
dotnet user-secrets set "VolunteerSync:SourceUsername" "<userid>"
dotnet user-secrets set "VolunteerSync:SourcePassword" "<password>"
dotnet run
```

`dotnet run` uses **Development** via `Properties/launchSettings.json` (`DOTNET_ENVIRONMENT=Development`), so `appsettings.Production.json` is not loaded and user secrets are merged into configuration. If you run the built `.exe` directly, set `$env:DOTNET_ENVIRONMENT = "Development"` first.

For **local DEBUG** builds, import APIs use `[AllowAnonymous]` and the sync job calls the API **without** a bearer token, so you can exercise download → parse → dry-run import without gcloud or IAM impersonation. Run the web app and VolunteerSync in **Debug** configuration (default for `dotnet run` / F5).

Imports are recorded as `volunteer-sync-local` when no JWT is present. **Release/Production builds** require the service-account JWT policy (metadata server on Cloud Run; optional `VolunteerSync:TargetIdentityToken` to test auth locally).

To verify JWT auth in DEBUG, set `VolunteerSync:TargetIdentityToken` in user secrets (from `gcloud auth print-identity-token --audiences=<TargetApiBaseUrl>`, with or without service-account impersonation if you have `roles/iam.serviceAccountTokenCreator`).



## Concurrency



- **Write lock:** coarse-grained mutex for bulk import; 15-minute TTL handles crashed jobs.

- **Generation-based optimistic concurrency:** still applies to division JSON files on save (existing behavior).



If an admin attempts to save volunteer points while a sync lock is held, the grid POST shows: *"A volunteer points import is currently in progress. Please try again shortly."*

