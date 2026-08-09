# PostgreSQL DateTime Handling

## Issue: DateTimeKind Requirements

PostgreSQL's `timestamp with time zone` type **requires** `DateTimeKind.Utc`. Attempting to save a `DateTime` with `DateTimeKind.Unspecified` will throw:

```
System.ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported.
```

---

## Solution: Always Use UTC

### ✅ When Parsing DateTimes

```csharp
// BAD - creates Unspecified kind
DateTime.TryParse(dateString, out var parsed);
row.ParsedScheduledDateTime = parsed;

// GOOD - explicitly set to UTC
DateTime.TryParse(dateString, out var parsed);
row.ParsedScheduledDateTime = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
```

### ✅ When Creating Test Data

```csharp
// BAD - creates Unspecified kind
private DateTime _baseDate = new DateTime(2026, 1, 1);

// GOOD - specify UTC
private DateTime _baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
```

### ✅ When Using Current Time

```csharp
// GOOD - already UTC
DateTime.UtcNow

// BAD - local time with local kind
DateTime.Now
```

---

## Current Implementation

### CSV Import (`CsvImportService.cs`)

Game times from SportsConnect CSV are parsed as **Pacific Time** (America/Los_Angeles) and properly converted to **UTC** for storage:

```csharp
var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
DateTime.TryParse(dateTimeString, out var parsedDateTime);

// Mark as unspecified so we can convert from Pacific
var pacificDateTime = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);

// Convert to UTC for storage
var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(pacificDateTime, pacificZone);
row.ParsedScheduledDateTime = utcDateTime;
```

### Display (`TimezoneHelper.cs` + `ViewHelpers.cs`)

UTC times from the database are converted back to Pacific Time for display:

```csharp
// In views
@game.ScheduledDateTime.ToPacificTime()
@game.ScheduledDateTime.ToPacificTimeWithZone() // Includes PST/PDT
```

This handles Daylight Saving Time automatically:
- **PST (Pacific Standard Time):** UTC-8 (November - March)
- **PDT (Pacific Daylight Time):** UTC-7 (March - November)

---

## Usage Examples

### In Controllers

```csharp
using Region42.ScoresStandings.Application.Helpers;

// Convert Pacific to UTC for storage
var pacificTime = new DateTime(2025, 3, 15, 10, 0, 0);
var utcTime = TimezoneHelper.ToUtc(pacificTime);
game.ScheduledDateTime = utcTime;

// Convert UTC to Pacific for display
var displayTime = TimezoneHelper.ToPacificTime(game.ScheduledDateTime);
```

### In Views

```razor
@* Simple format *@
@game.ScheduledDateTime.ToPacificTime()
@* Output: 3/15/2025 10:00 AM *@

@* With timezone *@
@game.ScheduledDateTime.ToPacificTimeWithZone()
@* Output: 3/15/2025 10:00 AM PDT *@

@* Date only *@
@game.ScheduledDateTime.ToPacificDate()
@* Output: 3/15/2025 *@

@* Time only *@
@game.ScheduledDateTime.ToPacificTimeOnly()
@* Output: 10:00 AM *@
```

---

## Testing

All test data builders (`TestDataBuilder.cs`) use UTC dates to match production behavior.

---

## References

- [Npgsql Date and Time Handling](https://www.npgsql.org/doc/types/datetime.html)
- [PostgreSQL Timestamp Types](https://www.postgresql.org/docs/current/datatype-datetime.html)
