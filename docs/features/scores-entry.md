# 📝 Scores Entry & Scheduling Specification

The **Scores Entry** module provides a dynamic, unified grid interface for league administrators to input/update game scores, adjust scheduling pairings, and monitor audit trails.

---

## 🧭 1. Dynamic Cascading Reload Workflow

To make weekly scheduling and entry seamless for administrators, the view relies on sequential dependent dropdown selections:

```plaintext
[Select Division] ──(triggers)──> [Load Division Rounds] ──(triggers)──> [Load Weekly Game Grid]
```

1.  **Division Selection:** The administrator selects one of the active division levels (e.g. `10U Boys`, `12U Girls`).
2.  **Round Selection:** The application immediately populates the second dropdown with the valid round integers (`1` to `TotalRounds` defined for that division).
3.  **Grid Hydration:** Upon selecting a valid Round, the page reloads the game grid showing all scheduled matches for that division/week. The grid contains editable dropdowns for Home/Away team alignments, score text inputs, and audit markers of who last updated each fixture.

---

## 🚫 2. Strict Peer Score Validation Rules

To maintain correct point-of-time league standings calculations, the score entry engine enforces **Double Input completeness**:

*   **Partial Scores Prohibited:** For any game row, both the `HomeScore` and `AwayScore` must contain values, or both must be empty (indicating a future or pending match).
*   **Error Behavior:** Attempting to submit a single team's score (e.g., leaving the Home score blank while entering `3` for the Away score) fails validation. The dashboard blocks the update and flashes a detailed warning:
	> "Game [X]: Both home and away scores must be entered. A game is not complete until both scores are added."
*   **Logical Constraints:** Input scores must be non-negative integers ($\ge 0$). Negative scores are caught by both client-side constraints and server-level domain logic.

---

## 🔀 3. Live Pairings Modification & Team Uniqueness

Alongside scoring, administrators can modify team pairings for games. The application runs strict schedule-integrity rules upon grid submission:

1.  **Team Uniqueness per Round:** No team can be assigned to multiple games within the same round/week. If any team’s identifier appears more than once on the grid, the transaction is canceled with a detailed notice:
	> "The following team(s) appear more than once in this round: [TeamName]. Each team can only have one game per round."
2.  **Self-Match Protection:** A team cannot be scheduled to play against itself (`HomeTeamId != AwayTeamId`). This triggers an immediate cancellation:
	> "A team cannot play against itself. Please check the schedule."

---

## 🕵️‍♂️ 4. Audit Tracking & Retroactive Corrections

Because historical score corrections can fluctuate due to referee reports, the database includes a persistent logging mechanism:

*   **Audit Heritage:** The `Score` entity inherits from `BaseEntity`, recording metadata:
	*   `CreatedAt` & `CreatedBy` (username retrieved from Google OAuth claims)
	*   `ModifiedAt` & `ModifiedBy` (records who submitted subsequent adjustments)
*   **RowVersion Concurrency:** Implements optimistic locking via an integer `RowVersion` column. If two administrators attempt to submit scores for the same game simultaneously, the first write succeeds and the second triggers a DbUpdateConcurrencyException, preventing double-write corruption.
