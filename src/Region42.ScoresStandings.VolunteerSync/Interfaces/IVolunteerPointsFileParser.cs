using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Interfaces;

public interface IVolunteerPointsFileParser
{
	IReadOnlyList<VolunteerPointsRawRow> Parse(Stream fileStream);
}
