namespace Region42.ScoresStandings.Domain.Entities;

public class VolunteerPoints : BaseEntity
{
	public int TeamId { get; set; }
	public int Round { get; set; }
	public int Points { get; set; }
	public string Notes { get; set; } = string.Empty;

	public Team Team { get; set; } = null!;
}
