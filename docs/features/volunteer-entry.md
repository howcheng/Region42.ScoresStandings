# 🤝 Volunteer Points Entry Specification

The **Volunteer Points Entry** module provides a streamlined, matrix-style bulk-entry interface allowing administrators to allocate auxiliary points to teams for completing system volunteer tasks (referee duties, field management, snack stand assistance, etc.). 

These points are added directly to game performance points to determine the comprehensive division rankings.

---

## 📅 1. Team × Round Grid Matrix Layout

The user interface handles volunteer points by rendering a highly optimized matrix layout:

*   **Row-axis:** Lists all active **Teams** within the selected Division (ordered alphabetically by name).
*   **Column-axis:** Lists each of the scheduling **Rounds/Weeks** (`1` through `TotalRounds` associated with that division).
*   **Intersection Cells:** Individual editable text inputs where administrators enter the points earned.

### Visual Representation of the Entry Matrix:

| Team Name | Round 1 | Round 2 | Round 3 | ... | Round N | Total |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **01 Jets** | `[ 1 ]` | `[ 0 ]` | `[ 2 ]` | ... | `[ 0 ]` | 3 pts |
| **02 Eagles** | `[ 0 ]` | `[ 1 ]` | `[ 1 ]` | ... | `[ 1 ]` | 3 pts |
| **03 Lions** | `[ 2 ]` | `[ 2 ]` | `[ 0 ]` | ... | `[ 1 ]` | 5 pts |

---

## 🚫 2. Rules & Business Constraints

The `VolunteerPointsService` applies strict validation rules when a bulk save is submitted:

1.  **Non-Negative Integrity:** All point entries must be greater than or equal to zero ($\ge 0$). Negative submissions are blocked in the field validation stage.
2.  **Support for Explicit Zeroes:** Entering `"0"` is fully supported and saved. If a team's earned points are manually reduced back down to zero (or if they run a week without earned volunteer slots), entering `0` overrides previous points, allowing backward adjustments.
3.  **Active Teams Only:** Volunteer points can only be recorded for teams that have their `IsActive` flag set to `true`. Inactive (soft-deleted) team slots are excluded from the grid.
4.  **Round Boundaries:** Points must map to a valid integer round increment starting at `1` up to the division's max rounds.

---

## 🔄 3. Integration with League Standings

The points entered on this grid directly influence the calculations of the `StandingsService`:

*   **Total Standing Points formula:**
	$$\text{Total Points} = \text{Game Points (Wins } \times 3 + \text{ Draws } \times 1) + \text{Accumulated Volunteer Points}$$
*   **Point-in-Time Support:** When calculating retrospective league standings "through Round X", the standings provider queries historical volunteer achievements with `Round <= X`, allowing users to see exactly what the standings looked like in any past week of the season.
*   **Storage Model:**
	*   Entity name: `VolunteerPoints` (maps to DB table `VolunteerPoints` with unique constraint on `TeamId` + `Round`).
	*   Saves contain optional `Notes` field for describing the underlying task (e.g. *"Referee duty U12 Boys - Game 4"*).
*   **Audit support:** Extends audit tracking timestamps (`CreatedAt`, `ModifiedAt`) and actor properties (`CreatedBy`, `ModifiedBy`) to track adjustments made by organizers.
