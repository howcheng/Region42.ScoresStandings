using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Web.Controllers;
using Region42.ScoresStandings.Web.Tests.Helpers;
using System.Linq.Expressions;

namespace Region42.ScoresStandings.Web.Tests.Controllers;

public class TeamsControllerTests
{
	private readonly Mock<ITeamService> _mockTeamService;
	private readonly Mock<IRepository<Division>> _mockDivisionRepo;
	private readonly Mock<IRepository<Season>> _mockSeasonRepo;
	private readonly TeamsController _controller;
	private readonly TestDataBuilder _builder;

	public TeamsControllerTests()
	{
		_mockTeamService = new Mock<ITeamService>();
		_mockDivisionRepo = new Mock<IRepository<Division>>();
		_mockSeasonRepo = new Mock<IRepository<Season>>();
		_controller = new TeamsController(_mockTeamService.Object, _mockDivisionRepo.Object, _mockSeasonRepo.Object);
		_builder = new TestDataBuilder();

		ControllerTestHelper.SetupControllerContext(_controller, "testuser");
	}

	[Fact]
	public void Controller_ShouldHaveAuthorizeAttribute()
	{
		// Arrange & Act
		var authorizeAttributes = typeof(TeamsController)
			.GetCustomAttributes(typeof(AuthorizeAttribute), true);

		// Assert
		authorizeAttributes.Should().NotBeEmpty();
		var authorizeAttr = authorizeAttributes.First() as AuthorizeAttribute;
		authorizeAttr!.Policy.Should().Be("AdminPolicy");
	}

	[Fact]
	public async Task Index_WithNoActiveSeason_ReturnsViewWithEmptyList()
	{
		// Arrange
		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season>());

		// Act
		var result = await _controller.Index(null);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		viewResult!.Model.Should().BeAssignableTo<IEnumerable<Team>>();
		var model = viewResult.Model as IEnumerable<Team>;
		model.Should().BeEmpty();
	}

	[Fact]
	public async Task Index_WithDivisionFilter_ReturnsTeamsForDivision()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);
		var teams = new List<Team>
		{
			_builder.BuildTeam(division.Id, "Team A"),
			_builder.BuildTeam(division.Id, "Team B")
		};

		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });
		_mockTeamService.Setup(s => s.GetTeamsByDivisionAsync(division.Id))
			.ReturnsAsync(teams);

		// Act
		var result = await _controller.Index(division.Id);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		var model = viewResult!.Model as IEnumerable<Team>;
		model.Should().HaveCount(2);
		model.Should().Contain(t => t.Name == "Team A");
	}

	[Fact]
	public async Task Create_Post_WithValidTeam_RedirectsToIndex()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);
		var team = _builder.BuildTeam(division.Id);

		_mockTeamService.Setup(s => s.IsTeamNameUniqueAsync(team.Name, team.DivisionId, null))
			.ReturnsAsync(true);
		_mockTeamService.Setup(s => s.CreateTeamAsync(It.IsAny<Team>()))
			.ReturnsAsync(team);

		// Act
		var result = await _controller.Create(team);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		var redirectResult = result as RedirectToActionResult;
		redirectResult!.ActionName.Should().Be("Index");
		_mockTeamService.Verify(s => s.CreateTeamAsync(It.IsAny<Team>()), Times.Once);
	}

	[Fact]
	public async Task Create_Post_WithDuplicateName_ReturnsViewWithError()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);
		var team = _builder.BuildTeam(division.Id);

		_mockTeamService.Setup(s => s.IsTeamNameUniqueAsync(team.Name, team.DivisionId, null))
			.ReturnsAsync(false);
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });
		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });

		// Act
		var result = await _controller.Create(team);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		_controller.ModelState.Should().ContainKey("Name");
		_mockTeamService.Verify(s => s.CreateTeamAsync(It.IsAny<Team>()), Times.Never);
	}

	[Fact]
	public async Task Edit_WithValidId_ReturnsViewWithTeam()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);
		var team = _builder.BuildTeam(division.Id);

		_mockTeamService.Setup(s => s.GetTeamByIdAsync(team.Id))
			.ReturnsAsync(team);
		_mockDivisionRepo.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });
		_mockSeasonRepo.Setup(r => r.GetAllAsync())
			.ReturnsAsync(new List<Season> { season });

		// Act
		var result = await _controller.Edit(team.Id);

		// Assert
		result.Should().BeOfType<ViewResult>();
		var viewResult = result as ViewResult;
		viewResult!.Model.Should().Be(team);
	}

	[Fact]
	public async Task Edit_WithInvalidId_ReturnsNotFound()
	{
		// Arrange
		_mockTeamService.Setup(s => s.GetTeamByIdAsync(It.IsAny<int>()))
			.ReturnsAsync((Team?)null);

		// Act
		var result = await _controller.Edit(999);

		// Assert
		result.Should().BeOfType<NotFoundResult>();
	}

	[Fact]
	public async Task DeleteConfirmed_WithValidId_DeactivatesTeamAndRedirects()
	{
		// Arrange
		var season = _builder.BuildSeason();
		var division = _builder.BuildDivision(season.Id);
		var team = _builder.BuildTeam(division.Id);

		_mockTeamService.Setup(s => s.GetTeamByIdAsync(team.Id))
			.ReturnsAsync(team);
		_mockTeamService.Setup(s => s.DeactivateTeamAsync(team.Id))
			.Returns(Task.CompletedTask);

		// Act
		var result = await _controller.DeleteConfirmed(team.Id);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		var redirectResult = result as RedirectToActionResult;
		redirectResult!.ActionName.Should().Be("Index");
		_mockTeamService.Verify(s => s.DeactivateTeamAsync(team.Id), Times.Once);
	}

	[Fact]
	public async Task DeleteConfirmed_WithException_RedirectsWithErrorMessage()
	{
		// Arrange
		var team = _builder.BuildTeam(1);
		_mockTeamService.Setup(s => s.GetTeamByIdAsync(team.Id))
			.ReturnsAsync(team);
		_mockTeamService.Setup(s => s.DeactivateTeamAsync(team.Id))
			.ThrowsAsync(new InvalidOperationException("Cannot delete team with games"));

		// Act
		var result = await _controller.DeleteConfirmed(team.Id);

		// Assert
		result.Should().BeOfType<RedirectToActionResult>();
		_controller.TempData["ErrorMessage"].Should().NotBeNull();
	}
}
