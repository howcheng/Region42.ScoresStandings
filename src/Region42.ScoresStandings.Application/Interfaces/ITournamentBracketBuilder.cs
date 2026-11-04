using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Domain.Entities;

namespace Region42.ScoresStandings.Application.Interfaces;

public interface ITournamentBracketBuilder
{
	int TeamCount { get; }

	IReadOnlyList<EliminationGame> BuildInitialGames(
		int divisionId,
		int round,
		IReadOnlyList<TeamStanding> seededTeams);

	bool ApplyProgression(
		IReadOnlyList<TeamStanding> seededTeams,
		IReadOnlyList<EliminationGame> bracketGames);

	TournamentDisplayDto BuildDisplay(
		Division division,
		IReadOnlyList<TeamStanding> seededTeams,
		IReadOnlyList<EliminationGame> bracketGames,
		IReadOnlyDictionary<int, Team> teamsById);
}
