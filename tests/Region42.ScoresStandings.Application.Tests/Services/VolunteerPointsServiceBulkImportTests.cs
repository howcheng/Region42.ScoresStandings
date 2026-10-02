using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Exceptions;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Application.Tests.Helpers;
using Region42.ScoresStandings.Domain;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Interfaces;
using System.Linq.Expressions;

namespace Region42.ScoresStandings.Application.Tests.Services;

public class VolunteerPointsServiceBulkImportTests
{
	private readonly Mock<IRepository<VolunteerPoints>> _mockVolunteerPointsRepository;
	private readonly Mock<IRepository<Team>> _mockTeamRepository;
	private readonly Mock<IRepository<Division>> _mockDivisionRepository;
	private readonly Mock<IStandingsRefreshService> _mockStandingsRefreshService;
	private readonly Mock<IStorageWriteLock> _mockStorageWriteLock;
	private readonly VolunteerPointsService _service;

	public VolunteerPointsServiceBulkImportTests()
	{
		_mockVolunteerPointsRepository = new Mock<IRepository<VolunteerPoints>>();
		_mockTeamRepository = new Mock<IRepository<Team>>();
		_mockDivisionRepository = new Mock<IRepository<Division>>();
		_mockStandingsRefreshService = new Mock<IStandingsRefreshService>();
		_mockStorageWriteLock = new Mock<IStorageWriteLock>();
		_service = new VolunteerPointsService(
			_mockVolunteerPointsRepository.Object,
			_mockTeamRepository.Object,
			_mockDivisionRepository.Object,
			Mock.Of<ISeasonService>(),
			Mock.Of<IGameService>(),
			_mockStandingsRefreshService.Object,
			_mockStorageWriteLock.Object,
			Mock.Of<ILogger<VolunteerPointsService>>());
	}

	[Fact]
	public async Task BulkImportAsync_DryRun_ValidatesWithoutSaving()
	{
		var division = TestDataBuilder.CreateDivision(id: 5, totalRounds: 10);
		var team = TestDataBuilder.CreateTeam(id: 10, divisionId: 5);

		_mockDivisionRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(division);
		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new[] { team });
		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(Array.Empty<VolunteerPoints>());

		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 5,
			Entries =
			[
				new VolunteerPointsEntryDto { TeamId = 10, Round = 1, Points = 2 }
			]
		};

		var result = await _service.BulkImportAsync(request, dryRun: true, importedBy: "test");

		result.DryRun.Should().BeTrue();
		result.ImportedCount.Should().Be(1);
		result.AffectedDivisionIds.Should().Contain(5);
		_mockStandingsRefreshService.Verify(s => s.RefreshDivisionStandingsAsync(It.IsAny<int>()), Times.Never);
		_mockVolunteerPointsRepository.Verify(r => r.SaveChangesAsync(), Times.Never);
		_mockStorageWriteLock.Verify(
			l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
			Times.Never);
	}

	[Fact]
	public async Task BulkImportAsync_RefreshesStandingsOncePerDivision()
	{
		var division = TestDataBuilder.CreateDivision(id: 5, totalRounds: 10);
		var team = TestDataBuilder.CreateTeam(id: 10, divisionId: 5);

		_mockDivisionRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(division);
		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new[] { team });
		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync(Array.Empty<VolunteerPoints>());
		_mockStorageWriteLock
			.Setup(l => l.TryAcquireAsync(StorageWriteLockNames.VolunteerPointsImport, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync(new TestStorageWriteLockHandle());

		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 5,
			Entries =
			[
				new VolunteerPointsEntryDto { TeamId = 10, Round = 1, Points = 2 },
				new VolunteerPointsEntryDto { TeamId = 10, Round = 2, Points = 1 }
			]
		};

		var result = await _service.BulkImportAsync(request, dryRun: false, importedBy: "volunteer-sync");

		result.ImportedCount.Should().Be(2);
		_mockStandingsRefreshService.Verify(s => s.RefreshDivisionStandingsAsync(5), Times.Once);
		_mockVolunteerPointsRepository.Verify(r => r.SaveChangesAsync(), Times.Once);
	}

	[Fact]
	public async Task BulkImportAsync_ThrowsWhenLockCannotBeAcquired()
	{
		var division = TestDataBuilder.CreateDivision(id: 5);
		_mockDivisionRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(division);
		_mockStorageWriteLock
			.Setup(l => l.TryAcquireAsync(StorageWriteLockNames.VolunteerPointsImport, It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync((IStorageWriteLockHandle?)null);

		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 5,
			Entries = [new VolunteerPointsEntryDto { TeamId = 10, Round = 1, Points = 1 }]
		};

		var act = () => _service.BulkImportAsync(request, dryRun: false, importedBy: "volunteer-sync");

		await act.Should().ThrowAsync<StorageWriteLockHeldException>();
	}

	[Fact]
	public async Task BulkImportAsync_AuthoritativeSync_DryRun_ReportsStaleZeroes()
	{
		var division = TestDataBuilder.CreateDivision(id: 5, totalRounds: 10);
		var team = TestDataBuilder.CreateTeam(id: 10, divisionId: 5);

		_mockDivisionRepository.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(division);
		_mockTeamRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Team, bool>>>()))
			.ReturnsAsync(new[] { team });

		var stale = TestDataBuilder.CreateVolunteerPoints(id: 99, teamId: 10, round: 3, points: 2);
		stale.ModifiedBy = "region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com";
		stale.Team = team;

		_mockVolunteerPointsRepository
			.Setup(r => r.FindAsync(It.IsAny<Expression<Func<VolunteerPoints, bool>>>()))
			.ReturnsAsync((Expression<Func<VolunteerPoints, bool>> predicate) =>
			{
				var compiled = predicate.Compile();
				var all = new VolunteerPoints[] { stale };
				return all.Where(compiled).ToList();
			});

		var request = new VolunteerPointsBulkUpdateDto
		{
			DivisionId = 5,
			Entries = [new VolunteerPointsEntryDto { TeamId = 10, Round = 1, Points = 1.5m }]
		};

		var result = await _service.BulkImportAsync(
			request,
			dryRun: true,
			importedBy: "region42-volunteer-sync@ayso-region-42.iam.gserviceaccount.com",
			authoritativeSync: true);

		result.ImportedCount.Should().Be(1);
		result.StaleZeroedCount.Should().Be(1);
	}

	private sealed class TestStorageWriteLockHandle : IStorageWriteLockHandle
	{
		public string LockName => StorageWriteLockNames.VolunteerPointsImport;
		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
