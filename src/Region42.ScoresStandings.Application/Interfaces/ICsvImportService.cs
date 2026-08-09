using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.Interfaces;

/// <summary>
/// Service for importing teams and games from CSV files.
/// CSV validation must show ALL errors before allowing import.
/// Only processes events with "Games" in the event name and containing "10U", "12U", or "14U".
/// </summary>
public interface ICsvImportService
{
	/// <summary>
	/// Validates CSV file structure and content without importing.
	/// Returns all validation errors found.
	/// </summary>
	Task<CsvValidationResult> ValidateCsvAsync(Stream csvStream, int seasonId);

	/// <summary>
	/// Imports teams and games from a validated CSV file.
	/// Will only import if validation passes (no errors).
	/// </summary>
	Task<CsvImportResult> ImportCsvAsync(Stream csvStream, int seasonId);

	/// <summary>
	/// Previews what would be imported without committing to database.
	/// </summary>
	Task<CsvPreviewResult> PreviewImportAsync(Stream csvStream, int seasonId);
}

/// <summary>
/// Result of CSV validation showing all errors found.
/// </summary>
public class CsvValidationResult
{
	public bool IsValid { get; set; }
	public List<string> Errors { get; set; } = new();
	public List<string> Warnings { get; set; } = new();
	public int TotalRows { get; set; }
	public int ValidRows { get; set; }
	public int SkippedRows { get; set; }  // Practice/non-game rows
}

/// <summary>
/// Result of CSV import operation.
/// </summary>
public class CsvImportResult
{
	public bool Success { get; set; }
	public string Message { get; set; } = string.Empty;
	public int TeamsCreated { get; set; }
	public int TeamsUpdated { get; set; }
	public int GamesCreated { get; set; }
	public int RowsProcessed { get; set; }
	public int RowsSkipped { get; set; }
	public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Preview of what would be imported from CSV.
/// </summary>
public class CsvPreviewResult
{
	public List<CsvTeamPreview> Teams { get; set; } = new();
	public List<CsvGamePreview> Games { get; set; } = new();
	public CsvValidationResult Validation { get; set; } = new();
}

/// <summary>
/// Preview of a team to be imported.
/// </summary>
public class CsvTeamPreview
{
	public string TeamName { get; set; } = string.Empty;
	public AgeGroup AgeGroup { get; set; }
	public Gender Gender { get; set; }
	public string CoachName { get; set; } = string.Empty;
	public bool IsExisting { get; set; }  // True if team already exists
}

/// <summary>
/// Preview of a game to be imported.
/// </summary>
public class CsvGamePreview
{
	public string HomeTeam { get; set; } = string.Empty;
	public string AwayTeam { get; set; } = string.Empty;
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public int Round { get; set; }
	public string EventName { get; set; } = string.Empty;
}
