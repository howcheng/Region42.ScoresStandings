using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Domain.Entities;

public class Game : BaseEntity
{
	public int DivisionId { get; set; }
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public DateTime ScheduledDateTime { get; set; }
	public int Round { get; set; }
	public string Location { get; set; } = string.Empty;
	public GameStatus Status { get; set; }

	public Division Division { get; set; } = null!;
	public Team HomeTeam { get; set; } = null!;
	public Team AwayTeam { get; set; } = null!;
	public Score? Score { get; set; }
}
