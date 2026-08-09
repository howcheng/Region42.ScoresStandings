# Database Migrations Guide

## Session Configuration

**Important:** This application uses **session-based TempData storage** instead of cookie-based storage to handle large CSV preview data (186+ games). Without session storage, you would encounter HTTP 431 (Request Header Fields Too Large) errors during CSV import preview.

Session is configured in `Program.cs`:
- In-memory distributed cache for development
- 30-minute idle timeout
- Essential cookie for GDPR compliance

For production, consider using a persistent session store (Redis, SQL Server, etc.) if running multiple instances.

---

## Overview

This application uses Entity Framework Core migrations to manage database schema changes. Different strategies are used for development vs. production environments.

## Development Environment

**Automatic Migrations:** In development, migrations are applied automatically on application startup via `Program.cs`. This provides a seamless developer experience.

```csharp
// Program.cs checks for pending migrations and applies them
if (app.Environment.IsDevelopment())
{
	// Auto-apply migrations
}
```

### Creating a New Migration

```powershell
# Navigate to Web project
cd src/Region42.ScoresStandings.Web

# Create migration with descriptive name
dotnet ef migrations add AddIsRegion42TeamToTeam

# Migration is auto-applied on next app startup
```

## Production Environment

**Manual Deployment:** Migrations should be applied via your deployment pipeline, NOT automatically on startup.

### Recommended Approach

1. **Generate SQL Script** (for review/audit):
   ```powershell
   dotnet ef migrations script --idempotent --output migration.sql
   ```

2. **Apply via Pipeline** (Azure DevOps example):
   ```yaml
   - task: DotNetCoreCLI@2
	 displayName: 'Apply Database Migrations'
	 inputs:
	   command: 'custom'
	   custom: 'ef'
	   arguments: 'database update --connection "$(ConnectionString)"'
	   workingDirectory: 'src/Region42.ScoresStandings.Web'
   ```

## Rollback Strategy

EF Core migrations are **forward-only by design**. To rollback changes:

### Option 1: Create a Reverting Migration (Recommended)

If you need to undo `AddIsRegion42TeamToTeam`:

```powershell
# Create a new migration that reverses the changes
dotnet ef migrations add RevertIsRegion42TeamColumn

# In the new migration, manually write the Down logic:
public partial class RevertIsRegion42TeamColumn : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		// Remove the column we added earlier
		migrationBuilder.DropColumn(
			name: "IsRegion42Team",
			table: "Teams");
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		// Add it back if we need to rollback THIS migration
		migrationBuilder.AddColumn<bool>(
			name: "IsRegion42Team",
			table: "Teams",
			nullable: false,
			defaultValue: true);
	}
}
```

**Pros:**
- Safe for production (data preserved)
- Creates audit trail
- Works with existing data

**Cons:**
- Requires manual SQL knowledge for complex scenarios

---

### Option 2: Revert to Specific Migration (Development Only)

⚠️ **WARNING: This will LOSE DATA added after the target migration!**

```powershell
# List all migrations
dotnet ef migrations list

# Revert to specific migration (by name)
dotnet ef database update PreviousMigrationName

# Remove the migration files
dotnet ef migrations remove
```

**Use Case:** Only in development when you made a mistake and no production data exists.

---

### Option 3: Manual SQL Script (Emergency)

For production emergencies where you need immediate rollback:

1. Review the migration's `Down()` method
2. Generate equivalent SQL manually
3. Test in staging
4. Apply via database admin tools

```sql
-- Example: Rolling back the IsRegion42Team column
ALTER TABLE "Teams" DROP COLUMN "IsRegion42Team";
```

---

## Migration Best Practices

### ✅ Do:
- Use descriptive migration names: `AddIsRegion42TeamToTeam` not `UpdateTeams`
- Test migrations in staging before production
- Keep migrations small and focused
- Review generated SQL before deploying
- Back up production database before applying migrations

### ❌ Don't:
- Don't edit migrations after they've been applied to production
- Don't use auto-migrations in production
- Don't rollback using `ef database update` in production
- Don't delete migration files that have been deployed

---

## Common Scenarios

### Adding a Nullable Column (Safe)
```csharp
migrationBuilder.AddColumn<string>(
	name: "NewColumn",
	table: "Teams",
	nullable: true);  // ✅ Safe - no default required
```

### Adding a Non-Nullable Column (Requires Default)
```csharp
migrationBuilder.AddColumn<bool>(
	name: "IsRegion42Team",
	table: "Teams",
	nullable: false,
	defaultValue: true);  // ✅ Provides default for existing rows
```

### Renaming a Column (Data Preserved)
```csharp
migrationBuilder.RenameColumn(
	name: "OldName",
	table: "Teams",
	newName: "NewName");  // ✅ Data is preserved
```

### Dropping a Column (⚠️ Data Loss!)
```csharp
migrationBuilder.DropColumn(
	name: "IsRegion42Team",
	table: "Teams");  // ⚠️ Permanent data loss!
```

Always consider a "soft delete" approach (e.g., `IsDeleted` flag) instead of dropping columns with important data.

---

## Troubleshooting

### "Pending model changes" error
```powershell
# Create a migration to sync the model
dotnet ef migrations add SyncModelChanges
```

### Migration fails with constraint violation
```powershell
# Check migration SQL
dotnet ef migrations script LastGoodMigration FailingMigration

# Fix data manually, then retry
```

### Database is ahead of code
This means migrations were applied directly to the database without corresponding code files.

```powershell
# Generate a snapshot migration
dotnet ef migrations add CatchUpMigration --no-build
# Review and adjust the generated code
```

---

## References

- [EF Core Migrations Overview](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/)
- [Applying Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [Migration SQL Script Generation](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying#sql-scripts)
