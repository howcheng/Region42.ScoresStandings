namespace Region42.ScoresStandings.Domain.Enums;

public enum TournamentStage
{
	None = 0,
	Quarterfinal1 = 1,
	Quarterfinal2 = 2,
	Quarterfinal3 = 3,
	Quarterfinal4 = 4,
	Semifinal1 = 5,
	Semifinal2 = 6,
	PoolPlay1 = 7,
	PoolPlay2 = 8,
	PoolPlay3 = 9,
	PoolPlay4 = 10,
	PoolPlay5 = 11,
	PoolPlay6 = 12,
	Final = 13
}

public static class TournamentStageExtensions
{
	public static bool IsEliminationStage(this TournamentStage stage)
	{
		return stage is TournamentStage.Quarterfinal1
			or TournamentStage.Quarterfinal2
			or TournamentStage.Quarterfinal3
			or TournamentStage.Quarterfinal4
			or TournamentStage.Semifinal1
			or TournamentStage.Semifinal2
			or TournamentStage.Final;
	}

	public static bool IsPoolPlayStage(this TournamentStage stage)
	{
		return stage is TournamentStage.PoolPlay1
			or TournamentStage.PoolPlay2
			or TournamentStage.PoolPlay3
			or TournamentStage.PoolPlay4
			or TournamentStage.PoolPlay5
			or TournamentStage.PoolPlay6;
	}

	public static string GetDisplayLabel(this TournamentStage stage)
	{
		return stage switch
		{
			TournamentStage.Quarterfinal1 => "Quarterfinal 1",
			TournamentStage.Quarterfinal2 => "Quarterfinal 2",
			TournamentStage.Quarterfinal3 => "Quarterfinal 3",
			TournamentStage.Quarterfinal4 => "Quarterfinal 4",
			TournamentStage.Semifinal1 => "Semifinal 1",
			TournamentStage.Semifinal2 => "Semifinal 2",
			TournamentStage.PoolPlay1 => "Pool Play 1",
			TournamentStage.PoolPlay2 => "Pool Play 2",
			TournamentStage.PoolPlay3 => "Pool Play 3",
			TournamentStage.PoolPlay4 => "Pool Play 4",
			TournamentStage.PoolPlay5 => "Pool Play 5",
			TournamentStage.PoolPlay6 => "Pool Play 6",
			TournamentStage.Final => "Final",
			_ => string.Empty
		};
	}
}
