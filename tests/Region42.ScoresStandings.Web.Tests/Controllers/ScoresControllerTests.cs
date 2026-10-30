using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Controllers;
using Region42.ScoresStandings.Web.Tests.Helpers;
using System.Linq.Expressions;

namespace Region42.ScoresStandings.Web.Tests.Controllers;

public class ScoresControllerTests
{
	private readonly Mock<IScoreService> _mockScoreService;
	private readonly Mock<IGameService> _mockGameService;
	private readonly Mock<ITeamService> _mockTeamService;
	private readonly Mock<IRepository<Division>> _mockDivisionRepo;
	private readonly Mock<IRepository<Season>> _mockSeasonRepo;
	private readonly Mock<IRepository<Score>> _mockScoreRepo;
	private readonly Mock<ILogger<ScoresController>> _mockLogger;
	private readonly ScoresController _controller;
	private readonly TestDataBuilder _builder;

	public ScoresControllerTests()
	{
		_mockScoreService = new Mock<IScoreService>();
		_mockGameService = new Mock<IGameService>();
		_mockTeamService = new Mock<ITeamService>();
		_mockDivisionRepo = new Mock<IRepository<Division>>();
		_mockSeasonRepo = new Mock<IRepository<Season>>();
		_mockScoreRepo = new Mock<IRepository<Score>>();
		_mockLogger = new Mock<ILogger<ScoresController>>();
		_controller = new ScoresController(
			_mockScoreService.Object,
			_mockGameService.Object,
			_mockTeamService.Object,
			_mockDivisionRepo.Object,
			_mockSeasonRepo.Object,
			_mockScoreRepo.Object,
			_mockLogger.Object);
		_builder = new TestDataBuilder();

		ControllerTestHelper.SetupControllerContext(_controller, "testuser");
	}

	[Fact]
	public void Controller_ShouldHaveAuthorizeAttribute()
	{
		// Arrange & Act
		var authorizeAttributes = typeof(ScoresController)
			.GetCustomAttributes(typeof(AuthorizeAttribute), true);

		// Assert
		authorizeAttributes.Should().NotBeEmpty();
		var authorizeAttr = authorizeAttributes.First() as AuthorizeAttribute;
		authorizeAttr!.Policy.Should().Be("AdminPolicy");
	}

	[Fact]
	public async Task Entry_WithNoActiveSeason_ReturnsEmptyList()
	{
		// Arrange
		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season>());

		// Act
		var result = await _controller.Entry(null, null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as List<ScoreEntryDto>;
		model.Should().BeEmpty();
	}

