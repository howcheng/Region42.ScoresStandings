using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Exceptions;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Web.Controllers.Api;
using System.Security.Claims;

namespace Region42.ScoresStandings.Web.Tests.Controllers;

public class VolunteerPointsImportControllerTests
{
	private readonly Mock<IVolunteerPointsService> _mockVolunteerPointsService;
	private readonly VolunteerPointsImportController _controller;

	public VolunteerPointsImportControllerTests()
	{
		_mockVolunteerPointsService = new Mock<IVolunteerPointsService>();
		_controller = new VolunteerPointsImportController(
			_mockVolunteerPointsService.Object,
			Mock.Of<ILogger<VolunteerPointsImportController>>());

		var identity = new ClaimsIdentity(
		[
			new Claim("email", "region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com")
		],
		"Bearer");

		_controller.ControllerContext = new ControllerContext
		{
			HttpContext = new DefaultHttpContext
			{
				User = new ClaimsPrincipal(identity)
			}
		};
	}

	[Fact]
	public async Task Import_ReturnsOk_WithImportResult()
	{
		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 1,
			Entries = [new VolunteerPointsEntryDto { TeamId = 10, Round = 1, Points = 2 }]
		};

		var expected = new VolunteerPointsImportResultDto
		{
			ImportedCount = 1,
			AffectedDivisionIds = [1]
		};

		_mockVolunteerPointsService
			.Setup(s => s.BulkImportAsync(request, false, It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
			.ReturnsAsync(expected);

		var result = await _controller.Import(request, dryRun: false, authoritativeSync: false, CancellationToken.None);

		var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
		okResult.Value.Should().BeEquivalentTo(expected);
	}

	[Fact]
	public async Task Import_ReturnsConflict_WhenLockIsHeld()
	{
		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 1,
			Entries = [new VolunteerPointsEntryDto { TeamId = 10, Round = 1, Points = 2 }]
		};

		_mockVolunteerPointsService
			.Setup(s => s.BulkImportAsync(request, false, It.IsAny<string>(), false, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new StorageWriteLockHeldException(Region42.ScoresStandings.Domain.StorageWriteLockNames.VolunteerPointsImport));

		var result = await _controller.Import(request, dryRun: false, authoritativeSync: false, CancellationToken.None);

		result.Result.Should().BeOfType<ConflictObjectResult>();
	}
}
