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
/// Unit tests for GameService focusing on CRUD operations and validation.
/// </summary>
public class GameServiceTests
{
	private readonly Mock<IRepository<Game>> _mockGameRepository;
	private readonly Mock<IRepository<Team>> _mockTeamRepository;
	private readonly Mock<IRepository<Division>> _mockDivisionRepository;
	private readonly Mock<IRepository<Score>> _mockScoreRepository;
	private readonly Mock<ILogger<GameService>> _mockLogger;
	private readonly GameService _service;

	public GameServiceTests()
	{
		_mockGameRepository = new Mock<IRepository<Game>>();
		_mockTeamRepository = new Mock<IRepository<Team>>();
		_mockDivisionRepository = new Mock<IRepository<Division>>();
		_mockScoreRepository = new Mock<IRepository<Score>>();
		_mockLogger = new Mock<ILogger<GameService>>();

		_service = new GameService(
			_mockGameRepository.Object,
			_mockTeamRepository.Object,
			_mockDivisionRepository.Object,
			_mockScoreRepository.Object,
			_mockLogger.Object
		);
	}

	#region GetGamesByDivisionAsync Tests

	[Fact]
	public async Task GetGamesByDivisionAsync_ReturnsAllGamesForDivision()
	{
		// Arrange
		var divisionId = 1;
		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(1, divisionId, 1, 2),
			TestDataBuilder.CreateGame(2, divisionId, 3, 4)
		};

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				games.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.GetGamesByDivisionAsync(divisionId);

