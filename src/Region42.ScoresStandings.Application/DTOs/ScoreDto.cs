namespace Region42.ScoresStandings.Application.DTOs;

/// <summary>
/// DTO for score entry and display.
/// </summary>
public class ScoreEntryDto
{
	public int GameId { get; set; }
	public int HomeTeamId { get; set; }
	public string HomeTeamName { get; set; } = string.Empty;
	public int AwayTeamId { get; set; }
	public string AwayTeamName { get; set; } = string.Empty;
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public int Round { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
	public DateTime? LastModified { get; set; }
	public string? LastModifiedBy { get; set; }
}

/// <summary>
/// DTO for updating a score.
/// </summary>
public class ScoreUpdateDto
{
	public int GameId { get; set; }
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
}
