# Step 20: Team Service Implementation - Complete

## Overview
Implemented the complete Team Service with CRUD operations, validation, and business rules enforcement. Includes comprehensive test coverage ensuring data integrity and proper error handling.

## What Was Implemented

### 1. TeamService Implementation
**File**: `src/Region42.ScoresStandings.Application/Services/TeamService.cs`

#### Key Features:
- **CRUD Operations**: Create, Read (by ID, by division, by season), Update, Deactivate
- **Validation Rules**:
  - Team name must be unique within a division (case-insensitive)
  - Division must exist before creating/updating teams
  - Cannot deactivate teams with game history (soft delete protection)
  - Same team name allowed in different divisions
- **Soft Delete**: Teams with games cannot be deleted, maintaining historical records
- **Audit Support**: All operations automatically tracked via BaseEntity (CreatedAt/By, ModifiedAt/By)

#### Methods Implemented:
1. **GetTeamsByDivisionAsync** - Returns all active teams for a specific division
2. **GetTeamByIdAsync** - Retrieves a single team by ID
3. **GetTeamsBySeasonAsync** - Gets all active teams across all divisions in a season
4. **CreateTeamAsync** - Creates new team with validation
5. **UpdateTeamAsync** - Updates team with name uniqueness validation
6. **DeactivateTeamAsync** - Soft deletes team (only if no games)
7. **IsTeamNameUniqueAsync** - Validates team name uniqueness within division

### 2. Business Rules Enforced

#### Team Name Uniqueness
- Team names must be unique within a division
- Case-insensitive comparison ("TEAM A" = "Team A")
- Same name allowed in different divisions (e.g., "Eagles" can exist in 10U Boys and 12U Girls)
- Uniqueness check excludes the team being updated

#### Data Integrity
- **Division Validation**: Division must exist before team creation/update
- **Game Protection**: Teams with game history cannot be deactivated
  - Rationale: Historical data integrity for standings, scores, and reports
  - Teams remain active for archival purposes
- **Active Flag**: Teams marked inactive are excluded from queries by default

#### Audit Trail
- CreatedAt/CreatedBy automatically set on creation
- ModifiedAt/ModifiedBy automatically updated on changes
- RowVersion for concurrency control (via BaseEntity/DbContext)

### 3. Comprehensive Test Coverage
**File**: `tests/Region42.ScoresStandings.Application.Tests/Services/TeamServiceTests.cs`

#### Test Categories (22 Tests Total):

**GetTeamsByDivisionAsync Tests (2 tests)**
- ✅ Returns only active teams for division
- ✅ Returns empty list when no teams exist

**GetTeamByIdAsync Tests (2 tests)**
- ✅ Returns team when exists
- ✅ Returns null when not found

**GetTeamsBySeasonAsync Tests (1 test)**
- ✅ Returns active teams from all divisions in season
- ✅ Excludes inactive teams

**CreateTeamAsync Tests (4 tests)**
- ✅ Creates team successfully with all validations
- ✅ Throws when division not found
- ✅ Throws when team name exists in same division
- ✅ Allows same team name in different divisions

**UpdateTeamAsync Tests (5 tests)**
- ✅ Updates team successfully
- ✅ Throws when team not found
- ✅ Throws when division not found
- ✅ Throws when new name conflicts with another team
- ✅ Allows keeping the same name for same team

**DeactivateTeamAsync Tests (3 tests)**
- ✅ Deactivates team successfully when no games
- ✅ Throws when team not found
- ✅ Throws when team has associated games (protection)

**IsTeamNameUniqueAsync Tests (5 tests)**
- ✅ Returns true when name is unique
- ✅ Returns false when name exists
- ✅ Case-insensitive comparison
- ✅ Excludes specified team (for updates)
- ✅ Ignores inactive teams

### 4. Error Handling & Logging

#### Exception Types:
- **ArgumentException**: Invalid IDs (team not found, division not found)
- **InvalidOperationException**: Business rule violations (duplicate name, team has games)

#### Logging Levels:
- **Information**: Successful operations (create, update, deactivate), queries with filters
- **Warning**: Validation failures, not found scenarios
- **Debug**: Simple retrieval operations

### 5. Integration with Existing Services

The TeamService integrates seamlessly with:
- **CsvImportService**: Creates teams during schedule imports
- **StandingsService**: Retrieves teams for standings calculations
- **Future Game Service**: Will validate team existence and division membership

## Test Results
```
✅ All 57 tests passing
   ├─ 16 CSV import tests
   ├─ 19 standings tests (including playoff qualification)
   └─ 22 team service tests (NEW)

Build Status: ✅ Successful
```

## Usage Examples

### Creating a Team
```csharp
var team = new Team
{
	DivisionId = 1,
	Name = "Eagles",
	ContactName = "John Smith",
	ContactEmail = "john@example.com",
	ContactPhone = "555-1234"
};

var createdTeam = await teamService.CreateTeamAsync(team);
```

### Updating a Team
```csharp
var team = await teamService.GetTeamByIdAsync(teamId);
team.Name = "Updated Eagles";
team.ContactName = "Jane Doe";

var updated = await teamService.UpdateTeamAsync(team);
```

### Validating Uniqueness Before Save
```csharp
if (!await teamService.IsTeamNameUniqueAsync("Team Name", divisionId))
{
	// Show error to user
	ModelState.AddModelError("Name", "Team name already exists in this division");
}
```

### Preventing Deletion of Teams with Games
```csharp
try
{
	await teamService.DeactivateTeamAsync(teamId);
}
catch (InvalidOperationException ex)
{
	// Show error: "Cannot deactivate team - has associated games"
}
```

## Design Decisions

### Why Soft Delete?
Teams with game history must remain in the database to preserve:
- Historical standings calculations
- Score records and game results
- Volunteer points tracking
- Audit trails for season reports

### Why Case-Insensitive Names?
Users may enter team names inconsistently:
- "Eagles" vs "EAGLES" vs "eagles"
- Prevents confusion and duplicate-looking entries
- ToLower() comparison ensures consistency

### Why Division-Scoped Uniqueness?
Different divisions are independent:
- 10U Boys "Eagles" ≠ 12U Girls "Eagles"
- Reduces constraint conflicts
- Allows common team names across age groups

### Why Check Games Before Deactivation?
Prevents orphaned data:
- Games would reference non-existent teams
- Standings calculations would break
- Historical records would be incomplete

## Files Created/Modified
- ✏️ Created: `src/Region42.ScoresStandings.Application/Services/TeamService.cs`
- ✏️ Created: `tests/Region42.ScoresStandings.Application.Tests/Services/TeamServiceTests.cs`
- 📄 Updated: Plan file progress (58% → 60%)

## Next Steps (Suggested)
1. **Game Service**: Implement schedule management and game CRUD
2. **Score Service**: Implement score entry with retroactive corrections
3. **Volunteer Points Service**: Implement bulk grid entry
4. **Team Management UI**: Create Razor Pages for team CRUD
5. **Integration Tests**: Test TeamService with real database

## Technical Notes
- All tests use Moq for repository mocking
- TestDataBuilder provides consistent test data creation
- FluentAssertions for readable test assertions
- Async/await throughout for proper async testing
- Follows existing patterns from StandingsService and CsvImportService

---
**Step 20 Status**: ✅ **COMPLETE**  
**Test Coverage**: 57/57 passing  
**Ready for**: UI implementation and next service (GameService)

Last Updated: After implementation of TeamService
