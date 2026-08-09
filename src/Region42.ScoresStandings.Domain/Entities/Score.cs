namespace Region42.ScoresStandings.Domain.Entities;

public class Score : BaseEntity
{
	public int GameId { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }

	public Game Game { get; set; } = null!;
}
