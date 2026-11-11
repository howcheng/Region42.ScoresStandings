# 📝 Scores Entry & Scheduling Specification

The **Scores Entry** module (`ScoresController`, `ScoreService`, `GameService`, `Entry.cshtml`, and `scores-entry.js`) provides a unified grid interface for league administrators to input/update game scores, make in-place schedule/location corrections, manage weather reschedules, run end-of-season tournaments, and monitor audit trails.

---

## 🧭 1. Dynamic Cascading Reload Workflow

To make weekly scheduling and entry seamless for administrators, the view relies on sequential dependent dropdown selections:

```plaintext
[Select Division] ──(triggers)──> [Load Division Rounds] ──(triggers)──> [Load Weekly Game Grid]
```

1.  **Division Selection:** The administrator selects one of the active division levels (e.g. `10U Boys`, `12U Girls`).
2.  **Round Selection:** The application immediately populates the second dropdown with the valid round integers (`1` to `TotalRounds` defined for that division). When a division has an end-of-season tournament configured (`EndOfSeasonTournamentFormat != None`), the final round is labeled `Round {N} (Tournament)`.
3.  **Grid Hydration:** Upon selecting a valid Round, the page reloads the game grid showing all matches for that division/week. The grid contains editable dropdowns for Home/Away team alignments, score inputs, tournament-specific controls (forfeits, yellow/red cards, penalty shootouts), status/schedule controls, and audit markers of who last updated each fixture.

---

## 🚫 2. Score Entry, Clearing & Validation Rules

To maintain accurate point-in-time league standings and tournament progressions, the score entry engine enforces strict client-side (`scores-entry.js`) and server-side (`ScoresController` / `ScoreService`) validation:

*   **Partial Scores Prohibited:** For any game row, both `HomeScore` and `AwayScore` must contain values, or both must be empty (indicating a future/pending match). Attempting to submit only one team's score fails validation:
	> "Game [X]: Both home and away scores must be entered. A game is not complete until both scores are added."
*   **Clearing Scores:** Leaving both `HomeScore` and `AwayScore` empty on a game that previously had a score deletes the existing score (`DeleteScoreAsync`). If the game's status was `Completed`, it reverts to `Scheduled` (or `Unscheduled` if no date/time is set).
*   **Non-Negative Constraints:** Scores (and card/penalty counts) must be non-negative integers ($\ge 0$).
*   **Tournament Score Rules** *(see [End-of-Season Tournaments Specification](tournaments.md) for full details)*:
	*   **Forfeits (`TournamentScore.IsForfeit`):** Forfeit games must be scored as `1-0` or `0-1`.
	*   **Pool Play Cards (`PoolPlayScore`):** Pool-play games include `HomeYellowCards`, `HomeRedCards`, `AwayYellowCards`, and `AwayRedCards` inputs used in the AYSO 10-point calculation.
	*   **Elimination Penalty Shootouts (`EliminationScore`):** Elimination games that end tied in regulation require **PK Shootout** (`IsDecidedByPenalties`) and non-equal `HomePenaltyScore` and `AwayPenaltyScore` values.

---

## 🗓️ 3. In-Place Schedule Editing vs. Rescheduling

Administrators can adjust game dates, times, and field locations directly from the Score Entry grid:

1.  **Always-Available Schedule Edit Toggle:**
	*   Every game row includes an edit toggle button (<i class="bi bi-pencil-square"></i>) next to the **Status** dropdown that expands/collapses the `Date/Time` and `Location` inputs for that game.
	*   Games with status `Unscheduled` or `Rescheduled` automatically expand these inputs by default.
2.  **In-Place Schedule & Location Corrections (`Scheduled`, `Completed`, `Unscheduled`, `Cancelled`):**
	*   For any status other than `Rescheduled`, editing `Date/Time` or `Location` updates the game directly in place without creating a makeup game.
	*   This allows administrators to fix a date, time, or field location error at any point—even after a game is marked `Completed`.
	*   **Scheduling an Unscheduled Game:** Providing a `Date/Time` on an `Unscheduled` game schedules it in place and automatically transitions its status from `Unscheduled` to `Scheduled`.
	*   **Reverting to Unscheduled:** Changing a scheduled game's status back to `Unscheduled` (without entering a new date/time) clears its `ScheduledDateTime`.
3.  **True Reschedules (`Status == Rescheduled`):**
	*   When a game is postponed (e.g., due to weather or field closures), the administrator sets **Status** to `Rescheduled` and enters the new `Date/Time`, `Location`, and optional `New Round`.
	*   The original game retains its original round, date/time, and location with `Status = Rescheduled`, and the system creates (or updates) a linked makeup `Game` (`RescheduledToGameId` / `RescheduledFromGameId`) in the target round.
	*   If `New Round` is left blank, the target round is inferred automatically from the new date's week (`GameRoundCalculator`), or admins can specify `TotalRounds + 1` to place the game in a makeup round.
	*   If a `Rescheduled` game is later changed back to another status before its makeup game is scored, the unplayed makeup game is automatically deleted.

---

## 🔀 4. Live Pairings Modification & Team Uniqueness

Alongside scoring and scheduling, administrators can modify team pairings for games. The application runs schedule-integrity checks upon grid submission:

1.  **Team Uniqueness per Round:** In regular-season play, no team can be assigned to multiple games within the same round/week:
	> "The following team(s) would appear more than once in round [N]: [TeamName]. Each team can only have one game per round."
	*   *Exemptions:* Games with `Status == Rescheduled`, makeup games (`RescheduledFromGameId != null`), and `TournamentGame` fixtures (where teams play multiple pool or bracket matches in the tournament round) are exempt from the one-game-per-round constraint.
2.  **Self-Match Protection:** A team cannot be scheduled to play against itself (`HomeTeamId != AwayTeamId`, except unassigned `0` / `TBD` placeholders on tournament games):
	> "A team cannot play against itself. Please check the schedule."

---

## 💾 5. Batch Persistence, Audit Tracking & Storage Concurrency

Because the application persists division data as JSON documents in Google Cloud Storage (`{year}/{slug}/{divisionKey}.json`), the score entry pipeline is designed to minimize storage writes and avoid GCS per-object rate limits (`429 TooManyRequests`):

*   **Change Detection (`HasScoreChanged`):** Submitting the Score Entry grid only invokes `ScoreService` for rows where a score (or forfeit/card/PK value) was newly entered, modified, or cleared. Unchanged rows are skipped so their `ModifiedAt` / `ModifiedBy` audit timestamps are preserved.
*   **Single Batch Save per Request:** All game updates, makeup game creations/deletions, and score updates within `POST /Scores/Entry` execute in memory (`saveChanges: false`), followed by a single tournament progression sync (`SyncTournamentProgressionAsync`), a single standings recalculation (`RefreshDivisionStandingsAsync`), and **one** `SaveChangesAsync()` call at the end of the request.
*   **Audit Heritage:** `Game` and `Score` entities inherit from `BaseEntity`, recording `CreatedAt`, `CreatedBy`, `ModifiedAt`, `ModifiedBy` (from Google OAuth claims), and an incrementing `RowVersion`.
*   **GCS Concurrency & Rate-Limit Resilience:** `GcsCompetitionDataStore` uses GCS object-generation preconditions (`IfGenerationMatch`) to retry concurrent writes (`412 PreconditionFailed`) and applies backoff retries if GCS returns `429 TooManyRequests`.

