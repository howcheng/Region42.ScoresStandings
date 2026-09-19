# Database Migrations (Legacy)

This application previously used PostgreSQL with Entity Framework Core migrations. **Data is now stored as JSON files in Google Cloud Storage.**

See **[JSON File Storage Guide](json-storage.md)** for the current storage model.

If you need to export remaining data from Cloud SQL, use:

```powershell
cd src/Region42.ScoresStandings.Web
dotnet run -- --export-from-postgres
```
