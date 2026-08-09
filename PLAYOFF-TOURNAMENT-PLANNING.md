# Playoff & Tournament Feature Planning

## Status Update
- ✅ **Phase 1 Complete** (Step 19) - Playoff qualification logic, league-wide settings, division configuration
- ⏸️ **Phase 2 Deferred** - Tournament structure and bracket management (waiting for user direction)

See [STEP-19-PLAYOFF-CONFIGURATION-PHASE1-COMPLETE.md](STEP-19-PLAYOFF-CONFIGURATION-PHASE1-COMPLETE.md) for Phase 1 implementation details.

---

## Overview

Two major features to add to the soccer league tracking application:
1. **✅ Playoff Qualification** - Configurable rules for which teams qualify for playoffs (COMPLETE)
2. **⏸️ End-of-Season Tournaments** - Round-robin and knockout tournament support (DEFERRED)

---

## 1. Playoff Qualification Requirements ✅ COMPLETE

### Implementation Summary (Phase 1)
- ✅ Created `Settings` entity for league-wide configuration (minimum volunteer points)
- ✅ Added `PlayoffSpots` property to `Division` entity for division-specific configuration
- ✅ Extended `TeamStanding` DTO with `QualifiesForPlayoffs` and `PlayoffQualificationNote`
- ✅ Updated `StandingsService` to calculate playoff qualification with two-factor logic:
  - Team rank ≤ Division.PlayoffSpots (division-specific)
  - Team volunteer points ≥ Settings.MinVolunteerPointsForPlayoff (league-wide)
- ✅ Added comprehensive test coverage (3 new tests, 35 total passing)
- ✅ Created database migration for new entities and properties

### Business Rules (AS CLARIFIED)
- **Minimum volunteer points requirement applies to ALL divisions** (not just 10U as originally stated)
- **Each division can have a different number of playoff spots** (configurable via admin)
- **Two-factor qualification**: Team must satisfy BOTH conditions:
  1. Rank within playoff spots for their division (e.g., top 2 if PlayoffSpots = 2)
  2. Meet or exceed league-wide minimum volunteer points threshold
- **Example**: If league minimum is 3 volunteer points and division has 2 playoff spots:
  - Top 2 teams with ≥3 volunteer points qualify
  - A team ranked #1 or #2 with only 2 volunteer points does NOT qualify

### Admin Configuration Requirements (Phase 1 ✅ / UI Pending)
- ✅ **League-wide settings** (Settings entity):
  - Minimum volunteer points for playoff eligibility
  - Default playoff spots for new divisions
- ✅ **Per-division settings** (Division.PlayoffSpots):
  - Number of playoff spots (division-specific)
  - Can be changed mid-season via admin page (business logic ready, UI pending)

### Standings Display Requirements (Business Logic ✅ / UI Pending)
- ✅ **Qualification data available** in TeamStanding DTO:
  - `QualifiesForPlayoffs` (bool)
  - `PlayoffQualificationNote` (string with user-friendly explanation)
- 🔲 **UI implementation pending**:
  - Visual indicator for playoff-qualifying teams
    - Suggestion: Different background color (e.g., light green)
    - Alternative: Icon/badge next to team name
    - Alternative: "Q" indicator in standings table
  - Show qualification status in real-time based on current standings
  - Display helpful notes (e.g., "Needs 2 more volunteer points to qualify")
  - Alternative: Icon/badge next to team name
  - Alternative: "Q" indicator in standings table
- **Show qualification status** in real-time based on current standings
- **Handle edge cases**:
  - Team drops below volunteer point threshold → lose qualification
  - Tied teams → both show as "in playoff position" or neither

---

## 2. Tournament Structure Requirements

### 10U Tournament (8 games + 2-phase tournament)

**Regular Season**: 8 games  
**Tournament Phase 1** (Round-Robin Groups):
- Teams split into groups (e.g., 2 groups of 4 teams)
- Each team plays others in their group
- Records determine seeding for knockout

