using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Domain.Helpers;

public static class DocumentEntityMapper
{
	public static Season ToEntity(SeasonInfoDocument doc)
	{
		return new Season
		{
			Id = doc.Id,
			Year = doc.Year,
			Name = doc.Name,
			IsActive = doc.IsActive,
			CustomMessage = doc.CustomMessage
		};
	}

	public static SeasonInfoDocument ToDocument(Season entity)
	{
		return new SeasonInfoDocument
		{
			Id = entity.Id,
			Year = entity.Year,
			Name = entity.Name,
			IsActive = entity.IsActive,
			CustomMessage = entity.CustomMessage
		};
	}

	public static Settings ToEntity(SettingsDocument doc, int id = 1)
	{
		return new Settings
		{
			Id = id,
			MinVolunteerPointsForPlayoff = doc.MinVolunteerPointsForPlayoff,
			DefaultPlayoffSpots = doc.DefaultPlayoffSpots,
			MaxVolunteerPointsPerSeason = doc.MaxVolunteerPointsPerSeason
		};
	}

	public static SettingsDocument ToDocument(Settings entity)
	{
		return new SettingsDocument
		{
			MinVolunteerPointsForPlayoff = entity.MinVolunteerPointsForPlayoff,
			DefaultPlayoffSpots = entity.DefaultPlayoffSpots,
			MaxVolunteerPointsPerSeason = entity.MaxVolunteerPointsPerSeason
		};
	}

	public static Division ToEntity(DivisionDocument doc, int seasonId)
	{
		var ageGroup = DivisionKeyHelper.ParseAgeGroup(doc.AgeGroup);
		var tournamentFormat = !string.IsNullOrWhiteSpace(doc.EndOfSeasonTournamentFormat) &&
			Enum.TryParse<TournamentFormatType>(doc.EndOfSeasonTournamentFormat, ignoreCase: true, out var parsedFormat)
				? parsedFormat
				: ageGroup switch
				{
					AgeGroup.U12 => TournamentFormatType.Bracket,
					AgeGroup.U14 => TournamentFormatType.PoolPlayWithFinal,
					_ => TournamentFormatType.None
				};

		return new Division
		{
			Id = doc.Id,
			SeasonId = seasonId,
			AgeGroup = ageGroup,
			Gender = DivisionKeyHelper.ParseGender(doc.Gender),
			TotalRounds = doc.TotalRounds,
			PlayoffSpots = doc.PlayoffSpots,
			ScrimmageRounds = doc.ScrimmageRounds,
			CustomMessage = doc.CustomMessage,
			EndOfSeasonTournamentFormat = tournamentFormat,
			PoolPlayTiebreakerWinnerTeamId = doc.PoolPlayTiebreakerWinnerTeamId
		};
	}

	public static DivisionDocument ToDocument(Division entity, IEnumerable<Team> teams)
	{
		return new DivisionDocument
		{
			Id = entity.Id,
			Key = DivisionKeyHelper.ToKey(entity.AgeGroup, entity.Gender),
			AgeGroup = DivisionKeyHelper.ToAgeGroupString(entity.AgeGroup),
			Gender = DivisionKeyHelper.ToGenderString(entity.Gender),
			TotalRounds = entity.TotalRounds,
			PlayoffSpots = entity.PlayoffSpots,
			ScrimmageRounds = entity.ScrimmageRounds,
			CustomMessage = entity.CustomMessage,
			EndOfSeasonTournamentFormat = entity.EndOfSeasonTournamentFormat.ToString(),
			PoolPlayTiebreakerWinnerTeamId = entity.PoolPlayTiebreakerWinnerTeamId,
			Teams = teams.Select(ToDocument).ToList()
		};
	}

	public static Team ToEntity(TeamDocument doc, int divisionId)
	{
		return new Team
		{
			Id = doc.Id,
			DivisionId = divisionId,
			Name = doc.Name,
			ShortName = doc.ShortName,
			ContactName = doc.ContactName,
			IsActive = doc.IsActive,
			IsRegion42Team = doc.IsRegion42Team
		};
	}

	public static TeamDocument ToDocument(Team entity)
	{
		return new TeamDocument
		{
			Id = entity.Id,
			Name = entity.Name,
			ShortName = entity.ShortName,
			ContactName = entity.ContactName,
			IsActive = entity.IsActive,
			IsRegion42Team = entity.IsRegion42Team
		};
	}

	public static Game ToEntity(GameDocument doc, int divisionId, Team? homeTeam = null, Team? awayTeam = null)
	{
		var stage = !string.IsNullOrWhiteSpace(doc.TournamentStage) &&
			Enum.TryParse<TournamentStage>(doc.TournamentStage, ignoreCase: true, out var parsedStage)
				? parsedStage
				: TournamentStage.None;

		Game game;
		if (stage.IsEliminationStage())
		{
			game = new EliminationGame
			{
				Stage = stage,
				HomeTeamPlaceholder = doc.HomeTeamPlaceholder,
				AwayTeamPlaceholder = doc.AwayTeamPlaceholder
			};
		}
		else if (stage.IsPoolPlayStage())
		{
			game = new PoolPlayGame
			{
				Stage = stage,
				HomeTeamPlaceholder = doc.HomeTeamPlaceholder,
				AwayTeamPlaceholder = doc.AwayTeamPlaceholder
			};
		}
		else if (stage != TournamentStage.None)
		{
			game = new TournamentGame
			{
				Stage = stage,
				HomeTeamPlaceholder = doc.HomeTeamPlaceholder,
				AwayTeamPlaceholder = doc.AwayTeamPlaceholder
			};
		}
		else
		{
			game = new Game();
		}

		game.Id = doc.Id;
		game.DivisionId = divisionId;
		game.Round = doc.Round;
		game.ScheduledDateTime = doc.ScheduledDateTime;
		game.Location = doc.Location;
		game.Status = Enum.Parse<GameStatus>(doc.Status, ignoreCase: true);
		game.HomeTeamId = doc.HomeTeamId;
		game.AwayTeamId = doc.AwayTeamId;
		game.RescheduledToGameId = doc.RescheduledToGameId;
		game.RescheduledFromGameId = doc.RescheduledFromGameId;
		game.ModifiedAt = doc.ModifiedAt;
		game.ModifiedBy = doc.ModifiedBy;

		if (homeTeam != null)
		{
			game.HomeTeam = homeTeam;
		}

		if (awayTeam != null)
		{
			game.AwayTeam = awayTeam;
		}

		var score = ToScoreEntity(doc);
		if (score != null)
		{
			score.Game = game;
			game.Score = score;
		}

		return game;
	}

