namespace Region42.ScoresStandings.Domain.Entities;

public class PoolPlayGame : TournamentGame
{
	public PoolPlayScore? PoolPlayScore => Score as PoolPlayScore;
}
