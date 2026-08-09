namespace Region42.ScoresStandings.Domain.Entities;

/// <summary>
/// League-wide configuration settings.
/// Singleton pattern - only one record should exist.
/// </summary>
public class Settings : BaseEntity
{
	/// <summary>
	/// Minimum volunteer points required for playoff qualification (applies to all divisions).
	/// </summary>
	public int MinVolunteerPointsForPlayoff { get; set; }

	/// <summary>
	/// Default number of playoff spots for new divisions (can be overridden per division).
	/// </summary>
	public int DefaultPlayoffSpots { get; set; } = 1;

	// Future extensibility - add more league-wide settings here
	// Examples:
	// - public int PointsForWin { get; set; } = 3;
	// - public int PointsForDraw { get; set; } = 1;
	// - public bool AllowScoreCorrections { get; set; } = true;
	// - public int MaxTeamsPerDivision { get; set; }
}
