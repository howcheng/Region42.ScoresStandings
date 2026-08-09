using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Application.Tests.Helpers;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Tests.Services;

/// <summary>
/// Unit tests for StandingsService focusing on standings calculation logic.
/// Tests soccer scoring rules (Win=3, Draw=1, Loss=0), volunteer points, and tie-breakers.
/// </summary>
public class StandingsServiceTests
{
	private readonly Mock<IRepository<Division>> _mockDivisionRepository;
	private readonly Mock<IRepository<Team>> _mockTeamRepository;
	private readonly Mock<IRepository<Game>> _mockGameRepository;
	private readonly Mock<IRepository<Score>> _mockScoreRepository;
	private readonly Mock<IRepository<VolunteerPoints>> _mockVolunteerPointsRepository;
	private readonly Mock<IRepository<Settings>> _mockSettingsRepository;
	private readonly Mock<ILogger<StandingsService>> _mockLogger;
	private readonly StandingsService _service;

	public StandingsServiceTests()
	{
		_mockDivisionRepository = new Mock<IRepository<Division>>();
		_mockTeamRepository = new Mock<IRepository<Team>>();
		_mockGameRepository = new Mock<IRepository<Game>>();
		_mockScoreRepository = new Mock<IRepository<Score>>();
		_mockVolunteerPointsRepository = new Mock<IRepository<VolunteerPoints>>();
		_mockSettingsRepository = new Mock<IRepository<Settings>>();
		_mockLogger = new Mock<ILogger<StandingsService>>();

		_service = new StandingsService(
			_mockDivisionRepository.Object,
			_mockTeamRepository.Object,
			_mockGameRepository.Object,
			_mockScoreRepository.Object,
			_mockVolunteerPointsRepository.Object,
			_mockSettingsRepository.Object,
			_mockLogger.Object
		);
	}

	#region GetCurrentStandingsAsync Tests

