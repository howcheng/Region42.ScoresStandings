# Step 21: Game Service Implementation - Complete

## Overview
Implemented the complete Game Service with comprehensive CRUD operations, extensive validation, and business rules enforcement. Includes schedule conflict detection, team assignment validation, and score protection for historical data integrity.

## What Was Implemented

### 1. GameService Implementation
**File**: `src/Region42.ScoresStandings.Application/Services/GameService.cs`

#### Key Features:
- **CRUD Operations**: Create, Read (by ID, by division, by round, by team), Update, Delete, Status updates
- **Comprehensive Validation**:
  - Division existence and team membership
  - Teams in same division requirement
  - Teams cannot play themselves
  - Round numbers within division limits
  - Schedule date validation (not in past)
  - Schedule conflict detection (2-hour window)
- **Data Protection**: Games with scores cannot be deleted (historical integrity)
- **Status Management**: Update game status (Scheduled → Completed, Cancelled, Rescheduled)

#### Methods Implemented:
1. **GetGamesByDivisionAsync** - All games for a division
2. **GetGamesByDivisionAndRoundAsync** - Games filtered by round
3. **GetGameByIdAsync** - Single game retrieval
4. **GetGamesByTeamAsync** - All games for a team (home and away)
5. **CreateGameAsync** - Creates game with full validation
6. **UpdateGameAsync** - Updates game with validation
7. **UpdateGameStatusAsync** - Quick status updates
8. **DeleteGameAsync** - Deletes game if no score exists
9. **ValidateNoScheduleConflictAsync** - Checks for scheduling conflicts with 2-hour buffer

### 2. Business Rules Enforced

#### Team Assignment Validation
- **Same Division**: Both teams must belong to the game's division
- **No Self-Play**: Home and away teams must be different
- **Team Existence**: Both teams must exist in the database
- **Active Teams**: Only active teams can be assigned to games

#### Scheduling Validation
- **Round Limits**: Round number between 1 and Division.TotalRounds
- **Future Dates**: Games cannot be scheduled more than 1 day in the past
- **Conflict Detection**: Teams cannot play multiple games within 2-hour window
  - Rationale: Allows time for travel, warm-up, and potential overtime
  - Checks both home and away teams
  - Ignores cancelled games
  - Can exclude specific game for updates

#### Data Integrity
- **Score Protection**: Games with entered scores cannot be deleted
  - Preserves historical records for standings
  - Maintains audit trail for completed games
  - Prevents orphaned score records
- **Status Management**: Proper game status lifecycle
  - Scheduled → Completed/Cancelled/Rescheduled
  - Cancelled games excluded from conflict detection

### 3. Comprehensive Test Coverage
**File**: `tests/Region42.ScoresStandings.Application.Tests/Services/GameServiceTests.cs`

#### Test Categories (26 Tests Total):

**GetGamesByDivisionAsync Tests (1 test)**
- ✅ Returns all games for division

**GetGamesByDivisionAndRoundAsync Tests (1 test)**
- ✅ Returns only games for specific round

**GetGameByIdAsync Tests (2 tests)**
- ✅ Returns game when exists
- ✅ Returns null when not found

**GetGamesByTeamAsync Tests (1 test)**
- ✅ Returns both home and away games for team
- ✅ Excludes games not involving the team

**CreateGameAsync Tests (12 tests)**
- ✅ Creates game successfully with valid data
- ✅ Throws when division not found
- ✅ Throws when home team not found
- ✅ Throws when away team not found
- ✅ Throws when home team not in division
- ✅ Throws when away team not in division
- ✅ Throws when team plays itself
- ✅ Throws when round number invalid (< 1 or > TotalRounds)
- ✅ Throws when scheduled in past
- ✅ Throws when home team has schedule conflict
- ✅ Sets default status to Scheduled
- ✅ Validates schedule conflicts with 2-hour buffer

**UpdateGameAsync Tests (2 tests)**
- ✅ Updates game successfully
- ✅ Throws when game not found
- ✅ Re-validates all business rules
- ✅ Excludes current game from conflict check

**UpdateGameStatusAsync Tests (2 tests)**
- ✅ Updates status successfully
- ✅ Throws when game not found

**DeleteGameAsync Tests (3 tests)**
- ✅ Deletes game successfully when no score
- ✅ Throws when game not found
- ✅ Throws when score exists (protection)

**ValidateNoScheduleConflictAsync Tests (4 tests)**
- ✅ Returns true when no conflict
- ✅ Returns false when conflict exists
- ✅ Excludes specified game (for updates)
- ✅ Ignores cancelled games

### 4. Schedule Conflict Detection Logic

#### Conflict Window
- **Buffer**: 2 hours before and after scheduled time
- **Rationale**: 
  - Travel time between fields
  - Warm-up and preparation
  - Potential overtime or delays
  - Better user experience (prevents back-to-back scheduling)

