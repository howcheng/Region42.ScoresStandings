# 🚀 Google Cloud Run Production Deployment Guide

This guide details how to configure Google Cloud Platform (GCP), Secret Manager, and GitHub Actions to deploy the **Youth Soccer League Tracking Web Application (Region42)** to Google Cloud Run.

---

## 📋 Prerequisites & GCP Setup

Ensure you have the Google Cloud SDK (`gcloud`) installed locally and you are authenticated:
```powershell
gcloud auth login
gcloud config set project ayso-region-42
```

### 1. Enable Required GCP APIs
Enable Secret Manager, Artifact Registry, Cloud Run, and Cloud SQL admin services:
```bash
gcloud services enable \
  secretmanager.googleapis.com \
  artifactregistry.googleapis.com \
  run.googleapis.com \
  sqladmin.googleapis.com \
  --project=ayso-region-42
```

### 2. Create Artifact Registry Repository
Create a secure Docker repository inside Artifact Registry for storing application images:
```bash
gcloud artifacts repositories create region42 \
  --repository-format=docker \
  --location=us-west2 \
  --description="Region42 Scores & Standings Docker Images"
```

---

## 🔐 Configuration & Secrets Management

To maintain a 12-factor cloud-native app, **no direct Google Secret Manager SDK code** is added. Instead, GCP Secret Manager secrets are mounted directly into ASP.NET Core environment variables at runtime!

### 1. Create Secrets in GCP Secret Manager

```bash
# Database connection string (runs over standard Cloud SQL local-socket proxy in Cloud Run)
echo -n "Host=/cloudsql/ayso-region-42:us-west2:region-42-scores-standings;Database=region42;Username=postgres;Password=YOUR_PROD_DB_PASSWORD" | \
  gcloud secrets create region42-db-connection-string --data-file=-

# Production OAuth credentials
echo -n "YOUR_PROD_CLIENT_ID.apps.googleusercontent.com" | \
  gcloud secrets create region42-google-oauth-client-id --data-file=-

echo -n "YOUR_PROD_CLIENT_SECRET" | \
  gcloud secrets create region42-google-oauth-client-secret --data-file=-
```

### 2. Configure Cloud Run Service Account Permissions
GCP secrets require explicit permissions. Grant the Cloud Run service identity access:

```bash
# Get your default Cloud Run service account email
SERVICE_ACCOUNT=$(gcloud run services describe region42-scores-standings \
  --format="value(spec.template.spec.serviceAccountName)" --region=us-west2)

# Grant Secret Access to the service account
for secret in region42-db-connection-string region42-google-oauth-client-id region42-google-oauth-client-secret; do
  gcloud secrets add-iam-policy-binding $secret \
	--member="serviceAccount:$SERVICE_ACCOUNT" \
	--role="roles/secretmanager.secretAccessor"
done
```

---

## 🔄 Production Database Migrations

Since `Program.cs` only applies database migrations automatically in `Development` mode, how do you apply migrations to your production PostgreSQL database?

### Option A: Local Deployment Migration (e.g. CLI via Proxy)
This is the **easiest and safest** approach.
1. Run the Cloud SQL Auth Proxy on your local computer to connect to your production DB securely:
   ```powershell
   ./cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings
   ```
2. In a separate terminal, execute Entity Framework tool pointing to `localhost` (port 5432 redirected by proxy):
   ```powershell
   cd src/Region42.ScoresStandings.Web
   dotnet ef database update --connection "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=YOUR_PROD_DB_PASSWORD"
   ```

### Option B: Runtime Environment Flag (App Startup)
If you prefer the container to run migrations on startup in production when a flag is present, you can modify `Program.cs` to check for an environment variable. If they check this, we recommend setting:
```env
RUN_MIGRATIONS_ON_START_IN_PROD=true
```
To enable this, we would make a simple 3-line adjustment to `Program.cs` allowing database migration during start-up.

---

## 🌐 Google OAuth Consent & Redirect URIs

When deploying to Google Cloud Run, your app's web address will change (e.g. `https://region42-scores-standings-xxxxxx-uw.a.run.app`).

1. Go page: https://console.cloud.google.com/apis/credentials?project=ayso-region-42
2. Edit your **OAuth 2.0 Client ID Credentials**.
3. Under **Authorized redirect URIs**, append your production redirects:
   - `https://region42-scores-standings-xxxxxx-uw.a.run.app/signin-google`
4. Save the changes.

---

## 🛠️ GitHub Actions Setup (CI/CD)

The GitHub Actions workflow requires secret variables configured in your GitHub repository (`Settings -> Secrets and variables -> Actions`):

1. **`GCP_SA_KEY`**: (Recommended) A JSON Service Account Key with the following roles in GCP:
   - **Artifact Registry Writer** (`roles/artifactregistry.writer`)
   - **Cloud Run Developer** (`roles/run.developer`)
   - **Service Account User** (`roles/iam.serviceAccountUser`)
   - Ensure the service account has access to push images and execute deployments.

*(Alternatively, configure Workload Identity Federation by populating `GCP_WIF_PROVIDER` and `GCP_WIF_SERVICE_ACCOUNT` secrets as documented in the workflow file.)*
