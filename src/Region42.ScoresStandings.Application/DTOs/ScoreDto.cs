using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.DTOs;

/// <summary>
/// DTO for score entry and display.
/// </summary>
public class ScoreEntryDto
{
	public int GameId { get; set; }
	public int HomeTeamId { get; set; }
	public string HomeTeamName { get; set; } = string.Empty;
	public int AwayTeamId { get; set; }
	public string AwayTeamName { get; set; } = string.Empty;
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public int Round { get; set; }
	public GameStatus Status { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
	public DateTime? LastModified { get; set; }
	public string? LastModifiedBy { get; set; }
	public DateTime? RescheduledDateTime { get; set; }
	public string? RescheduledLocation { get; set; }
	public int? RescheduledRound { get; set; }
}

/// <summary>
/// DTO for tournament game score entry and display.
/// </summary>
public class TournamentScoreEntryDto : ScoreEntryDto
{
	public TournamentStage Stage { get; set; }
	public string StageLabel { get; set; } = string.Empty;
	public string? HomeTeamPlaceholder { get; set; }
	public string? AwayTeamPlaceholder { get; set; }
	public bool IsForfeit { get; set; }
}

/// <summary>
/// DTO for pool-play tournament game score entry and display (AYSO 10-point system with cards).
/// </summary>
public class PoolPlayScoreEntryDto : TournamentScoreEntryDto
{
	public int HomeRedCards { get; set; }
	public int AwayRedCards { get; set; }
	public int HomeYellowCards { get; set; }
	public int AwayYellowCards { get; set; }
}

/// <summary>
/// DTO for single-elimination tournament game score entry and display (supports penalty shootouts).
/// </summary>
public class EliminationScoreEntryDto : TournamentScoreEntryDto
{
	public bool IsDecidedByPenalties { get; set; }
	public int? HomePenaltyScore { get; set; }
	public int? AwayPenaltyScore { get; set; }
}

/// <summary>
/// DTO for updating a score.
/// </summary>
public class ScoreUpdateDto
{
	public int GameId { get; set; }
	public int HomeTeamId { get; set; }
	public int AwayTeamId { get; set; }
	public int? HomeScore { get; set; }
	public int? AwayScore { get; set; }
	public bool IsForfeit { get; set; }
	public bool IsDecidedByPenalties { get; set; }
	public int? HomePenaltyScore { get; set; }
	public int? AwayPenaltyScore { get; set; }
	public int HomeRedCards { get; set; }
	public int AwayRedCards { get; set; }
	public int HomeYellowCards { get; set; }
	public int AwayYellowCards { get; set; }
	public GameStatus Status { get; set; }
	public DateTime? NewScheduledDateTime { get; set; }
	public string? NewLocation { get; set; }
	public int? NewRound { get; set; }
}