#### Conflict Detection Rules
```csharp
Conflict exists if:
  - Team (home OR away) has another game
  - Within 2-hour window (+/- 2 hours)
  - Game status != Cancelled
  - Not the game being updated (if excludeGameId provided)
```

#### Example Scenarios:
- Game at 10:00 AM blocks 8:00 AM - 12:00 PM
- Team cannot have games at 9:30 AM (conflict)
- Team can have game at 12:30 PM (no conflict)
- Cancelled games don't block time slots

### 5. Error Handling & Logging

#### Exception Types:
- **ArgumentException**: Invalid IDs, invalid round numbers, past dates
- **InvalidOperationException**: Business rule violations (team self-play, wrong division, schedule conflicts, score protection)

#### Logging Levels:
- **Information**: Successful operations, queries with filters
- **Warning**: Validation failures, not found scenarios, conflicts
- **Debug**: Simple conflict checks

### 6. Integration with Existing Services

The GameService integrates seamlessly with:
- **CsvImportService**: Creates games during schedule imports
- **StandingsService**: Retrieves games for standings calculations
- **Future Score Service**: Will validate game existence before score entry
- **TeamService**: Validates team existence and division membership

## Test Results
```
✅ All 83 tests passing
   ├─ 16 CSV import tests
   ├─ 19 standings tests (including playoff qualification)
   ├─ 22 team service tests
   └─ 26 game service tests (NEW)

Build Status: ✅ Successful
Test Duration: ~1.0s
```

## Usage Examples

### Creating a Game
```csharp
var game = new Game
{
	DivisionId = 1,
	HomeTeamId = 5,
	AwayTeamId = 8,
	Scheduled DateTime = DateTime.UtcNow.AddDays(7),
	Round = 3,
	Location = "Field 1 - Park A",
	Status = GameStatus.Scheduled
};

var createdGame = await gameService.CreateGameAsync(game);
```

### Updating Game Status
```csharp
await gameService.UpdateGameStatusAsync(gameId, GameStatus.Completed);
```

### Checking Schedule Conflicts
```csharp
var hasConflict = await gameService.ValidateNoScheduleConflictAsync(
	teamId, 
	scheduledDateTime,
	excludeGameId: currentGameId // For updates
);

if (!hasConflict)
{
	// Proceed with scheduling
}
```

### Deleting a Game (Score Protection)
```csharp
try
{
	await gameService.DeleteGameAsync(gameId);
}
catch (InvalidOperationException ex)
{
	// Cannot delete - score exists
	// Show error to user
}
```

## Design Decisions

### Why 2-Hour Conflict Window?
Prevents scheduling issues:
- **Travel Time**: Teams need time to reach the field
- **Preparation**: Warm-up, equipment setup
- **Buffer**: Games may run long, weather delays
- **User Experience**: Reduces physical exhaustion, improves performance

### Why Prevent Deletion of Games with Scores?
Maintains data integrity:
- **Historical Records**: Standings depend on past games
- **Audit Trail**: Score corrections traceable via ModifiedAt/By
- **Referenced Data**: Scores would become orphaned
- **Reporting**: Season summaries need complete game history

### Why Check Both Home and Away Teams?
Comprehensive conflict detection:
- Team may be home in one game, away in another
- Both assignments prevent the team from playing
- Ensures no team is double-booked

### Why Validate Division Membership?
Prevents data corruption:
- **Standings Accuracy**: Teams only compete within division
- **Fair Competition**: Different divisions have different rules
- **Reporting**: Division-based reports would be incorrect

### Why Allow Scheduling 1 Day in Past?
Practical flexibility:
- **Late Entry**: Games played yesterday can still be added
- **Make-up Games**: Catch up on data entry
- **Time Zones**: UTC conversion buffer
- **Too Strict > Too Lenient**: Past 1 day likely data error

## Files Created/Modified
- ✏️ Created: `src/Region42.ScoresStandings.Application/Services/GameService.cs`
- ✏️ Created: `tests/Region42.ScoresStandings.Application.Tests/Services/GameServiceTests.cs`
- 📄 Updated: Plan file progress (60% → 62%)

## Next Steps (Suggested)
1. **Score Service**: Implement score entry with retroactive corrections
2. **Volunteer Points Service**: Implement bulk grid entry
3. **Game Management UI**: Create Razor Pages for schedule management
4. **Schedule Display**: Public-facing game schedules
5. **Notification System**: Game reminders and schedule changes

## Technical Notes
- All tests use Moq for repository mocking
- TestDataBuilder provides consistent test data creation
- Default status set to Scheduled if not provided
- Schedule conflict buffer is configurable (currently 2 hours)
- Conflict detection uses expression compilation for performance
- Status updates bypass full validation for efficiency

---
**Step 21 Status**: ✅ **COMPLETE**  
**Test Coverage**: 83/83 passing  
**Ready for**: Score Service implementation and UI development

Last Updated: After implementation of GameService
