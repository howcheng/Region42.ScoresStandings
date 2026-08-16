namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for calculating standings with support for point-in-time queries.
/// Implements standard soccer scoring (Win=3pts, Draw=1pt, Loss=0pts) plus volunteer points.
/// Handles divisions with odd number of teams (adjusted for games played).
/// </summary>
public interface IStandingsService
{
	/// <summary>
	/// Calculates current standings for a division through the latest completed round.
	/// </summary>
	Task<StandingsResult> GetCurrentStandingsAsync(int divisionId);

	/// <summary>
	/// Calculates point-in-time standings for a division through a specific round.
	/// </summary>
	Task<StandingsResult> GetStandingsByRoundAsync(int divisionId, int throughRound);

	/// <summary>
	/// Calculates standings for all divisions in a season.
	/// </summary>
	Task<IEnumerable<StandingsResult>> GetStandingsBySeasonAsync(int seasonId);

	/// <summary>
	/// Recalculates standings after a score correction.
	/// </summary>
	Task<StandingsResult> RecalculateStandingsAsync(int divisionId);
}

/// <summary>
/// Result object containing calculated standings for a division.
/// </summary>
public class StandingsResult
{
	public int DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public int ThroughRound { get; set; }
	public DateTime CalculatedAt { get; set; }
	public List<TeamStanding> Standings { get; set; } = new();

	/// <summary>
	/// Number of leading rounds configured as scrimmages for this division (do not count toward standings).
	/// </summary>
	public int ScrimmageRounds { get; set; }

	/// <summary>
	/// True if the range of rounds being displayed includes at least one scrimmage round.
	/// </summary>
	public bool IncludesScrimmageRounds => ScrimmageRoundsInRange > 0;

	/// <summary>
	/// Number of scrimmage rounds included within the currently displayed range.
	/// </summary>
	public int ScrimmageRoundsInRange { get; set; }
}

/// <summary>
/// Individual team standing within a division.
/// </summary>
public class TeamStanding
{
	public int Rank { get; set; }
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public string TeamShortName { get; set; } = string.Empty;
	public int GamesPlayed { get; set; }
	public int Wins { get; set; }
	public int Draws { get; set; }
	public int Losses { get; set; }
	public int GoalsFor { get; set; }
	public int GoalsAgainst { get; set; }
	public int GoalDifferential { get; set; }
	public int GamePoints { get; set; }  // Win=3, Draw=1, Loss=0
	public int VolunteerPoints { get; set; }
	public int TotalPoints { get; set; }  // GamePoints + VolunteerPoints

	// Adjusted points per game for divisions with odd teams
	public decimal PointsPerGame { get; set; }

	// Playoff qualification
	public bool QualifiesForPlayoffs { get; set; }
	public string? PlayoffQualificationNote { get; set; }  
	// e.g., "Needs 2 more volunteer points to qualify" or "Clinched playoff spot"
}
