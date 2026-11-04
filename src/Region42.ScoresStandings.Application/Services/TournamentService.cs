using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services.Tournaments;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

public class TournamentService : ITournamentService
{
	public const int MinPoolPlayTeams = 3;
	public const int MaxPoolPlayTeams = 4;

	private readonly ICompetitionDataContext _context;
	private readonly ITournamentBracketBuilderFactory _bracketBuilderFactory;
	private readonly ILogger<TournamentService> _logger;

	public TournamentService(
		ICompetitionDataContext context,
		ITournamentBracketBuilderFactory bracketBuilderFactory,
		ILogger<TournamentService> logger)
	{
		_context = context;
		_bracketBuilderFactory = bracketBuilderFactory;
		_logger = logger;
	}

	public bool EnsureTournamentGames(int divisionId)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		_context.LoadAllAsync().GetAwaiter().GetResult();
		var createdOrUpdated = EnsureTournamentGamesCore(divisionId, requirePriorRoundsCompleted: true, forceReset: false);
		if (createdOrUpdated)
		{
			_context.SaveChangesAsync().GetAwaiter().GetResult();
		}

		return createdOrUpdated;
	}

	public Task<bool> EnsureTournamentGamesAsync(int divisionId)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		return EnsureTournamentGamesInternalAsync(divisionId);
	}

	private async Task<bool> EnsureTournamentGamesInternalAsync(int divisionId)
	{
		await _context.LoadAllAsync();
		var createdOrUpdated = EnsureTournamentGamesCore(divisionId, requirePriorRoundsCompleted: true, forceReset: false);
		if (createdOrUpdated)
		{
			await _context.SaveChangesAsync();
		}

		return createdOrUpdated;
	}

	public bool GenerateOrResetTournamentGames(int divisionId, bool forceReset = false)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		_context.LoadAllAsync().GetAwaiter().GetResult();
		var createdOrUpdated = EnsureTournamentGamesCore(divisionId, requirePriorRoundsCompleted: false, forceReset: forceReset);
		if (createdOrUpdated)
		{
			_context.SaveChangesAsync().GetAwaiter().GetResult();
		}

		return createdOrUpdated;
	}

	public Task<bool> GenerateOrResetTournamentGamesAsync(int divisionId, bool forceReset = false)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		return GenerateOrResetTournamentGamesInternalAsync(divisionId, forceReset);
	}

	private async Task<bool> GenerateOrResetTournamentGamesInternalAsync(int divisionId, bool forceReset)
	{
		await _context.LoadAllAsync();
		var createdOrUpdated = EnsureTournamentGamesCore(divisionId, requirePriorRoundsCompleted: false, forceReset: forceReset);
		if (createdOrUpdated)
		{
			await _context.SaveChangesAsync();
		}

		return createdOrUpdated;
	}

	private bool EnsureTournamentGamesCore(int divisionId, bool requirePriorRoundsCompleted, bool forceReset)
	{
		var division = GetRequiredDivision(divisionId);
		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.None)
		{
			return false;
		}

		if (requirePriorRoundsCompleted && !AreRegularSeasonRoundsComplete(division))
		{
			return false;
		}

		var seededTeams = GetRoundNineSeededStandings(division);
		if (seededTeams.Count == 0)
		{
			return false;
		}

		var existingTournamentGames = _context.Games
			.OfType<TournamentGame>()
			.Where(g => g.DivisionId == division.Id)
			.ToList();

		if (existingTournamentGames.Count > 0)
		{
			var anyScoreEntered = existingTournamentGames.Any(g => g.Score?.HomeScore != null || g.Score?.AwayScore != null);
			if (anyScoreEntered && !forceReset)
			{
				return SyncTournamentProgressionCore(division, seededTeams);
			}

			if (forceReset)
			{
				var tournamentGameIds = existingTournamentGames.Select(g => g.Id).ToHashSet();
				var makeupGames = _context.Games
					.Where(g => g.DivisionId == division.Id && g.RescheduledFromGameId.HasValue && tournamentGameIds.Contains(g.RescheduledFromGameId.Value))
					.ToList();
				foreach (var makeup in makeupGames)
				{
					_context.Delete(makeup);
				}

				foreach (var existing in existingTournamentGames)
				{
					var existingScores = _context.Scores.Where(s => s.GameId == existing.Id).ToList();
					foreach (var s in existingScores)
					{
						_context.Delete(s);
					}
					existing.Score = null;
					existing.RescheduledToGameId = null;
					existing.Status = existing.ScheduledDateTime == default ? GameStatus.Unscheduled : GameStatus.Scheduled;
					_context.Update(existing);
				}

				if (division.PoolPlayTiebreakerWinnerTeamId.HasValue)
				{
					division.PoolPlayTiebreakerWinnerTeamId = null;
					_context.Update(division);
				}
			}

			return SyncTournamentProgressionCore(division, seededTeams);
		}

		// Remove any non-tournament placeholder games that might exist in the tournament round
		var regularGamesInTournamentRound = _context.Games
			.Where(g => g.DivisionId == division.Id && g.Round == division.TotalRounds && g is not TournamentGame)
			.ToList();

		foreach (var regularGame in regularGamesInTournamentRound)
		{
			if (regularGame.Score == null)
			{
				_context.Delete(regularGame);
			}
		}

		var initialGames = BuildInitialTournamentGames(division, seededTeams);
		foreach (var game in initialGames)
		{
			_context.Add(game);
		}

		_logger.LogInformation(
			"Generated {Count} tournament games for division {DivisionId} ({Format})",
			initialGames.Count,
			division.Id,
			division.EndOfSeasonTournamentFormat);

		return initialGames.Count > 0;
	}

	private Division GetRequiredDivision(int divisionId)
	{
		return _context.Divisions.FirstOrDefault(d => d.Id == divisionId)
			?? throw new ArgumentException($"Division with ID {divisionId} not found.", nameof(divisionId));
	}

	private bool AreRegularSeasonRoundsComplete(Division division)
	{
		var lastRegularRound = Math.Max(1, division.TotalRounds - 1);
		var regularGames = _context.Games
			.Where(g => g.DivisionId == division.Id && g.Round <= lastRegularRound && g is not TournamentGame)
			.ToList();

		if (regularGames.Count == 0)
		{
			return false;
		}

		var hasCompletedInLastRegularRound = regularGames
			.Any(g => g.Round == lastRegularRound && g.Status == GameStatus.Completed);

		var hasPendingScheduledGames = regularGames
			.Any(g => g.Status is GameStatus.Scheduled or GameStatus.Unscheduled);

		return hasCompletedInLastRegularRound && !hasPendingScheduledGames;
	}

	private List<TeamStanding> GetRoundNineSeededStandings(Division division)
	{
		var seedingRound = Math.Max(1, division.TotalRounds - 1);
		var teams = _context.Teams.Where(t => t.DivisionId == division.Id).ToList();
		var games = _context.Games.Where(g => g.DivisionId == division.Id).ToList();
		var gameIds = games.Select(g => g.Id).ToHashSet();
		var scores = _context.Scores.Where(s => gameIds.Contains(s.GameId)).ToList();
		var teamIds = teams.Select(t => t.Id).ToHashSet();
		var volunteerPoints = _context.VolunteerPoints.Where(vp => teamIds.Contains(vp.TeamId)).ToList();

		var standingsResult = StandingsCalculator.Calculate(
			division,
			teams,
			games,
			scores,
			volunteerPoints,
			_context.Settings.MinVolunteerPointsForPlayoff,
			seedingRound,
			_context.Settings.MaxVolunteerPointsPerSeason);

		return standingsResult.Standings;
	}

	private IReadOnlyList<TournamentGame> BuildInitialTournamentGames(
		Division division,
		IReadOnlyList<TeamStanding> seededTeams)
	{
		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.Bracket)
		{
			var builder = _bracketBuilderFactory.GetBuilder(seededTeams.Count);
			return builder.BuildInitialGames(division.Id, division.TotalRounds, seededTeams);
		}

		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.PoolPlayWithFinal)
		{
			return BuildPoolPlayInitialGames(division.Id, division.TotalRounds, seededTeams);
		}

		return Array.Empty<TournamentGame>();
	}

	private static IReadOnlyList<TournamentGame> BuildPoolPlayInitialGames(
		int divisionId,
		int round,
		IReadOnlyList<TeamStanding> seededTeams)
	{
		if (seededTeams.Count < MinPoolPlayTeams || seededTeams.Count > MaxPoolPlayTeams)
		{
			throw new NotSupportedException(
				$"Pool play tournaments only support divisions with {MinPoolPlayTeams} or {MaxPoolPlayTeams} teams, but found {seededTeams.Count}.");
		}

		var games = new List<TournamentGame>();
		var pairings = GetPoolPlaySeedPairings(seededTeams.Count);

		for (var i = 0; i < pairings.Count; i++)
		{
			var (homeSeedIndex, awaySeedIndex, stage) = pairings[i];
			var homeSeed = seededTeams[homeSeedIndex];
			var awaySeed = seededTeams[awaySeedIndex];

			games.Add(new PoolPlayGame
			{
				DivisionId = divisionId,
				Round = round,
				Stage = stage,
				HomeTeamId = homeSeed.TeamId,
				AwayTeamId = awaySeed.TeamId,
				HomeTeamPlaceholder = $"Seed {homeSeedIndex + 1}",
				AwayTeamPlaceholder = $"Seed {awaySeedIndex + 1}",
				ScheduledDateTime = default,
				Location = string.Empty,
				Status = GameStatus.Unscheduled
			});
		}

		games.Add(new EliminationGame
		{
			DivisionId = divisionId,
			Round = round,
			Stage = TournamentStage.Final,
			HomeTeamId = 0,
			AwayTeamId = 0,
			HomeTeamPlaceholder = "Pool 1st Place",
			AwayTeamPlaceholder = "Pool 2nd Place",
			ScheduledDateTime = default,
			Location = string.Empty,
			Status = GameStatus.Unscheduled
		});

		return games;
	}

	private static List<(int HomeSeedIndex, int AwaySeedIndex, TournamentStage Stage)> GetPoolPlaySeedPairings(int teamCount)
	{
		if (teamCount == 3)
		{
			return new List<(int, int, TournamentStage)>
			{
				(0, 1, TournamentStage.PoolPlay1),
				(1, 2, TournamentStage.PoolPlay2),
				(0, 2, TournamentStage.PoolPlay3)
			};
		}

		return new List<(int, int, TournamentStage)>
		{
			(0, 3, TournamentStage.PoolPlay1),
			(1, 2, TournamentStage.PoolPlay2),
			(0, 2, TournamentStage.PoolPlay3),
			(1, 3, TournamentStage.PoolPlay4),
			(0, 1, TournamentStage.PoolPlay5),
			(2, 3, TournamentStage.PoolPlay6)
		};
	}

	public bool SyncTournamentProgression(int divisionId)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		_context.LoadAllAsync().GetAwaiter().GetResult();
		var division = GetRequiredDivision(divisionId);
		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.None)
		{
			return false;
		}

		var seededTeams = GetRoundNineSeededStandings(division);
		var updated = SyncTournamentProgressionCore(division, seededTeams);
		if (updated)
		{
			_context.SaveChangesAsync().GetAwaiter().GetResult();
		}

		return updated;
	}

	public Task<bool> SyncTournamentProgressionAsync(int divisionId)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		return SyncTournamentProgressionInternalAsync(divisionId);
	}

	private async Task<bool> SyncTournamentProgressionInternalAsync(int divisionId)
	{
		await _context.LoadAllAsync();
		var division = GetRequiredDivision(divisionId);
		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.None)
		{
			return false;
		}

		var seededTeams = GetRoundNineSeededStandings(division);
		var updated = SyncTournamentProgressionCore(division, seededTeams);
		if (updated)
		{
			await _context.SaveChangesAsync();
		}

		return updated;
	}

	private bool SyncTournamentProgressionCore(Division division, List<TeamStanding> seededTeams)
	{
		WireDivisionTournamentScores(division.Id);

		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.Bracket)
		{
			var bracketGames = _context.Games
				.OfType<EliminationGame>()
				.Where(g => g.DivisionId == division.Id)
				.ToList();

			if (bracketGames.Count == 0 || seededTeams.Count == 0)
			{
				return false;
			}

			var builder = _bracketBuilderFactory.GetBuilder(seededTeams.Count);
			var changed = builder.ApplyProgression(seededTeams, bracketGames);
			if (changed)
			{
				foreach (var game in bracketGames)
				{
					_context.Update(game);
				}
			}

			return changed;
		}

		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.PoolPlayWithFinal)
		{
			var poolGames = _context.Games
				.OfType<PoolPlayGame>()
				.Where(g => g.DivisionId == division.Id)
				.OrderBy(g => g.Stage)
				.ToList();

			var finalGame = _context.Games
				.OfType<EliminationGame>()
				.FirstOrDefault(g => g.DivisionId == division.Id && g.Stage == TournamentStage.Final);

			if (poolGames.Count == 0 || seededTeams.Count < MinPoolPlayTeams || seededTeams.Count > MaxPoolPlayTeams)
			{
				return false;
			}

			var changed = false;

			// Re-seed initial pool matchups if no pool game scores have been entered yet
			if (!poolGames.Any(g => g.Score?.HomeScore != null) && finalGame?.Score?.HomeScore == null)
			{
				var pairings = GetPoolPlaySeedPairings(seededTeams.Count);
				foreach (var (homeIdx, awayIdx, stage) in pairings)
				{
					var poolGame = poolGames.FirstOrDefault(g => g.Stage == stage);
					if (poolGame != null)
					{
						var newHomeId = seededTeams[homeIdx].TeamId;
						var newAwayId = seededTeams[awayIdx].TeamId;
						if (poolGame.HomeTeamId != newHomeId || poolGame.AwayTeamId != newAwayId)
						{
							poolGame.HomeTeamId = newHomeId;
							poolGame.AwayTeamId = newAwayId;
							_context.Update(poolGame);
							changed = true;
						}
					}
				}
			}

			if (finalGame != null)
			{
				var poolResult = PoolPlayStandingsCalculator.Calculate(
					seededTeams,
					poolGames,
					division.PoolPlayTiebreakerWinnerTeamId);

				var expectedHomeId = poolResult.AllPoolGamesCompleted && poolResult.Standings.Count >= 2
					? poolResult.Standings[0].TeamId
					: 0;
				var expectedAwayId = poolResult.AllPoolGamesCompleted && poolResult.Standings.Count >= 2
					? poolResult.Standings[1].TeamId
					: 0;

				if (finalGame.HomeTeamId != expectedHomeId || finalGame.AwayTeamId != expectedAwayId)
				{
					finalGame.HomeTeamId = expectedHomeId;
					finalGame.AwayTeamId = expectedAwayId;
					_context.Update(finalGame);
					changed = true;
				}
			}

			return changed;
		}

		return false;
	}

	private void WireDivisionTournamentScores(int divisionId)
	{
		var scoresByGameId = _context.Scores
			.GroupBy(s => s.GameId)
			.ToDictionary(g => g.Key, g => g.First());

		foreach (var game in _context.Games.OfType<TournamentGame>().Where(g => g.DivisionId == divisionId))
		{
			if (scoresByGameId.TryGetValue(game.Id, out var score))
			{
				game.Score = score;
				score.Game = game;
			}
		}
	}

	public TournamentDisplayDto? GetTournamentBracket(int divisionId)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		_context.LoadAllAsync().GetAwaiter().GetResult();
		return GetTournamentBracketCore(divisionId);
	}

	public Task<TournamentDisplayDto?> GetTournamentBracketAsync(int divisionId)
	{
		if (divisionId <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(divisionId), "Division ID must be greater than zero.");
		}

		return GetTournamentBracketInternalAsync(divisionId);
	}

	private async Task<TournamentDisplayDto?> GetTournamentBracketInternalAsync(int divisionId)
	{
		await _context.LoadAllAsync();
		return GetTournamentBracketCore(divisionId);
	}

	private TournamentDisplayDto? GetTournamentBracketCore(int divisionId)
	{
		var division = GetRequiredDivision(divisionId);
		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.None)
		{
			return null;
		}

		EnsureTournamentGamesCore(divisionId, requirePriorRoundsCompleted: true, forceReset: false);
		WireDivisionTournamentScores(divisionId);

		var seededTeams = GetRoundNineSeededStandings(division);
		var teamsById = _context.Teams.Where(t => t.DivisionId == division.Id).ToDictionary(t => t.Id);

		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.Bracket)
		{
			var bracketGames = _context.Games
				.OfType<EliminationGame>()
				.Where(g => g.DivisionId == division.Id)
				.OrderBy(g => g.Stage)
				.ToList();

			if (seededTeams.Count < TournamentBracketBuilderFactory.MinSupportedTeams ||
				seededTeams.Count > TournamentBracketBuilderFactory.MaxSupportedTeams)
			{
				return new TournamentDisplayDto
				{
					DivisionId = division.Id,
					DivisionName = $"{division.AgeGroup} {division.Gender}",
					Format = TournamentFormatType.Bracket,
					TournamentRound = division.TotalRounds,
					IsReady = false
				};
			}

			var builder = _bracketBuilderFactory.GetBuilder(seededTeams.Count);
			return builder.BuildDisplay(division, seededTeams, bracketGames, teamsById);
		}

		if (division.EndOfSeasonTournamentFormat == TournamentFormatType.PoolPlayWithFinal)
		{
			var poolGames = _context.Games
				.OfType<PoolPlayGame>()
				.Where(g => g.DivisionId == division.Id)
				.OrderBy(g => g.Stage)
				.ToList();

			var finalGame = _context.Games
				.OfType<EliminationGame>()
				.FirstOrDefault(g => g.DivisionId == division.Id && g.Stage == TournamentStage.Final);

			return BuildPoolPlayDisplay(division, seededTeams, poolGames, finalGame, teamsById);
		}

		return null;
	}

	private static TournamentDisplayDto BuildPoolPlayDisplay(
		Division division,
		List<TeamStanding> seededTeams,
		List<PoolPlayGame> poolGames,
		EliminationGame? finalGame,
		IReadOnlyDictionary<int, Team> teamsById)
	{
		var seedByTeamId = seededTeams.ToDictionary(s => s.TeamId, s => s.Rank);
		var poolResult = PoolPlayStandingsCalculator.Calculate(
			seededTeams,
			poolGames,
			division.PoolPlayTiebreakerWinnerTeamId);

		var display = new TournamentDisplayDto
		{
			DivisionId = division.Id,
			DivisionName = $"{division.AgeGroup} {division.Gender}",
			Format = TournamentFormatType.PoolPlayWithFinal,
			TournamentRound = division.TotalRounds,
			IsReady = seededTeams.Count >= MinPoolPlayTeams && (poolGames.Count > 0 || finalGame != null),
			SeedByTeamId = seedByTeamId,
			PoolStandings = poolResult.Standings,
			AnyForfeitInPool = poolResult.AnyForfeitInPool,
			RequiresPenaltyShootoutTiebreaker = poolResult.RequiresPenaltyShootoutTiebreaker,
			PoolPlayTiebreakerWinnerTeamId = division.PoolPlayTiebreakerWinnerTeamId,
			TiedTeamsForTiebreaker = seededTeams
				.Where(s => poolResult.TiedTeamIdsForTiebreaker.Contains(s.TeamId))
				.Select(s => new TournamentSeedTeamDto
				{
					Seed = s.Rank,
					TeamId = s.TeamId,
					TeamName = s.TeamName,
					TeamShortName = s.TeamShortName
				})
				.ToList()
		};

		foreach (var poolGame in poolGames.OrderBy(g => g.Stage))
		{
			display.PoolPlayMatches.Add(ToPoolMatchDisplayDto(poolGame, seedByTeamId, teamsById));
		}

		if (finalGame != null)
		{
			var finalDto = ToFinalMatchDisplayDto(finalGame, seedByTeamId, teamsById);
			display.FinalMatch = finalDto;
			if (finalDto.WinnerTeamId.HasValue && teamsById.TryGetValue(finalDto.WinnerTeamId.Value, out var champion))
			{
				display.IsCompleted = true;
				display.ChampionTeamId = champion.Id;
				display.ChampionTeamName = champion.Name;
			}
		}

		return display;
	}

	private static TournamentMatchDisplayDto ToPoolMatchDisplayDto(
		PoolPlayGame game,
		IReadOnlyDictionary<int, int> seedByTeamId,
		IReadOnlyDictionary<int, Team> teamsById)
	{
		teamsById.TryGetValue(game.HomeTeamId, out var homeTeam);
		teamsById.TryGetValue(game.AwayTeamId, out var awayTeam);

		var poolScore = game.PoolPlayScore;
		int? winnerTeamId = null;
		if (game.Status == GameStatus.Completed && game.Score is { HomeScore: { } h, AwayScore: { } a } && h != a)
		{
			winnerTeamId = h > a ? game.HomeTeamId : game.AwayTeamId;
		}

		return new TournamentMatchDisplayDto
		{
			GameId = game.Id,
			Stage = game.Stage,
			StageLabel = game.Stage.GetDisplayLabel(),
			ScheduledDateTime = TimezoneHelper.ToPacificTime(game.ScheduledDateTime),
			Location = game.Location,
			Status = game.Status,
			HomeTeamId = game.HomeTeamId,
			HomeSeed = game.HomeTeamId > 0 && seedByTeamId.TryGetValue(game.HomeTeamId, out var hSeed) ? hSeed : null,
			HomeTeamName = homeTeam?.Name ?? game.HomeTeamPlaceholder ?? "TBD",
			HomeTeamShortName = homeTeam?.ShortName ?? game.HomeTeamPlaceholder ?? "TBD",
			AwayTeamId = game.AwayTeamId,
			AwaySeed = game.AwayTeamId > 0 && seedByTeamId.TryGetValue(game.AwayTeamId, out var aSeed) ? aSeed : null,
			AwayTeamName = awayTeam?.Name ?? game.AwayTeamPlaceholder ?? "TBD",
			AwayTeamShortName = awayTeam?.ShortName ?? game.AwayTeamPlaceholder ?? "TBD",
			HomeScore = game.Score?.HomeScore,
			AwayScore = game.Score?.AwayScore,
			IsForfeit = game.TournamentScore?.IsForfeit ?? false,
			HomeRedCards = poolScore?.HomeRedCards ?? 0,
			AwayRedCards = poolScore?.AwayRedCards ?? 0,
			HomeYellowCards = poolScore?.HomeYellowCards ?? 0,
			AwayYellowCards = poolScore?.AwayYellowCards ?? 0,
			WinnerTeamId = winnerTeamId
		};
	}

	private static TournamentMatchDisplayDto ToFinalMatchDisplayDto(
		EliminationGame game,
		IReadOnlyDictionary<int, int> seedByTeamId,
		IReadOnlyDictionary<int, Team> teamsById)
	{
		teamsById.TryGetValue(game.HomeTeamId, out var homeTeam);
		teamsById.TryGetValue(game.AwayTeamId, out var awayTeam);

		var elimScore = game.EliminationScore;

		return new TournamentMatchDisplayDto
		{
			GameId = game.Id,
			Stage = game.Stage,
			StageLabel = "Final (Pool 1st vs Pool 2nd)",
			ScheduledDateTime = TimezoneHelper.ToPacificTime(game.ScheduledDateTime),
			Location = game.Location,
			Status = game.Status,
			HomeTeamId = game.HomeTeamId,
			HomeSeed = game.HomeTeamId > 0 && seedByTeamId.TryGetValue(game.HomeTeamId, out var hSeed) ? hSeed : null,
			HomeTeamName = homeTeam?.Name ?? game.HomeTeamPlaceholder ?? "Pool 1st Place",
			HomeTeamShortName = homeTeam?.ShortName ?? game.HomeTeamPlaceholder ?? "Pool 1st Place",
			AwayTeamId = game.AwayTeamId,
			AwaySeed = game.AwayTeamId > 0 && seedByTeamId.TryGetValue(game.AwayTeamId, out var aSeed) ? aSeed : null,
			AwayTeamName = awayTeam?.Name ?? game.AwayTeamPlaceholder ?? "Pool 2nd Place",
			AwayTeamShortName = awayTeam?.ShortName ?? game.AwayTeamPlaceholder ?? "Pool 2nd Place",
			HomeScore = game.Score?.HomeScore,
			AwayScore = game.Score?.AwayScore,
			IsForfeit = elimScore?.IsForfeit ?? false,
			IsDecidedByPenalties = elimScore?.IsDecidedByPenalties ?? false,
			HomePenaltyScore = elimScore?.HomePenaltyScore,
			AwayPenaltyScore = elimScore?.AwayPenaltyScore,
			WinnerTeamId = game.TryGetWinnerTeamId()
		};
	}
}
