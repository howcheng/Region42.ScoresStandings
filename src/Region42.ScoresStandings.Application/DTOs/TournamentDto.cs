using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Application.DTOs;

public class TournamentDisplayDto
{
	public int DivisionId { get; set; }
	public string DivisionName { get; set; } = string.Empty;
	public TournamentFormatType Format { get; set; }
	public int TournamentRound { get; set; }
	public bool IsReady { get; set; }
	public bool IsCompleted { get; set; }
	public int? ChampionTeamId { get; set; }
	public string? ChampionTeamName { get; set; }
	public Dictionary<int, int> SeedByTeamId { get; set; } = new();
	public List<TournamentSeedTeamDto> ByeTeams { get; set; } = new();
	public List<TournamentMatchDisplayDto> Quarterfinals { get; set; } = new();
	public List<TournamentMatchDisplayDto> Semifinals { get; set; } = new();
	public List<TournamentMatchDisplayDto> PoolPlayMatches { get; set; } = new();
	public List<PoolPlayStandingDto> PoolStandings { get; set; } = new();
	public bool AnyForfeitInPool { get; set; }
	public bool RequiresPenaltyShootoutTiebreaker { get; set; }
	public List<TournamentSeedTeamDto> TiedTeamsForTiebreaker { get; set; } = new();
	public int? PoolPlayTiebreakerWinnerTeamId { get; set; }
	public TournamentMatchDisplayDto? FinalMatch { get; set; }
}

public class TournamentSeedTeamDto
{
	public int Seed { get; set; }
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public string TeamShortName { get; set; } = string.Empty;
}

public class TournamentMatchDisplayDto
{
	public int GameId { get; set; }
	public TournamentStage Stage { get; set; }
	public string StageLabel { get; set; } = string.Empty;
	public DateTime ScheduledDateTime { get; set; }
	public string Location { get; set; } = string.Empty;
	public GameStatus Status { get; set; }
	public int HomeTeamId { get; set; }
	public int? HomeSeed { get; set; }
	public string HomeTeamName { get; set; } = string.Empty;
	public string HomeTeamShortName { get; set; } = string.Empty;
	public int AwayTeamId { get; set; }
	public int? AwaySeed { get; set; }
	public string AwayTeamName { get; set; } = string.Empty;
	public string AwayTeamShortName { get; set; } = string.Empty;
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
	public int? WinnerTeamId { get; set; }
}

public class PoolPlayStandingDto
{
	public int Rank { get; set; }
	public int Seed { get; set; }
	public int TeamId { get; set; }
	public string TeamName { get; set; } = string.Empty;
	public string TeamShortName { get; set; } = string.Empty;
	public int GamesPlayed { get; set; }
	public int Wins { get; set; }
	public int Draws { get; set; }
	public int Losses { get; set; }
	public int GoalsFor { get; set; }
	public int GoalsAgainst { get; set; }
	public int GoalDifferential { get; set; }
	public int CappedGoalsAllowed { get; set; }
	public int CappedGoalDifferential { get; set; }
	public int RedCards { get; set; }
	public int YellowCards { get; set; }
	public int PoolPoints { get; set; }
	public bool AdvancesToFinal { get; set; }
}

public class PoolPlayStandingsResult
{
	public List<PoolPlayStandingDto> Standings { get; set; } = new();
	public bool AnyForfeitInPool { get; set; }
	public bool AllPoolGamesCompleted { get; set; }
	public bool RequiresPenaltyShootoutTiebreaker { get; set; }
	public List<int> TiedTeamIdsForTiebreaker { get; set; } = new();
}
