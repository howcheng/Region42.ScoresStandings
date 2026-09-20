using Microsoft.Extensions.Options;
using Npgsql;
using Region42.ScoresStandings.Application.Services;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Helpers;
using Region42.ScoresStandings.Domain.Interfaces;
using Region42.ScoresStandings.Infrastructure.Storage;

namespace Region42.ScoresStandings.Web.Migration;

/// <summary>
/// One-time migration utility: reads existing PostgreSQL data and writes JSON files to storage.
/// Run with: dotnet run --project src/Region42.ScoresStandings.Web -- --export-from-postgres
/// </summary>
public class PostgresToJsonExporter
{
	private readonly IConfiguration _configuration;
	private readonly ICompetitionDataStore _store;
	private readonly StorageOptions _options;
	private readonly ILogger<PostgresToJsonExporter> _logger;

	public PostgresToJsonExporter(
		IConfiguration configuration,
		ICompetitionDataStore store,
		IOptions<StorageOptions> options,
		ILogger<PostgresToJsonExporter> logger)
	{
		_configuration = configuration;
		_store = store;
		_options = options.Value;
		_logger = logger;
	}

	public async Task ExportAsync(CancellationToken cancellationToken = default)
	{
		var connectionString = _configuration.GetConnectionString("DefaultConnection")
			?? throw new InvalidOperationException("Connection string 'DefaultConnection' is required for export.");

		connectionString = connectionString
			.Replace(";IamAuth=true", "", StringComparison.OrdinalIgnoreCase)
			.Replace("IamAuth=true;", "", StringComparison.OrdinalIgnoreCase)
			.Replace("IamAuth=true", "", StringComparison.OrdinalIgnoreCase);

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		var seasons = await ReadSeasonsAsync(connection, cancellationToken);
		if (!seasons.Any())
		{
			_logger.LogWarning("No seasons found in PostgreSQL.");
			return;
		}

		var settings = await ReadSettingsAsync(connection, cancellationToken);
		var index = new IndexDocument();

		foreach (var season in seasons)
		{
			var year = season.Year;
			var slug = _options.CompetitionSlug;
			var divisions = await ReadDivisionsAsync(connection, season.Id, cancellationToken);
			var divisionDocs = new List<DivisionDocument>();

			foreach (var division in divisions)
			{
				var teams = await ReadTeamsAsync(connection, division.Id, cancellationToken);
				divisionDocs.Add(DocumentEntityMapper.ToDocument(division, teams));

				var games = await ReadGamesAsync(connection, division.Id, cancellationToken);
				var scores = await ReadScoresAsync(connection, games.Select(g => g.Id).ToList(), cancellationToken);
				var volunteerPoints = await ReadVolunteerPointsAsync(connection, teams.Select(t => t.Id).ToList(), cancellationToken);

				var standingsByRound = StandingsCalculator.CalculateAllRounds(
					division,
					teams,
					games,
					scores,
					volunteerPoints,
					settings.MinVolunteerPointsForPlayoff);

				var divisionKey = DivisionKeyHelper.ToKey(division.AgeGroup, division.Gender);
				var divisionData = new DivisionDataDocument
				{
					SchemaVersion = DivisionDataDocument.CurrentSchemaVersion,
					Version = 1,
					DivisionKey = divisionKey,
					DivisionId = division.Id,
					ModifiedAt = DateTime.UtcNow,
					ModifiedBy = "export",
					Games = games.Select(g =>
					{
						var score = scores.FirstOrDefault(s => s.GameId == g.Id);
						g.Score = score;
						return DocumentEntityMapper.ToDocument(g);
					}).ToList(),
					VolunteerPoints = volunteerPoints.Select(DocumentEntityMapper.ToDocument).ToList(),
					StandingsByRound = standingsByRound
				};

				await _store.SaveDivisionDataAsync(year, slug, divisionKey, divisionData, null, cancellationToken);
				_logger.LogInformation("Exported division {DivisionKey} for season {SeasonId}", divisionKey, season.Id);
			}

			var seasonDocument = new SeasonDocument
			{
				SchemaVersion = SeasonDocument.CurrentSchemaVersion,
				Version = 1,
				ModifiedAt = DateTime.UtcNow,
				ModifiedBy = "export",
				Season = DocumentEntityMapper.ToDocument(season),
				Settings = DocumentEntityMapper.ToDocument(settings),
				Divisions = divisionDocs
			};

			await _store.SaveSeasonDocumentAsync(year, slug, seasonDocument, null, cancellationToken);

			index.Competitions.Add(new CompetitionIndexEntryDocument
			{
				Year = year,
				Slug = slug,
				SeasonId = season.Id,
				Name = season.Name,
				IsActive = season.IsActive,
				SeasonPath = DivisionKeyHelper.GetSeasonObjectPath(year, slug)
			});

			_logger.LogInformation("Exported season {SeasonName} ({Year})", season.Name, year);
		}

		await _store.SaveIndexAsync(index, null, cancellationToken);
		_logger.LogInformation("Export complete. {SeasonCount} season(s) written to storage.", seasons.Count);
	}

