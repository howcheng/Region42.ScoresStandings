using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Helpers;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Services.Tournaments;

/// <summary>
/// Builds and advances a 6-team single-elimination bracket:
/// - Seeds 1 and 2 get a first-round bye.
/// - Quarterfinal 1: 4th seed vs 5th seed (winner plays 1st seed in Semifinal 1).
/// - Quarterfinal 2: 3rd seed vs 6th seed (winner plays 2nd seed in Semifinal 2).
/// - Final: Winner of Semifinal 1 vs Winner of Semifinal 2.
/// </summary>
public class SixTeamBracketBuilder : ITournamentBracketBuilder
{
	public const int SupportedTeamCount = 6;

	public int TeamCount => SupportedTeamCount;

	public IReadOnlyList<EliminationGame> BuildInitialGames(
		int divisionId,
		int round,
		IReadOnlyList<TeamStanding> seededTeams)
	{
		ArgumentNullException.ThrowIfNull(seededTeams);
		if (seededTeams.Count != SupportedTeamCount)
		{
			throw new ArgumentException($"Expected {SupportedTeamCount} seeded teams, but received {seededTeams.Count}.", nameof(seededTeams));
		}

		var seed1 = seededTeams[0];
		var seed2 = seededTeams[1];
		var seed3 = seededTeams[2];
		var seed4 = seededTeams[3];
		var seed5 = seededTeams[4];
		var seed6 = seededTeams[5];

		return new List<EliminationGame>
		{
			new EliminationGame
			{
				DivisionId = divisionId,
				Round = round,
				Stage = TournamentStage.Quarterfinal1,
				HomeTeamId = seed4.TeamId,
				AwayTeamId = seed5.TeamId,
				HomeTeamPlaceholder = "4th Seed",
				AwayTeamPlaceholder = "5th Seed",
				ScheduledDateTime = default,
				Location = string.Empty,
				Status = GameStatus.Unscheduled
			},
			new EliminationGame
			{
				DivisionId = divisionId,
				Round = round,
				Stage = TournamentStage.Quarterfinal2,
				HomeTeamId = seed3.TeamId,
				AwayTeamId = seed6.TeamId,
				HomeTeamPlaceholder = "3rd Seed",
				AwayTeamPlaceholder = "6th Seed",
				ScheduledDateTime = default,
				Location = string.Empty,
				Status = GameStatus.Unscheduled
			},
			new EliminationGame
			{
				DivisionId = divisionId,
				Round = round,
				Stage = TournamentStage.Semifinal1,
				HomeTeamId = seed1.TeamId,
				AwayTeamId = 0,
				HomeTeamPlaceholder = "1st Seed",
				AwayTeamPlaceholder = "Winner of 4th vs 5th",
				ScheduledDateTime = default,
				Location = string.Empty,
				Status = GameStatus.Unscheduled
			},
			new EliminationGame
			{
				DivisionId = divisionId,
				Round = round,
				Stage = TournamentStage.Semifinal2,
				HomeTeamId = seed2.TeamId,
				AwayTeamId = 0,
				HomeTeamPlaceholder = "2nd Seed",
				AwayTeamPlaceholder = "Winner of 3rd vs 6th",
				ScheduledDateTime = default,
				Location = string.Empty,
				Status = GameStatus.Unscheduled
			},
			new EliminationGame
			{
				DivisionId = divisionId,
				Round = round,
				Stage = TournamentStage.Final,
				HomeTeamId = 0,
				AwayTeamId = 0,
				HomeTeamPlaceholder = "Winner of Semifinal 1",
				AwayTeamPlaceholder = "Winner of Semifinal 2",
				ScheduledDateTime = default,
				Location = string.Empty,
				Status = GameStatus.Unscheduled
			}
		};
	}

	public bool ApplyProgression(
		IReadOnlyList<TeamStanding> seededTeams,
		IReadOnlyList<EliminationGame> bracketGames)
	{
		ArgumentNullException.ThrowIfNull(seededTeams);
		ArgumentNullException.ThrowIfNull(bracketGames);

		var changed = false;

		var qf1 = bracketGames.FirstOrDefault(g => g.Stage == TournamentStage.Quarterfinal1);
		var qf2 = bracketGames.FirstOrDefault(g => g.Stage == TournamentStage.Quarterfinal2);
		var sf1 = bracketGames.FirstOrDefault(g => g.Stage == TournamentStage.Semifinal1);
		var sf2 = bracketGames.FirstOrDefault(g => g.Stage == TournamentStage.Semifinal2);
		var finalGame = bracketGames.FirstOrDefault(g => g.Stage == TournamentStage.Final);

		if (seededTeams.Count == SupportedTeamCount && !bracketGames.Any(g => g.Score?.HomeScore != null))
		{
			changed |= UpdateTeamIfDifferent(qf1, isHome: true, seededTeams[3].TeamId);
			changed |= UpdateTeamIfDifferent(qf1, isHome: false, seededTeams[4].TeamId);
			changed |= UpdateTeamIfDifferent(qf2, isHome: true, seededTeams[2].TeamId);
			changed |= UpdateTeamIfDifferent(qf2, isHome: false, seededTeams[5].TeamId);
			changed |= UpdateTeamIfDifferent(sf1, isHome: true, seededTeams[0].TeamId);
			changed |= UpdateTeamIfDifferent(sf2, isHome: true, seededTeams[1].TeamId);
		}

		if (sf1 != null)
		{
			var qf1Winner = qf1?.TryGetWinnerTeamId() ?? 0;
			changed |= UpdateTeamIfDifferent(sf1, isHome: false, qf1Winner);
		}

		if (sf2 != null)
		{
			var qf2Winner = qf2?.TryGetWinnerTeamId() ?? 0;
			changed |= UpdateTeamIfDifferent(sf2, isHome: false, qf2Winner);
		}

		if (finalGame != null)
		{
			var sf1Winner = sf1?.TryGetWinnerTeamId() ?? 0;
			var sf2Winner = sf2?.TryGetWinnerTeamId() ?? 0;
			changed |= UpdateTeamIfDifferent(finalGame, isHome: true, sf1Winner);
			changed |= UpdateTeamIfDifferent(finalGame, isHome: false, sf2Winner);
		}

		return changed;
	}

