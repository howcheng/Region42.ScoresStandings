using Region42.ScoresStandings.Application.DTOs;

namespace Region42.ScoresStandings.Application.Interfaces;

public interface ITournamentService
{
	bool EnsureTournamentGames(int divisionId);
	Task<bool> EnsureTournamentGamesAsync(int divisionId);

	bool GenerateOrResetTournamentGames(int divisionId, bool forceReset = false);
	Task<bool> GenerateOrResetTournamentGamesAsync(int divisionId, bool forceReset = false);

	bool SyncTournamentProgression(int divisionId);
	Task<bool> SyncTournamentProgressionAsync(int divisionId);

	TournamentDisplayDto? GetTournamentBracket(int divisionId);
	Task<TournamentDisplayDto?> GetTournamentBracketAsync(int divisionId);
}