**Tournament Phase 2** (Knockout):
- Single elimination
- Winner advances, loser out
- Final determines division champion

### 12U & 14U Tournament (Knockout Only)

**Regular Season**: Full schedule  
**Tournament** (Knockout Only):
- Single elimination bracket
- Seeded by regular season standings
- Championship game determines winner

### Tournament Game Distinctions
- **Separate from regular season games**
- **Do not affect regular season standings**
- **May have different scoring rules** (e.g., penalty shootouts for ties)
- **Track tournament bracket/progression**

---

## 3. Proposed Domain Model Changes

### 3.1. Update Division Entity

Add playoff configuration fields:

```csharp
public class Division : BaseEntity
{
	// Existing fields...
	public int SeasonId { get; set; }
	public AgeGroup AgeGroup { get; set; }
	public Gender Gender { get; set; }
	public int TotalRounds { get; set; }

	// NEW: Playoff Configuration
	public int PlayoffSpots { get; set; }  // Number of teams that qualify (default: 1)
	public int? MinVolunteerPointsForPlayoff { get; set; }  // Null = no requirement

	// Existing navigation properties...
	public Season Season { get; set; } = null!;
	public ICollection<Team> Teams { get; set; } = new List<Team>();
	public ICollection<Game> Games { get; set; } = new List<Game>();
}
```

**Migration Impact**: Requires database migration to add columns.

### 3.2. New Tournament Entity

```csharp
public enum TournamentType
{
	RoundRobin = 0,
	Knockout = 1,
	RoundRobinThenKnockout = 2  // 10U style
}

public enum TournamentRoundType
{
	GroupStage = 0,      // Round-robin groups
	RoundOf16 = 1,
	Quarterfinal = 2,
	Semifinal = 3,
	Final = 4,
	ThirdPlace = 5       // Optional 3rd/4th place game
}

public class Tournament : BaseEntity
{
	public int DivisionId { get; set; }
	public string Name { get; set; } = string.Empty;  // e.g., "Fall 2025 10U Boys Tournament"
	public TournamentType Type { get; set; }
	public DateTime StartDate { get; set; }
	public DateTime? EndDate { get; set; }
	public bool IsActive { get; set; }  // True during tournament, false after

	// Navigation
	public Division Division { get; set; } = null!;
	public ICollection<TournamentRound> Rounds { get; set; } = new List<TournamentRound>();
}
```

### 3.3. New TournamentRound Entity

```csharp
public class TournamentRound : BaseEntity
{
	public int TournamentId { get; set; }
	public TournamentRoundType RoundType { get; set; }
	public string Name { get; set; } = string.Empty;  // e.g., "Group A", "Semifinals"
	public int RoundNumber { get; set; }  // 1, 2, 3... for ordering
	public DateTime? ScheduledDate { get; set; }

	// For group-stage rounds
	public string? GroupName { get; set; }  // "Group A", "Group B", etc.

	// Navigation
	public Tournament Tournament { get; set; } = null!;
	public ICollection<TournamentGame> Games { get; set; } = new List<TournamentGame>();
}
```

### 3.4. New TournamentGame Entity

Option A: Separate entity entirely

```csharp
public class TournamentGame : BaseEntity
{
	public int TournamentRoundId { get; set; }
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public GameStatus Status { get; set; }

	// Tournament-specific
	public int? WinnerTeamId { get; set; }  // Set after game completes
	public bool WentToShootout { get; set; }  // For ties in knockout
	public string? NextGameReference { get; set; }  // "Winner to Game 15", etc.

	// Navigation
	public TournamentRound TournamentRound { get; set; } = null!;
	public Team HomeTeam { get; set; } = null!;
	public Team AwayTeam { get; set; } = null!;
	public Team? WinnerTeam { get; set; }
	public TournamentScore? Score { get; set; }
}
```

Option B: Extend existing Game entity

