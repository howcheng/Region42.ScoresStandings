# 🏆 End-of-Season Tournaments Specification

The **End-of-Season Tournament Engine** (`TournamentService`, `ITournamentBracketBuilderFactory`, and `PoolPlayStandingsCalculator`) manages Round 10 (`TotalRounds`) tournaments for divisions configured with a tournament format. Tournament games are kept separate from regular-season statistics while awarding a playoff berth to the tournament champion (subject to volunteer point requirements).

---

## ⚙️ 1. Division Configuration & Tournament Formats

Each `Division` specifies an `EndOfSeasonTournamentFormat` property of type `TournamentFormatType`:

*   **`None` (`0`):** Standard regular season across all rounds (default for **10U** divisions).
*   **`Bracket` (`1`):** Single-elimination bracket tournament seeded from Round 9 (`TotalRounds - 1`) standings (default for **12U** divisions).
*   **`PoolPlayWithFinal` (`2`):** Round-robin pool play (3 or 4 teams) scored using the **AYSO 10-Point System**, followed by a Championship Final between the top two pool finishers (default for **14U** divisions).

Administrators can view and change a division's tournament format at any time via **Manage Divisions → Edit Division** (`DivisionsController`). When importing a schedule from CSV (`CsvImportService`), regular-season games falling in Round 10 are automatically skipped for divisions whose tournament format is not `None`.

---

## 🧱 2. Domain Model & Polymorphic Persistence

To avoid polluting regular-season `Game` and `Score` entities with tournament-only fields, tournament matches and scores use a polymorphic class hierarchy stored within the division's JSON document (`DivisionDataDocument`):

*   **`Game`** *(regular-season game)*
	*   **`TournamentGame : Game`** — Adds `Stage` (`TournamentStage`), `HomeTeamPlaceholder`, and `AwayTeamPlaceholder`.
		*   **`PoolPlayGame : TournamentGame`** — Represents a round-robin pool-play match (`PoolPlay1`–`PoolPlay6`); exposes `PoolPlayScore`.
		*   **`EliminationGame : TournamentGame`** — Represents a single-elimination match (`Quarterfinal1`–`Quarterfinal4`, `Semifinal1`–`Semifinal2`, `Final`); exposes `EliminationScore` and `TryGetWinnerTeamId()`.
*   **`Score`** *(regular-season score)*
	*   **`TournamentScore : Score`** — Adds `IsForfeit`.
		*   **`PoolPlayScore : TournamentScore`** — Adds `HomeRedCards`, `AwayRedCards`, `HomeYellowCards`, and `AwayYellowCards`.
		*   **`EliminationScore : TournamentScore`** — Adds `IsDecidedByPenalties`, `HomePenaltyScore`, and `AwayPenaltyScore`.

---

## 🗓️ 3. Seeding, Auto-Generation & On-Demand Regeneration

1.  **Seeding Lock:** Tournament seedings are determined from the regular-season standings through Round `TotalRounds - 1` (Round 9).
2.  **Automatic Generation:** Once Round 9 is complete (at least one completed regular-season game in Round 9 and no remaining `Scheduled` or `Unscheduled` games in Rounds `1..9`), `TournamentService.EnsureTournamentGamesAsync` automatically creates the Round 10 tournament fixtures with `GameStatus.Unscheduled`.
3.  **Automatic Re-Seeding Before Kickoff:** If a Round 1–9 score or referee point entry is corrected *before* any Round 10 tournament game has a score entered, `StandingsRefreshService` automatically updates the Round 10 initial pairings to match the updated Round 9 standings while preserving any dates, times, or locations already assigned to those games.
4.  **Manual Generate / Re-Seed:** On the Admin **Score Entry** page (when Round 10 is selected for a tournament division), admins can click **Generate Tournament Games** (to create the bracket early on demand) or **Re-seed / Regenerate Tournament** (with a confirmation prompt if tournament scores have already been entered).
5.  **Scheduling Unscheduled Games:** Auto-generated tournament games begin in `Unscheduled` status. In **Score Entry**, admins can enter a date/time and location directly on an `Unscheduled` game to schedule it in place without creating a separate makeup game.

---

## 🥇 4. Single-Elimination Bracket Format (`Bracket`)

Bracket construction and progression are delegated through `ITournamentBracketBuilderFactory` to an `ITournamentBracketBuilder` implementation based on the number of active Region 42 teams in the division (designed for 3 to 8 teams).

### 6-Team Bracket (`SixTeamBracketBuilder`)
*   **First-Round Byes:** **1st Seed** and **2nd Seed** receive a bye into the Semifinals.
*   **Quarterfinal 1 (`Quarterfinal1`):** **4th Seed** (Home) vs. **5th Seed** (Away).
*   **Quarterfinal 2 (`Quarterfinal2`):** **3rd Seed** (Home) vs. **6th Seed** (Away).
*   **Semifinal 1 (`Semifinal1`):** **1st Seed** (Home) vs. **Winner of Quarterfinal 1** (Away).
*   **Semifinal 2 (`Semifinal2`):** **2nd Seed** (Home) vs. **Winner of Quarterfinal 2** (Away).
*   **Championship Final (`Final`):** **Winner of Semifinal 1** (Home) vs. **Winner of Semifinal 2** (Away).