	private static async Task<List<Season>> ReadSeasonsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT "Id", "Name", "Year", "IsActive", "CustomMessage",
			       "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "Seasons"
			ORDER BY "Year"
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var seasons = new List<Season>();
		while (await reader.ReadAsync(cancellationToken))
		{
			seasons.Add(new Season
			{
				Id = reader.GetInt32(0),
				Name = reader.GetString(1),
				Year = reader.GetInt32(2),
				IsActive = reader.GetBoolean(3),
				CustomMessage = reader.IsDBNull(4) ? null : reader.GetString(4),
				CreatedAt = reader.GetDateTime(5),
				CreatedBy = reader.GetString(6),
				ModifiedAt = reader.GetDateTime(7),
				ModifiedBy = reader.GetString(8),
				RowVersion = reader.GetInt32(9)
			});
		}

		return seasons;
	}

	private static async Task<Settings> ReadSettingsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT "Id", "MinVolunteerPointsForPlayoff", "DefaultPlayoffSpots",
			       "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "Settings"
			ORDER BY "Id"
			LIMIT 1
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			return new Settings { DefaultPlayoffSpots = 1 };
		}

		return new Settings
		{
			Id = reader.GetInt32(0),
			MinVolunteerPointsForPlayoff = reader.GetInt32(1),
			DefaultPlayoffSpots = reader.GetInt32(2),
			CreatedAt = reader.GetDateTime(3),
			CreatedBy = reader.GetString(4),
			ModifiedAt = reader.GetDateTime(5),
			ModifiedBy = reader.GetString(6),
			RowVersion = reader.GetInt32(7)
		};
	}

	private static async Task<List<Division>> ReadDivisionsAsync(NpgsqlConnection connection, int seasonId, CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT "Id", "SeasonId", "AgeGroup", "Gender", "TotalRounds", "PlayoffSpots",
			       "ScrimmageRounds", "CustomMessage", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "Divisions"
			WHERE "SeasonId" = @seasonId
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("seasonId", seasonId);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		var divisions = new List<Division>();
		while (await reader.ReadAsync(cancellationToken))
		{
			divisions.Add(new Division
			{
				Id = reader.GetInt32(0),
				SeasonId = reader.GetInt32(1),
				AgeGroup = (AgeGroup)reader.GetInt32(2),
				Gender = (Gender)reader.GetInt32(3),
				TotalRounds = reader.GetInt32(4),
				PlayoffSpots = reader.GetInt32(5),
				ScrimmageRounds = reader.GetInt32(6),
				CustomMessage = reader.IsDBNull(7) ? null : reader.GetString(7),
				CreatedAt = reader.GetDateTime(8),
				CreatedBy = reader.GetString(9),
				ModifiedAt = reader.GetDateTime(10),
				ModifiedBy = reader.GetString(11),
				RowVersion = reader.GetInt32(12)
			});
		}

		return divisions;
	}

	private static async Task<List<Team>> ReadTeamsAsync(NpgsqlConnection connection, int divisionId, CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT "Id", "Name", "ShortName", "DivisionId", "ContactName", "IsActive", "IsRegion42Team",
			       "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "Teams"
			WHERE "DivisionId" = @divisionId
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("divisionId", divisionId);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		var teams = new List<Team>();
		while (await reader.ReadAsync(cancellationToken))
		{
			teams.Add(new Team
			{
				Id = reader.GetInt32(0),
				Name = reader.GetString(1),
				ShortName = reader.GetString(2),
				DivisionId = reader.GetInt32(3),
				ContactName = reader.GetString(4),
				IsActive = reader.GetBoolean(5),
				IsRegion42Team = reader.GetBoolean(6),
				CreatedAt = reader.GetDateTime(7),
				CreatedBy = reader.GetString(8),
				ModifiedAt = reader.GetDateTime(9),
				ModifiedBy = reader.GetString(10),
				RowVersion = reader.GetInt32(11)
			});
		}

		return teams;
	}

	private static async Task<List<Game>> ReadGamesAsync(NpgsqlConnection connection, int divisionId, CancellationToken cancellationToken)
	{
		const string sql = """
			SELECT "Id", "DivisionId", "HomeTeamId", "AwayTeamId", "ScheduledDateTime", "Round",
			       "Location", "Status", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "Games"
			WHERE "DivisionId" = @divisionId
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("divisionId", divisionId);
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		var games = new List<Game>();
		while (await reader.ReadAsync(cancellationToken))
		{
			games.Add(new Game
			{
				Id = reader.GetInt32(0),
				DivisionId = reader.GetInt32(1),
				HomeTeamId = reader.GetInt32(2),
				AwayTeamId = reader.GetInt32(3),
				ScheduledDateTime = reader.GetDateTime(4),
				Round = reader.GetInt32(5),
				Location = reader.GetString(6),
				Status = (GameStatus)reader.GetInt32(7),
				CreatedAt = reader.GetDateTime(8),
				CreatedBy = reader.GetString(9),
				ModifiedAt = reader.GetDateTime(10),
				ModifiedBy = reader.GetString(11),
				RowVersion = reader.GetInt32(12)
			});
		}

		return games;
	}

	private static async Task<List<Score>> ReadScoresAsync(NpgsqlConnection connection, List<int> gameIds, CancellationToken cancellationToken)
	{
		if (!gameIds.Any())
		{
			return new List<Score>();
		}

		const string sql = """
			SELECT "Id", "GameId", "HomeScore", "AwayScore", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "Scores"
			WHERE "GameId" = ANY(@gameIds)
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("gameIds", gameIds.ToArray());
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		var scores = new List<Score>();
		while (await reader.ReadAsync(cancellationToken))
		{
			scores.Add(new Score
			{
				Id = reader.GetInt32(0),
				GameId = reader.GetInt32(1),
				HomeScore = reader.IsDBNull(2) ? null : reader.GetInt32(2),
				AwayScore = reader.IsDBNull(3) ? null : reader.GetInt32(3),
				CreatedAt = reader.GetDateTime(4),
				CreatedBy = reader.GetString(5),
				ModifiedAt = reader.GetDateTime(6),
				ModifiedBy = reader.GetString(7),
				RowVersion = reader.GetInt32(8)
			});
		}

		return scores;
	}

	private static async Task<List<VolunteerPoints>> ReadVolunteerPointsAsync(NpgsqlConnection connection, List<int> teamIds, CancellationToken cancellationToken)
	{
		if (!teamIds.Any())
		{
			return new List<VolunteerPoints>();
		}

		const string sql = """
			SELECT "Id", "TeamId", "Round", "Points", "Notes", "CreatedAt", "CreatedBy", "ModifiedAt", "ModifiedBy", "RowVersion"
			FROM "VolunteerPoints"
			WHERE "TeamId" = ANY(@teamIds)
			""";

		await using var command = new NpgsqlCommand(sql, connection);
		command.Parameters.AddWithValue("teamIds", teamIds.ToArray());
		await using var reader = await command.ExecuteReaderAsync(cancellationToken);

		var points = new List<VolunteerPoints>();
		while (await reader.ReadAsync(cancellationToken))
		{
			points.Add(new VolunteerPoints
			{
				Id = reader.GetInt32(0),
				TeamId = reader.GetInt32(1),
				Round = reader.GetInt32(2),
				Points = reader.GetInt32(3),
				Notes = reader.GetString(4),
				CreatedAt = reader.GetDateTime(5),
				CreatedBy = reader.GetString(6),
				ModifiedAt = reader.GetDateTime(7),
				ModifiedBy = reader.GetString(8),
				RowVersion = reader.GetInt32(9)
			});
		}

		return points;
	}
}