```csharp
public class Game : BaseEntity
{
	// Existing fields...
	public int? DivisionId { get; set; }  // Nullable now
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public DateTime ScheduledDateTime { get; set; }
	public int? Round { get; set; }  // Nullable for tournament games
	public string Location { get; set; } = string.Empty;
	public GameStatus Status { get; set; }

	// NEW: Tournament fields
	public int? TournamentRoundId { get; set; }
	public bool IsTournamentGame { get; set; }  // Flag to distinguish types

	// Navigation
	public Division? Division { get; set; }
	public TournamentRound? TournamentRound { get; set; }
	public Team HomeTeam { get; set; } = null!;
	public Team AwayTeam { get; set; } = null!;
	public Score? Score { get; set; }
}
```

**Recommendation**: **Option A** (separate TournamentGame entity)
- Cleaner separation of concerns
- Different score handling (shootouts)
- Tournament-specific fields don't pollute Game entity
- Easier to query regular season vs tournament games

### 3.5. New TournamentScore Entity

```csharp
public class TournamentScore : BaseEntity
{
	public int TournamentGameId { get; set; }
	public int HomeScore { get; set; }
	public int AwayScore { get; set; }

	// Shootout tracking (for knockout ties)
	public int? HomeShootoutScore { get; set; }
	public int? AwayShootoutScore { get; set; }

	// Navigation
	public TournamentGame TournamentGame { get; set; } = null!;
}
```

---

## 4. Updated StandingsResult DTOs

### Update TeamStanding

```csharp
public class TeamStanding
{
	// Existing fields...
	public int Rank { get; set; }
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public int GamesPlayed { get; set; }
	public int Wins { get; set; }
	public int Draws { get; set; }
	public int Losses { get; set; }
	public int GoalsFor { get; set; }
	public int GoalsAgainst { get; set; }
	public int GoalDifferential { get; set; }
	public int GamePoints { get; set; }
	public int VolunteerPoints { get; set; }
	public int TotalPoints { get; set; }
	public decimal PointsPerGame { get; set; }

	// NEW: Playoff qualification
	public bool QualifiesForPlayoffs { get; set; }
	public string? PlayoffQualificationNote { get; set; }  
	// e.g., "Needs 2 more volunteer points to qualify"
}
```

### Update StandingsService

Add methods:
```csharp
public interface IStandingsService
{
	// Existing methods...
	Task<StandingsResult> GetCurrentStandingsAsync(int divisionId);
	Task<StandingsResult> GetStandingsByRoundAsync(int divisionId, int throughRound);

	// NEW: Playoff-aware methods
	Task<StandingsResult> GetStandingsWithPlayoffIndicatorsAsync(int divisionId);
	Task<List<Team>> GetPlayoffQualifiedTeamsAsync(int divisionId);
}
```

---

## 5. New Services Needed

### 5.1. IPlayoffConfigurationService

```csharp
public interface IPlayoffConfigurationService
{
	// Get configuration
	Task<PlayoffConfiguration> GetConfigurationAsync(int divisionId);

	// Update configuration (admin)
	Task UpdateConfigurationAsync(int divisionId, PlayoffConfiguration config);

	// Determine qualifiers
	Task<List<Team>> GetQualifyingTeamsAsync(int divisionId);
	Task<bool> DoesTeamQualifyAsync(int teamId);
}

public class PlayoffConfiguration
{
	public int DivisionId { get; set; }
	public int PlayoffSpots { get; set; }
	public int? MinVolunteerPoints { get; set; }
}
```

### 5.2. ITournamentService

```csharp
public interface ITournamentService
{
	// Tournament CRUD
	Task<Tournament> CreateTournamentAsync(int divisionId, TournamentType type);
	Task<Tournament> GetTournamentAsync(int tournamentId);
	Task<List<Tournament>> GetTournamentsByDivisionAsync(int divisionId);

	// Tournament rounds
	Task<TournamentRound> CreateRoundAsync(int tournamentId, TournamentRoundType roundType);
	Task<List<TournamentRound>> GetRoundsAsync(int tournamentId);

	// Tournament games
	Task<TournamentGame> ScheduleGameAsync(int roundId, int homeTeamId, int awayTeamId);
	Task<List<TournamentGame>> GetGamesForRoundAsync(int roundId);

	// Bracket generation
	Task GenerateKnockoutBracketAsync(int tournamentId, List<Team> seededTeams);
	Task GenerateRoundRobinGroupsAsync(int tournamentId, List<Team> teams, int groupCount);
}
```

