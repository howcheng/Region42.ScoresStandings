namespace Region42.ScoresStandings.Application.DTOs;

/// <summary>
/// DTO for creating or updating a team.
/// </summary>
public class TeamDto
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int DivisionId { get; set; }
	public string ContactName { get; set; } = string.Empty;
	public string ContactEmail { get; set; } = string.Empty;
	public string ContactPhone { get; set; } = string.Empty;
	public bool IsActive { get; set; }
}

/// <summary>
/// DTO for displaying team information with division details.
/// </summary>
public class TeamDisplayDto
{
	public int Id { get; set; }
	public string Name { get; set; } = string.Empty;
	public int DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public string ContactName { get; set; } = string.Empty;
	public string ContactEmail { get; set; } = string.Empty;
	public string ContactPhone { get; set; } = string.Empty;
	public bool IsActive { get; set; }
	public int GamesPlayed { get; set; }
}