	public static GameDocument ToDocument(Game entity)
	{
		var doc = new GameDocument
		{
			Id = entity.Id,
			Round = entity.Round,
			ScheduledDateTime = entity.ScheduledDateTime,
			Location = entity.Location,
			Status = entity.Status.ToString(),
			HomeTeamId = entity.HomeTeamId,
			AwayTeamId = entity.AwayTeamId,
			HomeScore = entity.Score?.HomeScore,
			AwayScore = entity.Score?.AwayScore,
			RescheduledToGameId = entity.RescheduledToGameId,
			RescheduledFromGameId = entity.RescheduledFromGameId,
			ModifiedAt = entity.ModifiedAt,
			ModifiedBy = entity.ModifiedBy
		};

		if (entity is TournamentGame tournamentGame && tournamentGame.Stage != TournamentStage.None)
		{
			doc.TournamentStage = tournamentGame.Stage.ToString();
			doc.HomeTeamPlaceholder = tournamentGame.HomeTeamPlaceholder;
			doc.AwayTeamPlaceholder = tournamentGame.AwayTeamPlaceholder;
		}

		if (entity.Score is TournamentScore tournamentScore)
		{
			doc.IsForfeit = tournamentScore.IsForfeit;
		}

		if (entity.Score is EliminationScore eliminationScore)
		{
			doc.IsDecidedByPenalties = eliminationScore.IsDecidedByPenalties;
			doc.HomePenaltyScore = eliminationScore.HomePenaltyScore;
			doc.AwayPenaltyScore = eliminationScore.AwayPenaltyScore;
		}

		if (entity.Score is PoolPlayScore poolPlayScore)
		{
			doc.HomeRedCards = poolPlayScore.HomeRedCards;
			doc.AwayRedCards = poolPlayScore.AwayRedCards;
			doc.HomeYellowCards = poolPlayScore.HomeYellowCards;
			doc.AwayYellowCards = poolPlayScore.AwayYellowCards;
		}

		return doc;
	}

	public static Score ToEntity(Score? score, Game game)
	{
		if (score == null)
		{
			throw new ArgumentNullException(nameof(score));
		}

		score.GameId = game.Id;
		score.Game = game;
		return score;
	}

	public static Score? ToScoreEntity(GameDocument doc)
	{
		if (!doc.HomeScore.HasValue && !doc.AwayScore.HasValue)
		{
			return null;
		}

		var stage = !string.IsNullOrWhiteSpace(doc.TournamentStage) &&
			Enum.TryParse<TournamentStage>(doc.TournamentStage, ignoreCase: true, out var parsedStage)
				? parsedStage
				: TournamentStage.None;

		Score score;
		if (stage.IsEliminationStage())
		{
			score = new EliminationScore
			{
				IsForfeit = doc.IsForfeit,
				IsDecidedByPenalties = doc.IsDecidedByPenalties,
				HomePenaltyScore = doc.HomePenaltyScore,
				AwayPenaltyScore = doc.AwayPenaltyScore
			};
		}
		else if (stage.IsPoolPlayStage())
		{
			score = new PoolPlayScore
			{
				IsForfeit = doc.IsForfeit,
				HomeRedCards = doc.HomeRedCards,
				AwayRedCards = doc.AwayRedCards,
				HomeYellowCards = doc.HomeYellowCards,
				AwayYellowCards = doc.AwayYellowCards
			};
		}
		else if (stage != TournamentStage.None)
		{
			score = new TournamentScore
			{
				IsForfeit = doc.IsForfeit
			};
		}
		else
		{
			score = new Score();
		}

		score.Id = doc.Id;
		score.GameId = doc.Id;
		score.HomeScore = doc.HomeScore;
		score.AwayScore = doc.AwayScore;
		score.ModifiedAt = doc.ModifiedAt;
		score.ModifiedBy = doc.ModifiedBy;

		return score;
	}

	public static VolunteerPoints ToEntity(VolunteerPointsDocument doc, int id = 0)
	{
		return new VolunteerPoints
		{
			Id = id,
			TeamId = doc.TeamId,
			Round = doc.Round,
			Points = doc.Points,
			Notes = doc.Notes,
			ModifiedAt = doc.ModifiedAt,
			ModifiedBy = doc.ModifiedBy
		};
	}

	public static VolunteerPointsDocument ToDocument(VolunteerPoints entity)
	{
		return new VolunteerPointsDocument
		{
			TeamId = entity.TeamId,
			Round = entity.Round,
			Points = entity.Points,
			Notes = entity.Notes,
			ModifiedAt = entity.ModifiedAt,
			ModifiedBy = entity.ModifiedBy
		};
	}

}
