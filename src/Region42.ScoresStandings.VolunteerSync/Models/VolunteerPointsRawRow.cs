namespace Region42.ScoresStandings.VolunteerSync.Models;

public class VolunteerPointsRawRow
{
	public string DivisionCode { get; set; } = string.Empty;
	public string Team { get; set; } = string.Empty;
	public DateTime Date { get; set; }
	public decimal Points { get; set; }
}
