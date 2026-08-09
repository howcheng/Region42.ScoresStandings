using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Application.Tests.Helpers;
using System.Linq.Expressions;

namespace Region42.ScoresStandings.Application.Tests.Services;

public class ScoreServiceTests
{
	private readonly Mock<IRepository<Score>> _mockScoreRepository;
	private readonly Mock<IRepository<Game>> _mockGameRepository;
	private readonly Mock<ILogger<ScoreService>> _mockLogger;
	private readonly ScoreService _scoreService;

	public ScoreServiceTests()
	{
		_mockScoreRepository = new Mock<IRepository<Score>>();
		_mockGameRepository = new Mock<IRepository<Game>>();
		_mockLogger = new Mock<ILogger<ScoreService>>();
		_scoreService = new ScoreService(_mockScoreRepository.Object, _mockGameRepository.Object, _mockLogger.Object);
	}

	#region GetScoreByGameIdAsync Tests

	[Fact]
	public async Task GetScoreByGameIdAsync_ReturnsScore_WhenExists()
	{
		// Arrange
		var gameId = 1;
		var score = new Score
		{
			GameId = gameId,
			HomeScore = 3,
			AwayScore = 2
		};

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score> { score });

		// Act
		var result = await _scoreService.GetScoreByGameIdAsync(gameId);

