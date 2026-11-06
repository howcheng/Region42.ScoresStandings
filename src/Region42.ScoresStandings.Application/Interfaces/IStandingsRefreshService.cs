namespace Region42.ScoresStandings.Application.Interfaces;

public interface IStandingsRefreshService
{
	void RefreshDivisionStandings(int divisionId);
	Task RefreshDivisionStandingsAsync(int divisionId);
}
