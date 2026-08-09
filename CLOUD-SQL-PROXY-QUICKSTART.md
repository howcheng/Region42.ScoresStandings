# Cloud SQL Proxy - Quick Start

## ✅ ADC Now Configured

Application Default Credentials saved to:
`C:\Users\howard\AppData\Roaming\gcloud\application_default_credentials.json`

---

## Login / Reauthenticate (Do This First!)

**Before starting the proxy**, make sure your credentials are fresh:

```powershell
gcloud auth application-default login
```

This will open your browser for authentication. Complete the login flow.

**Why?** Google periodically requires reauthentication for security. If you see errors like:
- `invalid_grant`
- `reauth related error (invalid_rapt)`
- `cannot fetch token: 400`

...just run the login command above again.

---

## Start Cloud SQL Proxy

### Option 1: Run in Current Terminal (Foreground)
```powershell
.\cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings
```

Keep this terminal open. The proxy will run on `localhost:5432`

### Option 2: Run in Background (PowerShell)
```powershell
Start-Process -FilePath ".\cloud-sql-proxy.exe" -ArgumentList "ayso-region-42:us-west2:region-42-scores-standings" -WindowStyle Minimized
```

### Option 3: Run on Different Port (if 5432 is taken)
```powershell
.\cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings --port 5433
```

Then use `Host=127.0.0.1;Port=5433;...` in your connection string

---

## Set Cloud SQL Password

Before connecting, you need to set a password for the `postgres` user:

```powershell
gcloud sql users set-password postgres `
  --instance=region-42-scores-standings `
  --password=YOUR_SECURE_PASSWORD
```

**⚠️ Important**: Choose a strong password and remember it!

---

## Configure User Secrets

After setting the password and starting the proxy:

```powershell
cd Region42.ScoresStandings.Web

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=127.0.0.1;Port=5432;Database=region42;Username=postgres;Password=YOUR_SECURE_PASSWORD"
```

---

## Test Connection

Once proxy is running and secrets are set:

```powershell
# From the Web project directory
dotnet run
```

Or test with psql (if installed):
```powershell
psql -h 127.0.0.1 -p 5432 -U postgres -d region42
```

---

## Troubleshooting

### "invalid_grant" or "invalid_rapt" error
- **Cause**: Application Default Credentials expired or need reauthentication
- **Solution**: Run `gcloud auth application-default login` and complete the browser authentication

### "bind: Only one usage of each socket address"
- **Cause**: Port 5432 is already in use (maybe local PostgreSQL?)
- **Solution**: Use a different port with `--port 5433`

### "invalid password"
- **Cause**: Password not set or incorrect
- **Solution**: Run the `gcloud sql users set-password` command above

### Proxy exits immediately
- **Cause**: Missing ADC (but you just fixed this!)
- **Verify**: Check that `application_default_credentials.json` exists

---

## Stop Cloud SQL Proxy

Press `Ctrl+C` in the terminal where it's running, or:

```powershell
# Find and kill the process
Get-Process -Name "cloud-sql-proxy" | Stop-Process
```

---

## Alternative: Use Local PostgreSQL Instead

If Cloud SQL Proxy is too complex for development, you can use local PostgreSQL:

```powershell
# Start Docker PostgreSQL
docker run --name region42-postgres `
  -e POSTGRES_DB=region42 `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=LocalDevPassword123! `
  -p 5432:5432 `
  -v region42_pgdata:/var/lib/postgresql/data `
  -d postgres:16

# Configure user secrets
cd Region42.ScoresStandings.Web
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=region42;Username=postgres;Password=LocalDevPassword123!"
```

This is often simpler for local development!