### Elimination Match Rules
*   Elimination matches cannot end in a tie.
*   **Penalty Shootouts:** If regulation ends in a draw (`HomeScore == AwayScore`), the admin checks **PK Shootout** (`IsDecidedByPenalties`) and enters non-equal `HomePenaltyScore` and `AwayPenaltyScore` values.
*   **Forfeits:** Checking **Forfeit** (`IsForfeit`) requires a `1-0` or `0-1` score and disables penalty shootout inputs.
*   As scores are saved, `SyncTournamentProgressionAsync` automatically populates the advancing winners into the next round's matchups.

> **TODO:** Implement `ITournamentBracketBuilder` classes for 3-, 4-, 5-, 7-, and 8-team divisions in future seasons.

---

## ⚽ 5. Pool Play with Final Format (`PoolPlayWithFinal`)

Used for divisions with **3 or 4 active teams** (`PoolPlayStandingsCalculator`):
*   **3-Team Pool:** Generates 3 round-robin games (`1v2`, `2v3`, `1v3`) plus the `Final`.
*   **4-Team Pool:** Generates 6 round-robin games (`1v4`, `2v3`, `1v3`, `2v4`, `1v2`, `3v4`) plus the `Final`.
*   Once all pool-play matches are completed, the top 2 teams in the pool standings automatically populate into the `Final` (`Pool 1st Place` vs. `Pool 2nd Place`).

### AYSO 10-Point Scoring System (Pool Play Only)
Unlike regular-season games, pool-play standings do **not** include referee/volunteer points and are scored on the standard AYSO tournament **10-point system**:

| Outcome / Event | Points Awarded / Deducted |
| :--- | :--- |
| **Win** | `+6` points |
| **Tie (Draw)** | `+3` points |
| **Loss** | `0` points |
| **Goals Scored** | `+1` point per goal scored, up to a maximum of **`3` points** per game (win, lose, or draw) |
| **Earned Shutout** | `+1` point when `GoalsAgainst == 0` (including a `0-0` draw, where both teams earn `3 + 1 = 4` points) |
| **Forfeit Win** | **`7` points** total (scored as a `1-0` win: `6` for Win + `1` for Goal; **no shutout point** as it is not an earned shutout) |
| **Red Card** | `-2` points per red card issued to a player, substitute, or coach |
| **Yellow Card** | `-1` point per yellow card issued to a player, substitute, or coach |

### Pool Play Tie-Breaker Cascade
When teams finish pool play tied on `PoolPoints`, rankings are resolved in the following order:

1.  **Head-to-Head Competition:** Result of the pool-play game between the tied teams *(applied **only** in a 2-way tie; omitted in 3-way or 4-way ties)*.
2.  **Most Number of Wins:** Total pool-play wins *(omitted if **any** forfeit game occurred anywhere in the pool)*.
3.  **Fewest Red Cards:** Lowest total red cards in pool play.
4.  **Fewest Yellow Cards:** Lowest total yellow cards in pool play.
5.  **Fewest Goals Allowed (Capped):** Lowest sum of goals allowed in pool play, capped at a maximum of **`5` goals per game**.
6.  **Goal Differential (Capped):** Highest sum of $(\min(\text{GoalsFor}, 3) - \text{GoalsAgainst})$ across pool games *(omitted if **any** forfeit game occurred anywhere in the pool)*.
7.  **FIFA Kicks from the Penalty Mark:** If teams remain tied after all statistical tiebreakers and the tie affects advancement to (or seeding in) the Final, an alert and **Pool Play FIFA Kicks Winner** selector (`PoolPlayTiebreakerWinnerTeamId`) appear on the Admin Score Entry page so the administrator can record the shootout winner.

---

## 🎟️ 6. Standings Isolation, Playoff Berths & Final Standings

*   **Regular-Season Stat Isolation:** `TournamentGame` scores are excluded from `StandingsCalculator` match statistics—they never alter regular-season Games Played, Wins, Draws, Losses, Goals For/Against, or Game Points.
*   **Round 10 Referee Points:** Referee/volunteer points earned during Round 10 **are** included in the Round 10 standings, meaning a team can still meet the minimum referee point threshold or move up in the final standings table during tournament week.
*   **Playoff Berth Allocation & Cascading:**
	*   All playoff qualifiers must meet `VolunteerPoints >= MinVolunteerPointsForPlayoff`.
	*   When `PlayoffSpots == 1`, the top eligible team in the final standings table qualifies.
	*   When `PlayoffSpots >= 2` (`N`), the top `N - 1` eligible teams in the final standings qualify via standings rank, and the **Tournament Champion** earns the remaining berth (if eligible).
	*   If the Tournament Champion is already among the top `N - 1` eligible teams in the standings (for example, the #1 ranked team also wins the tournament) or does not meet `MinVolunteerPointsForPlayoff`, the berth cascades to the next highest-ranked eligible team in the final standings table.
*   **Public UI Display (`Home/Standings`):**
	*   When viewing Round 10 for a tournament division, the left column replaces the standard Round Scores list with the **End-of-Season Tournament Bracket** (showing first-round byes, Quarterfinals, Semifinals, and Final) or **End-of-Season Tournament (Pool Play & Final)** card (showing the 10-point Pool Play Standings table, pool games, and Final).
	*   Once the Championship Final is completed, the right-hand card header updates to **`{Division} Final Standings`**, displays a trophy badge next to the Tournament Champion, and highlights all playoff-qualifying teams.
