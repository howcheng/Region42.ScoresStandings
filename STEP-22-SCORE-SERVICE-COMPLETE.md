# Step 22: Score Service Implementation - Complete

## Overview
Implemented the complete Score Service with score entry/update, comprehensive validation, game status enforcement, and automatic audit trail support. Enforces business rules to ensure data integrity and supports retroactive score corrections.

## What Was Implemented

### 1. ScoreService Implementation
**File**: `src/Region42.ScoresStandings.Application/Services/ScoreService.cs`

#### Key Features:
- **CRUD Operations**: Get (by game ID, by division, by round), Enter/Update, Delete
- **Smart Upsert**: Single method handles both creation and updates
- **Comprehensive Validation**:
  - Game must exist
  - Game must be marked as Completed before scoring
  - Scores must be non-negative (zero allowed)
- **Audit Trail**: Automatic tracking via BaseEntity (CreatedAt/ModifiedAt/CreatedBy/ModifiedBy)
- **Zero Score Support**: Allows 0-0 scoreless draws

#### Methods Implemented:
1. **GetScoreByGameIdAsync** - Retrieves score for a specific game
2. **EnterOrUpdateScoreAsync** - Smart upsert for score entry/corrections
3. **GetScoresByDivisionAsync** - All scores for division standings
4. **GetScoresByDivisionAndRoundAsync** - Point-in-time scores for historical standings
5. **CanEnterScoreAsync** - Validates game status before allowing entry
6. **DeleteScoreAsync** - Administrative deletion (returns bool for success)

### 2. Business Rules Enforced

#### Game Status Validation
- **Completed Required**: Games must have status `GameStatus.Completed` before score entry
- **Rationale**: 
  - Prevents scoring scheduled games prematurely
  - Enforces workflow: schedule → play → mark complete → enter score
  - Avoids confusion from partial/preliminary scores
- **Blocked Statuses**: Scheduled, Rescheduled, Cancelled

#### Score Validation
- **Non-Negative**: Both home and away scores must be >= 0
- **Zero Allowed**: 0-0 scores permitted for scoreless draws
- **No Upper Limit**: Youth soccer can have high scores; no artificial ceiling

#### Smart Upsert Logic
```csharp
if (score exists for game)
{
	Update existing score (audit trail logged)
}
else
{
	Create new score
}
```

**Benefits**:
- Single API call for both scenarios
- Simplified UI logic
- Automatic correction support
- Consistent error handling

#### Audit Trail Support
- **Automatic**: Leverages BaseEntity.ModifiedAt/ModifiedBy
- **Corrections Visible**: UI can show "Last modified" timestamps
- **No Extra Code**: Repository pattern handles auditing
- **Benefits**: Accountability, dispute resolution, error tracking

### 3. Comprehensive Test Coverage
**File**: `tests/Region42.ScoresStandings.Application.Tests/Services/ScoreServiceTests.cs`

#### Test Categories (19 Tests Total):

**GetScoreByGameIdAsync Tests (2 tests)**
- ✅ Returns score when exists
- ✅ Returns null when not found

**EnterOrUpdateScoreAsync Tests (8 tests)**
- ✅ Creates new score when game exists and no score
- ✅ Updates existing score when score already exists
- ✅ Throws ArgumentException when game not found
- ✅ Throws ArgumentException when home score negative
- ✅ Throws ArgumentException when away score negative
- ✅ Throws InvalidOperationException when game not completed
- ✅ Allows zero scores (0-0 scoreless draw)
- ✅ Proper repository interaction (Add vs Update paths)

**GetScoresByDivisionAsync Tests (2 tests)**
- ✅ Returns all scores for division
- ✅ Returns empty list when no scores

**GetScoresByDivisionAndRoundAsync Tests (2 tests)**
- ✅ Returns scores through specified round
- ✅ Excludes scores after specified round

**CanEnterScoreAsync Tests (4 tests)**
- ✅ Returns true when game completed
- ✅ Returns false when game scheduled
- ✅ Returns false when game cancelled
- ✅ Returns false when game not found

