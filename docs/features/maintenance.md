# ⚙️ Season, Division & Teams Maintenance Specification

This document details the configuration lifecycles, cookies, and protection behaviors built into the management of Seasons, Divisions, and Teams.

---

## 📅 1. Season Lifecycle & Automatic Start Logic

The `Season` entity acts as the parent boundary for all league divisions, schedules, and standings.

### Key Characteristics:
*   **Properties:** `Id`, `Name`, `Year`, `IsActive`, `CustomMessage`, and relationships to `Divisions`.
*   **Automatic Start Date:** To remain perfectly aligned with Southern California soccer leagues, seasons start on a computed property date of **August 1st**:
	```csharp
	public DateTime StartDate => new DateTime(Year, 8, 1);
	```
*   **Active Status:** Only one Season can be active at a time. Activating a season automatically toggles other seasons to inactive status.

---

## 🍪 2. Division Preference Cookie Lifecycle

To keep the fan view highly user-friendly, the site remembers the user's last selected division utilizing a client-side HTTP cookie:

*   **Cookie Name:** `PreferredDivisionId`
*   **Storage Priority during Load:**
	$$\text{Selected Division} = \text{URL Parameter (QueryString)} \rightarrow \text{Cookie Preference} \rightarrow \text{First Division (Alphabetical Fallback)}$$
*   **Dynamic Expiration Calculation:** To avoid cookie rot while preserving the user's focus over the course of standard yearly league cycles, the expiration dates are dynamically anchored around the two main administrative markers:
	*   **July 31st (23:59:59):** Triggers before the new autumn season registrations start.
	*   **December 31st (23:59:59):** Triggers after the current autumn season and subsequent post-season playoffs wrap up.
	*   **Formula:** Calculated as `July 31` or `December 31` of the current year—whichever is **later** than the current timestamp.

---

## 🛡️ 3. Team Administration & Soft-Delete Safeguards

The `Team` entity maps competing groups (such as `10UB02 Tornadoes` or `14UG01 Hawks`) to parent divisions. The CRUD engine includes safeguards:

### A. Team Name Uniqueness
A Team cannot be saved with a duplicate name within the same Division (`t.Name.ToLower() == teamName.ToLower()`). This uniqueness check is case-insensitive and filters out inactive/soft-deleted records.

### B. Rigid Soft-Delete Safe Bars
Instead of deleting database rows (which would cascade and corrupt historical results), teams are soft-deleted by setting `IsActive = false`. 

To prevent breaking historical integrity, the `TeamService` runs an explicit check:
*   **The Guard Rail:** If a team is linked to *any* scheduled matches in the database (either as home or away), deactivation is **blocked**:
	```csharp
	var hasGames = (await _gameRepository.FindAsync(g => 
		g.HomeTeamId == teamId || g.AwayTeamId == teamId)).Any();
	```
*   **Error Raised:** The application throws an exception that reports:
	> "Cannot deactivate team '[TeamName]' because it has associated games. Teams with game history should remain active for historical records."
	This prevents broken references and preserves the reliability of point-of-time historical standings reports.
