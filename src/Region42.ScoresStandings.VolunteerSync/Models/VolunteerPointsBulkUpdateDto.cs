namespace Region42.ScoresStandings.VolunteerSync.Models;

public class VolunteerPointsBulkUpdateDto
{
	public int DivisionId { get; set; }
	public List<VolunteerPointsEntryDto> Entries { get; set; } = new();
}

public class VolunteerPointsEntryDto
{
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public int Round { get; set; }
	public decimal Points { get; set; }
	public string Notes { get; set; } = string.Empty;
}
