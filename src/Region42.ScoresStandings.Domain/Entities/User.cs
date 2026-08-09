namespace Region42.ScoresStandings.Domain.Entities;

public class User : BaseEntity
{
	public string GoogleId { get; set; } = string.Empty;
	public string Email { get; set; } = string.Empty;
	public string DisplayName { get; set; } = string.Empty;
	public DateTime LastLogin { get; set; }
}
