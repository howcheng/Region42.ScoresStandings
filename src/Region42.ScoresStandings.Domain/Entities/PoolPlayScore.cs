namespace Region42.ScoresStandings.Domain.Entities;

public class PoolPlayScore : TournamentScore
{
	public int HomeRedCards { get; set; }
	public int AwayRedCards { get; set; }
	public int HomeYellowCards { get; set; }
	public int AwayYellowCards { get; set; }
}
