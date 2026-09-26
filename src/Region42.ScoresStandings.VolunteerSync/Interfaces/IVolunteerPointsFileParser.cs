using Region42.ScoresStandings.VolunteerSync.Models;

namespace Region42.ScoresStandings.VolunteerSync.Interfaces;

public interface IVolunteerPointsFileParser
{
	VolunteerPointsBulkUpdateDto Parse(Stream fileStream, int divisionId);
}
