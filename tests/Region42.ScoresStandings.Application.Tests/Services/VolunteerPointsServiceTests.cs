using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Application.Tests.Helpers;
using System.Linq.Expressions;

namespace Region42.ScoresStandings.Application.Tests.Services;

public class VolunteerPointsServiceTests
{
	private readonly Mock<IRepository<VolunteerPoints>> _mockVolunteerPointsRepository;
	private readonly Mock<IRepository<Team>> _mockTeamRepository;
	private readonly Mock<ILogger<VolunteerPointsService>> _mockLogger;
	private readonly VolunteerPointsService _volunteerPointsService;

	public VolunteerPointsServiceTests()
	{
		_mockVolunteerPointsRepository = new Mock<IRepository<VolunteerPoints>>();
		_mockTeamRepository = new Mock<IRepository<Team>>();
		_mockLogger = new Mock<ILogger<VolunteerPointsService>>();
		_volunteerPointsService = new VolunteerPointsService(
			_mockVolunteerPointsRepository.Object,
			_mockTeamRepository.Object,
			_mockLogger.Object);
	}

	#region GetVolunteerPointsByTeamAsync Tests

	[Fact]
	public async Task GetVolunteerPointsByTeamAsync_ReturnsAllPointsForTeam()
	{
		// Arrange
		var teamId = 1;
		var volunteerPoints = new[]
		{
			TestDataBuilder.CreateVolunteerPoints(1, teamId, 1, 3, "Concessions"),
			TestDataBuilder.CreateVolunteerPoints(2, teamId, 2, 3, "Field setup"),
			TestDataBuilder.CreateVolunteerPoints(3, teamId, 3, 3, "Refereeing")
		};

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(volunteerPoints);

		// Act
		var result = await _volunteerPointsService.GetVolunteerPointsByTeamAsync(teamId);

		// Assert
		result.Should().HaveCount(3);
		result.Should().BeEquivalentTo(volunteerPoints);
	}

	[Fact]
	public async Task GetVolunteerPointsByTeamAsync_ReturnsEmptyList_WhenNoPoints()
	{
		// Arrange
		var teamId = 999;
		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(new List<VolunteerPoints>());

		// Act
		var result = await _volunteerPointsService.GetVolunteerPointsByTeamAsync(teamId);

		// Assert
		result.Should().BeEmpty();
	}

	#endregion

	#region GetVolunteerPointsByTeamAndRoundAsync Tests

	[Fact]
	public async Task GetVolunteerPointsByTeamAndRoundAsync_ReturnsPoints_WhenExists()
	{
		// Arrange
		var teamId = 1;
		var round = 2;
		var volunteerPoints = new[]
		{
			TestDataBuilder.CreateVolunteerPoints(1, teamId, round, 3, "Field setup")
		};

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(volunteerPoints);

		// Act
		var result = await _volunteerPointsService.GetVolunteerPointsByTeamAndRoundAsync(teamId, round);

		// Assert
		result.Should().NotBeNull();
		result!.TeamId.Should().Be(teamId);
		result.Round.Should().Be(round);
		result.Points.Should().Be(3);
	}

	[Fact]
	public async Task GetVolunteerPointsByTeamAndRoundAsync_ReturnsNull_WhenNotFound()
	{
		// Arrange
		var teamId = 1;
		var round = 999;
		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(new List<VolunteerPoints>());

		// Act
		var result = await _volunteerPointsService.GetVolunteerPointsByTeamAndRoundAsync(teamId, round);

		// Assert
		result.Should().BeNull();
	}

	#endregion

	#region GetVolunteerPointsByDivisionAsync Tests

	[Fact]
	public async Task GetVolunteerPointsByDivisionAsync_ReturnsAllPointsForDivision()
	{
		// Arrange
		var divisionId = 1;
		var volunteerPoints = new[]
		{
			TestDataBuilder.CreateVolunteerPoints(1, 1, 1, 3, "Team 1 - Round 1"),
			TestDataBuilder.CreateVolunteerPoints(2, 2, 1, 3, "Team 2 - Round 1"),
			TestDataBuilder.CreateVolunteerPoints(3, 1, 2, 3, "Team 1 - Round 2")
		};

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(volunteerPoints);

		// Act
		var result = await _volunteerPointsService.GetVolunteerPointsByDivisionAsync(divisionId);

		// Assert
		result.Should().HaveCount(3);
	}

	#endregion

	#region GetVolunteerPointsByDivisionAndRoundAsync Tests

	[Fact]
	public async Task GetVolunteerPointsByDivisionAndRoundAsync_ReturnsPointsThroughSpecifiedRound()
	{
		// Arrange
		var divisionId = 1;
		var throughRound = 2;
		var volunteerPoints = new[]
		{
			TestDataBuilder.CreateVolunteerPoints(1, 1, 1, 3, "Round 1"),
			TestDataBuilder.CreateVolunteerPoints(2, 2, 2, 3, "Round 2")
		};

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(volunteerPoints);

		// Act
		var result = await _volunteerPointsService.GetVolunteerPointsByDivisionAndRoundAsync(divisionId, throughRound);

		// Assert
		result.Should().HaveCount(2);
	}

	#endregion

	#region EnterOrUpdateVolunteerPointsAsync Tests

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_CreatesNewEntry_WhenNotExists()
	{
		// Arrange
		var teamId = 1;
		var round = 1;
		var points = 3;
		var notes = "Concessions duty";
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(new List<VolunteerPoints>());

		_mockVolunteerPointsRepository
			.Setup(r => r.AddAsync(It.IsAny<VolunteerPoints>()))
			.Returns(Task.CompletedTask);

		_mockVolunteerPointsRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, round, points, notes);

