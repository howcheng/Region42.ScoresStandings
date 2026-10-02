namespace Region42.ScoresStandings.VolunteerSync.Models;

public class VolunteerPointsSyncContextDto
{
	public int SeasonId { get; set; }
	public int SeasonYear { get; set; }
	public List<VolunteerPointsSyncDivisionDto> Divisions { get; set; } = new();
}

public class VolunteerPointsSyncDivisionDto
{
	public int DivisionId { get; set; }
	public string CgiDivisionCode { get; set; } = string.Empty;
	public int TotalRounds { get; set; }
	public Dictionary<string, int> RoundByDate { get; set; } = new(StringComparer.Ordinal);
}
