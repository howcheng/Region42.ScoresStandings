# ⚙️ Development Setup & Google Cloud Run Deployment Guide

This guide details how to set up the development environment locally using Docker PostgreSQL and Google OAuth, manage secrets, connect to the production database via proxies, apply database migrations, and deploy to Google Cloud Run.

---

## 💻 Part 1: Local Development Environment Setup

### choice A: Local PostgreSQL (Recommended)

1. **Start Docker Desktop** on your machine.
2. **Launch a Local PostgreSQL Container** on port `5432` with a robust persistence volume:
   ```powershell
   docker run --name region42-postgres `
	 -e POSTGRES_DB=region42 `
	 -e POSTGRES_USER=postgres `
	 -e POSTGRES_PASSWORD=LocalDevPassword123! `
	 -p 5432:5432 `
	 -v region42_pgdata:/var/lib/postgresql/data `
	 -d postgres:16
   ```
3. **Configure User Secrets** for the Web application pointing to localhost:
   ```powershell
   cd src/Region42.ScoresStandings.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=LocalDevPassword123!"
   ```

### choice B: Connect to production Cloud SQL via SSL/Proxy

For testing with cloud data or staging datasets, run the local Auth proxy.
1. **Set Cloud SQL Master Postgres Password** (if not already set):
   ```powershell
   gcloud sql users set-password postgres `
	 --instance=region-42-scores-standings `
	 --password=YOUR_SECURE_PASSWORD
   ```
2. **Download & Start Cloud SQL Auth Proxy**:
   * Windows PowerShell:
	 ```powershell
	 Invoke-WebRequest -Uri "https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.14.2/cloud-sql-proxy.x64.exe" -OutFile "cloud-sql-proxy.exe"
	 ./cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings
	 ```
   * *Note: Keep this terminal window open!*
3. **Configure User Secrets** to point to the secure tunnel forwarded by the proxy to local port `5432`:
   ```powershell
   cd src/Region42.ScoresStandings.Web
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=YOUR_SECURE_PASSWORD"
   ```

---

## 🔐 Part 2: Google OAuth Configuration

To support the authentication flow, you must configure Google Client API credentials. Both local development and serverless production use a two-layer security model.

