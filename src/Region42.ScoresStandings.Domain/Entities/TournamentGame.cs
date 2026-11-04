using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Domain.Entities;

public class TournamentGame : Game
{
	public TournamentStage Stage { get; set; }
	public string? HomeTeamPlaceholder { get; set; }
	public string? AwayTeamPlaceholder { get; set; }

	public TournamentScore? TournamentScore => Score as TournamentScore;
}
