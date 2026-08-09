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
}
