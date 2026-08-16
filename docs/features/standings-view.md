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

When teams finish with identical total standing points, the engine resolves rankings using the following ordered cascade. Tie-breaker sorting is deterministic down to the final fallback:

1.  **Total Points:** Sum of competitive and volunteer points (highest first).
2.  **Goal Differential:** Calculated as $\text{Goals For} - \text{Goals Against}$ (highest first).
3.  **Goals For:** Total goals scored in non-scrimmage games (highest first).
4.  **Alphabetical Ordering:** Sorts lexicographically ascending by **Team Name** (e.g., `'01 Jets'` ranks above `'02 Eagles'`).

---

## ⚖️ 3. Handling Odd Number of Teams (PPG Balancing)

When a division contains an odd number of teams, at least one team sits out each week on a regular rotation (a "bye"). This creates a games-played discrepancy.

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
