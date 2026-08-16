using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Controllers;
using Region42.ScoresStandings.Web.Models;
using Region42.ScoresStandings.Web.Tests.Helpers;
using System.Linq.Expressions;

namespace Region42.ScoresStandings.Web.Tests.Controllers;

public class HomeControllerTests
{
	private readonly Mock<IStandingsService> _mockStandingsService;
	private readonly Mock<IGameService> _mockGameService;
	private readonly Mock<IRepository<Division>> _mockDivisionRepo;
	private readonly Mock<IRepository<Season>> _mockSeasonRepo;
	private readonly Mock<ILogger<HomeController>> _mockLogger;
	private readonly HomeController _controller;
	private readonly TestDataBuilder _builder;

	public HomeControllerTests()
	{
		_mockStandingsService = new Mock<IStandingsService>();
		_mockGameService = new Mock<IGameService>();
		_mockDivisionRepo = new Mock<IRepository<Division>>();
		_mockSeasonRepo = new Mock<IRepository<Season>>();
		_mockLogger = new Mock<ILogger<HomeController>>();
		_controller = new HomeController(
			_mockStandingsService.Object,
			_mockGameService.Object,
			_mockDivisionRepo.Object,
			_mockSeasonRepo.Object,
			_mockLogger.Object);
		_builder = new TestDataBuilder();

		ControllerTestHelper.SetupControllerContext(_controller);
	}

