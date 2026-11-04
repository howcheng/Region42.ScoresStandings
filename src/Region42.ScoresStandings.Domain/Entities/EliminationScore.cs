namespace Region42.ScoresStandings.Domain.Entities;

public class EliminationScore : TournamentScore
{
	public bool IsDecidedByPenalties { get; set; }
	public int? HomePenaltyScore { get; set; }
	public int? AwayPenaltyScore { get; set; }
}
