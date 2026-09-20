using System.Text.Json;
using System.Text.Json.Serialization;

namespace Region42.ScoresStandings.Infrastructure.Storage;

public static class CompetitionJsonSerializer
{
	private static readonly JsonSerializerOptions Options = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
	};

	public static string Serialize<T>(T value)
	{
		return JsonSerializer.Serialize(value, Options);
	}

	public static T Deserialize<T>(string json)
	{
		return JsonSerializer.Deserialize<T>(json, Options)
			?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");
	}

	public static async Task<T> DeserializeAsync<T>(Stream stream, CancellationToken cancellationToken = default)
	{
		return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
			?? throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}.");
	}
}
