using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Region42.ScoresStandings.Application.DTOs;
using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

/// <summary>
/// Service for importing teams and games from CSV files exported from SportsConnect.
/// Implements comprehensive validation that shows ALL errors before allowing import.
/// Only processes events with "Games" in the event name and containing "10U", "12U", or "14U".
/// </summary>
public class CsvImportService : ICsvImportService
{
	private readonly IRegion42DbContext _dbContext;
	private readonly IRepository<Season> _seasonRepository;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Team> _teamRepository;
	private readonly IRepository<Game> _gameRepository;
	private readonly ILogger<CsvImportService> _logger;

	public CsvImportService(
		IRegion42DbContext dbContext,
		IRepository<Season> seasonRepository,
		IRepository<Division> divisionRepository,
		IRepository<Team> teamRepository,
		IRepository<Game> gameRepository,
		ILogger<CsvImportService> logger)
	{
		_dbContext = dbContext;
		_seasonRepository = seasonRepository;
		_divisionRepository = divisionRepository;
		_teamRepository = teamRepository;
		_gameRepository = gameRepository;
		_logger = logger;
	}

	public async Task<CsvValidationResult> ValidateCsvAsync(Stream csvStream, int seasonId)
	{
		var result = new CsvValidationResult();

		try
		{
			// Verify season exists
			var season = await _seasonRepository.GetByIdAsync(seasonId);
			if (season == null)
			{
				result.Errors.Add($"Season with ID {seasonId} not found.");
				return result;
			}

			// Parse CSV
			var rows = await ParseCsvAsync(csvStream);
			result.TotalRows = rows.Count;

			if (rows.Count == 0)
			{
				result.Errors.Add("CSV file is empty or could not be parsed.");
				return result;
			}

			// Filter and validate rows
			foreach (var row in rows)
			{
				ProcessRow(row);

				if (!row.ShouldImport)
				{
					result.SkippedRows++;
					continue;
				}

				if (row.ValidationErrors.Any())
				{
					result.Errors.AddRange(row.ValidationErrors.Select(e => $"Row {rows.IndexOf(row) + 2} (Match {row.MatchId}): {e}"));
				}
				else
				{
					result.ValidRows++;
				}
			}

			result.IsValid = result.Errors.Count == 0 && result.ValidRows > 0;

			if (result.ValidRows == 0 && result.Errors.Count == 0)
			{
				result.Warnings.Add("No valid game rows found to import. Ensure CSV contains events with 'Games' and age groups 10U, 12U, or 14U.");
			}

			_logger.LogInformation("CSV validation completed: {ValidRows} valid, {ErrorCount} errors, {SkippedRows} skipped",
				result.ValidRows, result.Errors.Count, result.SkippedRows);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error validating CSV");
			result.Errors.Add($"Error reading CSV file: {ex.Message}");
		}

		return result;
	}

