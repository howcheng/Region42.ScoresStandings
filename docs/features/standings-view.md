# 🏆 Standings Calculation & Playoff Qualification Specification

The **Standings Calculation Engine** (`StandingsService`) computes real-time ranking matrices for divisions. It supports historical point-in-time standings and incorporates volunteer contributions alongside core competitive statistics.

---

## ⚽ 1. Core Match Performance Points

Standings calculations are resolved on standard AYSO and competitive criteria:

*   **Result Points:**
	*   **Win:** `3` points
	*   **Draw:** `1` point
	*   **Loss:** `0` points
*   **Total Standing Points Formula:**
	$$\text{Total Points} = \text{Game Points} + \text{Accumulated Volunteer Points}$$
*   **Scrimmage Rounds Isolation:**
	Games played during designated scrimmage rounds (`Round <= ScrimmageRounds` defined on the division) are strictly excluded from standings calculations. They do not accumulate wins, losses, draws, goals for/against, or game points.

---

## 📋 2. Comprehensive Tie-Breaker Hierarchy

Total standing points ties are resolved using the following ordered cascade:

1.  **Total Points:** Sum of competitive and volunteer points (highest first).
2.  **Game Points:** Total points earned from regular-season match play (highest first, rewarding actual game play).
3.  **Head-to-Head Results:** Points earned in completed non-scrimmage regular-season games among the tied teams (highest first).
4.  **Goal Differential:** Calculated as $\text{Goals For} - \text{Goals Against}$ (highest first).
5.  **Goals For:** Total goals scored in non-scrimmage games (highest first).
6.  **Alphabetical Ordering:** Sorts lexicographically ascending by **Team Name** (e.g., `'01 Jets'` ranks above `'02 Eagles'`).

> **TODO:** If teams remain tied after head-to-head results when determining cascading playoff berths, add a manual admin override input to designate the tiebreaker winner.

---

## ⚖️ 3. Handling Odd Number of Teams (PPG Balancing)

When a division contains an odd number of teams, at least one team sits out each week on a regular rotation (a "bye"). This creates a games-played discrepancy. This may also happen when games are cancelled.

*   **Points-Per-Game (PPG) Trigger:** If the engine detects that active teams in the division have completed an unequal number of games, it automatically calculates and exposes a `PointsPerGame` value for each team:
	$$\text{Points Per Game (PPG)} = \frac{\text{Total Points}}{\text{Games Played}} \quad (\text{Rounded to 2 decimal places})$$
*   *Note: PPG serves as the clarifying indicator for ranks on the mobile and desktop views when schedules are unbalanced.*

---

## 🎟️ 4. Playoff Spot Designation & Volunteer Thresholds

Post-season playoff qualifications require meeting both competitive and league involvement criteria:

### The Two-Gate Validation model:
1.  **Competitive Rank Gate:** Team rank must be within the division's allocated **Playoff Spots** (e.g., top 4 teams).
2.  **Volunteer Point Gate:** Team must have accumulated volunteer points $\ge$ **`MinVolunteerPointsForPlayoff`** (defined in global `Settings`).

### Qualification Outcomes & Interface Status Notes:

Depending on which gates are satisfied, the system appends precise status guidelines for display in the standing grids:

| Rank Check | Volunteer Points Check | Status Output Notes |
| :--- | :--- | :--- |
| **Within Spots** ($\le$ Playoff Spots) | **Threshold Met** ($\ge$ Min Points) | `"Clinched playoff spot"` |
| **Within Spots** ($\le$ Playoff Spots) | **Below Threshold** ($<$ Min Points) | `"Needs X more volunteer point(s) to qualify"` |
| **Outside Spots** ($>$ Playoff Spots) | **Threshold Met** ($\ge$ Min Points) | `"Eliminated from playoffs"` |
| **Outside Spots** ($>$ Playoff Spots) | **Below Threshold** ($<$ Min Points) | `"Needs X more volunteer point(s) and must improve standing"` |
---

## 5. End-of-season tournaments

Divisions configure their tournament format via `EndOfSeasonTournamentFormat` (`TournamentFormatType`):
*   **`None`:** Used for 10U divisions. Round 10 is a standard regular-season round.
*   **`Bracket`:** Used for 12U divisions. Single-elimination bracket seeded from Round 9 standings via `ITournamentBracketBuilderFactory` / `ITournamentBracketBuilder` (currently implemented for 6-team divisions via `SixTeamBracketBuilder`: 1st and 2nd seeds receive a first-round bye; QF1 is 4th vs 5th playing 1st; QF2 is 3rd vs 6th playing 2nd; winners meet in the Final). Elimination matches support forfeits (`1-0` / `0-1`) and penalty shootout scores (`HomePenaltyScore` / `AwayPenaltyScore`) when regulation ends in a draw.
*   **`PoolPlayWithFinal`:** Used for 14U divisions (supports 3-team or 4-team pools). Teams play a round-robin pool stage scored on the **AYSO 10-Point System** (Win = 6, Tie = 3, Loss = 0, +1 per goal up to 3, +1 earned shutout, Forfeit = 7 [1-0 win, no shutout], Red Card = -2, Yellow Card = -1; volunteer points are excluded), with the top 2 pool finishers advancing to the Championship Final.
    *   **Pool Play Tie-Breakers:** (1) Head-to-head (2-way ties only; omitted in 3-way or 4-way ties), (2) Most wins (omitted if any forfeit game exists in the pool), (3) Fewest red cards, then fewest yellow cards, (4) Fewest goals allowed (capped at 5 per game), (5) Goal differential (goals scored capped at 3 per game less total goals allowed; omitted if any forfeit game exists in the pool), (6) FIFA kicks from the penalty mark (admin selector override).

Tournament game scores are kept separate from regular-season statistics and do not affect the regular-season standings table, while Round 10 referee points still count toward final standings. In divisions with tournaments, when there is only one playoff spot available, the top eligible team in the standings gets it. When there are 2 or more playoff spots (`N`), the top `N - 1` eligible teams in the standings qualify, and the tournament champion earns the remaining playoff berth (provided they meet `MinVolunteerPointsForPlayoff`). If the tournament champion is already among the top `N - 1` eligible teams (e.g., the #1 ranked team wins the tournament) or lacks the minimum volunteer points, the berth cascades to the next highest-ranked eligible team in the standings.

> **TODO:** Implement `ITournamentBracketBuilder` classes for 3-, 4-, 5-, 7-, and 8-team divisions in future seasons.
> **TODO:** Add an admin manual override input for regular-season cascading playoff berths in the event teams remain tied after head-to-head results.
