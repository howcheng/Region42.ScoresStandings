using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.DTOs;

/// <summary>
/// DTO for mapping CSV rows from Schedule Match Report.
/// Maps to the CSV columns from SportsConnect export.
/// </summary>
public class CsvGameRowDto
{
	/// <summary>
	/// Match ID from CSV (used for tracking/debugging)
	/// </summary>
	public string MatchId { get; set; } = string.Empty;

	/// <summary>
	/// Event Name - must contain "Games" and one of "10U", "12U", "14U"
	/// Example: "Region 42 Fall 2025 - 12U - Girls (Games)"
	/// </summary>
	public string EventName { get; set; } = string.Empty;

	/// <summary>
	/// Group Name (usually same as Event Name with "-Group" suffix)
	/// </summary>
	public string GroupName { get; set; } = string.Empty;

	/// <summary>
	/// Home Team name or "Practice" for practice events
	/// </summary>
	public string HomeTeam { get; set; } = string.Empty;

	/// <summary>
	/// Away Team name or empty for practice events
	/// </summary>
	public string AwayTeam { get; set; } = string.Empty;

	/// <summary>
	/// Date in MM/DD/YYYY format
	/// </summary>
	public string Date { get; set; } = string.Empty;

	/// <summary>
	/// Start Time in h:mm tt format (e.g., "6:30 PM")
	/// </summary>
	public string StartTime { get; set; } = string.Empty;

	/// <summary>
	/// End Time in h:mm tt format
	/// </summary>
	public string EndTime { get; set; } = string.Empty;

	/// <summary>
	/// Field name (e.g., "DV 3A", "Borchard C")
	/// </summary>
	public string Field { get; set; } = string.Empty;

	/// <summary>
	/// Location name (e.g., "Dos Vientos Community Park")
	/// </summary>
	public string Location { get; set; } = string.Empty;

	/// <summary>
	/// Home team head coach first name
	/// </summary>
	public string HomeTeamHeadCoachFirstName { get; set; } = string.Empty;

	/// <summary>
	/// Home team head coach last name
	/// </summary>
	public string HomeTeamHeadCoachLastName { get; set; } = string.Empty;

	/// <summary>
	/// Away team head coach first name
	/// </summary>
	public string AwayTeamHeadCoachFirstName { get; set; } = string.Empty;

	/// <summary>
	/// Away team head coach last name
	/// </summary>
	public string AwayTeamHeadCoachLastName { get; set; } = string.Empty;

	/// <summary>
	/// Home team score (usually empty on export)
	/// </summary>
	public string HomeTeamScore { get; set; } = string.Empty;

	/// <summary>
	/// Away team score (usually empty on export)
	/// </summary>
	public string AwayTeamScore { get; set; } = string.Empty;

	/// <summary>
	/// Scheduled Status from CSV
	/// </summary>
	public string ScheduledStatus { get; set; } = string.Empty;

	// Computed properties after parsing

	/// <summary>
	/// Parsed age group from EventName
	/// </summary>
	public AgeGroup? ParsedAgeGroup { get; set; }

	/// <summary>
	/// Parsed gender from EventName
	/// </summary>
	public Gender? ParsedGender { get; set; }

	/// <summary>
	/// Parsed scheduled date/time
	/// </summary>
	public DateTime? ParsedScheduledDateTime { get; set; }

	/// <summary>
	/// True if this row should be imported (is a game, not practice)
	/// </summary>
	public bool ShouldImport { get; set; }

	/// <summary>
	/// Validation errors for this row
	/// </summary>
	public List<string> ValidationErrors { get; set; } = new();
}
