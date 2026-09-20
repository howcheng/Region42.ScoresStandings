namespace Region42.ScoresStandings.Infrastructure.Storage;

public class StorageOptions
{
	public const string SectionName = "Storage";

	public string Provider { get; set; } = "LocalFile";
	public string BucketName { get; set; } = "region42-storage";
	public string CompetitionSlug { get; set; } = "core-season";
	public string LocalRoot { get; set; } = "../../.local-data/storage";
}
