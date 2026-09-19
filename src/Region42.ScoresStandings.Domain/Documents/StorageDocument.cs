namespace Region42.ScoresStandings.Domain.Documents;

/// <summary>
/// Wrapper for a document loaded from storage, carrying concurrency metadata.
/// </summary>
public class StorageDocument<T>
{
	public T Content { get; set; } = default!;
	public long? Generation { get; set; }
	public string ObjectPath { get; set; } = string.Empty;
}
