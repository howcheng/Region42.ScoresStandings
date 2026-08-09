using Region42.ScoresStandings.Application.Interfaces;

namespace Region42.ScoresStandings.Web.Models;

/// <summary>
/// View model for displaying standings with division and round filtering.
/// </summary>
public class StandingsViewModel
{
	public string SeasonName { get; set; } = string.Empty;
	public int DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public int ThroughRound { get; set; }
	public int TotalRounds { get; set; }
	public DateTime CalculatedAt { get; set; }
	public List<TeamStanding> Standings { get; set; } = new();
	public List<GameScoreDisplay> Scores { get; set; } = new();
}

/// <summary>
/// Represents a game score for display on standings page.
/// </summary>
public class GameScoreDisplay
{
	public int GameId { get; set; }
	public string HomeTeamName { get; set; } = string.Empty;
	public string AwayTeamName { get; set; } = string.Empty;
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public int Round { get; set; }
}
