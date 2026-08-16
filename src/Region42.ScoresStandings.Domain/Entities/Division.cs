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

	/// <summary>
	/// Number of leading rounds (starting at Round 1) that are scrimmages and do not count
	/// toward standings. Scores are still entered for these rounds, but only volunteer points
	/// earned during them contribute to standings.
	/// </summary>
	public int ScrimmageRounds { get; set; } = 0;

	/// <summary>
	/// Custom announcements or notice text for this specific division (per-season, since Divisions are scoped to a Season).
	/// </summary>
	public string? CustomMessage { get; set; }

	public Season Season { get; set; } = null!;
	public ICollection<Team> Teams { get; set; } = new List<Team>();
	public ICollection<Game> Games { get; set; } = new List<Game>();
}
