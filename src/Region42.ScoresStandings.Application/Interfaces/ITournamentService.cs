using Region42.ScoresStandings.Application.DTOs;

namespace Region42.ScoresStandings.Application.Interfaces;

public interface ITournamentService
{
	bool EnsureTournamentGames(int divisionId);
	bool EnsureTournamentGames(int divisionId, bool saveChanges);
	Task<bool> EnsureTournamentGamesAsync(int divisionId);
	Task<bool> EnsureTournamentGamesAsync(int divisionId, bool saveChanges);

	bool GenerateOrResetTournamentGames(int divisionId, bool forceReset = false);
	bool GenerateOrResetTournamentGames(int divisionId, bool forceReset, bool saveChanges);
	Task<bool> GenerateOrResetTournamentGamesAsync(int divisionId, bool forceReset = false);
	Task<bool> GenerateOrResetTournamentGamesAsync(int divisionId, bool forceReset, bool saveChanges);

	bool SyncTournamentProgression(int divisionId);
	bool SyncTournamentProgression(int divisionId, bool saveChanges);
	Task<bool> SyncTournamentProgressionAsync(int divisionId);
	Task<bool> SyncTournamentProgressionAsync(int divisionId, bool saveChanges);

	TournamentDisplayDto? GetTournamentBracket(int divisionId);
	Task<TournamentDisplayDto?> GetTournamentBracketAsync(int divisionId);
}
