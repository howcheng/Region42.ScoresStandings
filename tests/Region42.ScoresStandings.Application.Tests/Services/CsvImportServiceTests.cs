using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Tests.Services;

/// <summary>
/// Unit tests for CsvImportService focusing on CSV parsing, filtering, and validation logic.
/// </summary>
public class CsvImportServiceTests
{
	private readonly Mock<IRegion42DbContext> _mockDbContext;
	private readonly Mock<IRepository<Season>> _mockSeasonRepository;
	private readonly Mock<IRepository<Division>> _mockDivisionRepository;
	private readonly Mock<IRepository<Team>> _mockTeamRepository;
	private readonly Mock<IRepository<Game>> _mockGameRepository;
	private readonly Mock<ILogger<CsvImportService>> _mockLogger;
	private readonly CsvImportService _service;

	public CsvImportServiceTests()
	{
		_mockDbContext = new Mock<IRegion42DbContext>();
		_mockSeasonRepository = new Mock<IRepository<Season>>();
		_mockDivisionRepository = new Mock<IRepository<Division>>();
		_mockTeamRepository = new Mock<IRepository<Team>>();
		_mockGameRepository = new Mock<IRepository<Game>>();
		_mockLogger = new Mock<ILogger<CsvImportService>>();

		_service = new CsvImportService(
			_mockDbContext.Object,
			_mockSeasonRepository.Object,
			_mockDivisionRepository.Object,
			_mockTeamRepository.Object,
			_mockGameRepository.Object,
			_mockLogger.Object
		);
	}

	#region CSV Parsing Tests

	[Fact]
	public async Task ValidateCsvAsync_WithEmptyCsv_ReturnsErrorMessage()
	{
		// Arrange
		var csvContent = "";
		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.Contains("empty") || e.Contains("parsed"));
	}

	[Fact]
	public async Task ValidateCsvAsync_WithHeaderOnly_ReturnsErrorMessage()
	{
		// Arrange
		var csvContent = "Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status";
		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.Contains("empty") || e.Contains("parsed"));
	}

	#endregion

	#region Filtering Tests

	[Fact]
	public async Task ValidateCsvAsync_WithPracticeRows_SkipsThem()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
122142854,Region 42 Fall 2025 - 12U - Girls (Practices),Region 42 Fall 2025 - 12U - Girls (Practices)-Group,12UG07 (MIller),Practice,09/09/2025,6:30 PM,8:00 PM,DV 3A,Dos Vientos,john,miller,,,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(1);
		result.ValidRows.Should().Be(0);
	}

	[Fact]
	public async Task ValidateCsvAsync_With16UGames_SkipsThem()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
122145689,Region 42 Fall 2025 - 16U - Girls (Games),Region 42 Fall 2025 - 16U - Girls (Games)-Group,16UG01,16UG02,09/14/2025,9:00 AM,10:30 AM,Borchard C,Borchard Park,Debra,Heirshberg,Jane,Doe,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(1);
		result.ValidRows.Should().Be(0);
		result.Warnings.Should().Contain(w => w.Contains("No valid game rows found"));
	}

	[Fact]
	public async Task ValidateCsvAsync_With10UGames_ProcessesThem()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Region 42 Fall 2025 - 10U - Boys (Games)-Group,10UB01,10UB02,09/14/2025,9:00 AM,10:30 AM,Field 1,Park A,John,Smith,Jane,Doe,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(0);
		result.ValidRows.Should().Be(1);
		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateCsvAsync_With12UGames_ProcessesThem()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123457,Region 42 Fall 2025 - 12U - Girls (Games),Region 42 Fall 2025 - 12U - Girls (Games)-Group,12UG01,12UG02,09/14/2025,9:00 AM,10:30 AM,Field 2,Park B,Mary,Johnson,Bob,Williams,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(0);
		result.ValidRows.Should().Be(1);
		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateCsvAsync_With14UGames_ProcessesThem()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123458,Region 42 Fall 2025 - 14U - Boys (Games),Region 42 Fall 2025 - 14U - Boys (Games)-Group,14UB01,14UB02,09/14/2025,11:00 AM,12:30 PM,Field 3,Park C,Tom,Brown,Alice,Davis,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(0);
		result.ValidRows.Should().Be(1);
		result.IsValid.Should().BeTrue();
	}

	[Fact]
	public async Task ValidateCsvAsync_WithBoardMemberRows_SkipsThem()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