### 1. Retrieve Client Credentials
1. Visit the [GCP API Credentials Console](https://console.cloud.google.com/apis/credentials?project=ayso-region-42).
2. Create or verify an **OAuth 2.0 Web Client ID**. The configured app names/client names are:
   * **OAuth Consent App Name:** `Region 42 Scores & Standings`
   * **Local Development Client Name:** `Region42 Local Development`
   * **Production Client Name:** `Region 42 Scores & Standings Production`
3. Configure the **Authorized redirect URIs** to allow local loops and production domains:
   * **Local Development (HTTPS):** `https://localhost:7269/signin-google` (taken from `Properties/launchSettings.json`)
   * **Local Development (HTTP):** `http://localhost:5231/signin-google` (taken from `Properties/launchSettings.json`)
   * **Production Domain:** `https://region42-scores-standings-lnk4qitvsa-uc.a.run.app/signin-google` (or your active Cloud Run URL)
4. Copy the resulting **Client ID** and **Client Secret**.

### 2. Store OAuth Keys in Local Secrets
Apply these keys inside the dotnet User Secrets manager on your local machine:

**PowerShell / cmd:**
```powershell
cd src/Region42.ScoresStandings.Web
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

**Bash:**
```bash
cd src/Region42.ScoresStandings.Web
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

---

## 🐘 Part 3: Database Migrations Guide

The application utilizes EF Core migrations. To manage migrations safely without risking database inconsistencies in production:

### 1. Applying Migrations Locally
Run standard dotnet-ef update commands in your terminal:
```powershell
cd src/Region42.ScoresStandings.Web
dotnet ef database update
```

### 2. Applying Migrations in Production
Because auto-migration is disabled inside the production startup environment for container stability, choose one of these methods:
* **Option A (Proxy update - Recommended):** Ensure Cloud SQL Proxy is running locally, toggle secrets/connection to production, and execute:
  ```powershell
  cd src/Region42.ScoresStandings.Web
  dotnet ef database update --connection "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=YOUR_PROD_DB_PASSWORD"
  ```
* **Option B (Docker Database Migration Container):** Execute a one-off database migration container task using Cloud Build or GKE jobs that runs `dotnet ef database update`.

---

## 🚀 Part 4: Google Cloud Run Production Deployment

Google Cloud Run serves the application from a multi-stage Docker container deployed serverlessly. Configuration secrets are stored securely in GCP Secret Manager and bound as environment variables.

### 1. Enable Core GCP APIs
Ensure all standard container engines, secret managers, and proxy APIs are active in GCP.

**Bash:**
```bash
gcloud services enable \
  secretmanager.googleapis.com \
  artifactregistry.googleapis.com \
  run.googleapis.com \
  sqladmin.googleapis.com \
  --project=ayso-region-42
```

**PowerShell:**
```powershell
gcloud services enable `
  secretmanager.googleapis.com `
  artifactregistry.googleapis.com `
  run.googleapis.com `
  sqladmin.googleapis.com `
  --project=ayso-region-42
```

### 2. Create Artifact Registry
```bash
gcloud artifacts repositories create region42 \
  --repository-format=docker \
  --location=us-west2 \
  --description="Region42 Scores & Standings Docker Images"
```

### 3. Create Secrets in GCP Secret Manager
To preserve a clean 12-factor cloud architectural approach without putting GCP libraries in code, create GCP secret manager secrets and mount them directly as environment variables.

To prevent trailing newline characters (which can corrupt connection strings or OAuth keys), follow these shell-specific entry commands:

#### Option A: Bash
```bash
# Production Connection String
echo -n "Host=/cloudsql/ayso-region-42:us-west2:region-42-scores-standings;Database=region42;Username=postgres;Password=YOUR_PROD_DB_PASSWORD" | \
  gcloud secrets create region42-db-connection-string --data-file=-

# OAuth Production Client ID & Secret
echo -n "YOUR_PROD_CLIENT_ID.apps.googleusercontent.com" | \
  gcloud secrets create region42-google-oauth-client-id --data-file=-

echo -n "YOUR_PROD_CLIENT_SECRET" | \
  gcloud secrets create region42-google-oauth-client-secret --data-file=-
```

#### Option B: PowerShell
In PowerShell, standard pipe commands like `echo` or `Out-File` append carriage returns. Instead, write to a temporary file with explicit Encoding or leverage the .NET raw byte stream pipeline:

```powershell
# Create temporary un-buffered files without trailing newlines, import, and delete:
[System.IO.File]::WriteAllText("$pwd\conn.tmp", "Host=/cloudsql/ayso-region-42:us-west2:region-42-scores-standings;Database=region42;Username=postgres;Password=YOUR_PROD_DB_PASSWORD")
gcloud secrets create region42-db-connection-string --data-file="$pwd\conn.tmp"
Remove-Item "$pwd\conn.tmp"

[System.IO.File]::WriteAllText("$pwd\id.tmp", "YOUR_PROD_CLIENT_ID.apps.googleusercontent.com")
gcloud secrets create region42-google-oauth-client-id --data-file="$pwd\id.tmp"
Remove-Item "$pwd\id.tmp"

[System.IO.File]::WriteAllText("$pwd\secret.tmp", "YOUR_PROD_CLIENT_SECRET")
gcloud secrets create region42-google-oauth-client-secret --data-file="$pwd\secret.tmp"
Remove-Item "$pwd\secret.tmp"
```

### 4. Authorize Cloud Run Service Account
To read configurations from Secret Manager, the Google Cloud Run service account must be granted the **`roles/secretmanager.secretAccessor`** IAM role on each secret.

The specific service account utilized by active production containers is:
`region42-web-app@ayso-region-42.iam.gserviceaccount.com`

#### Option A: Bash
```bash
SERVICE_ACCOUNT="region42-web-app@ayso-region-42.iam.gserviceaccount.com"

# Grant Secret Access to the service account
for secret in region42-db-connection-string region42-google-oauth-client-id region42-google-oauth-client-secret; do
  gcloud secrets add-iam-policy-binding $secret \
    --member="serviceAccount:$SERVICE_ACCOUNT" \
    --role="roles/secretmanager.secretAccessor"
done
```

#### Option B: PowerShell
```powershell
$secrets = @("region42-db-connection-string", "region42-google-oauth-client-id", "region42-google-oauth-client-secret")
foreach ($secret in $secrets) {
    gcloud secrets add-iam-policy-binding $secret `
        --member="serviceAccount:region42-web-app@ayso-region-42.iam.gserviceaccount.com" `
        --role="roles/secretmanager.secretAccessor"
}
```

### 5. Build and Deploy Command
When building from source manually or via the continuous integration workflows defined in `.github/workflows/deploy.yml`:

**Command:**
```bash
# Build the Docker container and push to registry using the specific repository image identifier
docker build -t us-west2-docker.pkg.dev/ayso-region-42/region42/region42-scores-standings:latest .
docker push us-west2-docker.pkg.dev/ayso-region-42/region42/region42-scores-standings:latest

# Deploy to Cloud Run mounting secrets as standard connection string envs
gcloud run deploy region42-scores-standings \
  --image us-west2-docker.pkg.dev/ayso-region-42/region42/region42-scores-standings:latest \
  --region us-west2 \
  --add-cloudsql-instances ayso-region-42:us-west2:region-42-scores-standings \
  --set-env-vars="ASPNETCORE_ENVIRONMENT=Production" \
  --set-secrets="ConnectionStrings:DefaultConnection=region42-db-connection-string:latest,Authentication:Google:ClientId=region42-google-oauth-client-id:latest,Authentication:Google:ClientSecret=region42-google-oauth-client-secret:latest"