	public TournamentDisplayDto BuildDisplay(
		Division division,
		IReadOnlyList<TeamStanding> seededTeams,
		IReadOnlyList<EliminationGame> bracketGames,
		IReadOnlyDictionary<int, Team> teamsById)
	{
		ArgumentNullException.ThrowIfNull(division);
		ArgumentNullException.ThrowIfNull(seededTeams);
		ArgumentNullException.ThrowIfNull(bracketGames);
		ArgumentNullException.ThrowIfNull(teamsById);

		var seedByTeamId = seededTeams.ToDictionary(s => s.TeamId, s => s.Rank);

		var display = new TournamentDisplayDto
		{
			DivisionId = division.Id,
			DivisionName = $"{division.AgeGroup} {division.Gender}",
			Format = TournamentFormatType.Bracket,
			TournamentRound = division.TotalRounds,
			IsReady = seededTeams.Count == SupportedTeamCount && bracketGames.Count > 0,
			SeedByTeamId = seedByTeamId
		};

		if (seededTeams.Count >= 2)
		{
			display.ByeTeams.Add(new TournamentSeedTeamDto
			{
				Seed = 1,
				TeamId = seededTeams[0].TeamId,
				TeamName = seededTeams[0].TeamName,
				TeamShortName = seededTeams[0].TeamShortName
			});
			display.ByeTeams.Add(new TournamentSeedTeamDto
			{
				Seed = 2,
				TeamId = seededTeams[1].TeamId,
				TeamName = seededTeams[1].TeamName,
				TeamShortName = seededTeams[1].TeamShortName
			});
		}

		foreach (var game in bracketGames.OrderBy(g => g.Stage))
		{
			var matchDto = ToMatchDisplayDto(game, seedByTeamId, teamsById);
			switch (game.Stage)
			{
				case TournamentStage.Quarterfinal1:
				case TournamentStage.Quarterfinal2:
					display.Quarterfinals.Add(matchDto);
					break;
				case TournamentStage.Semifinal1:
				case TournamentStage.Semifinal2:
					display.Semifinals.Add(matchDto);
					break;
				case TournamentStage.Final:
					display.FinalMatch = matchDto;
					if (matchDto.WinnerTeamId.HasValue && teamsById.TryGetValue(matchDto.WinnerTeamId.Value, out var champion))
					{
						display.IsCompleted = true;
						display.ChampionTeamId = champion.Id;
						display.ChampionTeamName = champion.Name;
					}
					break;
			}
		}

		return display;
	}

	private static bool UpdateTeamIfDifferent(EliminationGame? game, bool isHome, int newTeamId)
	{
		if (game == null)
		{
			return false;
		}

		if (isHome)
		{
			if (game.HomeTeamId == newTeamId)
			{
				return false;
			}

			game.HomeTeamId = newTeamId;
			return true;
		}

		if (game.AwayTeamId == newTeamId)
		{
			return false;
		}

		game.AwayTeamId = newTeamId;
		return true;
	}

	private static TournamentMatchDisplayDto ToMatchDisplayDto(
		EliminationGame game,
		IReadOnlyDictionary<int, int> seedByTeamId,
		IReadOnlyDictionary<int, Team> teamsById)
	{
		teamsById.TryGetValue(game.HomeTeamId, out var homeTeam);
		teamsById.TryGetValue(game.AwayTeamId, out var awayTeam);

		var homeFallback = game.HomeTeamPlaceholder ?? "TBD";
		var awayFallback = game.AwayTeamPlaceholder ?? "TBD";

		var stageLabel = game.Stage switch
		{
			TournamentStage.Quarterfinal1 => "Quarterfinal 1 (4th vs 5th)",
			TournamentStage.Quarterfinal2 => "Quarterfinal 2 (3rd vs 6th)",
			TournamentStage.Semifinal1 => "Semifinal 1 (1st vs QF1 Winner)",
			TournamentStage.Semifinal2 => "Semifinal 2 (2nd vs QF2 Winner)",
			TournamentStage.Final => "Final",
			_ => game.Stage.GetDisplayLabel()
		};

		var elimScore = game.EliminationScore;

		return new TournamentMatchDisplayDto
		{
			GameId = game.Id,
			Stage = game.Stage,
			StageLabel = stageLabel,
			ScheduledDateTime = TimezoneHelper.ToPacificTime(game.ScheduledDateTime),
			Location = game.Location,
			Status = game.Status,
			HomeTeamId = game.HomeTeamId,
			HomeSeed = game.HomeTeamId > 0 && seedByTeamId.TryGetValue(game.HomeTeamId, out var hSeed) ? hSeed : null,
			HomeTeamName = homeTeam?.Name ?? homeFallback,
			HomeTeamShortName = homeTeam?.ShortName ?? homeFallback,
			AwayTeamId = game.AwayTeamId,
			AwaySeed = game.AwayTeamId > 0 && seedByTeamId.TryGetValue(game.AwayTeamId, out var aSeed) ? aSeed : null,
			AwayTeamName = awayTeam?.Name ?? awayFallback,
			AwayTeamShortName = awayTeam?.ShortName ?? awayFallback,
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