	public async Task<CsvImportResult> ImportCsvAsync(Stream csvStream, int seasonId)
	{
		var importResult = new CsvImportResult();

		// First validate
		csvStream.Position = 0; // Reset stream
		var validationResult = await ValidateCsvAsync(csvStream, seasonId);

		if (!validationResult.IsValid)
		{
			importResult.Success = false;
			importResult.Message = "Validation failed. Cannot import.";
			importResult.Errors = validationResult.Errors;
			return importResult;
		}

		// Use a database transaction to ensure atomicity (all-or-nothing)
		await using var transaction = await _dbContext.BeginTransactionAsync();

		try
		{
			// Parse CSV again for import
			csvStream.Position = 0;
			var rows = await ParseCsvAsync(csvStream);

			// Process each row to set ShouldImport flag and parse fields
			foreach (var row in rows)
			{
				ProcessRow(row);
			}

			// Get or create divisions
			var divisionMap = await GetOrCreateDivisionsAsync(seasonId, rows);

			// Save divisions first to ensure they have IDs
			await _divisionRepository.SaveChangesAsync();

			// Process teams first - collect all unique team names per division
			var validRows = rows.Where(r => r.ShouldImport && !r.ValidationErrors.Any()).ToList();

			// Dictionary to track unique teams we've already processed in this import: (divisionId, originalTeamName) -> team info
			var processedTeams = new HashSet<(int divisionId, string originalTeamName)>();

			foreach (var row in validRows)
			{
				var division = divisionMap[(row.ParsedAgeGroup!.Value, row.ParsedGender!.Value)];

				// Process home team if not already processed
				var homeKey = (division.Id, row.HomeTeam);
				if (!processedTeams.Contains(homeKey))
				{
					await GetOrCreateTeamAsync(row.HomeTeam, division.Id, 
						row.HomeTeamHeadCoachFirstName, row.HomeTeamHeadCoachLastName, importResult);
					processedTeams.Add(homeKey);
				}

				// Process away team if not already processed
				var awayKey = (division.Id, row.AwayTeam);
				if (!processedTeams.Contains(awayKey))
				{
					await GetOrCreateTeamAsync(row.AwayTeam, division.Id,
						row.AwayTeamHeadCoachFirstName, row.AwayTeamHeadCoachLastName, importResult);
					processedTeams.Add(awayKey);
				}
			}

			// Save all teams to database so they have IDs before creating games
			await _teamRepository.SaveChangesAsync();

			// Now process games - teams will have valid IDs
			// Group rows by division for round calculation
			var rowsByDivision = validRows.GroupBy(r => (r.ParsedAgeGroup!.Value, r.ParsedGender!.Value)).ToDictionary(g => g.Key, g => g.ToList());

			foreach (var row in validRows)
			{
				var divisionKey = (row.ParsedAgeGroup!.Value, row.ParsedGender!.Value);
				var divisionRows = rowsByDivision[divisionKey];
				await ProcessGameRowAsync(row, seasonId, divisionMap, divisionRows, importResult);
				importResult.RowsProcessed++;
			}

			importResult.RowsSkipped = validationResult.SkippedRows;

			// Save games
			await _gameRepository.SaveChangesAsync();

			// Commit transaction - all changes succeed together
			await transaction.CommitAsync();

			importResult.Success = true;
			importResult.Message = $"Import completed successfully. Created {importResult.TeamsCreated} teams, updated {importResult.TeamsUpdated} teams, created {importResult.GamesCreated} games.";

			_logger.LogInformation("CSV import completed: {TeamsCreated} teams created, {GamesCreated} games created",
				importResult.TeamsCreated, importResult.GamesCreated);
		}
		catch (Exception ex)
		{
			// Rollback transaction on any error - no orphaned records
			await transaction.RollbackAsync();

			_logger.LogError(ex, "Error importing CSV - transaction rolled back");
			importResult.Success = false;
			importResult.Message = $"Import failed: {ex.Message}";
			importResult.Errors.Add(ex.Message);
		}

		return importResult;
	}

