namespace Region42.ScoresStandings.Application.Interfaces;

public interface IStandingsRefreshService
{
	Task RefreshDivisionStandingsAsync(int divisionId);
}