---

## 6. Database Migration Plan

### New Tables
1. **Tournaments** - Tournament metadata
2. **TournamentRounds** - Phases/rounds within tournament
3. **TournamentGames** - Games within tournament
4. **TournamentScores** - Scores including shootouts

### Updated Tables
1. **Divisions** - Add `PlayoffSpots`, `MinVolunteerPointsForPlayoff` columns

### Migration Script Outline
```sql
-- Add columns to Divisions
ALTER TABLE Divisions 
ADD PlayoffSpots INT NOT NULL DEFAULT 1,
	MinVolunteerPointsForPlayoff INT NULL;

-- Create Tournaments table
CREATE TABLE Tournaments (
	Id INT PRIMARY KEY IDENTITY,
	DivisionId INT NOT NULL,
	Name NVARCHAR(200) NOT NULL,
	Type INT NOT NULL,  -- TournamentType enum
	StartDate DATETIME2 NOT NULL,
	EndDate DATETIME2 NULL,
	IsActive BIT NOT NULL DEFAULT 1,
	-- BaseEntity audit fields...
	FOREIGN KEY (DivisionId) REFERENCES Divisions(Id)
);

-- Create TournamentRounds table...
-- Create TournamentGames table...
-- Create TournamentScores table...
```

---

## 7. UI Changes Needed

### 7.1. Standings Display

**Current**:
```
Rank | Team     | GP | W | D | L | GF | GA | GD | Pts
---------------------------------------------------------
1    | Team A   | 8  | 6 | 1 | 1 | 18 | 8  | 10 | 19
2    | Team B   | 8  | 5 | 2 | 1 | 15 | 7  | 8  | 17
```

**Updated with Playoff Indicator**:
```
Rank | Team     | GP | W | D | L | GF | GA | GD | VP | Pts | Status
------------------------------------------------------------------------
1    | Team A   | 8  | 6 | 1 | 1 | 18 | 8  | 10 | 10 | 29  | ✓ Qualified
2    | Team B   | 8  | 5 | 2 | 1 | 15 | 7  | 8  | 8  | 25  | ⚠ Needs 2 VP
3    | Team C   | 8  | 4 | 3 | 1 | 14 | 9  | 5  | 12 | 27  |
```

CSS classes:
- `.playoff-qualified` - Light green background
- `.playoff-conditional` - Light yellow background (10U teams near threshold)
- `.playoff-bubble` - No special styling (in contention but not currently qualifying)

### 7.2. Admin Configuration Page

**New Page**: `/Admin/PlayoffConfiguration`

Form fields per division:
- Number of playoff spots: [dropdown: 1, 2, 3, 4]
- Minimum volunteer points: [number input] (optional, for 10U)
- Save button

Grid view showing all divisions with current settings.

### 7.3. Tournament Management Pages

**New Section**: `/Admin/Tournaments`

- List tournaments for a season
- Create new tournament (wizard):
  1. Select division
  2. Choose type (round-robin, knockout, both)
  3. Select qualified teams or all teams
  4. Generate bracket/groups
- View tournament bracket/results
- Enter tournament game scores

---

## 8. Implementation Phases

### Phase 1: Playoff Configuration (IMMEDIATE - Before Step 19)
**Why now**: Affects Division entity and StandingsService  
**Tasks**:
1. ✅ Update Division entity (add playoff fields)
2. ✅ Create database migration
3. ✅ Update StandingsService to calculate playoff qualifiers
4. ✅ Update TeamStanding DTO with qualification flags
5. ✅ Add tests for playoff qualification logic
6. ⏳ Admin page for configuration (Step 21+)
7. ⏳ Update standings view with visual indicators (Step 21+)

