# IMMEDIATE ACTION REQUIRED: Configure Database Connection

User secrets have been initialized, but you need to choose a database connection method.

## Current Status

✅ User Secrets ID: `c3b0fb10-4b9c-4c85-bc5b-0d9fb3b3dd1b`
✅ Cloud SQL Instance Found: `ayso-region-42:us-west2:region-42-scores-standings`
❌ Docker is not running (needed for local PostgreSQL)
⚠️  Database connection string NOT YET CONFIGURED
⚠️  Google OAuth credentials NOT YET CONFIGURED

---

## OPTION 1: Local PostgreSQL (Recommended for Development)

### Step 1: Start Docker Desktop
1. Open Docker Desktop
2. Wait for it to fully start

### Step 2: Create Local PostgreSQL Container
```powershell
docker run --name region42-postgres `
  -e POSTGRES_DB=region42 `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=LocalDevPassword123! `
  -p 5432:5432 `
  -v region42_pgdata:/var/lib/postgresql/data `
  -d postgres:16
```

### Step 3: Configure User Secrets
```powershell
cd Region42.ScoresStandings.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=LocalDevPassword123!"
```

---

## OPTION 2: Cloud SQL via Proxy (Testing with Real Data)

### Step 1: Set Password for Cloud SQL postgres User
```powershell
gcloud sql users set-password postgres `
  --instance=region-42-scores-standings `
  --password=YOUR_SECURE_PASSWORD
```

### Step 2: Download Cloud SQL Auth Proxy
```powershell
# Download
Invoke-WebRequest -Uri "https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.14.2/cloud-sql-proxy.x64.exe" -OutFile "cloud-sql-proxy.exe"

# Or install via Chocolatey
choco install cloud-sql-proxy
```

### Step 3: Start Cloud SQL Proxy (keep this running in a terminal)
```powershell
./cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings
```

### Step 4: Configure User Secrets
```powershell
cd Region42.ScoresStandings.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=YOUR_SECURE_PASSWORD"
```

---

## CONFIGURE GOOGLE OAUTH (REQUIRED FOR BOTH OPTIONS)

### Step 1: Create OAuth Client
1. Visit: https://console.cloud.google.com/apis/credentials?project=ayso-region-42
2. Click **"Create Credentials"** → **"OAuth 2.0 Client ID"**
3. Configure OAuth consent screen if prompted:
   - User Type: **Internal**
   - App name: **Region 42 Scores & Standings**
   - User support email: **howard.cheng@aysoregion42.org**
4. Create OAuth Client:
   - Application type: **Web application**
   - Name: **Region42 Local Development**
   - Authorized redirect URIs:
	 - `https://localhost:5001/signin-google`
	 - `http://localhost:5000/signin-google`
5. Copy the **Client ID** and **Client Secret**

### Step 2: Configure User Secrets
```powershell
cd Region42.ScoresStandings.Web
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

---

## VERIFY CONFIGURATION

```powershell
cd Region42.ScoresStandings.Web
dotnet user-secrets list
```

You should see:
```
Authentication:Google:ClientId = (your client id)
Authentication:Google:ClientSecret = (your secret)
ConnectionStrings:DefaultConnection = Host=...
```

---

## NEXT STEPS (After Configuration)

1. **Create and apply migrations:**
   ```powershell
   cd Region42.ScoresStandings.Web
   dotnet ef migrations add InitialCreate
   dotnet ef database update
   ```

2. **Run the application:**
   ```powershell
   dotnet run
   ```

3. **Open browser:**
   - Navigate to: https://localhost:5001

---

## Quick Reference

**Cloud SQL Instance Details:**
- Instance Name: `region-42-scores-standings`
- Connection Name: `ayso-region-42:us-west2:region-42-scores-standings`
- Region: `us-west2`
- Database: PostgreSQL 18
- Public IP: `34.102.124.228` (if needed for direct connection)

**Project:**
- GCP Project: `ayso-region-42`
- Your Account: `howard.cheng@aysoregion42.org`

**User Secrets Location:**
- Windows: `%APPDATA%\Microsoft\UserSecrets\c3b0fb10-4b9c-4c85-bc5b-0d9fb3b3dd1b\secrets.json`
