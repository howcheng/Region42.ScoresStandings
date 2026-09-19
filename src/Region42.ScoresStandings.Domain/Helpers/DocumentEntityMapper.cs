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
			DefaultPlayoffSpots = doc.DefaultPlayoffSpots
		};
	}

	public static SettingsDocument ToDocument(Settings entity)
	{
		return new SettingsDocument
		{
			MinVolunteerPointsForPlayoff = entity.MinVolunteerPointsForPlayoff,
			DefaultPlayoffSpots = entity.DefaultPlayoffSpots
		};
	}

	public static Division ToEntity(DivisionDocument doc, int seasonId)
	{
		return new Division
		{
			Id = doc.Id,
			SeasonId = seasonId,
			AgeGroup = DivisionKeyHelper.ParseAgeGroup(doc.AgeGroup),
			Gender = DivisionKeyHelper.ParseGender(doc.Gender),
			TotalRounds = doc.TotalRounds,
			PlayoffSpots = doc.PlayoffSpots,
			ScrimmageRounds = doc.ScrimmageRounds,
			CustomMessage = doc.CustomMessage
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
		var game = new Game
		{
			Id = doc.Id,
			DivisionId = divisionId,
			Round = doc.Round,
			ScheduledDateTime = doc.ScheduledDateTime,
			Location = doc.Location,
			Status = Enum.Parse<GameStatus>(doc.Status, ignoreCase: true),
			HomeTeamId = doc.HomeTeamId,
			AwayTeamId = doc.AwayTeamId,
			ModifiedAt = doc.ModifiedAt,
			ModifiedBy = doc.ModifiedBy
		};

		if (homeTeam != null)
		{
			game.HomeTeam = homeTeam;
		}

		if (awayTeam != null)
		{
			game.AwayTeam = awayTeam;
		}

		if (doc.HomeScore.HasValue || doc.AwayScore.HasValue)
		{
			game.Score = new Score
			{
				GameId = doc.Id,
				HomeScore = doc.HomeScore,
				AwayScore = doc.AwayScore,
				ModifiedAt = doc.ModifiedAt,
				ModifiedBy = doc.ModifiedBy
			};
		}

		return game;
	}

	public static GameDocument ToDocument(Game entity)
	{
		return new GameDocument
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
			ModifiedAt = entity.ModifiedAt,
			ModifiedBy = entity.ModifiedBy
		};
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

		return new Score
		{
			Id = doc.Id,
			GameId = doc.Id,
			HomeScore = doc.HomeScore,
			AwayScore = doc.AwayScore,
			ModifiedAt = doc.ModifiedAt,
			ModifiedBy = doc.ModifiedBy
		};
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