**Estimated**: 2-3 hours

### Phase 2: Tournament Foundation (LATER - After Step 20)
**Why later**: Doesn't block current development  
**Tasks**:
1. Create Tournament, TournamentRound, TournamentGame entities
2. Create database migration
3. Create ITournamentService interface
4. Implement basic tournament CRUD
5. Add tests

**Estimated**: 4-5 hours

### Phase 3: Tournament Logic (MUCH LATER - After UI complete)
**Why last**: Complex, requires full UI, not urgent for regular season  
**Tasks**:
1. Bracket generation algorithms
2. Seeding logic from standings
3. Group-stage scheduling
4. Knockout advancement logic
5. Tournament views and editing

**Estimated**: 8-10 hours

---

## 9. Immediate Action Items (Before Continuing Step 19)

### To Implement NOW:

1. **Update Division Entity** ✅ (5 min)
   ```csharp
   public int PlayoffSpots { get; set; } = 1;
   public int? MinVolunteerPointsForPlayoff { get; set; }
   ```

2. **Create Migration** ✅ (5 min)
   ```bash
   dotnet ef migrations add AddPlayoffConfiguration
   ```

3. **Update TeamStanding DTO** ✅ (5 min)
   ```csharp
   public bool QualifiesForPlayoffs { get; set; }
   public string? PlayoffQualificationNote { get; set; }
   ```

4. **Update StandingsService** ✅ (30 min)
   - Add playoff qualification calculation
   - Check volunteer point threshold for 10U
   - Set flags in TeamStanding

5. **Add Tests** ✅ (30 min)
   - Test playoff qualification with volunteer point threshold
   - Test configurable playoff spots
   - Test edge cases (ties at playoff cutoff)

**Total Time**: ~90 minutes

### To Defer:

1. **Tournament entities** - After Step 20
2. **Tournament service** - After Step 20
3. **Admin configuration page** - Step 21+ (UI phase)
4. **Tournament bracket UI** - Much later

---

## 10. Updated Plan Document

The main plan document should be updated to include:

**New Steps** (insert after Step 27):
- Step 27a: Create admin playoff configuration page
- Step 27b: Update standings view with playoff indicators

**New Steps** (add at end, Steps 40-45):
- Step 40: Create Tournament entities and migrations
- Step 41: Implement Tournament service
- Step 42: Create tournament management pages
- Step 43: Implement bracket generation
- Step 44: Create tournament game entry pages
- Step 45: Tournament reporting and bracket display

---

## 11. Questions to Clarify

1. **10U Volunteer Point Threshold**: What's the typical minimum? (10 points? 15?)
   - This helps set reasonable defaults

2. **Playoff Expansion Mid-Season**: 
   - Should we log/audit when playoff spots change?
   - Email notifications to teams?

3. **Tournament Scoring**:
   - Do draws in knockout require shootouts or extra time?
   - How are shootouts scored for records? (still count as draw?)

4. **Tournament Seeding**:
   - Top-ranked teams get byes?
   - Random draw or strict seeding?

5. **Group Stage Groups**:
   - How many groups? (2, 3, 4?)
   - How many teams per group?

6. **Tournament Participation**:
   - All teams invited or only playoff qualifiers?
   - Can teams opt out?

---

## Summary

✅ **Recommend implementing Phase 1 NOW** (playoff configuration)  
⏳ **Defer Phase 2 and 3** (tournaments) until after basic UI is complete  

**Reason**: Playoff qualification affects the StandingsService we just built, so it's best to add that logic now while the code is fresh. Tournaments are a separate feature that doesn't interact much with current development.

**Next Steps**:
1. Review and approve this plan
2. Implement Phase 1 changes (90 min)
3. Continue with Step 19 (TeamService, GameService)

Ready to proceed?
