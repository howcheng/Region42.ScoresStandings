namespace Region42.ScoresStandings.Application.DTOs;

/// <summary>
/// DTO for volunteer points entry.
/// </summary>
public class VolunteerPointsEntryDto
{
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public int Round { get; set; }
	public int Points { get; set; }
	public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// DTO for bulk volunteer points update.
/// Used for grid entry (all teams × all rounds).
/// </summary>
public class VolunteerPointsBulkUpdateDto
{
	public int DivisionId { get; set; }
	public List<VolunteerPointsEntryDto> Entries { get; set; } = new();
}
