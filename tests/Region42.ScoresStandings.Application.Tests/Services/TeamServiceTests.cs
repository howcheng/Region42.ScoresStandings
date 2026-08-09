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
/// Unit tests for TeamService focusing on CRUD operations and validation.
/// </summary>
public class TeamServiceTests
{
	private readonly Mock<IRepository<Team>> _mockTeamRepository;
	private readonly Mock<IRepository<Division>> _mockDivisionRepository;
	private readonly Mock<IRepository<Game>> _mockGameRepository;
	private readonly Mock<ILogger<TeamService>> _mockLogger;
	private readonly TeamService _service;

	public TeamServiceTests()
	{
		_mockTeamRepository = new Mock<IRepository<Team>>();
		_mockDivisionRepository = new Mock<IRepository<Division>>();
		_mockGameRepository = new Mock<IRepository<Game>>();
		_mockLogger = new Mock<ILogger<TeamService>>();

		_service = new TeamService(
			_mockTeamRepository.Object,
			_mockDivisionRepository.Object,
			_mockGameRepository.Object,
			_mockLogger.Object
		);
	}

	#region GetTeamsByDivisionAsync Tests

	[Fact]
	public async Task GetTeamsByDivisionAsync_ReturnsActiveTeamsForDivision()
	{
		// Arrange
		var divisionId = 1;
		var activeTeams = new List<Team>
		{
			TestDataBuilder.CreateTeam(1, divisionId, "Team A", isActive: true),
			TestDataBuilder.CreateTeam(2, divisionId, "Team B", isActive: true)
		};
		var inactiveTeam = TestDataBuilder.CreateTeam(3, divisionId, "Team C", isActive: false);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
			{
				var allTeams = activeTeams.Concat(new[] { inactiveTeam });
				return allTeams.Where(predicate.Compile()).ToList();
			});

		// Act
		var result = await _service.GetTeamsByDivisionAsync(divisionId);

