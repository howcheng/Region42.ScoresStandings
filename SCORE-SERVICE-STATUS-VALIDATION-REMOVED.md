# Score Service Update - Game Status Validation Removed

## Change Summary
Removed the game status validation requirement from `EnterOrUpdateScoreAsync` based on user feedback about the need for flexible score corrections.

## What Changed

### Before:
```csharp
// Game must be Completed before score entry
if (game.Status != GameStatus.Completed)
{
	throw new InvalidOperationException("Game must be marked as Completed");
}
```

### After:
```csharp
// No status validation - scores can be entered/corrected regardless of game status
// UI can use CanEnterScoreAsync() to show warnings, but won't block operations
```

## Rationale

### Why Remove Status Validation?

1. **Score Corrections After Rescheduling**
   - If a game is played, scored, then later rescheduled/cancelled, the score should remain editable
   - Forcing status change before correction adds unnecessary friction

2. **Data Migration & Cleanup**
   - Historical data imports shouldn't require manipulating game statuses
   - Admins need flexibility to clean up data without workflow constraints

3. **Administrative Flexibility**
   - Power users/admins should be able to fix data errors regardless of status
   - Rigid validation blocks legitimate correction workflows

4. **UI Can Still Guide Users**
   - `CanEnterScoreAsync()` still checks status for UI warnings
   - UI can show: "This game isn't marked as completed. Are you sure?"
   - User can proceed if needed (e.g., for corrections)

### What `CanEnterScoreAsync` Is For Now

**Purpose**: UI guidance, not enforcement
- Returns `true` if game status is Completed
- Returns `false` otherwise (or if game not found)
- UI uses it to show warnings/recommendations
- **Does NOT block** the actual `EnterOrUpdateScoreAsync` operation

**Example UI Flow**:
```csharp
if (!await scoreService.CanEnterScoreAsync(gameId))
{
	// Show warning: "Game status is Scheduled. Typically scores are entered 
	// after marking the game as Completed. Continue anyway?"
	if (userConfirms)
	{
		await scoreService.EnterOrUpdateScoreAsync(gameId, home, away);
	}
}
else
{
	// Normal path - just enter the score
	await scoreService.EnterOrUpdateScoreAsync(gameId, home, away);
}
```

## Remaining Validations

The following validations are **still enforced**:

1. ✅ **Game Exists** - Cannot score non-existent game
2. ✅ **Non-Negative Scores** - Home/away must be >= 0
3. ✅ **Zero Allowed** - 0-0 scoreless draws permitted

## Test Changes

### Removed:
- `EnterOrUpdateScoreAsync_ThrowsInvalidOperationException_WhenGameNotCompleted`

### Updated:
- Removed `game.Status = GameStatus.Completed` from all remaining tests
- Tests now work with default game status (Scheduled)

### Test Count:
- **Before**: 102 tests
- **After**: 101 tests (1 removed)
- **Status**: ✅ All passing

## Code Impact

### Files Modified:
1. `src/Region42.ScoresStandings.Application/Services/ScoreService.cs`
   - Removed status validation block from `EnterOrUpdateScoreAsync`
   - Kept `CanEnterScoreAsync` for UI guidance

2. `tests/Region42.ScoresStandings.Application.Tests/Services/ScoreServiceTests.cs`
   - Removed status validation test
   - Removed status assignments from setup code in other tests

### Breaking Changes:
**None** - this is a relaxation of constraints

### Behavior Changes:
- **Before**: `EnterOrUpdateScoreAsync` throws if game not completed
- **After**: `EnterOrUpdateScoreAsync` allows any status, validates only game existence and score values

## Use Cases Now Supported

### 1. Score Correction After Status Change
```csharp
// Game was completed and scored
await scoreService.EnterOrUpdateScoreAsync(gameId, 3, 2);  // ✓

// Later, game is rescheduled due to weather
await gameService.UpdateGameStatusAsync(gameId, GameStatus.Rescheduled);

// Admin still needs to correct original score
await scoreService.EnterOrUpdateScoreAsync(gameId, 3, 1);  // ✓ Now allowed
```

### 2. Bulk Historical Import
```csharp
// Import old scores without touching game statuses
foreach (var (gameId, homeScore, awayScore) in historicalScores)
{
	await scoreService.EnterOrUpdateScoreAsync(gameId, homeScore, awayScore);  // ✓
}
```

### 3. Premature Score Entry (with UI warning)
```csharp
// UI detects game is still scheduled
if (!await scoreService.CanEnterScoreAsync(gameId))
{
	ShowWarning("Game hasn't been marked complete. Enter score anyway?");
	// User says yes → proceeds
}
await scoreService.EnterOrUpdateScoreAsync(gameId, 2, 1);  // ✓ Allowed
```

## Documentation Updates Needed

- [ ] Update API documentation for `EnterOrUpdateScoreAsync`
- [ ] Update `CanEnterScoreAsync` docs to clarify it's for UI guidance
- [ ] Update user guide to explain recommended workflow (complete → score) vs. allowed flexibility

---
**Change Status**: ✅ **COMPLETE**  
**Tests**: 101/101 passing  
**Impact**: Low (constraint relaxation, no breaking changes)

Last Updated: After removing game status validation requirement