	public async Task<CsvPreviewResult> PreviewImportAsync(Stream csvStream, int seasonId)
	{
		var preview = new CsvPreviewResult();

		try
		{
			csvStream.Position = 0;
			preview.Validation = await ValidateCsvAsync(csvStream, seasonId);

			if (!preview.Validation.IsValid)
			{
				return preview;
			}

			// Parse for preview
			csvStream.Position = 0;
			var rows = await ParseCsvAsync(csvStream);

			// Process each row to populate ShouldImport and parsed fields
			foreach (var row in rows)
			{
				ProcessRow(row);
			}

			var validRows = rows.Where(r => r.ShouldImport && !r.ValidationErrors.Any()).ToList();

			// Get or create divisions for the season to get division IDs for team name transformation
			var divisionsDict = await GetOrCreateDivisionsAsync(seasonId, validRows);

			// Extract unique teams with transformed names
			var teamNames = new HashSet<string>();
			var teamDivisions = new Dictionary<string, int>(); // Maps team name to division ID

			foreach (var row in validRows)
			{
				var divisionKey = (row.ParsedAgeGroup!.Value, row.ParsedGender!.Value);
				if (!divisionsDict.TryGetValue(divisionKey, out var division))
					continue;

				// Transform home team name if needed
				if (!string.IsNullOrWhiteSpace(row.HomeTeam) && row.HomeTeam != "Practice")
				{
					var (_, transformedHome) = await TransformAwayRegionTeamNameAsync(row.HomeTeam, division.Id);
					teamNames.Add(transformedHome);
					teamDivisions[transformedHome] = division.Id;
				}

				// Transform away team name if needed
				if (!string.IsNullOrWhiteSpace(row.AwayTeam) && row.AwayTeam != "Practice")
				{
					var (_, transformedAway) = await TransformAwayRegionTeamNameAsync(row.AwayTeam, division.Id);
					teamNames.Add(transformedAway);
					teamDivisions[transformedAway] = division.Id;
				}
			}

			// Get existing teams
			var existingTeams = await _teamRepository.GetAllAsync();
			var existingTeamNames = new HashSet<string>(existingTeams.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

			// Add team previews with transformed names
			foreach (var row in validRows)
			{
				var divisionKey = (row.ParsedAgeGroup!.Value, row.ParsedGender!.Value);
				if (!divisionsDict.TryGetValue(divisionKey, out var division))
					continue;

				if (!string.IsNullOrWhiteSpace(row.HomeTeam) && row.HomeTeam != "Practice")
				{
					var (_, transformedHome) = await TransformAwayRegionTeamNameAsync(row.HomeTeam, division.Id);
					AddTeamPreview(preview.Teams, transformedHome, row, existingTeamNames, isHomeTeam: true);
				}

				if (!string.IsNullOrWhiteSpace(row.AwayTeam) && row.AwayTeam != "Practice")
				{
					var (_, transformedAway) = await TransformAwayRegionTeamNameAsync(row.AwayTeam, division.Id);
					AddTeamPreview(preview.Teams, transformedAway, row, existingTeamNames, isHomeTeam: false);
				}
			}

			// Add game previews with transformed names (limit to first 50 for performance)
			var gamePreviews = new List<CsvGamePreview>();
			foreach (var row in validRows.Take(50))
			{
				var divisionKey = (row.ParsedAgeGroup!.Value, row.ParsedGender!.Value);
				if (!divisionsDict.TryGetValue(divisionKey, out var division))
					continue;

				var (_, transformedHome) = await TransformAwayRegionTeamNameAsync(row.HomeTeam, division.Id);
				var (_, transformedAway) = await TransformAwayRegionTeamNameAsync(row.AwayTeam, division.Id);

				gamePreviews.Add(new CsvGamePreview
				{
					HomeTeam = transformedHome,
					AwayTeam = transformedAway,
					ScheduledDateTime = row.ParsedScheduledDateTime ?? DateTime.MinValue,
					Location = $"{row.Field} - {row.Location}",
					Round = CalculateRound(row.ParsedScheduledDateTime ?? DateTime.MinValue, validRows),
					EventName = row.EventName
				});
			}

			preview.Games = gamePreviews;

			if (validRows.Count > 50)
			{
				preview.Validation.Warnings.Add($"Showing first 50 of {validRows.Count} games in preview.");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error previewing CSV import");
			preview.Validation.Errors.Add($"Error previewing import: {ex.Message}");
		}

		return preview;
	}

	#region Private Helper Methods

	private static async Task<List<CsvGameRowDto>> ParseCsvAsync(Stream csvStream)
	{
		var config = new CsvConfiguration(CultureInfo.InvariantCulture)
		{
			HasHeaderRecord = true,
			MissingFieldFound = null,
			HeaderValidated = null,
			TrimOptions = TrimOptions.Trim
		};

		using var reader = new StreamReader(csvStream, leaveOpen: true);
		using var csv = new CsvReader(reader, config);

		csv.Context.RegisterClassMap<CsvGameRowMap>();

		var records = new List<CsvGameRowDto>();
		await foreach (var record in csv.GetRecordsAsync<CsvGameRowDto>())
		{
			records.Add(record);
		}

		return records;
	}

	private void ProcessRow(CsvGameRowDto row)
	{
		// Check if this is a game row (not practice)
		if (!row.EventName.Contains("Games", StringComparison.OrdinalIgnoreCase))
		{
			row.ShouldImport = false;
			return;
		}

		// Check if age group is in scope (10U, 12U, 14U)
		if (!row.EventName.Contains("10U") && !row.EventName.Contains("12U") && !row.EventName.Contains("14U"))
		{
			row.ShouldImport = false;
			return;
		}

		// Check for "Practice" in team names
		if (row.HomeTeam.Equals("Practice", StringComparison.OrdinalIgnoreCase) ||
			row.AwayTeam.Equals("Practice", StringComparison.OrdinalIgnoreCase) ||
			string.IsNullOrWhiteSpace(row.AwayTeam))
		{
			row.ShouldImport = false;
			return;
		}

		row.ShouldImport = true;

		// Parse age group
		if (row.EventName.Contains("10U"))
			row.ParsedAgeGroup = AgeGroup.U10;
		else if (row.EventName.Contains("12U"))
			row.ParsedAgeGroup = AgeGroup.U12;
		else if (row.EventName.Contains("14U"))
			row.ParsedAgeGroup = AgeGroup.U14;

		// Parse gender - handle two formats:
		// 1. "Region 42 Fall 2025 - 10U - Boys (Games)" - contains full word "Boys" or "Girls"
		// 2. "2025 Games 14UB-Group" - contains "B" or "G" after age group
		if (row.EventName.Contains("Boys", StringComparison.OrdinalIgnoreCase))
		{
			row.ParsedGender = Gender.Boys;
		}
		else if (row.EventName.Contains("Girls", StringComparison.OrdinalIgnoreCase))
		{
			row.ParsedGender = Gender.Girls;
		}
		else if (row.EventName.Contains("10UB") || row.EventName.Contains("12UB") || row.EventName.Contains("14UB"))
		{
			row.ParsedGender = Gender.Boys;
		}
		else if (row.EventName.Contains("10UG") || row.EventName.Contains("12UG") || row.EventName.Contains("14UG"))
		{
			row.ParsedGender = Gender.Girls;
		}

		// Validate required fields
		if (!row.ParsedAgeGroup.HasValue)
		{
			row.ValidationErrors.Add("Could not determine age group from event name.");
		}

		if (!row.ParsedGender.HasValue)
		{
			row.ValidationErrors.Add("Could not determine gender from event name.");
		}

		if (string.IsNullOrWhiteSpace(row.HomeTeam))
		{
			row.ValidationErrors.Add("Home team is required.");
		}

		if (string.IsNullOrWhiteSpace(row.AwayTeam))
		{
			row.ValidationErrors.Add("Away team is required.");
		}

		if (row.HomeTeam.Equals(row.AwayTeam, StringComparison.OrdinalIgnoreCase))
		{
			row.ValidationErrors.Add("Home team and away team cannot be the same.");
		}

		// Parse date/time
		if (!string.IsNullOrWhiteSpace(row.Date) && !string.IsNullOrWhiteSpace(row.StartTime))
		{
			var dateTimeString = $"{row.Date} {row.StartTime}";
			if (DateTime.TryParse(dateTimeString, out var parsedDateTime))
			{
				// CSV dates are in Pacific Time (AYSO Region 42 is in California)
				// Convert to UTC for storage in PostgreSQL timestamp with time zone
				var pacificZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

				// TryParse creates Unspecified kind, so we need to specify it's Pacific Time
				var pacificDateTime = DateTime.SpecifyKind(parsedDateTime, DateTimeKind.Unspecified);

				// Convert Pacific Time to UTC
				var utcDateTime = TimeZoneInfo.ConvertTimeToUtc(pacificDateTime, pacificZone);

				row.ParsedScheduledDateTime = utcDateTime;
			}
			else
			{
				row.ValidationErrors.Add($"Could not parse date/time: {dateTimeString}");
			}
		}
		else
		{
			row.ValidationErrors.Add("Date and start time are required.");
		}
	}

	private async Task<Dictionary<(AgeGroup, Gender), Division>> GetOrCreateDivisionsAsync(int seasonId, List<CsvGameRowDto> rows)
	{
		var divisionMap = new Dictionary<(AgeGroup, Gender), Division>();

		var existingDivisions = (await _divisionRepository.FindAsync(d => d.SeasonId == seasonId)).ToList();

		var requiredDivisions = rows
			.Where(r => r.ShouldImport && r.ParsedAgeGroup.HasValue && r.ParsedGender.HasValue)
			.Select(r => (r.ParsedAgeGroup!.Value, r.ParsedGender!.Value))
			.Distinct();

		foreach (var (ageGroup, gender) in requiredDivisions)
		{
			var division = existingDivisions.FirstOrDefault(d => d.AgeGroup == ageGroup && d.Gender == gender);

			if (division == null)
			{
				division = new Division
				{
					SeasonId = seasonId,
					AgeGroup = ageGroup,
					Gender = gender,
					TotalRounds = 10 // Default, can be updated later
				};
				await _divisionRepository.AddAsync(division);
			}

			divisionMap[(ageGroup, gender)] = division;
		}

		return divisionMap;
	}

	private async Task ProcessGameRowAsync(CsvGameRowDto row, int seasonId, Dictionary<(AgeGroup, Gender), Division> divisionMap, List<CsvGameRowDto> allRowsInDivision, CsvImportResult result)
	{
		var division = divisionMap[(row.ParsedAgeGroup!.Value, row.ParsedGender!.Value)];

		// Get teams (they should already exist with IDs from previous pass)
		var (_, transformedHomeName) = await TransformAwayRegionTeamNameAsync(row.HomeTeam, division.Id);
		var finalHomeName = transformedHomeName;
		var homeTeam = (await _teamRepository.FindAsync(t => t.Name == finalHomeName && t.DivisionId == division.Id)).First();

		var (_, transformedAwayName) = await TransformAwayRegionTeamNameAsync(row.AwayTeam, division.Id);
		var finalAwayName = transformedAwayName;
		var awayTeam = (await _teamRepository.FindAsync(t => t.Name == finalAwayName && t.DivisionId == division.Id)).First();

		// Calculate round number based on date using all rows in this division
		var round = CalculateRound(row.ParsedScheduledDateTime!.Value, allRowsInDivision);

		// Create game
		var game = new Game
		{
			DivisionId = division.Id,
			HomeTeamId = homeTeam.Id,
			AwayTeamId = awayTeam.Id,
			ScheduledDateTime = row.ParsedScheduledDateTime!.Value,
			Round = round,
			Location = $"{row.Field} - {row.Location}",
			Status = GameStatus.Scheduled
		};

		await _gameRepository.AddAsync(game);
		result.GamesCreated++;
	}

	private async Task<Team> GetOrCreateTeamAsync(string teamName, int divisionId, string coachFirstName, string coachLastName, CsvImportResult result)
	{
		// Check if this is an away region team placeholder (e.g., "121b", "759a", "9b")
		var (isAwayRegion, transformedName) = await TransformAwayRegionTeamNameAsync(teamName, divisionId);
		var finalTeamName = isAwayRegion ? transformedName : teamName;

		var existingTeam = (await _teamRepository.FindAsync(t => t.Name == finalTeamName && t.DivisionId == divisionId)).FirstOrDefault();

		if (existingTeam != null)
		{
			// Update coach info if provided and different (only for Region 42 teams)
			if (!isAwayRegion)
			{
				var coachName = $"{coachFirstName} {coachLastName}".Trim();
				if (!string.IsNullOrWhiteSpace(coachName) && existingTeam.ContactName != coachName)
				{
					existingTeam.ContactName = coachName;
					_teamRepository.Update(existingTeam);
					result.TeamsUpdated++;
				}
			}
			return existingTeam;
		}

		// Create new team
		var team = new Team
		{
			Name = finalTeamName,
			ShortName = isAwayRegion ? finalTeamName : GenerateTeamShortName(finalTeamName),
			DivisionId = divisionId,
			ContactName = isAwayRegion ? $"Away Region Team" : $"{coachFirstName} {coachLastName}".Trim(),
			IsActive = true,
			IsRegion42Team = !isAwayRegion // Mark away region teams
		};

		await _teamRepository.AddAsync(team);
		result.TeamsCreated++;

		_logger.LogInformation("Created {TeamType} team: {TeamName}", 
			isAwayRegion ? "away region" : "Region 42", finalTeamName);

		return team;
	}

	/// <summary>
	/// Transforms away region team placeholders into parseable format.
	/// Examples: "121b" → "R121-14UB01", "759a" → "R759-14UB01"
	/// Returns tuple: (isAwayRegion, transformedName)
	/// </summary>
	private async Task<(bool isAwayRegion, string transformedName)> TransformAwayRegionTeamNameAsync(string teamName, int divisionId)
	{
		if (string.IsNullOrWhiteSpace(teamName))
			return (false, teamName);

		// Check if name matches away region pattern: digits followed by single letter (e.g., "121b", "9a")
		var match = System.Text.RegularExpressions.Regex.Match(teamName.Trim(), @"^(\d+)([a-z])$", 
			System.Text.RegularExpressions.RegexOptions.IgnoreCase);

		if (!match.Success)
			return (false, teamName); // Not an away region team

		string regionNumber = match.Groups[1].Value;
		string teamLetter = match.Groups[2].Value.ToUpper();

		// Get division to determine age group and gender for proper naming
		var division = await _divisionRepository.GetByIdAsync(divisionId);
		if (division == null)
			return (false, teamName); // Fallback if division not found

		// Convert letter to team number (a=01, b=02, c=03, etc.)
		int teamNumber = char.ToUpper(teamLetter[0]) - 'A' + 1;
		string teamNumberPadded = teamNumber.ToString("D2");

		// Build standardized name: R{region}-{ageGroup}{genderInitial}{teamNum}
		// Example: R121-14UB01 for "Region 121, 14U Boys, Team 1"
		string genderInitial = division.Gender == Gender.Boys ? "B" : "G";
		// Convert U10/U12/U14 to 10U/12U/14U format
		string ageGroup = division.AgeGroup.ToString().Replace("U", "") + "U";
		string transformedName = $"R{regionNumber}-{ageGroup}{genderInitial}{teamNumberPadded}";

		_logger.LogInformation("Transformed away region team '{Original}' → '{Transformed}'", 
			teamName, transformedName);

		return (true, transformedName);
	}

	/// <summary>
	/// Generates a short name for a team based on the standard naming format.
	/// Format: &lt;division&gt;&lt;number&gt; &lt;fun name&gt; (&lt;coach&gt;) → &lt;number&gt; &lt;fun name&gt;
	/// Format: &lt;division&gt;&lt;number&gt; (&lt;coach&gt;) → &lt;number&gt; &lt;coach&gt;
	/// Max 20 characters with ellipsis if truncated.
	/// </summary>
	private string GenerateTeamShortName(string teamName)
	{
		if (string.IsNullOrWhiteSpace(teamName))
			return string.Empty;

		// Find the opening parenthesis for coach name
		int coachStart = teamName.IndexOf('(');
		int coachEnd = teamName.IndexOf(')');

		if (coachStart == -1)
		{
			// No coach name found, just truncate the full name
			return TruncateWithEllipsis(teamName, 20);
		}

		// Extract everything before the coach name and trim
		string nameWithoutCoach = teamName.Substring(0, coachStart).Trim();

		// Extract coach name if available
		string coachName = string.Empty;
		if (coachEnd > coachStart)
		{
			coachName = teamName.Substring(coachStart + 1, coachEnd - coachStart - 1).Trim();
		}

		// Team name format: <division><number> <fun name>
		// Division format is like "10UB", "12UG", "14UB" (2 digits + 2 letters)
		// We want to extract just "<number> <fun name>" or "<number> <coach>" if no fun name

		// Find where the team number starts (after division prefix)
		// Division is typically 4 characters: "10UB", "12UG", "14UB"
		if (nameWithoutCoach.Length >= 4)
		{
			// Try to find the team number (digits after division code)
			int numberStart = -1;
			for (int i = 0; i < Math.Min(6, nameWithoutCoach.Length); i++)
			{
				if (char.IsDigit(nameWithoutCoach[i]) && i > 0 && !char.IsDigit(nameWithoutCoach[i - 1]))
				{
					// Found start of team number (digit preceded by non-digit)
					numberStart = i;
					break;
				}
			}

			if (numberStart > 0 && numberStart < nameWithoutCoach.Length)
			{
				// Extract from team number onwards (e.g., "01 Jets" or "01")
				string afterDivision = nameWithoutCoach.Substring(numberStart).Trim();

				// Check if there's a fun name after the number
				// If the string is just digits (e.g., "01"), use coach name
				bool isJustNumber = afterDivision.All(c => char.IsDigit(c) || char.IsWhiteSpace(c));

				if (isJustNumber && !string.IsNullOrEmpty(coachName))
				{
					// No fun name, use: "01 Smith"
					string shortName = $"{afterDivision} {coachName}";
					return TruncateWithEllipsis(shortName, 20);
				}
				else
				{
					// Has fun name, use: "01 Jets"
					return TruncateWithEllipsis(afterDivision, 20);
				}
			}
		}

		// Fallback: couldn't parse format
		// If we have coach name and nameWithoutCoach is short/just a number, include coach
		if (!string.IsNullOrEmpty(coachName) && nameWithoutCoach.Length < 5)
		{
			string shortName = $"{nameWithoutCoach} {coachName}";
			return TruncateWithEllipsis(shortName, 20);
		}

		// Final fallback: just use the name without coach
		return TruncateWithEllipsis(nameWithoutCoach, 20);
	}

	/// <summary>
	/// Truncates a string to the specified max length.
	/// If truncated, replaces the last character with an ellipsis (…).
	/// </summary>
	private string TruncateWithEllipsis(string value, int maxLength)
	{
		if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
			return value;

		// Truncate to maxLength - 1 and add ellipsis
		return value.Substring(0, maxLength - 1) + "…";
	}

	private int CalculateRound(DateTime gameDate, List<CsvGameRowDto> allRows)
	{
		// Get all unique game dates for this division, sorted
		var gameDates = allRows
			.Where(r => r.ParsedScheduledDateTime.HasValue)
			.Select(r => r.ParsedScheduledDateTime!.Value.Date)
			.Distinct()
			.OrderBy(d => d)
			.ToList();

		var index = gameDates.IndexOf(gameDate.Date);
		return index >= 0 ? index + 1 : 1;
	}

	private int CalculateRound(DateTime gameDate, List<Game> existingGames)
	{
		if (!existingGames.Any())
			return 1;

		// Get all unique game dates, sorted
		var gameDates = existingGames
			.Select(g => g.ScheduledDateTime.Date)
			.Distinct()
			.OrderBy(d => d)
			.ToList();

		// Check if this date already exists
		var index = gameDates.IndexOf(gameDate.Date);
		if (index >= 0)
			return index + 1;

		// This is a new date, determine where it falls
		gameDates.Add(gameDate.Date);
		gameDates = gameDates.OrderBy(d => d).ToList();
		return gameDates.IndexOf(gameDate.Date) + 1;
	}

	private void AddTeamPreview(List<CsvTeamPreview> previews, string teamName, CsvGameRowDto row, HashSet<string> existingTeamNames, bool isHomeTeam)
	{
		if (previews.Any(p => p.TeamName.Equals(teamName, StringComparison.OrdinalIgnoreCase)))
			return;

		var coachName = isHomeTeam
			? $"{row.HomeTeamHeadCoachFirstName} {row.HomeTeamHeadCoachLastName}".Trim()
			: $"{row.AwayTeamHeadCoachFirstName} {row.AwayTeamHeadCoachLastName}".Trim();

		previews.Add(new CsvTeamPreview
		{
			TeamName = teamName,
			AgeGroup = row.ParsedAgeGroup!.Value,
			Gender = row.ParsedGender!.Value,
			CoachName = coachName,
			IsExisting = existingTeamNames.Contains(teamName)
		});
	}

	#endregion
}

/// <summary>
/// CSV mapping configuration for CsvHelper.
/// Maps CSV columns to CsvGameRowDto properties.
/// </summary>
public class CsvGameRowMap : ClassMap<CsvGameRowDto>
{
	public CsvGameRowMap()
	{
		Map(m => m.MatchId).Name("Match ID");
		Map(m => m.EventName).Name("Event Name");
		Map(m => m.GroupName).Name("Group Name");
		Map(m => m.HomeTeam).Name("Home Team");
		Map(m => m.AwayTeam).Name("Away Team");
		Map(m => m.Date).Name("Date");
		Map(m => m.StartTime).Name("Start Time");
		Map(m => m.EndTime).Name("End Time");
		Map(m => m.Field).Name("Field");
		Map(m => m.Location).Name("Location");
		Map(m => m.HomeTeamHeadCoachFirstName).Name("Home Team Head Coach First Name");
		Map(m => m.HomeTeamHeadCoachLastName).Name("Home Team Head Coach Last Name");
		Map(m => m.AwayTeamHeadCoachFirstName).Name("Away Team Head Coach First Name");
		Map(m => m.AwayTeamHeadCoachLastName).Name("Away Team Head Coach Last Name");
		Map(m => m.HomeTeamScore).Name("Home Team Score");
		Map(m => m.AwayTeamScore).Name("Away Team Score");
		Map(m => m.ScheduledStatus).Name("Scheduled Status");
	}
}
