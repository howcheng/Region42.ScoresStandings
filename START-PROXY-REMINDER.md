# IMPORTANT: Cloud SQL Proxy Must Be Running!

## Start Cloud SQL Proxy (in a separate terminal)

Open a new PowerShell terminal and run:

```powershell
.\cloud-sql-proxy.exe ayso-region-42:us-west2:region-42-scores-standings
```

**Keep this terminal running!** You should see:
```
Ready for new connections
```

---

## Then Return Here and Continue

Once the proxy is running, you can:

1. **Create migrations** (creates database schema files)
2. **Apply migrations** (creates tables in the database)
3. **Run the application**

---

## Quick Commands

### Create Migration
```powershell
cd Region42.ScoresStandings.Web
dotnet ef migrations add InitialCreate
```

### Apply Migration (create database tables)
```powershell
dotnet ef database update
```

### Run the Application
```powershell
dotnet run
```

Then open: https://localhost:5001

---

## If Proxy Isn't Running

You'll see errors like:
- "Connection refused"
- "No connection could be made"
- "Host is unreachable"

**Solution**: Start the proxy in another terminal window first!
