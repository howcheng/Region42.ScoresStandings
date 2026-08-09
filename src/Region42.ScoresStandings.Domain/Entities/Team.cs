namespace Region42.ScoresStandings.Domain.Entities;

public class Team : BaseEntity
{
	public string Name { get; set; } = string.Empty;
	public string ShortName { get; set; } = string.Empty;
	public int DivisionId { get; set; }
	public string ContactName { get; set; } = string.Empty;
	public string ContactEmail { get; set; } = string.Empty;
	public string ContactPhone { get; set; } = string.Empty;
	public bool IsActive { get; set; }

	/// <summary>
	/// True for Region 42 teams, False for away region teams (inter-regional games).
	/// Away region teams are excluded from standings calculations.
	/// </summary>
	public bool IsRegion42Team { get; set; } = true;

	public Division Division { get; set; } = null!;
	public ICollection<Game> HomeGames { get; set; } = new List<Game>();
	public ICollection<Game> AwayGames { get; set; } = new List<Game>();
	public ICollection<VolunteerPoints> VolunteerPoints { get; set; } = new List<VolunteerPoints>();
}
