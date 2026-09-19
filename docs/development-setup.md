# Development Setup & Google Cloud Run Deployment Guide

This guide covers local development, Google OAuth configuration, and deploying to Google Cloud Run. The application stores data as JSON files (local filesystem in development, Google Cloud Storage in production).

---

## Part 1: Local Development

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Google OAuth credentials (see Part 2)

### Run the application

```powershell
cd src/Region42.ScoresStandings.Web
dotnet run
```

By default, data is stored under `./data/storage` mirroring the GCS bucket layout (`2026/core-season/season.json`, etc.). No database setup is required.

### Optional: configure storage location

```powershell
dotnet user-secrets set "Storage:Provider" "LocalFile"
dotnet user-secrets set "Storage:LocalRoot" "./data/storage"
```

---

## Part 2: Google OAuth Configuration

### 1. Retrieve client credentials

1. Visit the [GCP API Credentials Console](https://console.cloud.google.com/apis/credentials?project=ayso-region-42).
2. Create or verify an **OAuth 2.0 Web Client ID**.
3. Configure **Authorized redirect URIs**:
   - Local HTTPS: `https://localhost:7269/signin-google`
   - Local HTTP: `http://localhost:5231/signin-google`
   - Production: your Cloud Run URL + `/signin-google`

### 2. Store OAuth keys locally

```powershell
cd src/Region42.ScoresStandings.Web
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

---

## Part 3: Production Deployment

Deployment is automated via GitHub Actions (`.github/workflows/deploy.yml`) on push to `master`.

Production uses:
- **Storage:** `Gcs` provider, bucket `region42-storage`
- **Service account:** `region42-web-app@ayso-region-42.iam.gserviceaccount.com` (needs Storage Object Admin on the bucket)
- **Secrets:** Google OAuth client ID and secret from Secret Manager

See [JSON File Storage Guide](json-storage.md) for bucket structure and migration from PostgreSQL.

---

## Migrating existing PostgreSQL data

If you still have data in Cloud SQL, run the one-time export before decommissioning the instance:

```powershell
cd src/Region42.ScoresStandings.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "YOUR_CLOUD_SQL_CONNECTION_STRING"
dotnet user-secrets set "Storage:Provider" "Gcs"
dotnet run -- --export-from-postgres
```

---

## Running tests

```powershell
dotnet test
```
