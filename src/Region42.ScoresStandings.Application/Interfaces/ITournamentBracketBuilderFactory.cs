namespace Region42.ScoresStandings.Application.Interfaces;

public interface ITournamentBracketBuilderFactory
{
	ITournamentBracketBuilder GetBuilder(int teamCount);
}
