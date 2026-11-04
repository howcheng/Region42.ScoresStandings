using Region42.ScoresStandings.Application.Interfaces;

namespace Region42.ScoresStandings.Application.Services.Tournaments;

/// <summary>
/// Resolves an <see cref="ITournamentBracketBuilder"/> for a given number of teams in a division (supports 3 to 8 teams).
/// </summary>
public class TournamentBracketBuilderFactory : ITournamentBracketBuilderFactory
{
	public const int MinSupportedTeams = 3;
	public const int MaxSupportedTeams = 8;

	private readonly IReadOnlyDictionary<int, ITournamentBracketBuilder> _buildersByTeamCount;

	public TournamentBracketBuilderFactory(IEnumerable<ITournamentBracketBuilder> builders)
	{
		ArgumentNullException.ThrowIfNull(builders);
		_buildersByTeamCount = builders.ToDictionary(b => b.TeamCount);
	}

	public ITournamentBracketBuilder GetBuilder(int teamCount)
	{
		if (teamCount < MinSupportedTeams || teamCount > MaxSupportedTeams)
		{
			throw new ArgumentOutOfRangeException(
				nameof(teamCount),
				teamCount,
				$"Bracket tournaments only support divisions with {MinSupportedTeams} to {MaxSupportedTeams} teams.");
		}

		if (_buildersByTeamCount.TryGetValue(teamCount, out var builder))
		{
			return builder;
		}

		// TODO: Implement ITournamentBracketBuilder classes for 3-, 4-, 5-, 7-, and 8-team divisions in future seasons.
		throw new NotSupportedException(
			$"A tournament bracket builder for {teamCount} teams has not been implemented yet. Currently supported team counts: {string.Join(", ", _buildersByTeamCount.Keys.OrderBy(k => k))}.");
	}
}