122145715,2025 Board Members - Board Member  (Practices),2025 Board Members - Board Member  (Practices)-Group,Borchard B unavailable for,Practice,09/08/2025,6:30 PM,8:00 PM,Borchard B,Borchard Park,,,,,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(1);
		result.ValidRows.Should().Be(0);
	}

	#endregion

	#region Validation Tests

	[Fact]
	public async Task ValidateCsvAsync_WithMissingHomeTeam_ReturnsValidationError()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Region 42 Fall 2025 - 10U - Boys (Games)-Group,,10UB02,09/14/2025,9:00 AM,10:30 AM,Field 1,Park A,,,Jane,Doe,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.Contains("Home team is required"));
	}

	[Fact]
	public async Task ValidateCsvAsync_WithMissingAwayTeam_ReturnsValidationError()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Region 42 Fall 2025 - 10U - Boys (Games)-Group,10UB01,,09/14/2025,9:00 AM,10:30 AM,Field 1,Park A,John,Smith,,,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.SkippedRows.Should().Be(1); // Skipped because AwayTeam is empty
		result.ValidRows.Should().Be(0);
	}

	[Fact]
	public async Task ValidateCsvAsync_WithSameHomeAndAwayTeam_ReturnsValidationError()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Region 42 Fall 2025 - 10U - Boys (Games)-Group,10UB01,10UB01,09/14/2025,9:00 AM,10:30 AM,Field 1,Park A,John,Smith,John,Smith,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.Contains("cannot be the same"));
	}

	[Fact]
	public async Task ValidateCsvAsync_WithInvalidDate_ReturnsValidationError()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Region 42 Fall 2025 - 10U - Boys (Games)-Group,10UB01,10UB02,INVALID-DATE,9:00 AM,10:30 AM,Field 1,Park A,John,Smith,Jane,Doe,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.Contains("Could not parse date/time"));
	}

	[Fact]
	public async Task ValidateCsvAsync_WithMultipleErrors_ReturnsAllErrors()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Region 42 Fall 2025 - 10U - Boys (Games)-Group,10UB01,10UB01,INVALID,INVALID,10:30 AM,Field 1,Park A,John,Smith,John,Smith,,,
