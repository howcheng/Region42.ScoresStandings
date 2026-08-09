using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Domain.Entities;

public class Division : BaseEntity
{
	public int SeasonId { get; set; }
	public AgeGroup AgeGroup { get; set; }
	public Gender Gender { get; set; }
	public int TotalRounds { get; set; }

	/// <summary>
	/// Number of teams that qualify for playoffs from this division.
	/// Can be changed mid-season via admin page.
	/// </summary>
	public int PlayoffSpots { get; set; } = 1;

	public Season Season { get; set; } = null!;
	public ICollection<Team> Teams { get; set; } = new List<Team>();
	public ICollection<Game> Games { get; set; } = new List<Game>();
}