		// Assert
		result.Should().HaveCount(2);
	}

	#endregion

	#region GetGamesByDivisionAndRoundAsync Tests

	[Fact]
	public async Task GetGamesByDivisionAndRoundAsync_ReturnsGamesForSpecificRound()
	{
		// Arrange
		var divisionId = 1;
		var round = 2;
		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(1, divisionId, 1, 2, null, 1),
			TestDataBuilder.CreateGame(2, divisionId, 3, 4, null, 2),
			TestDataBuilder.CreateGame(3, divisionId, 1, 3, null, 2)
		};

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				games.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.GetGamesByDivisionAndRoundAsync(divisionId, round);

		// Assert
		result.Should().HaveCount(2);
		result.Should().OnlyContain(g => g.Round == round);
	}

	#endregion

	#region GetGameByIdAsync Tests

	[Fact]
	public async Task GetGameByIdAsync_ReturnsGameWhenExists()
	{
		// Arrange
		var game = TestDataBuilder.CreateGame(1, 1, 1, 2);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(game.Id))
			.ReturnsAsync(game);

		// Act
		var result = await _service.GetGameByIdAsync(game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(game.Id);
	}

	[Fact]
	public async Task GetGameByIdAsync_ReturnsNullWhenNotExists()
	{
		// Arrange
		_mockGameRepository
			.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
			.ReturnsAsync((Game?)null);

		// Act
		var result = await _service.GetGameByIdAsync(999);

		// Assert
		result.Should().BeNull();
	}

	#endregion

	#region GetGamesByTeamAsync Tests

	[Fact]
	public async Task GetGamesByTeamAsync_ReturnsHomeAndAwayGames()
	{
		// Arrange
		var teamId = 1;
		var games = new List<Game>
		{
			TestDataBuilder.CreateGame(1, 1, teamId, 2), // Team as home
			TestDataBuilder.CreateGame(2, 1, 3, teamId), // Team as away
			TestDataBuilder.CreateGame(3, 1, 4, 5)       // Team not involved
		};

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				games.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.GetGamesByTeamAsync(teamId);

		// Assert
		result.Should().HaveCount(2);
		result.Should().Contain(g => g.HomeTeamId == teamId);
		result.Should().Contain(g => g.AwayTeamId == teamId);
	}

	#endregion

	#region CreateGameAsync Tests

	[Fact]
	public async Task CreateGameAsync_CreatesGameSuccessfully()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var awayTeam = TestDataBuilder.CreateTeam(2, division.Id, "Team B");
		var game = TestDataBuilder.CreateGame(0, division.Id, homeTeam.Id, awayTeam.Id, DateTime.UtcNow.AddDays(7), 1);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(awayTeam.Id))
			.ReturnsAsync(awayTeam);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync(new List<Game>());

		_mockGameRepository
			.Setup(r => r.AddAsync(It.IsAny<Game>()))
			.Returns(Task.CompletedTask);

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.CreateGameAsync(game);

		// Assert
		result.Should().NotBeNull();
		result.Status.Should().Be(GameStatus.Scheduled);

		_mockGameRepository.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Once);
		_mockGameRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenDivisionNotFound()
	{
		// Arrange
		var game = TestDataBuilder.CreateGame(0, 999, 1, 2);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Division?)null);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Division with ID 999 does not exist*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenHomeTeamNotFound()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var game = TestDataBuilder.CreateGame(0, division.Id, 999, 2);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Team?)null);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Home team with ID 999 does not exist*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenAwayTeamNotFound()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var game = TestDataBuilder.CreateGame(0, division.Id, homeTeam.Id, 999);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Team?)null);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Away team with ID 999 does not exist*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenHomeTeamNotInDivision()
	{
		// Arrange
		var division1 = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var division2 = TestDataBuilder.CreateDivision(2, 1, AgeGroup.U12, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division2.Id, "Team A"); // Wrong division
		var awayTeam = TestDataBuilder.CreateTeam(2, division1.Id, "Team B");
		var game = TestDataBuilder.CreateGame(0, division1.Id, homeTeam.Id, awayTeam.Id);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division1.Id))
			.ReturnsAsync(division1);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*does not belong to the specified division*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenAwayTeamNotInDivision()
	{
		// Arrange
		var division1 = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var division2 = TestDataBuilder.CreateDivision(2, 1, AgeGroup.U12, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division1.Id, "Team A");
		var awayTeam = TestDataBuilder.CreateTeam(2, division2.Id, "Team B"); // Wrong division
		var game = TestDataBuilder.CreateGame(0, division1.Id, homeTeam.Id, awayTeam.Id);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division1.Id))
			.ReturnsAsync(division1);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(awayTeam.Id))
			.ReturnsAsync(awayTeam);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*does not belong to the specified division*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenTeamPlaysItself()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var team = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var game = TestDataBuilder.CreateGame(0, division.Id, team.Id, team.Id); // Same team

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(team.Id))
			.ReturnsAsync(team);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*cannot play against itself*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenRoundNumberInvalid()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10); // Max 10 rounds
		var homeTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var awayTeam = TestDataBuilder.CreateTeam(2, division.Id, "Team B");
		var game = TestDataBuilder.CreateGame(0, division.Id, homeTeam.Id, awayTeam.Id, null, 15); // Round > 10

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(awayTeam.Id))
			.ReturnsAsync(awayTeam);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Round must be between 1 and 10*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenScheduledInPast()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var awayTeam = TestDataBuilder.CreateTeam(2, division.Id, "Team B");
		var pastDate = DateTime.UtcNow.AddDays(-5);
		var game = TestDataBuilder.CreateGame(0, division.Id, homeTeam.Id, awayTeam.Id, pastDate, 1);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(awayTeam.Id))
			.ReturnsAsync(awayTeam);

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*cannot be scheduled in the past*");
	}

	[Fact]
	public async Task CreateGameAsync_ThrowsWhenHomeTeamHasScheduleConflict()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var awayTeam = TestDataBuilder.CreateTeam(2, division.Id, "Team B");
		var scheduledTime = DateTime.UtcNow.AddDays(7);
		var game = TestDataBuilder.CreateGame(0, division.Id, homeTeam.Id, awayTeam.Id, scheduledTime, 1);

		// Existing game at the same time
		var existingGame = TestDataBuilder.CreateGame(99, division.Id, homeTeam.Id, 3, scheduledTime, 1, GameStatus.Scheduled);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(awayTeam.Id))
			.ReturnsAsync(awayTeam);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				new List<Game> { existingGame }.Where(predicate.Compile()).ToList());

		// Act
		Func<Task> act = async () => await _service.CreateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*already scheduled to play*");
	}

	#endregion

	#region UpdateGameAsync Tests

	[Fact]
	public async Task UpdateGameAsync_UpdatesGameSuccessfully()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys, 10);
		var homeTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A");
		var awayTeam = TestDataBuilder.CreateTeam(2, division.Id, "Team B");
		var existingGame = TestDataBuilder.CreateGame(1, division.Id, homeTeam.Id, awayTeam.Id, DateTime.UtcNow.AddDays(7), 1);
		var updatedGame = TestDataBuilder.CreateGame(1, division.Id, homeTeam.Id, awayTeam.Id, DateTime.UtcNow.AddDays(14), 1);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(existingGame.Id))
			.ReturnsAsync(existingGame);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(homeTeam.Id))
			.ReturnsAsync(homeTeam);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(awayTeam.Id))
			.ReturnsAsync(awayTeam);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync(new List<Game>());

		_mockGameRepository
			.Setup(r => r.Update(It.IsAny<Game>()));

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.UpdateGameAsync(updatedGame);

		// Assert
		result.Should().NotBeNull();
		_mockGameRepository.Verify(r => r.Update(It.IsAny<Game>()), Times.Once);
		_mockGameRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task UpdateGameAsync_ThrowsWhenGameNotFound()
	{
		// Arrange
		var game = TestDataBuilder.CreateGame(999, 1, 1, 2);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Game?)null);

		// Act
		Func<Task> act = async () => await _service.UpdateGameAsync(game);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Game with ID 999 not found*");
	}

	#endregion

	#region UpdateGameStatusAsync Tests

	[Fact]
	public async Task UpdateGameStatusAsync_UpdatesStatusSuccessfully()
	{
		// Arrange
		var game = TestDataBuilder.CreateGame(1, 1, 1, 2, null, 1, GameStatus.Scheduled);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(game.Id))
			.ReturnsAsync(game);

		_mockGameRepository
			.Setup(r => r.Update(It.IsAny<Game>()));

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		await _service.UpdateGameStatusAsync(game.Id, GameStatus.Completed);

		// Assert
		game.Status.Should().Be(GameStatus.Completed);
		_mockGameRepository.Verify(r => r.Update(It.Is<Game>(g => g.Status == GameStatus.Completed)), Times.Once);
	}

	[Fact]
	public async Task UpdateGameStatusAsync_ThrowsWhenGameNotFound()
	{
		// Arrange
		_mockGameRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Game?)null);

		// Act
		Func<Task> act = async () => await _service.UpdateGameStatusAsync(999, GameStatus.Completed);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Game with ID 999 not found*");
	}

	#endregion

	#region DeleteGameAsync Tests

	[Fact]
	public async Task DeleteGameAsync_DeletesGameSuccessfully()
	{
		// Arrange
		var game = TestDataBuilder.CreateGame(1, 1, 1, 2);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(game.Id))
			.ReturnsAsync(game);

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score>());

		_mockGameRepository
			.Setup(r => r.Delete(It.IsAny<Game>()));

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.DeleteGameAsync(game.Id);

		// Assert
		result.Should().BeTrue();
		_mockGameRepository.Verify(r => r.Delete(It.IsAny<Game>()), Times.Once);
		_mockGameRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task DeleteGameAsync_ThrowsWhenGameNotFound()
	{
		// Arrange
		_mockGameRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Game?)null);

		// Act
		Func<Task> act = async () => await _service.DeleteGameAsync(999);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Game with ID 999 not found*");
	}

	[Fact]
	public async Task DeleteGameAsync_ThrowsWhenScoreExists()
	{
		// Arrange
		var game = TestDataBuilder.CreateGame(1, 1, 1, 2);
		var score = TestDataBuilder.CreateScore(1, game.Id, 2, 1);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(game.Id))
			.ReturnsAsync(game);

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Score, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Score, bool>> predicate) =>
				new List<Score> { score }.Where(predicate.Compile()).ToList());

		// Act
		Func<Task> act = async () => await _service.DeleteGameAsync(game.Id);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*score has been entered*");
	}

	#endregion

	#region ValidateNoScheduleConflictAsync Tests

	[Fact]
	public async Task ValidateNoScheduleConflictAsync_ReturnsTrueWhenNoConflict()
	{
		// Arrange
		var teamId = 1;
		var scheduledTime = DateTime.UtcNow.AddDays(7);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync(new List<Game>());

		// Act
		var result = await _service.ValidateNoScheduleConflictAsync(teamId, scheduledTime);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateNoScheduleConflictAsync_ReturnsFalseWhenConflictExists()
	{
		// Arrange
		var teamId = 1;
		var scheduledTime = DateTime.UtcNow.AddDays(7);
		var existingGame = TestDataBuilder.CreateGame(1, 1, teamId, 2, scheduledTime, 1, GameStatus.Scheduled);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				new List<Game> { existingGame }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.ValidateNoScheduleConflictAsync(teamId, scheduledTime);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateNoScheduleConflictAsync_ExcludesSpecifiedGame()
	{
		// Arrange
		var teamId = 1;
		var scheduledTime = DateTime.UtcNow.AddDays(7);
		var existingGame = TestDataBuilder.CreateGame(1, 1, teamId, 2, scheduledTime, 1, GameStatus.Scheduled);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				new List<Game> { existingGame }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.ValidateNoScheduleConflictAsync(teamId, scheduledTime, excludeGameId: 1);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateNoScheduleConflictAsync_IgnoresCancelledGames()
	{
		// Arrange
		var teamId = 1;
		var scheduledTime = DateTime.UtcNow.AddDays(7);
		var cancelledGame = TestDataBuilder.CreateGame(1, 1, teamId, 2, scheduledTime, 1, GameStatus.Cancelled);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				new List<Game> { cancelledGame }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.ValidateNoScheduleConflictAsync(teamId, scheduledTime);

		// Assert
		result.Should().BeTrue();
	}

	#endregion
}