		// Assert
		result.Should().HaveCount(2);
		result.Should().Contain(t => t.Name == "Team A");
		result.Should().Contain(t => t.Name == "Team B");
		result.Should().NotContain(t => t.Name == "Team C");
	}

	[Fact]
	public async Task GetTeamsByDivisionAsync_ReturnsEmptyForDivisionWithNoTeams()
	{
		// Arrange
		var divisionId = 1;

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new List<Team>());

		// Act
		var result = await _service.GetTeamsByDivisionAsync(divisionId);

		// Assert
		result.Should().BeEmpty();
	}

	#endregion

	#region GetTeamByIdAsync Tests

	[Fact]
	public async Task GetTeamByIdAsync_ReturnsTeamWhenExists()
	{
		// Arrange
		var team = TestDataBuilder.CreateTeam(1, 1, "Team A");

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(team.Id))
			.ReturnsAsync(team);

		// Act
		var result = await _service.GetTeamByIdAsync(team.Id);

		// Assert
		result.Should().NotBeNull();
		result!.Id.Should().Be(team.Id);
		result.Name.Should().Be("Team A");
	}

	[Fact]
	public async Task GetTeamByIdAsync_ReturnsNullWhenNotExists()
	{
		// Arrange
		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
			.ReturnsAsync((Team?)null);

		// Act
		var result = await _service.GetTeamByIdAsync(999);

		// Assert
		result.Should().BeNull();
	}

	#endregion

	#region GetTeamsBySeasonAsync Tests

	[Fact]
	public async Task GetTeamsBySeasonAsync_ReturnsTeamsFromAllDivisionsInSeason()
	{
		// Arrange
		var seasonId = 1;
		var division1 = TestDataBuilder.CreateDivision(1, seasonId, AgeGroup.U10, Gender.Boys);
		var division2 = TestDataBuilder.CreateDivision(2, seasonId, AgeGroup.U12, Gender.Girls);

		var divisions = new List<Division> { division1, division2 };

		var teams = new List<Team>
		{
			TestDataBuilder.CreateTeam(1, division1.Id, "Team A", isActive: true),
			TestDataBuilder.CreateTeam(2, division1.Id, "Team B", isActive: true),
			TestDataBuilder.CreateTeam(3, division2.Id, "Team C", isActive: true),
			TestDataBuilder.CreateTeam(4, division2.Id, "Team D", isActive: false) // Inactive
		};

		_mockDivisionRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Division, bool>>>()))
			.ReturnsAsync(divisions);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				teams.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.GetTeamsBySeasonAsync(seasonId);

		// Assert
		result.Should().HaveCount(3);
		result.Should().Contain(t => t.Name == "Team A");
		result.Should().Contain(t => t.Name == "Team B");
		result.Should().Contain(t => t.Name == "Team C");
		result.Should().NotContain(t => t.Name == "Team D");
	}

	#endregion

	#region CreateTeamAsync Tests

	[Fact]
	public async Task CreateTeamAsync_CreatesTeamSuccessfully()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys);
		var team = TestDataBuilder.CreateTeam(0, division.Id, "New Team");

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new List<Team>());

		_mockTeamRepository
			.Setup(r => r.AddAsync(It.IsAny<Team>()))
			.Returns(Task.CompletedTask);

		_mockTeamRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.CreateTeamAsync(team);

		// Assert
		result.Should().NotBeNull();
		result.Name.Should().Be("New Team");
		result.IsActive.Should().BeTrue();

		_mockTeamRepository.Verify(r => r.AddAsync(It.IsAny<Team>()), Times.Once);
		_mockTeamRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task CreateTeamAsync_ThrowsWhenDivisionNotFound()
	{
		// Arrange
		var team = TestDataBuilder.CreateTeam(0, 999, "New Team");

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Division?)null);

		// Act
		Func<Task> act = async () => await _service.CreateTeamAsync(team);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Division with ID 999 does not exist*");
	}

	[Fact]
	public async Task CreateTeamAsync_ThrowsWhenTeamNameNotUnique()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys);
		var existingTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A", isActive: true);
		var newTeam = TestDataBuilder.CreateTeam(0, division.Id, "Team A");

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam }.Where(predicate.Compile()).ToList());

		// Act
		Func<Task> act = async () => await _service.CreateTeamAsync(newTeam);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*Team name 'Team A' already exists in this division*");
	}

	[Fact]
	public async Task CreateTeamAsync_AllowsSameNameInDifferentDivisions()
	{
		// Arrange
		var division1 = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys);
		var division2 = TestDataBuilder.CreateDivision(2, 1, AgeGroup.U12, Gender.Boys);
		var existingTeam = TestDataBuilder.CreateTeam(1, division1.Id, "Team A", isActive: true);
		var newTeam = TestDataBuilder.CreateTeam(0, division2.Id, "Team A");

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division2.Id))
			.ReturnsAsync(division2);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam }.Where(predicate.Compile()).ToList());

		_mockTeamRepository
			.Setup(r => r.AddAsync(It.IsAny<Team>()))
			.Returns(Task.CompletedTask);

		_mockTeamRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.CreateTeamAsync(newTeam);

		// Assert
		result.Should().NotBeNull();
		result.Name.Should().Be("Team A");
		_mockTeamRepository.Verify(r => r.AddAsync(It.IsAny<Team>()), Times.Once);
	}

	#endregion

	#region UpdateTeamAsync Tests

	[Fact]
	public async Task UpdateTeamAsync_UpdatesTeamSuccessfully()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys);
		var existingTeam = TestDataBuilder.CreateTeam(1, division.Id, "Old Name", isActive: true);
		var updatedTeam = TestDataBuilder.CreateTeam(1, division.Id, "New Name", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(existingTeam.Id))
			.ReturnsAsync(existingTeam);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new List<Team>());

		_mockTeamRepository
			.Setup(r => r.Update(It.IsAny<Team>()));

		_mockTeamRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.UpdateTeamAsync(updatedTeam);

		// Assert
		result.Should().NotBeNull();
		result.Name.Should().Be("New Name");

		_mockTeamRepository.Verify(r => r.Update(It.IsAny<Team>()), Times.Once);
		_mockTeamRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task UpdateTeamAsync_ThrowsWhenTeamNotFound()
	{
		// Arrange
		var team = TestDataBuilder.CreateTeam(999, 1, "Team A");

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Team?)null);

		// Act
		Func<Task> act = async () => await _service.UpdateTeamAsync(team);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Team with ID 999 not found*");
	}

	[Fact]
	public async Task UpdateTeamAsync_ThrowsWhenDivisionNotFound()
	{
		// Arrange
		var existingTeam = TestDataBuilder.CreateTeam(1, 1, "Team A");
		var updatedTeam = TestDataBuilder.CreateTeam(1, 999, "Team A");

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(existingTeam.Id))
			.ReturnsAsync(existingTeam);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Division?)null);

		// Act
		Func<Task> act = async () => await _service.UpdateTeamAsync(updatedTeam);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Division with ID 999 does not exist*");
	}

	[Fact]
	public async Task UpdateTeamAsync_ThrowsWhenNewNameNotUnique()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys);
		var existingTeam1 = TestDataBuilder.CreateTeam(1, division.Id, "Team A", isActive: true);
		var existingTeam2 = TestDataBuilder.CreateTeam(2, division.Id, "Team B", isActive: true);
		var updatedTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team B"); // Name conflict

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(existingTeam1.Id))
			.ReturnsAsync(existingTeam1);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam1, existingTeam2 }.Where(predicate.Compile()).ToList());

		// Act
		Func<Task> act = async () => await _service.UpdateTeamAsync(updatedTeam);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*Team name 'Team B' already exists in this division*");
	}

	[Fact]
	public async Task UpdateTeamAsync_AllowsSameNameForSameTeam()
	{
		// Arrange
		var division = TestDataBuilder.CreateDivision(1, 1, AgeGroup.U10, Gender.Boys);
		var existingTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A", isActive: true);
		var updatedTeam = TestDataBuilder.CreateTeam(1, division.Id, "Team A", isActive: true); // Same name

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(existingTeam.Id))
			.ReturnsAsync(existingTeam);

		_mockDivisionRepository
			.Setup(r => r.GetByIdAsync(division.Id))
			.ReturnsAsync(division);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam }.Where(predicate.Compile()).ToList());

		_mockTeamRepository
			.Setup(r => r.Update(It.IsAny<Team>()));

		_mockTeamRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		var result = await _service.UpdateTeamAsync(updatedTeam);

		// Assert
		result.Should().NotBeNull();
		_mockTeamRepository.Verify(r => r.Update(It.IsAny<Team>()), Times.Once);
	}

	#endregion

	#region DeactivateTeamAsync Tests

	[Fact]
	public async Task DeactivateTeamAsync_DeactivatesTeamSuccessfully()
	{
		// Arrange
		var team = TestDataBuilder.CreateTeam(1, 1, "Team A", isActive: true);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(team.Id))
			.ReturnsAsync(team);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync(new List<Game>());

		_mockTeamRepository
			.Setup(r => r.Update(It.IsAny<Team>()));

		_mockTeamRepository
			.Setup(r => r.SaveChangesAsync())
			.ReturnsAsync(1);

		// Act
		await _service.DeactivateTeamAsync(team.Id);

		// Assert
		team.IsActive.Should().BeFalse();
		_mockTeamRepository.Verify(r => r.Update(It.Is<Team>(t => t.IsActive == false)), Times.Once);
		_mockTeamRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task DeactivateTeamAsync_ThrowsWhenTeamNotFound()
	{
		// Arrange
		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(999))
			.ReturnsAsync((Team?)null);

		// Act
		Func<Task> act = async () => await _service.DeactivateTeamAsync(999);

		// Assert
		await act.Should().ThrowAsync<ArgumentException>()
			.WithMessage("*Team with ID 999 not found*");
	}

	[Fact]
	public async Task DeactivateTeamAsync_ThrowsWhenTeamHasGames()
	{
		// Arrange
		var team = TestDataBuilder.CreateTeam(1, 1, "Team A", isActive: true);
		var game = TestDataBuilder.CreateGame(1, 1, team.Id, 2);

		_mockTeamRepository
			.Setup(r => r.GetByIdAsync(team.Id))
			.ReturnsAsync(team);

		_mockGameRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Game, bool>> predicate) =>
				new List<Game> { game }.Where(predicate.Compile()).ToList());

		// Act
		Func<Task> act = async () => await _service.DeactivateTeamAsync(team.Id);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>()
			.WithMessage("*Cannot deactivate team*has associated games*");
	}

	#endregion

	#region IsTeamNameUniqueAsync Tests

	[Fact]
	public async Task IsTeamNameUniqueAsync_ReturnsTrueWhenNameIsUnique()
	{
		// Arrange
		var divisionId = 1;

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new List<Team>());

		// Act
		var result = await _service.IsTeamNameUniqueAsync("New Team", divisionId);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task IsTeamNameUniqueAsync_ReturnsFalseWhenNameExists()
	{
		// Arrange
		var divisionId = 1;
		var existingTeam = TestDataBuilder.CreateTeam(1, divisionId, "Team A", isActive: true);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.IsTeamNameUniqueAsync("Team A", divisionId);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task IsTeamNameUniqueAsync_IsCaseInsensitive()
	{
		// Arrange
		var divisionId = 1;
		var existingTeam = TestDataBuilder.CreateTeam(1, divisionId, "Team A", isActive: true);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.IsTeamNameUniqueAsync("TEAM A", divisionId);

		// Assert
		result.Should().BeFalse();
	}

	[Fact]
	public async Task IsTeamNameUniqueAsync_ExcludesSpecifiedTeam()
	{
		// Arrange
		var divisionId = 1;
		var existingTeam = TestDataBuilder.CreateTeam(1, divisionId, "Team A", isActive: true);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { existingTeam }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.IsTeamNameUniqueAsync("Team A", divisionId, excludeTeamId: 1);

		// Assert
		result.Should().BeTrue();
	}

	[Fact]
	public async Task IsTeamNameUniqueAsync_IgnoresInactiveTeams()
	{
		// Arrange
		var divisionId = 1;
		var inactiveTeam = TestDataBuilder.CreateTeam(1, divisionId, "Team A", isActive: false);

		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				new List<Team> { inactiveTeam }.Where(predicate.Compile()).ToList());

		// Act
		var result = await _service.IsTeamNameUniqueAsync("Team A", divisionId);

		// Assert
		result.Should().BeTrue();
	}

	#endregion
}
