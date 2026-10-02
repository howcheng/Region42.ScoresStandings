namespace Region42.ScoresStandings.Application.DTOs;

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
	public List<VolunteerPointsSyncTeamDto> Teams { get; set; } = new();
	public Dictionary<string, int> RoundByDate { get; set; } = new(StringComparer.Ordinal);
}

public class VolunteerPointsSyncTeamDto
{
	public int TeamId { get; set; }
	public string Name { get; set; } = string.Empty;
	public string ShortName { get; set; } = string.Empty;
}