		// Assert
		result.Should().NotBeNull();
		result.TeamId.Should().Be(teamId);
		result.Round.Should().Be(round);
		result.Points.Should().Be(points);
		result.Notes.Should().Be(notes);

		_mockVolunteerPointsRepository.Verify(r => r.AddAsync(It.IsAny<VolunteerPoints>()), Times.Once);
		_mockVolunteerPointsRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_UpdatesExistingEntry_WhenExists()
	{
		// Arrange
		var teamId = 1;
		var round = 1;
		var newPoints = 6;
		var newNotes = "Updated notes";
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: true);
		var existingPoints = TestDataBuilder.CreateVolunteerPoints(1, teamId, round, 3, "Old notes");

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(new[] { existingPoints });

		_mockVolunteerPointsRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, round, newPoints, newNotes);

		// Assert
		result.Should().NotBeNull();
		result.TeamId.Should().Be(teamId);
		result.Round.Should().Be(round);
		result.Points.Should().Be(newPoints);
		result.Notes.Should().Be(newNotes);

		_mockVolunteerPointsRepository.Verify(r => r.Update(It.IsAny<VolunteerPoints>()), Times.Once);
		_mockVolunteerPointsRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
		_mockVolunteerPointsRepository.Verify(r => r.AddAsync(It.IsAny<VolunteerPoints>()), Times.Never);
	}

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_ThrowsArgumentException_WhenTeamNotFound()
	{
		// Arrange
		var teamId = 999;
		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync((Team?)null);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, 1, 3, "Notes"));
	}

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_ThrowsInvalidOperationException_WhenTeamInactive()
	{
		// Arrange
		var teamId = 1;
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: false);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		// Act & Assert
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, 1, 3, "Notes"));
	}

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_ThrowsArgumentException_WhenRoundInvalid()
	{
		// Arrange
		var teamId = 1;
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, 0, 3, "Notes"));
	}

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_ThrowsArgumentException_WhenPointsNegative()
	{
		// Arrange
		var teamId = 1;
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		// Act & Assert
		await Assert.ThrowsAsync<ArgumentException>(
			() => _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, 1, -3, "Notes"));
	}

	[Fact]
	public async Task EnterOrUpdateVolunteerPointsAsync_AllowsZeroPoints()
	{
		// Arrange
		var teamId = 1;
		var round = 1;
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(new List<VolunteerPoints>());

		_mockVolunteerPointsRepository
			.Setup(r => r.AddAsync(It.IsAny<VolunteerPoints>()))
			.Returns(Task.CompletedTask);

		_mockVolunteerPointsRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _volunteerPointsService.EnterOrUpdateVolunteerPointsAsync(teamId, round, 0, "No duty this round");

		// Assert
		result.Should().NotBeNull();
		result.Points.Should().Be(0);
	}

	#endregion

	#region DeleteVolunteerPointsAsync Tests

	[Fact]
	public async Task DeleteVolunteerPointsAsync_ReturnsTrue_WhenExists()
	{
		// Arrange
		var volunteerPointsId = 1;
		var volunteerPoints = TestDataBuilder.CreateVolunteerPoints(volunteerPointsId, 1, 1, 3, "Notes");

		_mockVolunteerPointsRepository
			.Setup(r => r.GetByIdAsync(volunteerPointsId))
			.ReturnsAsync(volunteerPoints);

		_mockVolunteerPointsRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _volunteerPointsService.DeleteVolunteerPointsAsync(volunteerPointsId);

		// Assert
		result.Should().BeTrue();
		_mockVolunteerPointsRepository.Verify(r => r.Delete(volunteerPoints), Times.Once);
		_mockVolunteerPointsRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task DeleteVolunteerPointsAsync_ReturnsFalse_WhenNotFound()
	{
		// Arrange
		var volunteerPointsId = 999;
		_mockVolunteerPointsRepository
			.Setup(r => r.GetByIdAsync(volunteerPointsId))
			.ReturnsAsync((VolunteerPoints?)null);

		// Act
		var result = await _volunteerPointsService.DeleteVolunteerPointsAsync(volunteerPointsId);

		// Assert
		result.Should().BeFalse();
		_mockVolunteerPointsRepository.Verify(r => r.Delete(It.IsAny<VolunteerPoints>()), Times.Never);
	}

	#endregion

	#region ValidateTeamAsync Tests

	[Fact]
	public async Task ValidateTeamAsync_ReturnsTrue_WhenTeamExistsAndActive()
	{
		// Arrange
		var teamId = 1;
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		// Act
		var result = await _volunteerPointsService.ValidateTeamAsync(teamId);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateTeamAsync_ReturnsFalse_WhenTeamNotFound()
	{
		// Arrange
		var teamId = 999;
		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync((Team?)null);

		// Act
		var result = await _volunteerPointsService.ValidateTeamAsync(teamId);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task ValidateTeamAsync_ReturnsFalse_WhenTeamInactive()
	{
		// Arrange
		var teamId = 1;
		var team = TestDataBuilder.CreateTeam(teamId, 1, "Team 1", "Coach Smith", isActive: false);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(teamId))
			.ReturnsAsync(team);

		// Act
		var result = await _volunteerPointsService.ValidateTeamAsync(teamId);

		// Assert
		result.Should().BeFalse();
	}

	#endregion
}
