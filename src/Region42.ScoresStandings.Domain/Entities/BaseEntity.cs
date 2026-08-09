namespace Region42.ScoresStandings.Domain.Entities;

public abstract class BaseEntity
{
	public int Id { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime ModifiedAt { get; set; }
	public string CreatedBy { get; set; } = string.Empty;
	public string ModifiedBy { get; set; } = string.Empty;
	public int RowVersion { get; set; } = 1;
}