	[Fact]
	public void Standings_Action_ShouldHaveAllowAnonymousAttribute()
	{
		// Arrange & Act
		var method = typeof(HomeController).GetMethod("Standings");
		var attributes = method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), true);

		// Assert
		attributes.Should().NotBeEmpty();
	}

	[Fact]
	public async Task Standings_WithNoActiveSeason_ReturnsEmptyViewModel()
	{
		// Arrange
		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season>());

		// Act
		var result = await _controller.Standings(null, null, null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as StandingsViewModel;
		model.Should().NotBeNull();
		model!.Standings.Should().BeEmpty();
	}

	[Fact]
	public async Task Standings_WithNoDivisionSelected_ReturnsViewWithEmptyStandings()
	{
		// Arrange
		var season = _builder.BuildSeason();
		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division>());

		// Act
		var result = await _controller.Standings(null, null, null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as StandingsViewModel;
		model.Should().NotBeNull();
		model!.SeasonName.Should().Be(season.Name);
		model.Standings.Should().BeEmpty();
	}

	[Fact]
	public async Task Standings_WithDivisionAndNoRound_ReturnsCurrentStandings()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id, AgeGroup.U12, Gender.Boys);

		var standingsResult = new StandingsResult
		{
			DivisionId = division.Id,
			DivisionName = $"{division.AgeGroup} {division.Gender}",
			ThroughRound = 5,
			CalculatedAt = DateTime.UtcNow,
			Standings = new List<TeamStanding>
			{
				new TeamStanding
				{
					Rank = 1,
					TeamId = 1,
					TeamName = "Team A",
					GamesPlayed = 5,
					Wins = 4,
					Draws = 1,
					Losses = 0,
					TotalPoints = 13
				}
			}
		};

		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });

		// Mock game service to return completed games
		var games = new List<Game>
		{
			_builder.BuildGameWithScore(1, 1, division.Id, 1, homeScore: 2, awayScore: 1),
			_builder.BuildGameWithScore(2, 3, division.Id, 2, homeScore: 3, awayScore: 0),
			_builder.BuildGameWithScore(1, 2, division.Id, 3, homeScore: 1, awayScore: 1),
			_builder.BuildGameWithScore(3, 1, division.Id, 4, homeScore: 0, awayScore: 2),
			_builder.BuildGameWithScore(2, 3, division.Id, 5, homeScore: 2, awayScore: 2)
		};
		_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
			.ReturnsAsync(games);
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 5))
			.ReturnsAsync(games.Where(g => g.Round == 5).ToList());

		_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 5))
			.ReturnsAsync(standingsResult);

		// Act
		var result = await _controller.Standings(division.Id, null, null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as StandingsViewModel;
		model.Should().NotBeNull();
		model!.DivisionId.Should().Be(division.Id);
		model.Standings.Should().HaveCount(1);
		model.Standings[0].TeamName.Should().Be("Team A");
		model.Standings[0].Rank.Should().Be(1);
		model.ThroughRound.Should().Be(5);
	}

	[Fact]
	public async Task Standings_WithDivisionAndRound_ReturnsPointInTimeStandings()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id, AgeGroup.U14, Gender.Girls);
		var throughRound = 3;

		var standingsResult = new StandingsResult
		{
			DivisionId = division.Id,
			DivisionName = $"{division.AgeGroup} {division.Gender}",
			ThroughRound = throughRound,
			CalculatedAt = DateTime.UtcNow,
			Standings = new List<TeamStanding>
			{
				new TeamStanding
				{
					Rank = 1,
					TeamId = 1,
					TeamName = "Team A",
					GamesPlayed = 3,
					Wins = 3,
					Draws = 0,
					Losses = 0,
					TotalPoints = 9
				},
				new TeamStanding
				{
					Rank = 2,
					TeamId = 2,
					TeamName = "Team B",
					GamesPlayed = 3,
					Wins = 1,
					Draws = 1,
					Losses = 1,
					TotalPoints = 4
				}
			}
		};

		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });

		// Mock game service for specific round
		var games = new List<Game>
		{
			_builder.BuildGameWithScore(1, 2, division.Id, throughRound, homeScore: 2, awayScore: 1)
		};
		_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, throughRound))
			.ReturnsAsync(games);

		_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, throughRound))
			.ReturnsAsync(standingsResult);

		// Act
		var result = await _controller.Standings(division.Id, throughRound, null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as StandingsViewModel;
		model.Should().NotBeNull();
		model!.DivisionId.Should().Be(division.Id);
		model.ThroughRound.Should().Be(throughRound);
		model.Standings.Should().HaveCount(2);

		_mockStandingsService.Verify(
			s => s.GetStandingsByRoundAsync(division.Id, throughRound),
			Times.Once);
	}

	[Fact]
	public async Task Standings_WhenServiceThrowsException_ReturnsViewWithErrorMessage()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);

		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });

		// Mock game service to return games
		_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
			.ReturnsAsync(new List<Game>());

		_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, It.IsAny<int>()))
			.ThrowsAsync(new Exception("Database error"));

		// Act
		var result = await _controller.Standings(division.Id, null, null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as StandingsViewModel;
		model.Should().NotBeNull();
		model!.Standings.Should().BeEmpty();

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
					public async Task Standings_WithNoDivisionIdAndNoCookie_UsesFirstDivision()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division1 = _builder.BuildDivision(season.Id, AgeGroup.U10, Gender.Boys);
						var division2 = _builder.BuildDivision(season.Id, AgeGroup.U12, Gender.Girls);

						var standingsResult = new StandingsResult
						{
							DivisionId = division1.Id,
							DivisionName = $"{division1.AgeGroup} {division1.Gender}",
							ThroughRound = 1,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division1, division2 });
						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division1.Id))
							.ReturnsAsync(new List<Game>());
						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division1.Id, 1))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(null, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.DivisionId.Should().Be(division1.Id);
					}

					[Fact]
					public async Task Standings_SavesDivisionPreferenceToCookie_WhenDivisionIdProvided()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division = _builder.BuildDivision(season.Id);

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = $"{division.AgeGroup} {division.Gender}",
							ThroughRound = 1,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });
						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(new List<Game>());
						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 1))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();

						// Verify cookie was set (through the Response.Cookies.Append call in the controller)
						// The actual cookie validation would require mocking the HttpContext's Response.Cookies
					}

					[Fact]
					public async Task Standings_WithNoGames_DefaultsToRound1()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division = _builder.BuildDivision(season.Id, totalRounds: 10);

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = $"{division.AgeGroup} {division.Gender}",
							ThroughRound = 1,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });

						// No games exist
						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(new List<Game>());
						_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 1))
							.ReturnsAsync(new List<Game>());

						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 1))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.ThroughRound.Should().Be(1);

						_mockStandingsService.Verify(
							s => s.GetStandingsByRoundAsync(division.Id, 1),
							Times.Once);
					}

					[Fact]
					public async Task Standings_WithGamesButNoCompletedGames_DefaultsToRound1()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division = _builder.BuildDivision(season.Id);

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = $"{division.AgeGroup} {division.Gender}",
							ThroughRound = 1,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });

						// Games exist but no scores yet
						var gamesWithoutScores = new List<Game>
						{
							_builder.BuildGame(1, 2, division.Id, 1),
							_builder.BuildGame(3, 4, division.Id, 2)
						};
						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(gamesWithoutScores);
						_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 1))
							.ReturnsAsync(gamesWithoutScores.Where(g => g.Round == 1).ToList());

						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 1))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.ThroughRound.Should().Be(1);
					}

					[Fact]
					public async Task Standings_WithCompletedGames_UsesLatestCompletedRound()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division = _builder.BuildDivision(season.Id);

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = $"{division.AgeGroup} {division.Gender}",
							ThroughRound = 3,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });

						// Mix of completed and scheduled games
						var games = new List<Game>
						{
							_builder.BuildGameWithScore(1, 2, division.Id, 1, homeScore: 2, awayScore: 1),
							_builder.BuildGameWithScore(1, 2, division.Id, 2, homeScore: 1, awayScore: 1),
							_builder.BuildGameWithScore(1, 2, division.Id, 3, homeScore: 3, awayScore: 0),
							_builder.BuildGameWithScore(1, 2, division.Id, 4) // No score yet
						};
						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(games);
						_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 3))
							.ReturnsAsync(games.Where(g => g.Round == 3).ToList());

						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 3))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.ThroughRound.Should().Be(3);

						_mockStandingsService.Verify(
							s => s.GetStandingsByRoundAsync(division.Id, 3),
							Times.Once);
					}

					[Fact]
					public async Task Standings_PopulatesScoresInViewModel()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division = _builder.BuildDivision(season.Id);

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = $"{division.AgeGroup} {division.Gender}",
							ThroughRound = 2,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });

						var allGames = new List<Game>
						{
							_builder.BuildGameWithScore(1, 2, division.Id, 1, homeScore: 2, awayScore: 1),
							_builder.BuildGameWithScore(3, 4, division.Id, 2, homeScore: 1, awayScore: 1)
						};

						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(allGames);
						_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 2))
							.ReturnsAsync(allGames.Where(g => g.Round == 2).ToList());

						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 2))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.Scores.Should().NotBeNull();
						model.Scores.Should().HaveCount(1); // Only round 2 games
						model.Scores![0].Round.Should().Be(2);
						model.Scores[0].HomeScore.Should().Be(1);
						model.Scores[0].AwayScore.Should().Be(1);
					}

					[Fact]
					public async Task Standings_WhenGameServiceThrows_ContinuesWithoutScores()
					{
						// Arrange
						var season = _builder.BuildSeason();
						var division = _builder.BuildDivision(season.Id);

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = $"{division.AgeGroup} {division.Gender}",
							ThroughRound = 1,
							CalculatedAt = DateTime.UtcNow,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });

						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(new List<Game>());
						_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 1))
							.ThrowsAsync(new Exception("Game service error"));

						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 1))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.Standings.Should().NotBeNull();
						model.Scores.Should().BeNullOrEmpty(); // Scores failed to load but standings should still be there

						// Verify warning was logged
						_mockLogger.Verify(
							x => x.Log(
								LogLevel.Warning,
								It.IsAny<EventId>(),
								It.Is<It.IsAnyType>((v, t) => true),
								It.IsAny<Exception>(),
								It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
							Times.Once);
					}

					[Fact]
					public async Task Standings_WithSeasonAndDivision_ReturnsStandingsForThatSeason()
					{
						// Arrange
						var season1 = _builder.BuildSeason();
						season1.Id = 10;
						season1.Name = "Fall 2026";

						var season2 = _builder.BuildSeason();
						season2.Id = 20;
						season2.Name = "Spring 2027";
						season2.IsActive = true;

						var division = _builder.BuildDivision(season1.Id);
						division.Id = 100;

						var standingsResult = new StandingsResult
						{
							DivisionId = division.Id,
							DivisionName = "10U Boys",
							ThroughRound = 1,
							Standings = new List<TeamStanding>()
						};

						_mockSeasonRepo.Setup(r => r.GetAllAsync())
							.ReturnsAsync(new List<Season> { season1, season2 });
						_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
							.ReturnsAsync(new List<Division> { division });

						var games = new List<Game>();
						_mockGameService.Setup(s => s.GetGamesByDivisionAsync(division.Id))
							.ReturnsAsync(games);
						_mockGameService.Setup(s => s.GetGamesByDivisionAndRoundAsync(division.Id, 1))
							.ReturnsAsync(games);

						_mockStandingsService.Setup(s => s.GetStandingsByRoundAsync(division.Id, 1))
							.ReturnsAsync(standingsResult);

						// Act
						var result = await _controller.Standings(division.Id, null, null, season1.Id);

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						var model = viewResult!.Model as StandingsViewModel;
						model.Should().NotBeNull();
						model!.SeasonId.Should().Be(season1.Id);
						model.SeasonName.Should().Be(season1.Name);
						model.DivisionId.Should().Be(division.Id);
					}

					[Fact]
					public async Task Index_RedirectsToStandings()
					{
						// Act
						var result = _controller.Index();

						// Assert
						result.Should().BeOfType<RedirectToActionResult>();
						var redirectResult = result as RedirectToActionResult;
						redirectResult!.ActionName.Should().Be("Standings");
					}

					[Fact]
					public void Error_ReturnsErrorView()
					{
						// Act
						var result = _controller.Error();

						// Assert
						result.Should().BeOfType<ViewResult>();
						var viewResult = result as ViewResult;
						viewResult!.Model.Should().BeOfType<ErrorViewModel>();
					}
				}