**DeleteScoreAsync Tests (2 tests)**
- ✅ Returns true when score exists (deletes successfully)
- ✅ Returns false when score not found

### 4. Repository Pattern Integration

#### Synchronous Operations
Unlike other services, ScoreService uses the actual repository signatures:
```csharp
void Update(T entity);              // Not UpdateAsync
void Delete(T entity);              // Not DeleteAsync
Task<int> SaveChangesAsync();       // Explicit save after changes
```

**Pattern Used**:
```csharp
// Update path
_scoreRepository.Update(existingScore);
await _scoreRepository.SaveChangesAsync();

// Delete path
_scoreRepository.Delete(score);
await _scoreRepository.SaveChangesAsync();
```

**Benefits**:
- Matches EF Core DbContext pattern
- Enables unit-of-work tracking
- Better transaction control
- Consistent with repository implementation

### 5. Error Handling & Logging

#### Exception Types:
- **ArgumentException**: Invalid IDs, negative scores
- **InvalidOperationException**: Business rule violations (game not completed)

#### Logging Levels:
- **Information**: Successful operations, updates with old/new values
- **Warning**: Validation failures, not found scenarios
- **Debug**: Simple status checks

#### Log Details Captured:
- GameId for all operations
- Old and new scores on updates (correction audit)
- Score values on creation
- Status information on validation failures

### 6. Integration with Existing Services

The ScoreService integrates seamlessly with:
- **GameService**: Validates game exists and status before scoring
- **StandingsService**: Retrieves scores for standings calculations
- **CsvImportService**: Can import scores if included in CSV
- **Future Score Entry UI**: Single endpoint for all score operations

### 7. Design Decisions

#### Why Require Game Status = Completed?
**Workflow Enforcement**:
- Game is scheduled
- Game is played
- Referee/coach marks game complete
- Score is entered
- Standings are updated

**Benefits**:
- Clear data state progression
- Prevents premature scoring
- Supports game cancellations (no orphaned scores)
- Enables score entry validation in UI

#### Why Allow Zero Scores?
**Real-World Scenarios**:
- Scoreless draws (0-0) are valid in soccer
- Both teams defend well
- Weather/field conditions impact scoring
- Youth soccer commonly has low-scoring games

**Implementation**:
```csharp
if (homeScore < 0)  // Note: < 0, not <= 0
{
	throw new ArgumentException("Home score cannot be negative");
}
```

#### Why Smart Upsert Instead of Separate Methods?
**Simplified API**:
- UI doesn't need to track "is this first entry or correction?"
- Single error handling path
- Consistent response type
- Less client-side logic

**Example Usage**:
```csharp
// UI code doesn't change between first entry and correction
var score = await scoreService.EnterOrUpdateScoreAsync(gameId, 3, 2);
```

#### Why Return bool for Delete?
**Idempotent Behavior**:
- Calling delete on non-existent score returns `false` (not error)
- UI can check result for success message
- Admin tools can safely retry
- Consistent with "soft delete" patterns elsewhere

### 8. Audit Trail for Score Corrections

#### Automatic Tracking:
Every score update logs:
- **ModifiedAt**: Timestamp of correction
- **ModifiedBy**: User who made correction
- **Old Values**: Captured in log message
- **New Values**: Stored in entity

#### Use Cases:
- **Dispute Resolution**: "When was this score changed?"
- **Error Investigation**: "Who corrected this score?"
- **Accountability**: Track all modifications
- **Reporting**: Score change history

#### Example Log Entry:
```
[Information] Updating existing score for game 123. Old: Home=2, Away=1. New: Home=3, Away=1
```

## Test Results
```
✅ All 102 tests passing
   ├─ 16 CSV import tests
   ├─ 19 standings tests (including playoff qualification)
   ├─ 22 team service tests
   ├─ 26 game service tests
   └─ 19 score service tests (NEW)

Build Status: ✅ Successful
Test Duration: ~1.0s
```

