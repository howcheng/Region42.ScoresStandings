using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Tests.Helpers;

/// <summary>
/// Helper class for creating test data fixtures.
/// </summary>
public static class TestDataBuilder
{
	public static Season CreateSeason(int id = 1, string name = "Fall 2025", bool isActive = true)
	{
		return new Season
		{
			Id = id,
			Name = name,
			Year = 2025,
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static Division CreateDivision(int id = 1, int seasonId = 1, AgeGroup ageGroup = AgeGroup.U12, Gender gender = Gender.Boys, int totalRounds = 10)
	{
		return new Division
		{
			Id = id,
			SeasonId = seasonId,
			AgeGroup = ageGroup,
			Gender = gender,
			TotalRounds = totalRounds,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static Team CreateTeam(int id = 1, int divisionId = 1, string name = "Team 1", string contactName = "Coach Smith", bool isActive = true)
	{
		return new Team
		{
			Id = id,
			DivisionId = divisionId,
			Name = name,
			ShortName = $"T{id}",
			ContactName = contactName,
			ContactEmail = "coach@example.com",
			ContactPhone = "555-1234",
			IsActive = isActive,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static Game CreateGame(int id = 1, int divisionId = 1, int homeTeamId = 1, int awayTeamId = 2, DateTime? scheduledDateTime = null, int round = 1, GameStatus status = GameStatus.Scheduled)
	{
		return new Game
		{
			Id = id,
			DivisionId = divisionId,
			HomeTeamId = homeTeamId,
			AwayTeamId = awayTeamId,
			ScheduledDateTime = scheduledDateTime ?? DateTime.UtcNow.AddDays(7),
			Round = round,
			Location = "Field 1 - Park A",
			Status = status,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static Score CreateScore(int id = 1, int gameId = 1, int homeScore = 2, int awayScore = 1)
	{
		return new Score
		{
			Id = id,
			GameId = gameId,
			HomeScore = homeScore,
			AwayScore = awayScore,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static Score CreateScore(int gameId, int? homeScore, int? awayScore)
	{
		return new Score
		{
			GameId = gameId,
			HomeScore = homeScore,
			AwayScore = awayScore,
			Game = CreateGame(gameId),
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static Score CreateScore(int gameId, int? homeScore, int? awayScore, int divisionId, int round = 1)
	{
		var game = CreateGame(gameId, divisionId, 1, 2, DateTime.UtcNow, round);
		return new Score
		{
			GameId = gameId,
			HomeScore = homeScore,
			AwayScore = awayScore,
			Game = game,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static VolunteerPoints CreateVolunteerPoints(int id = 1, int teamId = 1, int round = 1, int points = 3, string notes = "Volunteer duty completed")
	{
		return new VolunteerPoints
		{
			Id = id,
			TeamId = teamId,
			Round = round,
			Points = points,
			Notes = notes,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "test-user"
		};
	}

	public static User CreateUser(int id = 1, string email = "test@example.com", string displayName = "Test User")
	{
		return new User
		{
			Id = id,
			GoogleId = $"google-{id}",
			Email = email,
			DisplayName = displayName,
			LastLogin = DateTime.UtcNow,
			CreatedAt = DateTime.UtcNow,
			CreatedBy = "system"
		};
	}

	/// <summary>
	/// Creates a valid CSV content string with the specified rows.
	/// </summary>
	public static string CreateCsvContent(params CsvRowData[] rows)
	{
		var csv = new System.Text.StringBuilder();
		csv.AppendLine("Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status");

		foreach (var row in rows)
		{
			csv.AppendLine($"{row.MatchId},{row.EventName},{row.GroupName},{row.HomeTeam},{row.AwayTeam},{row.Date},{row.StartTime},{row.EndTime},{row.Field},{row.Location},{row.HomeCoachFirst},{row.HomeCoachLast},{row.AwayCoachFirst},{row.AwayCoachLast},{row.HomeScore},{row.AwayScore},{row.ScheduledStatus}");
		}

		return csv.ToString();
	}
}

/// <summary>
/// Helper class for building CSV row data.
/// </summary>
public class CsvRowData
{
	public string MatchId { get; set; } = "123456";
	public string EventName { get; set; } = "Region 42 Fall 2025 - 12U - Boys (Games)";
	public string GroupName { get; set; } = "Region 42 Fall 2025 - 12U - Boys (Games)-Group";
	public string HomeTeam { get; set; } = "12UB01";
	public string AwayTeam { get; set; } = "12UB02";
	public string Date { get; set; } = "09/14/2025";
	public string StartTime { get; set; } = "9:00 AM";
	public string EndTime { get; set; } = "10:30 AM";
	public string Field { get; set; } = "Field 1";
	public string Location { get; set; } = "Park A";
	public string HomeCoachFirst { get; set; } = "John";
	public string HomeCoachLast { get; set; } = "Smith";
	public string AwayCoachFirst { get; set; } = "Jane";
	public string AwayCoachLast { get; set; } = "Doe";
	public string HomeScore { get; set; } = "";
	public string AwayScore { get; set; } = "";
	public string ScheduledStatus { get; set; } = "";

	public static CsvRowData CreateGameRow(AgeGroup ageGroup, Gender gender, string homeTeam, string awayTeam)
	{
		var ageGroupStr = ageGroup switch
		{
			AgeGroup.U10 => "10U",
			AgeGroup.U12 => "12U",
			AgeGroup.U14 => "14U",
			_ => "12U"
		};

		var genderStr = gender == Gender.Boys ? "Boys" : "Girls";

		return new CsvRowData
		{
			EventName = $"Region 42 Fall 2025 - {ageGroupStr} - {genderStr} (Games)",
			GroupName = $"Region 42 Fall 2025 - {ageGroupStr} - {genderStr} (Games)-Group",
			HomeTeam = homeTeam,
			AwayTeam = awayTeam
		};
	}

	public static CsvRowData CreatePracticeRow(string teamName)
	{
		return new CsvRowData
		{
			EventName = "Region 42 Fall 2025 - 12U - Girls (Practices)",
			GroupName = "Region 42 Fall 2025 - 12U - Girls (Practices)-Group",
			HomeTeam = teamName,
			AwayTeam = "Practice"
		};
	}
}
