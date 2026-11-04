using Region42.ScoresStandings.Domain.Enums;

namespace Region42.ScoresStandings.Domain.Entities;

public class EliminationGame : TournamentGame
{
	public EliminationScore? EliminationScore => Score as EliminationScore;

	public int? TryGetWinnerTeamId()
	{
		if (Status != GameStatus.Completed || Score is not { HomeScore: { } home, AwayScore: { } away })
		{
			return null;
		}

		if (home > away)
		{
			return HomeTeamId > 0 ? HomeTeamId : null;
		}

		if (away > home)
		{
			return AwayTeamId > 0 ? AwayTeamId : null;
		}

		if (Score is EliminationScore { IsDecidedByPenalties: true, HomePenaltyScore: { } homePk, AwayPenaltyScore: { } awayPk } && homePk != awayPk)
		{
			return homePk > awayPk
				? (HomeTeamId > 0 ? HomeTeamId : null)
				: (AwayTeamId > 0 ? AwayTeamId : null);
		}

		return null;
	}
}
