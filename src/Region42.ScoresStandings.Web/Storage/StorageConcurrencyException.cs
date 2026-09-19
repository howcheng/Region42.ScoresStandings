namespace Region42.ScoresStandings.Web.Storage;

public class StorageConcurrencyException : Exception
{
	public StorageConcurrencyException(string objectPath)
		: base($"Storage object '{objectPath}' was modified by another process. Please retry.")
	{
		ObjectPath = objectPath;
	}

	public string ObjectPath { get; }
}