123457,Region 42 Fall 2025 - 12U - Girls (Games),Region 42 Fall 2025 - 12U - Girls (Games)-Group,,12UG02,09/14/2025,9:00 AM,10:30 AM,Field 2,Park B,,,Bob,Williams,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().HaveCountGreaterThan(1); // Multiple errors from both rows
		result.Errors.Should().Contain(e => e.Contains("cannot be the same"));
		result.Errors.Should().Contain(e => e.Contains("Could not parse date/time"));
		result.Errors.Should().Contain(e => e.Contains("Home team is required"));
	}

	#endregion

	#region Age Group and Gender Parsing Tests

	[Fact]
	public async Task ValidateCsvAsync_ParsesAgeGroupCorrectly()
	{
		// Arrange - Test all three age groups
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Group1,10UB01,10UB02,09/14/2025,9:00 AM,10:30 AM,Field 1,Park A,John,Smith,Jane,Doe,,,
123457,Region 42 Fall 2025 - 12U - Girls (Games),Group2,12UG01,12UG02,09/14/2025,11:00 AM,12:30 PM,Field 2,Park B,Mary,Johnson,Bob,Williams,,,
123458,Region 42 Fall 2025 - 14U - Boys (Games),Group3,14UB01,14UB02,09/14/2025,1:00 PM,2:30 PM,Field 3,Park C,Tom,Brown,Alice,Davis,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeTrue();
		result.ValidRows.Should().Be(3);
		result.Errors.Should().BeEmpty();
	}

	[Fact]
	public async Task ValidateCsvAsync_ParsesGenderCorrectly()
	{
		// Arrange - Test both genders
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
123456,Region 42 Fall 2025 - 10U - Boys (Games),Group1,10UB01,10UB02,09/14/2025,9:00 AM,10:30 AM,Field 1,Park A,John,Smith,Jane,Doe,,,
123457,Region 42 Fall 2025 - 12U - Girls (Games),Group2,12UG01,12UG02,09/14/2025,11:00 AM,12:30 PM,Field 2,Park B,Mary,Johnson,Bob,Williams,,,";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync(new Season { Id = seasonId });

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeTrue();
		result.ValidRows.Should().Be(2);
		result.Errors.Should().BeEmpty();
	}

	#endregion

	#region Season Validation Tests

	[Fact]
	public async Task ValidateCsvAsync_WithInvalidSeasonId_ReturnsError()
	{
		// Arrange
		var csvContent = "Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status";
		var stream = CreateStreamFromString(csvContent);
		var seasonId = 999;

		_mockSeasonRepository
			.Setup(r => r.GetByIdAsync(seasonId))
			.ReturnsAsync((Season?)null);

		// Act
		var result = await _service.ValidateCsvAsync(stream, seasonId);

		// Assert
		result.IsValid.Should().BeFalse();
		result.Errors.Should().Contain(e => e.Contains("Season") && e.Contains("not found"));
	}

	[Fact]
	public async Task ImportCsvAsync_WithAwayRegionTeams_TransformsNamesAndMarksCorrectly()
	{
		// Arrange
		var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
1,2025 Games 14UB-Group,Group A,14UB01 Eagles (Smith),121b,2025-03-15,10:00,11:00,Field 1,Main Field,John,Smith,,,,,Scheduled";

		var stream = CreateStreamFromString(csvContent);
		var seasonId = 1;

		var season = new Season { Id = seasonId, Name = "Fall 2025", Year = 2025, IsActive = true };
		var division = new Division { Id = 1, SeasonId = seasonId, AgeGroup = AgeGroup.U14, Gender = Gender.Boys };

		// Setup transaction mock
		var mockTransaction = new Mock<IDbTransaction>();
		_mockDbContext.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync(mockTransaction.Object);

		_mockSeasonRepository.Setup(r => r.GetByIdAsync(seasonId)).ReturnsAsync(season);
		_mockDivisionRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Division, bool>>>()))
			.ReturnsAsync(new List<Division> { division });
		_mockDivisionRepository.Setup(r => r.GetByIdAsync(division.Id)).ReturnsAsync(division);

		Team? capturedHomeTeam = null;
		Team? capturedAwayTeam = null;
		var addedTeams = new List<Team>();

		// Initially return empty list (no existing teams)
		_mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
			.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
			{
				// After teams are added, return them when queried
				return addedTeams.Where(predicate.Compile()).ToList();
			});

		_mockGameRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
			.ReturnsAsync(new List<Game>());

		_mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
			.Callback<Team>(team =>
			{
				team.Id = addedTeams.Count + 1;
				addedTeams.Add(team);
				if (capturedHomeTeam == null)
					capturedHomeTeam = team;
				else if (capturedAwayTeam == null)
					capturedAwayTeam = team;
			})
			.Returns(Task.CompletedTask);

		// Act
		var result = await _service.ImportCsvAsync(stream, seasonId);

		// Assert
		result.Success.Should().BeTrue();
		result.GamesCreated.Should().Be(1);
		result.TeamsCreated.Should().Be(2);

		// Verify home team (Region 42)
		capturedHomeTeam.Should().NotBeNull();
		capturedHomeTeam!.Name.Should().Be("14UB01 Eagles (Smith)");
		capturedHomeTeam.IsRegion42Team.Should().BeTrue();
		capturedHomeTeam.ContactName.Should().Be("John Smith");

		// Verify away team (transformed from "121b" where b = 2nd team)
		capturedAwayTeam.Should().NotBeNull();
		capturedAwayTeam!.Name.Should().Be("R121-14UB02");
		capturedAwayTeam.IsRegion42Team.Should().BeFalse();
		capturedAwayTeam.ContactName.Should().Be("Away Region Team");

			// Verify transaction was committed
			mockTransaction.Verify(t => t.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
			mockTransaction.Verify(t => t.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
		}

		[Fact]
		public async Task ImportCsvAsync_WithMultipleGames_DoesNotCreateDuplicateTeams()
		{
			// Arrange - CSV with 3 games where teams appear multiple times
			// Team "14UB01" appears in 2 games, Team "14UB02" appears in 2 games, Team "14UB03" appears once
			var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
		1,2025 Games 14UB-Group,Group A,14UB01 Eagles (Smith),14UB02 Tigers (Jones),2025-03-15,10:00,11:00,Field 1,Main Field,John,Smith,Mike,Jones,,,Scheduled
		2,2025 Games 14UB-Group,Group A,14UB03 Bears (Brown),14UB01 Eagles (Smith),2025-03-15,11:30,12:30,Field 2,Main Field,Tom,Brown,John,Smith,,,Scheduled
		3,2025 Games 14UB-Group,Group A,14UB02 Tigers (Jones),14UB03 Bears (Brown),2025-03-15,13:00,14:00,Field 1,Main Field,Mike,Jones,Tom,Brown,,,Scheduled";

			var stream = CreateStreamFromString(csvContent);
			var seasonId = 1;

			var season = new Season { Id = seasonId, Name = "Fall 2025", Year = 2025, IsActive = true };
			var division = new Division { Id = 1, SeasonId = seasonId, AgeGroup = AgeGroup.U14, Gender = Gender.Boys };

			// Setup transaction mock
			var mockTransaction = new Mock<IDbTransaction>();
			_mockDbContext.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
				.ReturnsAsync(mockTransaction.Object);

			_mockSeasonRepository.Setup(r => r.GetByIdAsync(seasonId)).ReturnsAsync(season);
			_mockDivisionRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Division, bool>>>()))
				.ReturnsAsync(new List<Division> { division });
			_mockDivisionRepository.Setup(r => r.GetByIdAsync(division.Id)).ReturnsAsync(division);

			var addedTeams = new List<Team>();
			int teamIdCounter = 1;

			// Track teams added to verify no duplicates
			_mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
				.Callback<Team>(team =>
				{
					team.Id = teamIdCounter++;
					addedTeams.Add(team);
				})
				.Returns(Task.CompletedTask);

			// Initially return empty list (no existing teams)
			_mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
				.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
				{
					// After teams are added, return them when queried
					return addedTeams.Where(predicate.Compile()).ToList();
				});

			_mockGameRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
				.ReturnsAsync(new List<Game>());

			// Act
			var result = await _service.ImportCsvAsync(stream, seasonId);

			// Assert
			result.Success.Should().BeTrue();
			result.GamesCreated.Should().Be(3, "because there are 3 games in the CSV");
			result.TeamsCreated.Should().Be(3, "because there are only 3 unique teams (14UB01, 14UB02, 14UB03)");

			// Verify exactly 3 teams were added (no duplicates)
			addedTeams.Should().HaveCount(3, "each unique team should be created exactly once");

			// Verify team names are unique
			var teamNames = addedTeams.Select(t => t.Name).ToList();
			teamNames.Should().OnlyHaveUniqueItems("no team should be created more than once");
			teamNames.Should().Contain("14UB01 Eagles (Smith)");
			teamNames.Should().Contain("14UB02 Tigers (Jones)");
			teamNames.Should().Contain("14UB03 Bears (Brown)");

				// Verify AddAsync was called exactly 3 times (once per unique team)
				_mockTeamRepository.Verify(r => r.AddAsync(It.IsAny<Team>()), Times.Exactly(3));
			}

			[Fact]
			public async Task ImportCsvAsync_WithGamesOnDifferentDates_AssignsCorrectRoundNumbers()
			{
				// Arrange - CSV with 5 games across 3 different dates
				// Round 1: 2 games on March 15
				// Round 2: 2 games on March 22
				// Round 3: 1 game on March 29
				var csvContent = @"Match ID,Event Name,Group Name,Home Team,Away Team,Date,Start Time,End Time,Field,Location,Home Team Head Coach First Name,Home Team Head Coach Last Name,Away Team Head Coach First Name,Away Team Head Coach Last Name,Home Team Score,Away Team Score,Scheduled Status
			1,2025 Games 14UB-Group,Group A,14UB01 Eagles (Smith),14UB02 Tigers (Jones),2025-03-15,10:00,11:00,Field 1,Main Field,John,Smith,Mike,Jones,,,Scheduled
			2,2025 Games 14UB-Group,Group A,14UB03 Bears (Brown),14UB04 Lions (Davis),2025-03-15,11:30,12:30,Field 2,Main Field,Tom,Brown,Sarah,Davis,,,Scheduled
			3,2025 Games 14UB-Group,Group A,14UB01 Eagles (Smith),14UB03 Bears (Brown),2025-03-22,10:00,11:00,Field 1,Main Field,John,Smith,Tom,Brown,,,Scheduled
			4,2025 Games 14UB-Group,Group A,14UB02 Tigers (Jones),14UB04 Lions (Davis),2025-03-22,11:30,12:30,Field 2,Main Field,Mike,Jones,Sarah,Davis,,,Scheduled
			5,2025 Games 14UB-Group,Group A,14UB01 Eagles (Smith),14UB04 Lions (Davis),2025-03-29,10:00,11:00,Field 1,Main Field,John,Smith,Sarah,Davis,,,Scheduled";

				var stream = CreateStreamFromString(csvContent);
				var seasonId = 1;

				var season = new Season { Id = seasonId, Name = "Fall 2025", Year = 2025, IsActive = true };
				var division = new Division { Id = 1, SeasonId = seasonId, AgeGroup = AgeGroup.U14, Gender = Gender.Boys };

				// Setup transaction mock
				var mockTransaction = new Mock<IDbTransaction>();
				_mockDbContext.Setup(db => db.BeginTransactionAsync(It.IsAny<CancellationToken>()))
					.ReturnsAsync(mockTransaction.Object);

				_mockSeasonRepository.Setup(r => r.GetByIdAsync(seasonId)).ReturnsAsync(season);
				_mockDivisionRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Division, bool>>>()))
					.ReturnsAsync(new List<Division> { division });
				_mockDivisionRepository.Setup(r => r.GetByIdAsync(division.Id)).ReturnsAsync(division);

				var addedTeams = new List<Team>();
				var addedGames = new List<Game>();
				int teamIdCounter = 1;
				int gameIdCounter = 1;

				// Track teams
				_mockTeamRepository.Setup(r => r.AddAsync(It.IsAny<Team>()))
					.Callback<Team>(team =>
					{
						team.Id = teamIdCounter++;
						addedTeams.Add(team);
					})
					.Returns(Task.CompletedTask);

				_mockTeamRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Team, bool>>>()))
					.ReturnsAsync((System.Linq.Expressions.Expression<Func<Team, bool>> predicate) =>
					{
						return addedTeams.Where(predicate.Compile()).ToList();
					});

				// Track games to verify round numbers
				_mockGameRepository.Setup(r => r.AddAsync(It.IsAny<Game>()))
					.Callback<Game>(game =>
					{
						game.Id = gameIdCounter++;
						addedGames.Add(game);
					})
					.Returns(Task.CompletedTask);

				_mockGameRepository.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Game, bool>>>()))
					.ReturnsAsync(new List<Game>());

				// Act
				var result = await _service.ImportCsvAsync(stream, seasonId);

				// Assert
				result.Success.Should().BeTrue();
				result.GamesCreated.Should().Be(5, "because there are 5 games in the CSV");

				// Verify games were created
				addedGames.Should().HaveCount(5);

				// Group games by date to verify round numbers
				var gamesByDate = addedGames.GroupBy(g => g.ScheduledDateTime.Date).OrderBy(g => g.Key).ToList();

				// Round 1: March 15 (2 games)
				var round1Games = gamesByDate[0].ToList();
				round1Games.Should().HaveCount(2);
				round1Games.Should().AllSatisfy(g => g.Round.Should().Be(1, "all games on March 15 should be Round 1"));
				round1Games[0].ScheduledDateTime.Date.Should().Be(new DateTime(2025, 3, 15));

				// Round 2: March 22 (2 games)
				var round2Games = gamesByDate[1].ToList();
				round2Games.Should().HaveCount(2);
				round2Games.Should().AllSatisfy(g => g.Round.Should().Be(2, "all games on March 22 should be Round 2"));
				round2Games[0].ScheduledDateTime.Date.Should().Be(new DateTime(2025, 3, 22));

				// Round 3: March 29 (1 game)
				var round3Games = gamesByDate[2].ToList();
				round3Games.Should().HaveCount(1);
				round3Games[0].Round.Should().Be(3, "game on March 29 should be Round 3");
				round3Games[0].ScheduledDateTime.Date.Should().Be(new DateTime(2025, 3, 29));
			}

	#endregion

	#region Helper Methods

	private static Stream CreateStreamFromString(string content)
	{
		var bytes = Encoding.UTF8.GetBytes(content);
		return new MemoryStream(bytes);
	}

	#endregion
}