		// Assert
		result.Should().NotBeNull();
		result!.GameId.Should().Be(gameId);
		result.HomeScore.Should().Be(3);
		result.AwayScore.Should().Be(2);
	}

	[Fact]
	public async Task GetScoreByGameIdAsync_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var gameId = 999;
		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score>());

		// Act
		var result = await _scoreService.GetScoreByGameIdAsync(gameId);

		// Assert
		result.Should().BeNull();
	}

	#endregion

	#region EnterOrUpdateScoreAsync Tests

	[Fact]
	public async Task EnterOrUpdateScoreAsync_CreatesNewScore_WhenGameExistsAndNoExistingScore()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score>());

		_mockScoreRepository
			.Setup(r => r.AddAsync(It.IsAny<Score>()))
			.Returns(Task.CompletedTask);

		_mockScoreRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		_mockGameRepository
			.Setup(r => r.Update(It.IsAny<Game>()))
			.Verifiable();

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _scoreService.EnterOrUpdateScoreAsync(gameId, 3, 2);

		// Assert
		result.Should().NotBeNull();
		result.GameId.Should().Be(gameId);
		result.HomeScore.Should().Be(3);
		result.AwayScore.Should().Be(2);

		_mockScoreRepository.Verify(r => r.AddAsync(It.IsAny<Score>()), Times.Once);
		_mockScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
		_mockScoreRepository.Verify(r => r.Update(It.IsAny<Score>()), Times.Never);
		_mockGameRepository.Verify(r => r.Update(It.IsAny<Game>()), Times.Once);
		_mockGameRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task EnterOrUpdateScoreAsync_UpdatesExistingScore_WhenScoreAlreadyExists()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(gameId, 1, 1, 2, DateTime.UtcNow);
		game.Status = GameStatus.Completed; // Already completed

		var existingScore = new Score
		{
			GameId = gameId,
			HomeScore = 2,
			AwayScore = 1
		};

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score> { existingScore });

		_mockScoreRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _scoreService.EnterOrUpdateScoreAsync(gameId, 4, 3);

		// Assert
		result.Should().NotBeNull();
		result.GameId.Should().Be(gameId);
		result.HomeScore.Should().Be(4);
		result.AwayScore.Should().Be(3);

		_mockScoreRepository.Verify(r => r.Update(It.IsAny<Score>()), Times.Once);
		_mockScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
		_mockScoreRepository.Verify(r => r.AddAsync(It.IsAny<Score>()), Times.Never);
		// Game status should NOT be updated since it's already Completed
		_mockGameRepository.Verify(r => r.Update(It.IsAny<Game>()), Times.Never);
	}

	[Fact]
	public async Task EnterOrUpdateScoreAsync_ThrowsArgumentException_WhenGameNotFound()
	{
		// Arrange
		var gameId = 999;
		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync((Game?)null);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _scoreService.EnterOrUpdateScoreAsync(gameId, 3, 2));
	}

	[Fact]
	public async Task EnterOrUpdateScoreAsync_ThrowsArgumentException_WhenHomeScoreNegative()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);
		game.Status = GameStatus.Completed;

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _scoreService.EnterOrUpdateScoreAsync(gameId, -1, 2));
	}

	[Fact]
	public async Task EnterOrUpdateScoreAsync_ThrowsArgumentException_WhenAwayScoreNegative()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);
		game.Status = GameStatus.Completed;

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _scoreService.EnterOrUpdateScoreAsync(gameId, 3, -1));
	}

	[Fact]
	public async Task EnterOrUpdateScoreAsync_AllowsZeroScores()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score>());

		_mockScoreRepository
			.Setup(r => r.AddAsync(It.IsAny<Score>()))
			.Returns(Task.CompletedTask);

		_mockScoreRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		_mockGameRepository
			.Setup(r => r.Update(It.IsAny<Game>()))
			.Verifiable();

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _scoreService.EnterOrUpdateScoreAsync(gameId, 0, 0);

		// Assert
		result.Should().NotBeNull();
		result.HomeScore.Should().Be(0);
		result.AwayScore.Should().Be(0);
	}

	#endregion

	#region GetScoresByDivisionAsync Tests

	[Fact]
	public async Task GetScoresByDivisionAsync_ReturnsAllScoresForDivision()
	{
		// Arrange
		var divisionId = 1;
		var scores = new[]
		{
			TestDataBuilder.CreateScore(1, 3, 2, divisionId),
			TestDataBuilder.CreateScore(2, 1, 1, divisionId),
			TestDataBuilder.CreateScore(3, 4, 0, divisionId)
		};

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(scores);

		// Act
		var result = await _scoreService.GetScoresByDivisionAsync(divisionId);

		// Assert
		result.Should().HaveCount(3);
		result.Should().BeEquivalentTo(scores);
	}

	[Fact]
	public async Task GetScoresByDivisionAsync_ReturnsEmptyList_WhenNoScores()
	{
		// Arrange
		var divisionId = 999;
		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score>());

		// Act
		var result = await _scoreService.GetScoresByDivisionAsync(divisionId);

		// Assert
		result.Should().BeEmpty();
	}

	#endregion

	#region GetScoresByDivisionAndRoundAsync Tests

	[Fact]
	public async Task GetScoresByDivisionAndRoundAsync_ReturnsScoresThroughSpecifiedRound()
	{
		// Arrange
		var divisionId = 1;
		var throughRound = 2;
		var scores = new[]
		{
			TestDataBuilder.CreateScore(1, 3, 2, divisionId, 1),
			TestDataBuilder.CreateScore(2, 1, 1, divisionId, 2)
		};

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(scores);

		// Act
		var result = await _scoreService.GetScoresByDivisionAndRoundAsync(divisionId, throughRound);

		// Assert
		result.Should().HaveCount(2);
	}

	[Fact]
	public async Task GetScoresByDivisionAndRoundAsync_ExcludesScoresAfterSpecifiedRound()
	{
		// Arrange
		var divisionId = 1;
		var throughRound = 2;
		var scoresRound1And2 = new[]
		{
			TestDataBuilder.CreateScore(1, 3, 2, divisionId, 1),
			TestDataBuilder.CreateScore(2, 1, 1, divisionId, 2)
		};

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.Is<Expression<Func<Score, bool>>>(
				expr => expr.Compile()(TestDataBuilder.CreateScore(3, 0, 0, divisionId, 3)) == false)))
			.ReturnsAsync(scoresRound1And2);

		// Act
		var result = await _scoreService.GetScoresByDivisionAndRoundAsync(divisionId, throughRound);

		// Assert
		result.Should().HaveCount(2);
		result.Should().OnlyContain(s => s.Game.Round <= throughRound);
	}

	#endregion

	#region CanEnterScoreAsync Tests

	[Fact]
	public async Task CanEnterScoreAsync_ReturnsTrue_WhenGameCompleted()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);
		game.Status = GameStatus.Completed;

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		// Act
		var result = await _scoreService.CanEnterScoreAsync(gameId);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task CanEnterScoreAsync_ReturnsFalse_WhenGameScheduled()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);
		game.Status = GameStatus.Scheduled;

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		// Act
		var result = await _scoreService.CanEnterScoreAsync(gameId);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanEnterScoreAsync_ReturnsFalse_WhenGameCancelled()
	{
		// Arrange
		var gameId = 1;
		var game = TestDataBuilder.CreateGame(id: 1, divisionId: 1, homeTeamId: 1, awayTeamId: 2, scheduledDateTime: DateTime.UtcNow);
		game.Status = GameStatus.Cancelled;

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		// Act
		var result = await _scoreService.CanEnterScoreAsync(gameId);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task CanEnterScoreAsync_ReturnsFalse_WhenGameNotFound()
	{
		// Arrange
		var gameId = 999;
		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync((Game?)null);

		// Act
		var result = await _scoreService.CanEnterScoreAsync(gameId);

		// Assert
		result.Should().BeFalse();
	}

	#endregion

	#region DeleteScoreAsync Tests

	[Fact]
	public async Task DeleteScoreAsync_ReturnsTrue_WhenScoreExists()
	{
		// Arrange
		var gameId = 1;
		var score = new Score
		{
			GameId = gameId,
			HomeScore = 3,
			AwayScore = 2
		};

		var game = TestDataBuilder.CreateGame(gameId, 1, 1, 2, DateTime.UtcNow);
		game.Status = GameStatus.Completed;

		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score> { score });

		_mockScoreRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		_mockGameRepository
			.Setup(r => r.GetByIdAsync(gameId))
			.ReturnsAsync(game);

		_mockGameRepository
			.Setup(r => r.Update(It.IsAny<Game>()))
			.Verifiable();

		_mockGameRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _scoreService.DeleteScoreAsync(gameId);

		// Assert
		result.Should().BeTrue();
		_mockScoreRepository.Verify(r => r.Delete(score), Times.Once);
		_mockScoreRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
		_mockGameRepository.Verify(r => r.Update(It.IsAny<Game>()), Times.Once);
		_mockGameRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task DeleteScoreAsync_ReturnsFalse_WhenScoreNotFound()
	{
		// Arrange
		var gameId = 999;
		_mockScoreRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Score, bool>>>()))
			.ReturnsAsync(new List<Score>());

		// Act
		var result = await _scoreService.DeleteScoreAsync(gameId);

		// Assert
		result.Should().BeFalse();
		_mockScoreRepository.Verify(r => r.Delete(It.IsAny<Score>()), Times.Never);
	}

	#endregion
}