	[Fact]
	public async Task Entry_WithDivisionAndRound_ReturnsScoreEntries()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);
		var team1 = _builder.BuildTeam(division.Id, "Team A");
		var team2 = _builder.BuildTeam(division.Id, "Team B");
		var game = _builder.BuildGame(division.Id, team1.Id, team2.Id, 1);
		game.HomeTeam = team1;
		game.AwayTeam = team2;

		var score = _builder.BuildScore(game.Id, 2, 1);

		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 1))
			.ReturnsAsync(new List<Game> { game });
		_mockTeamService.Setup(s => s.GetTeamsByDivisionAsync(division.Id))
			.ReturnsAsync(new List<Team> { team1, team2 });
		_mockScoreService.Setup(s => s.GetScoreByGameIdAsync(game.Id))
			.ReturnsAsync(score);

		// Act
		var result = await _controller.Entry(division.Id, 1);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as List<ScoreEntryDto>;
		model.Should().HaveCount(1);
		model![0].GameId.Should().Be(game.Id);
		model[0].HomeTeamName.Should().Be("Team A");
		model[0].AwayTeamName.Should().Be("Team B");
		model[0].HomeScore.Should().Be(2);
		model[0].AwayScore.Should().Be(1);
	}

	[Fact]
	public async Task Entry_Post_WithValidScores_SavesAndRedirects()
	{
		// Arrange
		var game1 = _builder.BuildGame(1, 1, 2, 1);
		game1.Id = 1;
		var game2 = _builder.BuildGame(1, 3, 4, 1);
		game2.Id = 2;

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto { GameId = 1, HomeTeamId = 1, AwayTeamId = 2, HomeScore = 3, AwayScore = 1 },
			new ScoreUpdateDto { GameId = 2, HomeTeamId = 3, AwayTeamId = 4, HomeScore = 2, AwayScore = 2 }
		};

		_mockGameService.Setup(s => s.GetGameByIdAsync(1))
			.ReturnsAsync(game1);
		_mockGameService.Setup(s => s.GetGameByIdAsync(2))
			.ReturnsAsync(game2);
		_mockScoreService.Setup(s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
			.ReturnsAsync(new Score());

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		var redirectResult = result as RedirectToActionResult;
		redirectResult!.ActionName.Should().Be("Entry");
		redirectResult.RouteValues!["divisionId"].Should().Be(1);
		redirectResult.RouteValues["round"].Should().Be(1);

		_mockScoreService.Verify(
			s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
			Times.Exactly(2));

		_controller.TempData["SuccessMessage"].Should().NotBeNull();
	}

	[Fact]
	public async Task Entry_Post_WithEmptyList_RedirectsWithError()
	{
		// Arrange
		var scores = new List<ScoreUpdateDto>();

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();
	}

	[Fact]
	public async Task Entry_Post_WithServiceException_ContinuesAndLogsError()
	{
		// Arrange
		var game1 = _builder.BuildGame(1, 1, 2, 1);
		game1.Id = 1;
		var game2 = _builder.BuildGame(1, 3, 4, 1);
		game2.Id = 2;

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto { GameId = 1, HomeTeamId = 1, AwayTeamId = 2, HomeScore = 2, AwayScore = 1 },
			new ScoreUpdateDto { GameId = 2, HomeTeamId = 3, AwayTeamId = 4, HomeScore = 1, AwayScore = 1 }
		};

		_mockGameService.Setup(s => s.GetGameByIdAsync(1))
			.ReturnsAsync(game1);
		_mockGameService.Setup(s => s.GetGameByIdAsync(2))
			.ReturnsAsync(game2);
		_mockScoreService.Setup(s => s.EnterOrUpdateScoreAsync(1, It.IsAny<int>(), It.IsAny<int>()))
			.ThrowsAsync(new Exception("Database error"));
		_mockScoreService.Setup(s => s.EnterOrUpdateScoreAsync(2, It.IsAny<int>(), It.IsAny<int>()))
			.ReturnsAsync(new Score());

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["SuccessMessage"].Should().NotBeNull();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();

		// Verify error was logged
		_mockLogger.Verify(
			x => x.Log(
				LogLevel.Error,
				It.IsAny<EventId>(),
				It.Is<It.IsAnyType>((v, t) => true),
				It.IsAny<Exception>(),
				It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
			Times.Once);
	}

	[Fact]
	public async Task Delete_WithValidGameId_DeletesScoreAndRedirects()
	{
		// Arrange
		_mockScoreService.Setup(s => s.DeleteScoreAsync(1))
			.ReturnsAsync(true);

		// Act
		var result = await _controller.Delete(1, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["SuccessMessage"].Should().NotBeNull();
		_mockScoreService.Verify(s => s.DeleteScoreAsync(1), Times.Once);
	}

	[Fact]
	public async Task Delete_WhenScoreNotFound_RedirectsWithError()
	{
		// Arrange
		_mockScoreService.Setup(s => s.DeleteScoreAsync(1))
			.ReturnsAsync(false);

		// Act
		var result = await _controller.Delete(1, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();
	}

	[Fact]
	public async Task Entry_Post_WithClearedScores_DeletesExistingScore()
	{
		// Arrange - score was entered for the wrong game; user blanks both scores and sets status back to Scheduled
		var game = _builder.BuildGame(1, 1, 2, 1);
		game.Id = 1;
		game.Status = GameStatus.Completed;
		game.Score = new Score
		{
			GameId = game.Id,
			HomeScore = 3,
			AwayScore = 1
		};

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto
			{
				GameId = 1,
				HomeTeamId = 1,
				AwayTeamId = 2,
				HomeScore = null,
				AwayScore = null,
				Status = GameStatus.Scheduled
			}
		};

		_mockGameService.Setup(s => s.GetGameByIdAsync(1))
			.ReturnsAsync(game);
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(1, 1))
			.ReturnsAsync(new List<Game> { game });
		_mockGameService.Setup(s => s.UpdateGameAsync(It.IsAny<Game>()))
			.ReturnsAsync((Game updated) => updated);
		_mockScoreService.Setup(s => s.DeleteScoreAsync(1))
			.ReturnsAsync(true);

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["SuccessMessage"].Should().NotBeNull();
		_controller.TempData["ErrorMessage"].Should().BeNull();
		_mockScoreService.Verify(s => s.DeleteScoreAsync(1), Times.Once);
		_mockScoreService.Verify(
			s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
			Times.Never);
		_mockGameService.Verify(s => s.UpdateGameAsync(It.Is<Game>(g => g.Status == GameStatus.Scheduled)), Times.Once);
	}

	[Fact]
	public async Task Entry_Post_WithPartialScore_ReturnsError()
	{
		// Arrange - One game with only home score, another with only away score
		var game1 = _builder.BuildGame(1, 1, 2, 1);
		game1.Id = 1;
		var game2 = _builder.BuildGame(1, 3, 4, 1);
		game2.Id = 2;

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto { GameId = 1, HomeTeamId = 1, AwayTeamId = 2, HomeScore = 3, AwayScore = null }, // Partial
			new ScoreUpdateDto { GameId = 2, HomeTeamId = 3, AwayTeamId = 4, HomeScore = null, AwayScore = 2 }  // Partial
		};

		_mockGameService.Setup(s => s.GetGameByIdAsync(1))
			.ReturnsAsync(game1);
		_mockGameService.Setup(s => s.GetGameByIdAsync(2))
			.ReturnsAsync(game2);

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();
		var errorMessage = _controller.TempData["ErrorMessage"] as string;
		errorMessage.Should().Contain("Both home and away scores must be entered");

		// Verify no scores were saved
		_mockScoreService.Verify(
			s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
			Times.Never);
	}

	[Fact]
	public async Task Entry_Post_WithDuplicateTeam_ReturnsError()
	{
		// Arrange - Team 1 appears in two games
		var team1 = _builder.BuildTeam(1, "Team A");
		team1.Id = 1;
		var team2 = _builder.BuildTeam(1, "Team B");
		team2.Id = 2;

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto { GameId = 1, HomeTeamId = 1, AwayTeamId = 2, HomeScore = 2, AwayScore = 1 },
			new ScoreUpdateDto { GameId = 2, HomeTeamId = 1, AwayTeamId = 3, HomeScore = 3, AwayScore = 0 } // Team 1 again!
		};

		_mockTeamService.Setup(s => s.GetTeamsByDivisionAsync(1))
			.ReturnsAsync(new List<Team> { team1, team2 });

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();
		var errorMessage = _controller.TempData["ErrorMessage"] as string;
		errorMessage.Should().Contain("appear more than once");
		errorMessage.Should().Contain("Team A");

		// Verify no scores were saved
		_mockScoreService.Verify(
			s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
			Times.Never);
	}

	[Fact]
	public async Task Entry_Post_WithTeamPlayingItself_ReturnsError()
	{
		// Arrange - Team 1 plays against itself
		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto { GameId = 1, HomeTeamId = 1, AwayTeamId = 1, HomeScore = 2, AwayScore = 1 }
		};

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();
		var errorMessage = _controller.TempData["ErrorMessage"] as string;
		errorMessage.Should().Contain("cannot play against itself");

		// Verify no scores were saved
		_mockScoreService.Verify(
			s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
			Times.Never);
	}

	[Fact]
	public async Task Entry_Post_WithRescheduledGame_KeepsOriginalAndCreatesMakeupGameInWeekRound()
	{
		// Arrange
		var originalGame = _builder.BuildGame(1, 1, 2, 1);
		originalGame.Id = 10;
		originalGame.ScheduledDateTime = new DateTime(2026, 8, 29, 15, 0, 0, DateTimeKind.Utc);
		originalGame.Location = "Field 1 - Deerhill Park";

		var round8Game = _builder.BuildGame(1, 3, 4, 8);
		round8Game.Id = 80;
		round8Game.ScheduledDateTime = new DateTime(2026, 10, 24, 15, 0, 0, DateTimeKind.Utc);

		_mockGameService.Setup(s => s.GetGameByIdAsync(10))
			.ReturnsAsync(originalGame);
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(1, 1))
			.ReturnsAsync(new List<Game> { originalGame });
		_mockGameService.Setup(s => s.GetGamesByDivisionAsync(1))
			.ReturnsAsync(new List<Game> { originalGame, round8Game });

		Game? createdMakeup = null;
		_mockGameService.Setup(s => s.CreateGameAsync(It.IsAny<Game>()))
			.Callback<Game>(g =>
			{
				g.Id = 775;
				createdMakeup = g;
			})
			.ReturnsAsync((Game g) => g);

		_mockGameService.Setup(s => s.UpdateGameAsync(It.IsAny<Game>()))
			.ReturnsAsync((Game g) => g);

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto
			{
				GameId = 10,
				HomeTeamId = 1,
				AwayTeamId = 2,
				Status = GameStatus.Rescheduled,
				NewScheduledDateTime = new DateTime(2026, 10, 19, 18, 30, 0),
				NewLocation = "Dos Vientos 1 (games) - Dos Vientos Community Park"
			}
		};

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().BeNull();
		_controller.TempData["SuccessMessage"].Should().NotBeNull();

		// Original game keeps its original round, date/time, and location
		originalGame.Round.Should().Be(1);
		originalGame.ScheduledDateTime.Should().Be(new DateTime(2026, 8, 29, 15, 0, 0, DateTimeKind.Utc));
		originalGame.Location.Should().Be("Field 1 - Deerhill Park");
		originalGame.Status.Should().Be(GameStatus.Rescheduled);
		originalGame.RescheduledToGameId.Should().Be(775);

		// Makeup game is created in Round 8 (the week of 2026-10-18 through 2026-10-24)
		createdMakeup.Should().NotBeNull();
		createdMakeup!.Round.Should().Be(8);
		createdMakeup.Location.Should().Be("Dos Vientos 1 (games) - Dos Vientos Community Park");
		createdMakeup.RescheduledFromGameId.Should().Be(10);
		createdMakeup.Status.Should().Be(GameStatus.Scheduled);
	}

	[Fact]
	public async Task Entry_Post_WithAlreadyRescheduledGame_UpdatesExistingMakeupGameWithoutDuplicating()
	{
		// Arrange
		var originalGame = _builder.BuildGame(1, 1, 2, 1, GameStatus.Rescheduled);
		originalGame.Id = 10;
		originalGame.ScheduledDateTime = new DateTime(2026, 8, 29, 15, 0, 0, DateTimeKind.Utc);
		originalGame.Location = "Field 1 - Deerhill Park";
		originalGame.RescheduledToGameId = 775;

		var existingMakeup = _builder.BuildGame(1, 1, 2, 8, GameStatus.Scheduled);
		existingMakeup.Id = 775;
		existingMakeup.ScheduledDateTime = new DateTime(2026, 10, 20, 1, 30, 0, DateTimeKind.Utc);
		existingMakeup.Location = "Dos Vientos 1 (games) - Dos Vientos Community Park";
		existingMakeup.RescheduledFromGameId = 10;

		var round9Game = _builder.BuildGame(1, 3, 4, 9);
		round9Game.Id = 90;
		round9Game.ScheduledDateTime = new DateTime(2026, 10, 31, 15, 0, 0, DateTimeKind.Utc);

		_mockGameService.Setup(s => s.GetGameByIdAsync(10))
			.ReturnsAsync(originalGame);
		_mockGameService.Setup(s => s.GetGameByIdAsync(775))
			.ReturnsAsync(existingMakeup);
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(1, 1))
			.ReturnsAsync(new List<Game> { originalGame });
		_mockGameService.Setup(s => s.GetGamesByDivisionAsync(1))
			.ReturnsAsync(new List<Game> { originalGame, existingMakeup, round9Game });
		_mockGameService.Setup(s => s.UpdateGameAsync(It.IsAny<Game>()))
			.ReturnsAsync((Game g) => g);

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto
			{
				GameId = 10,
				HomeTeamId = 1,
				AwayTeamId = 2,
				Status = GameStatus.Rescheduled,
				NewScheduledDateTime = new DateTime(2026, 10, 25, 10, 45, 0),
				NewLocation = "CC5 - Conejo Creek South Park"
			}
		};

		// Act
		var result = await _controller.Entry(scores, 1, 1);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_mockGameService.Verify(s => s.CreateGameAsync(It.IsAny<Game>()), Times.Never);
		existingMakeup.Round.Should().Be(9);
		existingMakeup.Location.Should().Be("CC5 - Conejo Creek South Park");
	}

	[Fact]
	public async Task Entry_Post_WithMakeupGameAndRegularGameForSameTeamInSameRound_AllowsSave()
	{
		// Arrange - Team 1 plays both a regular round 8 game and a makeup game in round 8
		var regularGame = _builder.BuildGame(1, 1, 2, 8);
		regularGame.Id = 80;

		var makeupGame = _builder.BuildGame(1, 1, 3, 8);
		makeupGame.Id = 775;
		makeupGame.RescheduledFromGameId = 10;

		_mockGameService.Setup(s => s.GetGameByIdAsync(80))
			.ReturnsAsync(regularGame);
		_mockGameService.Setup(s => s.GetGameByIdAsync(775))
			.ReturnsAsync(makeupGame);
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(1, 8))
			.ReturnsAsync(new List<Game> { regularGame, makeupGame });
		_mockScoreService.Setup(s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
			.ReturnsAsync(new Score());

		var scores = new List<ScoreUpdateDto>
		{
			new ScoreUpdateDto { GameId = 80, HomeTeamId = 1, AwayTeamId = 2, HomeScore = 3, AwayScore = 1, Status = GameStatus.Completed },
			new ScoreUpdateDto { GameId = 775, HomeTeamId = 1, AwayTeamId = 3, HomeScore = 2, AwayScore = 0, Status = GameStatus.Completed }
		};

		// Act
		var result = await _controller.Entry(scores, 1, 8);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().BeNull();
		_controller.TempData["SuccessMessage"].Should().NotBeNull();
		_mockScoreService.Verify(
			s => s.EnterOrUpdateScoreAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
			Times.Exactly(2));
	}
}
