# Local Development Database Setup Scripts

## Option A: Local PostgreSQL with Docker (Recommended for Development)

### 1. Start Local PostgreSQL Container
```powershell
docker run --name region42-postgres `
  -e POSTGRES_DB=region42 `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=LocalDevPassword123! `
  -p 5432:5432 `
  -v region42_pgdata:/var/lib/postgresql/data `
  -d postgres:16

# Check if it's running
docker ps | Select-String region42-postgres
```

### 2. Configure User Secrets for Local Database
```powershell
cd Region42.ScoresStandings.Web

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=LocalDevPassword123!"
```

### 3. Verify Connection
```powershell
# Using psql in the container
docker exec -it region42-postgres psql -U postgres -d region42 -c "SELECT version();"
```

---

## Option B: Connect to Cloud SQL Instance (Staging/Testing)

### Prerequisites
- Install Cloud SQL Auth Proxy: https://cloud.google.com/sql/docs/postgres/sql-proxy

### 1. Download and Install Cloud SQL Auth Proxy
```powershell
# Download (Windows)
Invoke-WebRequest -Uri "https://storage.googleapis.com/cloud-sql-connectors/cloud-sql-proxy/v2.14.2/cloud-sql-proxy.x64.exe" -OutFile "cloud-sql-proxy.exe"

# Or use chocolatey
choco install cloud-sql-proxy
```

### 2. Start Cloud SQL Proxy
```powershell
# Start the proxy (keep this terminal open)
./cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings
```

This will create a local connection on `127.0.0.1:5432`

### 3. Set Cloud SQL Password
You need to set a password for the postgres user:
```powershell
gcloud sql users set-password postgres `
  --instance=region-42-scores-standings `
  --password=YOUR_SECURE_PASSWORD_HERE
```

### 4. Configure User Secrets for Cloud SQL via Proxy
```powershell
cd Region42.ScoresStandings.Web

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=YOUR_SECURE_PASSWORD_HERE"
```

---

## Option C: Direct Cloud SQL Connection (Not Recommended for Development)

### Configure User Secrets for Direct Connection
```powershell
cd Region42.ScoresStandings.Web

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=34.102.124.228;Port=5432;Database=region42;Username=postgres;Password=YOUR_SECURE_PASSWORD_HERE;SSL Mode=Require"
```

**Note**: This requires your IP to be whitelisted in Cloud SQL. Not recommended for development due to security and latency.

---

## Google OAuth Setup

### 1. Create OAuth 2.0 Client ID
1. Go to: https://console.cloud.google.com/apis/credentials?project=ayso-region-42
2. Click **"Create Credentials"** → **"OAuth 2.0 Client ID"**
3. If prompted, configure the **OAuth consent screen**:
   - User Type: **Internal** (for AYSO Region 42 only)
   - App name: **Region 42 Scores & Standings**
   - User support email: **howard.cheng@aysoregion42.org**
   - Developer contact: **howard.cheng@aysoregion42.org**
4. Create OAuth Client:
   - Application type: **Web application**
   - Name: **Region42 Scores Local Dev**
   - Authorized redirect URIs:
	 - `https://localhost:5001/signin-google`
	 - `http://localhost:5000/signin-google`
   - Click **Create**
5. Copy the **Client ID** and **Client Secret**

### 2. Configure User Secrets with OAuth Credentials
```powershell
cd Region42.ScoresStandings.Web

dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID.apps.googleusercontent.com"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

---

## Verify Configuration

### Check User Secrets
```powershell
cd Region42.ScoresStandings.Web
dotnet user-secrets list
```

You should see:
```
Authentication:Google:ClientId = YOUR_CLIENT_ID
Authentication:Google:ClientSecret = YOUR_CLIENT_SECRET
ConnectionStrings:DefaultConnection = Host=...
```

---

## Quick Setup Commands (Copy & Paste)

### For Local Development (Recommended):
```powershell
# 1. Start local PostgreSQL
docker run --name region42-postgres -e POSTGRES_DB=region42 -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=LocalDevPassword123! -p 5432:5432 -v region42_pgdata:/var/lib/postgresql/data -d postgres:16

# 2. Wait a few seconds for it to start
Start-Sleep -Seconds 5

# 3. Configure connection string
cd Region42.ScoresStandings.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=LocalDevPassword123!"

# 4. You still need to manually configure OAuth (see above)
# Then add:
# dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_CLIENT_ID"
# dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_CLIENT_SECRET"
```

---

## Next Steps After Configuration

1. Run migrations to create database schema:
```powershell
cd Region42.ScoresStandings.Web
dotnet ef migrations add InitialCreate
dotnet ef database update
```

2. Run the application:
```powershell
dotnet run
```

3. Navigate to: https://localhost:5001
