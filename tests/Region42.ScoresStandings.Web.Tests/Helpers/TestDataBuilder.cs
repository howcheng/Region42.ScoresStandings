using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Web.Tests.Helpers;

/// <summary>
/// Test data builder for creating test entities with sensible defaults.
/// Provides fluent API for customizing test data.
/// </summary>
public class TestDataBuilder
{
	private int _nextId = 1;
	private DateTime _baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	public Season BuildSeason(string name = "Fall 2026", bool isActive = true)
	{
		return new Season
		{
			Id = _nextId++,
			Name = name,
			Year = 2026,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};
	}

	public Division BuildDivision(int seasonId, AgeGroup ageGroup = AgeGroup.U12, Gender gender = Gender.Boys, int totalRounds = 10)
	{
		return new Division
		{
			Id = _nextId++,
			SeasonId = seasonId,
			AgeGroup = ageGroup,
			Gender = gender,
			TotalRounds = totalRounds,
			PlayoffSpots = 1,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};
	}

	public Team BuildTeam(int divisionId, string name = "Test Team", bool isActive = true)
	{
		return new Team
		{
			Id = _nextId++,
			Name = name,
			ShortName = name.Length > 10 ? name.Substring(0, 10) : name,
			DivisionId = divisionId,
			ContactName = "Test Contact",
			ContactEmail = "test@test.com",
			ContactPhone = "555-1234",
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};
	}

	public Game BuildGame(int divisionId, int homeTeamId, int awayTeamId, int round = 1, GameStatus status = GameStatus.Scheduled)
	{
		return new Game
		{
			Id = _nextId++,
			DivisionId = divisionId,
			HomeTeamId = homeTeamId,
			AwayTeamId = awayTeamId,
			ScheduledDateTime = _baseDate.AddDays(round * 7),
			Round = round,
			Location = "Test Field",
			Status = status,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};
	}

	public Game BuildGameWithScore(int homeTeamId, int awayTeamId, int divisionId, int round, int? homeScore = null, int? awayScore = null)
	{
		var homeTeam = new Team { Id = homeTeamId, Name = $"Home Team {homeTeamId}" };
		var awayTeam = new Team { Id = awayTeamId, Name = $"Away Team {awayTeamId}" };

		var game = new Game
		{
			Id = _nextId++,
			DivisionId = divisionId,
			HomeTeamId = homeTeamId,
			HomeTeam = homeTeam,
			AwayTeamId = awayTeamId,
			AwayTeam = awayTeam,
			ScheduledDateTime = _baseDate.AddDays(round * 7),
			Round = round,
			Location = "Test Field",
			Status = homeScore.HasValue && awayScore.HasValue ? GameStatus.Completed : GameStatus.Scheduled,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};

		if (homeScore.HasValue && awayScore.HasValue)
		{
			game.Score = new Score
			{
				Id = _nextId++,
				GameId = game.Id,
				HomeScore = homeScore.Value,
				AwayScore = awayScore.Value,
				CreatedAt = DateTime.UtcNow,
				ModifiedAt = DateTime.UtcNow,
				CreatedBy = "test",
				ModifiedBy = "test"
			};
		}

		return game;
	}

	public Score BuildScore(int gameId, int homeScore, int awayScore)
	{
		return new Score
		{
			Id = _nextId++,
			GameId = gameId,
			HomeScore = homeScore,
			AwayScore = awayScore,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};
	}

	public VolunteerPoints BuildVolunteerPoints(int teamId, int round, int points = 0)
	{
		return new VolunteerPoints
		{
			Id = _nextId++,
			TeamId = teamId,
			Round = round,
			Points = points,
			Notes = string.Empty,
			CreatedAt = DateTime.UtcNow,
			ModifiedAt = DateTime.UtcNow,
			CreatedBy = "test",
			ModifiedBy = "test"
		};
	}

	public void ResetIds()
	{
		_nextId = 1;
	}
}