	[Fact]
	public async Task GetCurrentStandingsAsync_WithInvalidDivisionId_ThrowsArgumentException()
	{
		// Arrange
		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Division?)null);

		// Act
		var act = async () => await _service.GetCurrentStandingsAsync(999);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Division*999*not found*");
	}

	[Fact]
	public async Task GetCurrentStandingsAsync_WithNoTeams_ReturnsEmptyStandings()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1, seasonId: 1);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(1))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new List<Team>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		result.DivisionId.Should().Be(1);
		result.Standings.Should().BeEmpty();
	}

	[Fact]
	public async Task GetCurrentStandingsAsync_WithNoGames_ReturnsTeamsWithZeroStats()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teams = new List<Team>
		{
			TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A"),
			TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B")
		};

		SetupBasicMocks(division, teams, new List<Game>(), new List<Score>(), new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		result.Standings.Should().HaveCount(2);
		result.Standings.All(s => s.GamesPlayed == 0).Should().BeTrue();
		result.Standings.All(s => s.TotalPoints == 0).Should().BeTrue();
	}

	#endregion

	#region Soccer Scoring Rules Tests

	[Fact]
	public async Task CalculateStandings_WithWin_Awards3Points()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teams = new List<Team> { teamA, teamB };

		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed);
		var games = new List<Game> { game };

		var score = TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 3, awayScore: 1);
		var scores = new List<Score> { score };

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		var teamAStanding = result.Standings.First(s => s.TeamId == 1);
		var teamBStanding = result.Standings.First(s => s.TeamId == 2);

		teamAStanding.Wins.Should().Be(1);
		teamAStanding.GamePoints.Should().Be(3);
		teamAStanding.GoalsFor.Should().Be(3);
		teamAStanding.GoalsAgainst.Should().Be(1);

		teamBStanding.Losses.Should().Be(1);
		teamBStanding.GamePoints.Should().Be(0);
	}

	[Fact]
	public async Task CalculateStandings_WithDraw_Awards1PointToBothTeams()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teams = new List<Team> { teamA, teamB };

		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed);
		var games = new List<Game> { game };

		var score = TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 2, awayScore: 2);
		var scores = new List<Score> { score };

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		var teamAStanding = result.Standings.First(s => s.TeamId == 1);
		var teamBStanding = result.Standings.First(s => s.TeamId == 2);

		teamAStanding.Draws.Should().Be(1);
		teamAStanding.GamePoints.Should().Be(1);
		teamAStanding.TotalPoints.Should().Be(1);

		teamBStanding.Draws.Should().Be(1);
		teamBStanding.GamePoints.Should().Be(1);
		teamBStanding.TotalPoints.Should().Be(1);
	}

	[Fact]
	public async Task CalculateStandings_WithLoss_Awards0Points()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teams = new List<Team> { teamA, teamB };

		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed);
		var games = new List<Game> { game };

		var score = TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 0, awayScore: 2);
		var scores = new List<Score> { score };

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		var teamAStanding = result.Standings.First(s => s.TeamId == 1);
		teamAStanding.Losses.Should().Be(1);
		teamAStanding.GamePoints.Should().Be(0);
		teamAStanding.TotalPoints.Should().Be(0);
	}

	#endregion

	#region Volunteer Points Tests

	[Fact]
	public async Task CalculateStandings_AddsVolunteerPointsToGamePoints()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teams = new List<Team> { teamA };

		var volunteerPoints = new List<VolunteerPoints>
		{
			TestDataBuilder.CreateVolunteerPoints(id: 1, teamId: 1, round: 1, points: 5)
		};

		SetupBasicMocks(division, teams, new List<Game>(), new List<Score>(), volunteerPoints);

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		var standing = result.Standings.First();
		standing.VolunteerPoints.Should().Be(5);
		standing.GamePoints.Should().Be(0); // No games
		standing.TotalPoints.Should().Be(5); // Only volunteer points
	}

	[Fact]
	public async Task CalculateStandings_CombinesGamePointsAndVolunteerPoints()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teams = new List<Team> { teamA, teamB };

		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed);
		var games = new List<Game> { game };

		var score = TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 2, awayScore: 0);
		var scores = new List<Score> { score };

		var volunteerPoints = new List<VolunteerPoints>
		{
			TestDataBuilder.CreateVolunteerPoints(id: 1, teamId: 1, round: 1, points: 2)
		};

		SetupBasicMocks(division, teams, games, scores, volunteerPoints);

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		var teamAStanding = result.Standings.First(s => s.TeamId == 1);
		teamAStanding.GamePoints.Should().Be(3); // Win
		teamAStanding.VolunteerPoints.Should().Be(2);
		teamAStanding.TotalPoints.Should().Be(5); // 3 + 2
	}

	#endregion

	#region Sorting and Tie-Breaking Tests

	[Fact]
	public async Task CalculateStandings_SortsByTotalPointsDescending()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teamC = TestDataBuilder.CreateTeam(id: 3, divisionId: 1, name: "Team C");
		var teams = new List<Team> { teamA, teamB, teamC };

		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed),
			TestDataBuilder.CreateGame(id: 2, divisionId: 1, homeTeamId: 2, awayTeamId: 3, round: 1, status: GameStatus.Completed)
		};

		var scores = new List<Score>
		{
			TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 3, awayScore: 0), // Team A wins
			TestDataBuilder.CreateScore(id: 2, gameId: 2, homeScore: 1, awayScore: 1)  // Draw
		};

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		result.Standings[0].TeamName.Should().Be("Team A"); // 3 points
		result.Standings[0].Rank.Should().Be(1);
		result.Standings[1].TotalPoints.Should().Be(1); // Team B or C with 1 point
		result.Standings[2].TotalPoints.Should().Be(1); // Team B or C with 1 point
	}

	[Fact]
	public async Task CalculateStandings_TieBreaker_UseGoalDifferential()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teamC = TestDataBuilder.CreateTeam(id: 3, divisionId: 1, name: "Team C");
		var teams = new List<Team> { teamA, teamB, teamC };

		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 3, round: 1, status: GameStatus.Completed),
			TestDataBuilder.CreateGame(id: 2, divisionId: 1, homeTeamId: 2, awayTeamId: 3, round: 1, status: GameStatus.Completed)
		};

		var scores = new List<Score>
		{
			TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 3, awayScore: 0), // Team A wins, +3 GD
			TestDataBuilder.CreateScore(id: 2, gameId: 2, homeScore: 2, awayScore: 1)  // Team B wins, +1 GD
		};

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert - Both have 3 points, Team A should be first due to better GD
		result.Standings[0].TeamName.Should().Be("Team A");
		result.Standings[0].GoalDifferential.Should().Be(3);
		result.Standings[1].TeamName.Should().Be("Team B");
		result.Standings[1].GoalDifferential.Should().Be(1);
	}

	[Fact]
	public async Task CalculateStandings_TieBreaker_UseGoalsScoredWhenGDEqual()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teamC = TestDataBuilder.CreateTeam(id: 3, divisionId: 1, name: "Team C");
		var teamD = TestDataBuilder.CreateTeam(id: 4, divisionId: 1, name: "Team D");
		var teams = new List<Team> { teamA, teamB, teamC, teamD };

		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 3, round: 1, status: GameStatus.Completed),
			TestDataBuilder.CreateGame(id: 2, divisionId: 1, homeTeamId: 2, awayTeamId: 4, round: 1, status: GameStatus.Completed)
		};

		var scores = new List<Score>
		{
			TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 3, awayScore: 1), // Team A: +2 GD, 3 GF
			TestDataBuilder.CreateScore(id: 2, gameId: 2, homeScore: 2, awayScore: 0)  // Team B: +2 GD, 2 GF
		};

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert - Both have 3 points and +2 GD, Team A should be first due to more goals scored
		result.Standings[0].TeamName.Should().Be("Team A");
		result.Standings[0].GoalsFor.Should().Be(3);
		result.Standings[1].TeamName.Should().Be("Team B");
		result.Standings[1].GoalsFor.Should().Be(2);
	}

	#endregion

	#region Point-in-Time Tests

	[Fact]
	public async Task GetStandingsByRoundAsync_OnlyIncludesGamesUpToSpecifiedRound()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1, totalRounds: 10);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teams = new List<Team> { teamA, teamB };

		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed),
			TestDataBuilder.CreateGame(id: 2, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 2, status: GameStatus.Completed),
			TestDataBuilder.CreateGame(id: 3, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 3, status: GameStatus.Completed)
		};

		var scores = new List<Score>
		{
			TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 1, awayScore: 0), // Round 1: Team A wins
			TestDataBuilder.CreateScore(id: 2, gameId: 2, homeScore: 0, awayScore: 1), // Round 2: Team B wins
			TestDataBuilder.CreateScore(id: 3, gameId: 3, homeScore: 1, awayScore: 0)  // Round 3: Team A wins
		};

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetStandingsByRoundAsync(1, throughRound: 2);

		// Assert
		result.ThroughRound.Should().Be(2);
		var teamAStanding = result.Standings.First(s => s.TeamId == 1);
		var teamBStanding = result.Standings.First(s => s.TeamId == 2);

		teamAStanding.GamesPlayed.Should().Be(2); // Only rounds 1 and 2
		teamAStanding.Wins.Should().Be(1);
		teamAStanding.Losses.Should().Be(1);
		teamAStanding.TotalPoints.Should().Be(3);

		teamBStanding.GamesPlayed.Should().Be(2);
		teamBStanding.Wins.Should().Be(1);
		teamBStanding.Losses.Should().Be(1);
		teamBStanding.TotalPoints.Should().Be(3);
	}

	[Fact]
	public async Task GetStandingsByRoundAsync_OnlyIncludesVolunteerPointsUpToSpecifiedRound()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1, totalRounds: 10);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teams = new List<Team> { teamA };

		var volunteerPoints = new List<VolunteerPoints>
		{
			TestDataBuilder.CreateVolunteerPoints(id: 1, teamId: 1, round: 1, points: 2),
			TestDataBuilder.CreateVolunteerPoints(id: 2, teamId: 1, round: 2, points: 3),
			TestDataBuilder.CreateVolunteerPoints(id: 3, teamId: 1, round: 3, points: 1)
		};

		SetupBasicMocks(division, teams, new List<Game>(), new List<Score>(), volunteerPoints);

		// Act
		var result = await _service.GetStandingsByRoundAsync(1, throughRound: 2);

		// Assert
		var standing = result.Standings.First();
		standing.VolunteerPoints.Should().Be(5); // Only rounds 1 and 2 (2 + 3)
	}

	[Fact]
	public async Task GetStandingsByRoundAsync_WithInvalidRound_ThrowsArgumentException()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1, totalRounds: 10);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(1))
			.ReturnsAsync(division);

		// Act
		var actNegative = async () => await _service.GetStandingsByRoundAsync(1, throughRound: -1);
		var actTooHigh = async () => await _service.GetStandingsByRoundAsync(1, throughRound: 11);

		// Assert
		await actNegative.Should().ThrowAsync<ArgumentException>();
		await actTooHigh.Should().ThrowAsync<ArgumentException>();
	}

	#endregion

	#region Points Per Game (Odd Teams) Tests

	[Fact]
	public async Task CalculateStandings_WithDifferentGamesPlayed_CalculatesPointsPerGame()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(id: 1);
		var teamA = TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "Team A");
		var teamB = TestDataBuilder.CreateTeam(id: 2, divisionId: 1, name: "Team B");
		var teamC = TestDataBuilder.CreateTeam(id: 3, divisionId: 1, name: "Team C");
		var teams = new List<Team> { teamA, teamB, teamC };

		var games = new List<Game>
		{
			// Team A plays 2 games
			TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, round: 1, status: GameStatus.Completed),
			TestDataBuilder.CreateGame(id: 2, divisionId: 1, homeTeamId: 1, awayTeamId: 3, round: 2, status: GameStatus.Completed),
			// Team B plays 1 game
			// (already counted above)
			// Team C plays 1 game
			// (already counted above)
		};

		var scores = new List<Score>
		{
			TestDataBuilder.CreateScore(id: 1, gameId: 1, homeScore: 3, awayScore: 0), // Team A wins
			TestDataBuilder.CreateScore(id: 2, gameId: 2, homeScore: 3, awayScore: 0)  // Team A wins
		};

		SetupBasicMocks(division, teams, games, scores, new List<VolunteerPoints>());

		// Act
		var result = await _service.GetCurrentStandingsAsync(1);

		// Assert
		var teamAStanding = result.Standings.First(s => s.TeamId == 1);
		var teamBStanding = result.Standings.First(s => s.TeamId == 2);

		teamAStanding.GamesPlayed.Should().Be(2);
		teamAStanding.TotalPoints.Should().Be(6);
		teamAStanding.PointsPerGame.Should().Be(3.0m); // 6 / 2

		teamBStanding.GamesPlayed.Should().Be(1);
		teamBStanding.TotalPoints.Should().Be(0);
		teamBStanding.PointsPerGame.Should().Be(0);
	}

	#endregion

	#region GetStandingsBySeasonAsync Tests

	[Fact]
	public async Task GetStandingsBySeasonAsync_ReturnsStandingsForAllDivisions()
	{
		// Arrange
		var division1 = TestDataBuilder.CreateDivision(id: 1, seasonId: 1, ageGroup: AgeGroup.U10, gender: Gender.Boys);
		var division2 = TestDataBuilder.CreateDivision(id: 2, seasonId: 1, ageGroup: AgeGroup.U12, gender: Gender.Girls);

		_mockDivisionRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division1, division2 });

		// Setup for division 1
		_mockDivisionRepository.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(division1);
		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
			{
				var teams = new List<Team>
				{
					TestDataBuilder.CreateTeam(id: 1, divisionId: 1, name: "10UB01"),
					TestDataBuilder.CreateTeam(id: 2, divisionId: 2, name: "12UG01")
				};
				return teams.Where(predicate.Compile()).ToList();
			});

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync(new List<Game>());

		_mockScoreRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Score>());
		_mockVolunteerPointsRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<VolunteerPoints>());

		// Setup for division 2
		_mockDivisionRepository.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(division2);

		// Act
		var results = (await _service.GetStandingsBySeasonAsync(1)).ToList();

		// Assert
		results.Should().HaveCount(2);
		results.Should().Contain(r => r.DivisionId == 1);
		results.Should().Contain(r => r.DivisionId == 2);
	}

	#endregion

	#region Helper Methods

	private void SetupBasicMocks(
		Division division,
		List<Team> teams,
		List<Game> games,
		List<Score> scores,
		List<VolunteerPoints> volunteerPoints)
	{
		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(teams);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				games.Where(predicate.Compile()).ToList());

		_mockScoreRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(scores);

		_mockVolunteerPointsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(volunteerPoints);
	}

	#endregion

	#region Playoff Qualification Tests

	[Fact]
	public async Task GetCurrentStandingsAsync_CalculatesPlayoffQualification_WhenSettingsExist()
	{
		// Arrange
		var season = TestDataBuilder.CreateSeason(1, "Fall 2024");
		var division = TestDataBuilder.CreateDivision(1, season.Id, AgeGroup.U10, Gender.Boys, 10);
		division.PlayoffSpots = 2;

		var team1 = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var team2 = TestDataBuilder.CreateTeam(2, division.Id, "Team B");
		var team3 = TestDataBuilder.CreateTeam(3, division.Id, "Team C");

		var teams = new List<Team> { team1, team2, team3 };

		// Team 1: 3 wins (9 pts), 5 volunteer points = 14 total, qualifies
		var game1a = TestDataBuilder.CreateGame(1, division.Id, team1.Id, 999, null, 1, GameStatus.Completed);
		var game1b = TestDataBuilder.CreateGame(2, division.Id, team1.Id, 999, null, 2, GameStatus.Completed);
		var game1c = TestDataBuilder.CreateGame(3, division.Id, team1.Id, 999, null, 3, GameStatus.Completed);

		var score1a = TestDataBuilder.CreateScore(1, game1a.Id, 2, 0);
		var score1b = TestDataBuilder.CreateScore(2, game1b.Id, 2, 0);
		var score1c = TestDataBuilder.CreateScore(3, game1c.Id, 2, 0);

		var vp1 = TestDataBuilder.CreateVolunteerPoints(1, team1.Id, 1, 5, "Coach");

		// Team 2: 2 wins (6 pts), 3 volunteer points = 9 total, qualifies
		var game2a = TestDataBuilder.CreateGame(4, division.Id, team2.Id, 999, null, 1, GameStatus.Completed);
		var game2b = TestDataBuilder.CreateGame(5, division.Id, team2.Id, 999, null, 2, GameStatus.Completed);

		var score2a = TestDataBuilder.CreateScore(4, game2a.Id, 2, 0);
		var score2b = TestDataBuilder.CreateScore(5, game2b.Id, 2, 0);

		var vp2 = TestDataBuilder.CreateVolunteerPoints(2, team2.Id, 1, 3, "Coach");

		// Team 3: 1 win (3 pts), 2 volunteer points = 5 total, does not qualify (needs more vp)
		var game3a = TestDataBuilder.CreateGame(6, division.Id, team3.Id, 999, null, 1, GameStatus.Completed);

		var score3a = TestDataBuilder.CreateScore(6, game3a.Id, 2, 0);

		var vp3 = TestDataBuilder.CreateVolunteerPoints(3, team3.Id, 1, 2, "Coach");

		var allGames = new List<Game> { game1a, game1b, game1c, game2a, game2b, game3a };
		var allScores = new List<Score> { score1a, score1b, score1c, score2a, score2b, score3a };
		var allVp = new List<VolunteerPoints> { vp1, vp2, vp3 };

		var settings = new Settings
		{
			Id = 1,
			MinVolunteerPointsForPlayoff = 3,
			DefaultPlayoffSpots = 1
		};

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(teams);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				allGames.Where(predicate.Compile()).ToList());

		_mockScoreRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(allScores);

		_mockVolunteerPointsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(allVp);

		_mockSettingsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Settings> { settings });

		// Act
		var result = await _service.GetCurrentStandingsAsync(division.Id);

		// Assert
		result.Should().NotBeNull();
		result.Standings.Should().HaveCount(3);

		var team1Standing = result.Standings.First(s => s.TeamId == team1.Id);
		team1Standing.QualifiesForPlayoffs.Should().BeTrue();
		team1Standing.PlayoffQualificationNote.Should().Be("Clinched playoff spot");

		var team2Standing = result.Standings.First(s => s.TeamId == team2.Id);
		team2Standing.QualifiesForPlayoffs.Should().BeTrue();
		team2Standing.PlayoffQualificationNote.Should().Be("Clinched playoff spot");

		var team3Standing = result.Standings.First(s => s.TeamId == team3.Id);
		team3Standing.QualifiesForPlayoffs.Should().BeFalse();
		team3Standing.PlayoffQualificationNote.Should().Be("Needs 1 more volunteer point and must improve standing");
	}

	[Fact]
	public async Task GetCurrentStandingsAsync_ShowsNeedsVolunteerPoints_WhenTeamInPlayoffPositionButInsufficientPoints()
	{
		// Arrange
		var season = TestDataBuilder.CreateSeason(1, "Fall 2024");
		var division = TestDataBuilder.CreateDivision(1, season.Id, AgeGroup.U12, Gender.Girls, 10);
		division.PlayoffSpots = 2;

		var team1 = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var team2 = TestDataBuilder.CreateTeam(2, division.Id, "Team B");

		var teams = new List<Team> { team1, team2 };

		// Team 1: 3 wins (9 pts), 5 volunteer points = 14 total, qualifies
		var game1 = TestDataBuilder.CreateGame(1, division.Id, team1.Id, 999, null, 1, GameStatus.Completed);
		var score1 = TestDataBuilder.CreateScore(1, game1.Id, 2, 0);
		var vp1 = TestDataBuilder.CreateVolunteerPoints(1, team1.Id, 1, 5, "Coach");

		// Team 2: 2 wins (6 pts), 1 volunteer point = 7 total, in playoff position but needs more volunteer points
		var game2 = TestDataBuilder.CreateGame(2, division.Id, team2.Id, 999, null, 1, GameStatus.Completed);
		var score2 = TestDataBuilder.CreateScore(2, game2.Id, 2, 0);
		var vp2 = TestDataBuilder.CreateVolunteerPoints(2, team2.Id, 1, 1, "Coach");

		var allGames = new List<Game> { game1, game2 };
		var allScores = new List<Score> { score1, score2 };
		var allVp = new List<VolunteerPoints> { vp1, vp2 };

		var settings = new Settings
		{
			Id = 1,
			MinVolunteerPointsForPlayoff = 3,
			DefaultPlayoffSpots = 1
		};

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(teams);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				allGames.Where(predicate.Compile()).ToList());

		_mockScoreRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(allScores);

		_mockVolunteerPointsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(allVp);

		_mockSettingsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Settings> { settings });

		// Act
		var result = await _service.GetCurrentStandingsAsync(division.Id);

		// Assert
		var team2Standing = result.Standings.First(s => s.TeamId == team2.Id);
		team2Standing.QualifiesForPlayoffs.Should().BeFalse();
		team2Standing.PlayoffQualificationNote.Should().Be("Needs 2 more volunteer points to qualify");
	}

	[Fact]
	public async Task GetCurrentStandingsAsync_ShowsEliminatedFromPlayoffs_WhenTeamHasPointsButOutOfPosition()
	{
		// Arrange
		var season = TestDataBuilder.CreateSeason(1, "Fall 2024");
		var division = TestDataBuilder.CreateDivision(1, season.Id, AgeGroup.U14, Gender.Boys, 10);
		division.PlayoffSpots = 1;

		var team1 = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var team2 = TestDataBuilder.CreateTeam(2, division.Id, "Team B");

		var teams = new List<Team> { team1, team2 };

		// Team 1: 3 wins (9 pts), 5 volunteer points = 14 total, qualifies
		var game1 = TestDataBuilder.CreateGame(1, division.Id, team1.Id, 999, null, 1, GameStatus.Completed);
		var score1 = TestDataBuilder.CreateScore(1, game1.Id, 2, 0);
		var vp1 = TestDataBuilder.CreateVolunteerPoints(1, team1.Id, 1, 5, "Coach");

		// Team 2: 2 wins (6 pts), 5 volunteer points = 11 total, has volunteer points but eliminated
		var game2 = TestDataBuilder.CreateGame(2, division.Id, team2.Id, 999, null, 1, GameStatus.Completed);
		var score2 = TestDataBuilder.CreateScore(2, game2.Id, 2, 0);
		var vp2 = TestDataBuilder.CreateVolunteerPoints(2, team2.Id, 1, 5, "Coach");

		var allGames = new List<Game> { game1, game2 };
		var allScores = new List<Score> { score1, score2 };
		var allVp = new List<VolunteerPoints> { vp1, vp2 };

		var settings = new Settings
		{
			Id = 1,
			MinVolunteerPointsForPlayoff = 3,
			DefaultPlayoffSpots = 1
		};

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(teams);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				allGames.Where(predicate.Compile()).ToList());

		_mockScoreRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(allScores);

		_mockVolunteerPointsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(allVp);

		_mockSettingsRepository
			.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Settings> { settings });

		// Act
		var result = await _service.GetCurrentStandingsAsync(division.Id);

		// Assert
		var team2Standing = result.Standings.First(s => s.TeamId == team2.Id);
		team2Standing.QualifiesForPlayoffs.Should().BeFalse();
		team2Standing.PlayoffQualificationNote.Should().Be("Eliminated from playoffs");
	}

	#endregion
}
