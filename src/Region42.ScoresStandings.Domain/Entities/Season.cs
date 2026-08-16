namespace Region42.ScoresStandings.Domain.Entities;

public class Season : BaseEntity
{
	public string Name { get; set; } = string.Empty;
	public int Year { get; set; }
	public bool IsActive { get; set; }
	public string? CustomMessage { get; set; }

	public ICollection<Division> Divisions { get; set; } = new List<Division>();

	/// <summary>
	/// Seasons start on August 1 of the specified year.
	/// </summary>
	public DateTime StartDate => new DateTime(Year, 8, 1);
}