## Usage Examples

### Entering a Score (First Time)
```csharp
// Mark game as completed first (via GameService)
await gameService.UpdateGameStatusAsync(gameId, GameStatus.Completed);

// Enter score
var score = await scoreService.EnterOrUpdateScoreAsync(gameId, homeScore: 3, awayScore: 2);
```

### Correcting a Score
```csharp
// Same method - automatically detects existing score
var correctedScore = await scoreService.EnterOrUpdateScoreAsync(gameId, homeScore: 4, awayScore: 2);
// Logs: "Updating existing score for game X. Old: Home=3, Away=2. New: Home=4, Away=2"
```

### Checking Before Score Entry
```csharp
if (await scoreService.CanEnterScoreAsync(gameId))
{
	// Show score entry form
}
else
{
	// Show message: "Game must be marked as completed first"
}
```

### Getting Scores for Standings
```csharp
// Current standings (all rounds)
var allScores = await scoreService.GetScoresByDivisionAsync(divisionId);

// Point-in-time standings (through round 5)
var scoresThrough5 = await scoreService.GetScoresByDivisionAndRoundAsync(divisionId, throughRound: 5);
```

### Administrative Score Deletion
```csharp
var deleted = await scoreService.DeleteScoreAsync(gameId);
if (deleted)
{
	// Success message
}
else
{
	// "Score not found" message
}
```

## Design Patterns Used

### 1. Smart Upsert Pattern
Single method handles create/update by checking existence:
```csharp
var existing = await _repository.GetByIdAsync(id);
if (existing != null)
	Update(existing);
else
	Add(new Entity());
```

### 2. Guard Clause Pattern
Early validation returns/throws before core logic:
```csharp
if (game == null)
	throw new ArgumentException("Game not found");

if (game.Status != GameStatus.Completed)
	throw new InvalidOperationException("Game must be completed");

// Core logic here...
```

### 3. Repository + Unit of Work
Explicit save after modifications:
```csharp
_repository.Update(entity);
await _repository.SaveChangesAsync();  // Commit transaction
```

## Files Created/Modified
- ✏️ Created: `src/Region42.ScoresStandings.Application/Services/ScoreService.cs`
- ✏️ Created: `tests/Region42.ScoresStandings.Application.Tests/Services/ScoreServiceTests.cs`
- ✏️ Modified: `tests/Region42.ScoresStandings.Application.Tests/Helpers/TestDataBuilder.cs` (added Score factory overloads)
- 📄 Updated: Plan file progress (62% → 64%)

## Next Steps (Suggested)
1. **Volunteer Points Service**: Implement bulk grid entry and points management
2. **Service Registration**: Register all services in DI container
3. **Score Entry UI**: Create Razor Page for score entry/correction
4. **Game Management UI**: Create Razor Pages for schedule/status management
5. **Standings Display**: Public standings page with playoff markers

## Technical Notes
- Uses `IRepository<T>.Update()` and `Delete()` (synchronous) followed by `SaveChangesAsync()`
- Score entity uses nullable int for HomeScore/AwayScore (allows future "forfeit" scenarios)
- GameId is both primary key and foreign key on Score entity
- Audit trail handled automatically by BaseEntity and repository SaveChanges interceptor
- Zero scores explicitly allowed for scoreless draws
- Game status validation prevents scoring incomplete games
- Smart upsert pattern simplifies client code

## Edge Cases Handled
- ✅ Zero scores (0-0 draw)
- ✅ High scores (no upper limit)
- ✅ Game not found
- ✅ Score already exists (update path)
- ✅ Negative scores rejected
- ✅ Game not completed (workflow enforcement)
- ✅ Delete non-existent score (returns false, not error)

---
**Step 22 Status**: ✅ **COMPLETE**  
**Test Coverage**: 102/102 passing  
**Ready for**: Volunteer Points Service and UI development

Last Updated: After implementation of ScoreService
