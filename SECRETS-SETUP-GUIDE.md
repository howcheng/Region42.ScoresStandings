# User Secrets Configuration Guide

This document explains how to configure local development secrets and how they will map to Google Secret Manager for production.

## Prerequisites

1. Authenticate with GCP: `gcloud auth login`
2. Set project: `gcloud config set project ayso-region-42`

## Required Secrets

### 1. PostgreSQL Connection String

**Local Development (User Secrets):**
```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=YOUR_LOCAL_PASSWORD"
```

**Production (Google Secret Manager):**
- Secret name: `region42-db-connection-string`
- Format when using Cloud SQL: `Host=/cloudsql/ayso-region-42:REGION:INSTANCE_NAME;Database=region42;Username=DB_USER;Password=DB_PASSWORD`
- Or using Cloud SQL Proxy: `Host=127.0.0.1;Port=5432;Database=region42;Username=DB_USER;Password=DB_PASSWORD`

**To get Cloud SQL connection details:**
```bash
# List Cloud SQL instances
gcloud sql instances list

# Get instance connection name
gcloud sql instances describe INSTANCE_NAME --format="value(connectionName)"

# Create database user (if needed)
gcloud sql users create DB_USER --instance=INSTANCE_NAME --password=STRONG_PASSWORD
```

### 2. Google OAuth Credentials

**Local Development (User Secrets):**
```bash
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

**Production (Google Secret Manager):**
- Secret names:
  - `region42-google-oauth-client-id`
  - `region42-google-oauth-client-secret`

**To create OAuth credentials:**
1. Go to [Google Cloud Console - APIs & Credentials](https://console.cloud.google.com/apis/credentials?project=ayso-region-42)
2. Click "Create Credentials" → "OAuth 2.0 Client ID"
3. Application type: "Web application"
4. Name: "Region42 Scores & Standings"
5. Authorized redirect URIs:
   - Local: `https://localhost:5001/signin-google`
   - Production: `https://YOUR_CLOUD_RUN_URL/signin-google`
6. Save the Client ID and Client Secret

**Or use gcloud (if OAuth client already exists):**
```bash
# List OAuth clients
gcloud alpha iap oauth-clients list

# Get client details
gcloud alpha iap oauth-clients describe CLIENT_ID --format=json
```

## Setup Commands

### Step 1: Authenticate with GCP
```bash
gcloud auth login
```

### Step 2: Set Up Local PostgreSQL (if not already running)

**Using Docker:**
```bash
docker run --name region42-postgres \\
  -e POSTGRES_DB=region42 \\
  -e POSTGRES_USER=postgres \\
  -e POSTGRES_PASSWORD=YOUR_LOCAL_PASSWORD \\
  -p 5432:5432 \\
  -v region42_pgdata:/var/lib/postgresql/data \\
  -d postgres:16
```

**Or use the docker-compose.yml (to be created later)**

### Step 3: Configure User Secrets

**After you have the values, run:**
```bash
cd Region42.ScoresStandings.Web

# Database connection
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=YOUR_LOCAL_PASSWORD"

# Google OAuth
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

### Step 4: Verify User Secrets
```bash
dotnet user-secrets list
```

## Google Secret Manager Setup (for Production)

### Create secrets in Secret Manager:
```bash
# Database connection string
echo -n "Host=/cloudsql/ayso-region-42:REGION:INSTANCE_NAME;Database=region42;Username=DB_USER;Password=DB_PASSWORD" | \\
  gcloud secrets create region42-db-connection-string --data-file=-

# Google OAuth Client ID
echo -n "YOUR_CLIENT_ID" | \\
  gcloud secrets create region42-google-oauth-client-id --data-file=-

# Google OAuth Client Secret
echo -n "YOUR_CLIENT_SECRET" | \\
  gcloud secrets create region42-google-oauth-client-secret --data-file=-
```

### Grant Cloud Run service access to secrets:
```bash
# Get the Cloud Run service account
gcloud run services describe region42-scores-standings --format="value(spec.template.spec.serviceAccountName)" --region=us-central1

# Grant access (replace SERVICE_ACCOUNT with actual account)
gcloud secrets add-iam-policy-binding region42-db-connection-string \\
  --member="serviceAccount:SERVICE_ACCOUNT" \\
  --role="roles/secretmanager.secretAccessor"

gcloud secrets add-iam-policy-binding region42-google-oauth-client-id \\
  --member="serviceAccount:SERVICE_ACCOUNT" \\
  --role="roles/secretmanager.secretAccessor"

gcloud secrets add-iam-policy-binding region42-google-oauth-client-secret \\
  --member="serviceAccount:SERVICE_ACCOUNT" \\
  --role="roles/secretmanager.secretAccessor"
```

## Application Configuration

### In appsettings.json (checked into source control):
```json
{
  "ConnectionStrings": {
	"DefaultConnection": ""
  },
  "Authentication": {
	"Google": {
	  "ClientId": "",
	  "ClientSecret": ""
	}
  }
}
```

### In Program.cs (to be configured later):
```csharp
// For local development, use user secrets
// For production on Cloud Run, read from Secret Manager
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Alternative: explicitly read from Secret Manager in production
if (builder.Environment.IsProduction())
{
	// Read from Secret Manager
	// (Implementation in step 14)
}
```

## Current Status

✅ User secrets initialized (UserSecretsId: c3b0fb10-4b9c-4c85-bc5b-0d9fb3b3dd1b)
⏳ Pending: Database connection string (need to create Cloud SQL instance or use local PostgreSQL)
⏳ Pending: Google OAuth credentials (need to create OAuth 2.0 client)

## Next Steps

1. Run `gcloud auth login` to authenticate
2. I can then help you:
   - Check if Cloud SQL instance exists and get connection details
   - Create Cloud SQL instance if needed
   - Check if OAuth credentials exist
   - Create OAuth credentials if needed
   - Set up Secret Manager secrets
   - Configure local user secrets with the values

## Resources

- [Cloud SQL for PostgreSQL](https://cloud.google.com/sql/docs/postgres)
- [Google OAuth Configuration](https://developers.google.com/identity/protocols/oauth2)
- [Secret Manager](https://cloud.google.com/secret-manager/docs)
- [.NET User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
