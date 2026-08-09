using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.DTOs;

/// <summary>
/// DTO for creating or updating a game.
/// </summary>
public class GameDto
{
	public int Id { get; set; }
	public int DivisionId { get; set; }
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public DateTime ScheduledDateTime { get; set; }
	public int Round { get; set; }
	public string Location { get; set; } = string.Empty;
	public GameStatus Status { get; set; }
}

/// <summary>
/// DTO for displaying game information with team names.
/// </summary>
public class GameDisplayDto
{
	public int Id { get; set; }
	public int DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public int HomeTeamId { get; set; }
	public string HomeTeamName { get; set; } = string.Empty;
	public int AwayTeamId { get; set; }
	public string AwayTeamName { get; set; } = string.Empty;
	public DateTime ScheduledDateTime { get; set; }
	public int Round { get; set; }
	public string Location { get; set; } = string.Empty;
	public GameStatus Status { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
}
