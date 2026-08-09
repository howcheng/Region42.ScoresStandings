# Step 15 Complete: Service Interfaces Created ✅

## Files Created

All service interfaces have been created in `Region42.ScoresStandings.Application/Interfaces/`:

### 1. ITeamService.cs
**Purpose**: Team management (CRUD operations)
- Get teams by division, season, or ID
- Create, update, deactivate teams
- Validate team name uniqueness

### 2. IGameService.cs
**Purpose**: Game scheduling and management
- Get games by division, round, team
- Create, update games
- Update game status
- Delete games (if no score entered)
- Validate schedule conflicts

### 3. IScoreService.cs
**Purpose**: Score entry and management
- Enter/update scores with audit trail
- Support retroactive corrections
- Get scores by division/round for standings
- Validate game completion before score entry

### 4. IVolunteerPointsService.cs
**Purpose**: Volunteer points tracking
- Enter/update volunteer points by team and round
- Get points by team, division, or round
- Used in standings calculation

### 5. IStandingsService.cs
**Purpose**: Calculate standings
- Current standings and point-in-time standings (by round)
- Standard soccer scoring: Win=3pts, Draw=1pt, Loss=0pt
- Includes volunteer points
- Handles divisions with odd teams (points per game adjustment)

**Includes DTOs**:
- `StandingsResult` - Division standings container
- `TeamStanding` - Individual team record with rank, W-D-L, goals, points

### 6. ICsvImportService.cs
**Purpose**: Import teams/games from CSV files
- Validates CSV before import (shows ALL errors)
- Filters for "Games" events containing "10U", "12U", or "14U"
- Preview import without committing
- Track teams created/updated, games created

**Includes DTOs**:
- `CsvValidationResult` - Validation errors and warnings
- `CsvImportResult` - Import statistics
- `CsvPreviewResult` - Preview teams/games before import
- `CsvTeamPreview` - Team preview with existing indicator
- `CsvGamePreview` - Game preview details

## CSV Import Filter Rules

Based on the sample CSV file `Schedule Match Report - Schedule_Match.csv`, the CSV import will:

1. **Only process rows where**:
   - Event Name contains "Games" (exact text)
   - Event Name contains one of: "10U", "12U", or "14U"

2. **Skip rows that**:
   - Have "Practice" in Event Name
   - Don't match the age group criteria
   - Have "Board Members" or similar non-game events

3. **Example valid event names**:
   - "Region 42 Fall 2025 - 10U - Boys (Games)"
   - "Region 42 Fall 2025 - 12U - Girls (Games)"
   - "Region 42 Fall 2025 - 14U - Boys (Games)"

4. **Example invalid event names (will be skipped)**:
   - "Region 42 Fall 2025 - 12U - Girls (Practices)"
   - "2025 Board Members - Board Member (Practices)"
   - "Region 42 Fall 2025 - 16U - Girls (Games)" ❌ 16U not in scope

## Key Design Decisions

### 1. All Services Are Asynchronous
Every method returns `Task<T>` for scalability and responsiveness.

### 2. Separation of Concerns
- **ITeamService** - Team entities only
- **IGameService** - Game entities and scheduling logic
- **IScoreService** - Score entities with audit trail
- **IStandingsService** - Read-only calculations (no persistence)
- **ICsvImportService** - File processing orchestration

### 3. Validation at Service Layer
Each service includes validation methods:
- Team name uniqueness
- Schedule conflict detection
- Game completion status before score entry
- CSV structure and content validation

### 4. Point-in-Time Standings
`IStandingsService` supports historical standings queries:
```csharp
GetStandingsByRoundAsync(divisionId, throughRound: 5)
```
Shows standings after round 5 only.

### 5. Audit Trail Support
`IScoreService.EnterOrUpdateScoreAsync` leverages `BaseEntity.ModifiedAt/ModifiedBy` for score correction tracking.

## Compilation Status
✅ All files compile without errors

## Next Steps

### Step 16: Create DTOs
We already have some DTOs in place (StandingsResult, CsvImportResult, etc.), but we may need additional DTOs for:
- Team creation/update forms
- Game creation/update forms
- Score entry forms
- Validation error details

### Step 17: Implement CSV Import Service
The most complex service - will parse CSV, validate all rows, extract team info, create divisions, map games.

### Step 18-20: Implement Remaining Services
- TeamService
- GameService
- ScoreService
- VolunteerPointsService
- StandingsService

---

**Progress**: Step 15 of 39 complete (38%)

Ready to proceed to Step 16! 🚀
