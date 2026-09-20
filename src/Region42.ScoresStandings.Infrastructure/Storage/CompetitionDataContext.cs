using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Helpers;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Infrastructure.Storage;

public class CompetitionDataContext : ICompetitionDataContext
{
	private readonly ICompetitionDataStore _store;
	private readonly StorageOptions _options;
	private readonly IHttpContextAccessor _httpContextAccessor;

	private readonly List<Season> _seasons = new();
	private readonly List<Division> _divisions = new();
	private readonly List<Team> _teams = new();
	private readonly List<Game> _games = new();
	private readonly List<Score> _scores = new();
	private readonly List<VolunteerPoints> _volunteerPoints = new();
	private readonly Dictionary<int, Dictionary<string, StandingsSnapshotDocument>> _standingsByDivision = new();

	private IndexDocument _index = new();
	private Settings _settings = new();
	private bool _isLoaded;
	private bool _indexDirty;
	private readonly HashSet<string> _dirtySeasonPaths = new();
	private readonly HashSet<(int Year, string Slug, string DivisionKey)> _dirtyDivisionPaths = new();

	private readonly Dictionary<int, (int Year, string Slug)> _seasonPathById = new();
	private readonly Dictionary<int, string> _divisionKeyById = new();
	private readonly Dictionary<int, (int Year, string Slug)> _divisionSeasonPath = new();

	private StorageDocument<IndexDocument>? _indexStorage;
	private readonly Dictionary<string, StorageDocument<SeasonDocument>> _seasonStorage = new();
	private readonly Dictionary<string, StorageDocument<DivisionDataDocument>> _divisionStorage = new();

	private int _nextId = 1;
	private JsonDataTransaction? _activeTransaction;
	private JsonDataTransactionSnapshot? _transactionSnapshot;

	public CompetitionDataContext(
		ICompetitionDataStore store,
		IOptions<StorageOptions> options,
		IHttpContextAccessor httpContextAccessor)
	{
		_store = store;
		_options = options.Value;
		_httpContextAccessor = httpContextAccessor;
	}

	public IndexDocument Index => _index;
	public IReadOnlyList<Season> Seasons => _seasons;
	public IReadOnlyList<Division> Divisions => _divisions;
	public IReadOnlyList<Team> Teams => _teams;
	public IReadOnlyList<Game> Games => _games;
	public IReadOnlyList<Score> Scores => _scores;
	public IReadOnlyList<VolunteerPoints> VolunteerPoints => _volunteerPoints;
	public Settings Settings => _settings;

	public Dictionary<string, StandingsSnapshotDocument> GetStandingsByRound(int divisionId)
	{
		return _standingsByDivision.TryGetValue(divisionId, out var standings)
			? standings
			: new Dictionary<string, StandingsSnapshotDocument>();
	}

	public void SetStandingsForDivision(int divisionId, Dictionary<string, StandingsSnapshotDocument> standingsByRound)
	{
		_standingsByDivision[divisionId] = standingsByRound;
		MarkDivisionDirty(divisionId);
	}

	public (int Year, string Slug)? GetCompetitionPathForSeason(int seasonId)
	{
		return _seasonPathById.TryGetValue(seasonId, out var path) ? path : null;
	}

	public string? GetDivisionKey(int divisionId)
	{
		return _divisionKeyById.TryGetValue(divisionId, out var key) ? key : null;
	}

	public int AllocateNextId()
	{
		return _nextId++;
	}

	public async Task LoadAllAsync(CancellationToken cancellationToken = default)
	{
		if (_isLoaded)
		{
			return;
		}

		try
		{
			_indexStorage = await _store.GetIndexAsync(cancellationToken);
			_index = _indexStorage.Content;
		}
		catch (FileNotFoundException)
		{
			_index = new IndexDocument();
			_indexStorage = new StorageDocument<IndexDocument> { Content = _index, Generation = null, ObjectPath = "index.json" };
		}

		foreach (var entry in _index.Competitions)
		{
			await LoadSeasonAsync(entry.Year, entry.Slug, cancellationToken);
		}

		RecalculateNextId();
		_isLoaded = true;
	}

	public async Task LoadSeasonAsync(int year, string competitionSlug, CancellationToken cancellationToken = default)
	{
		var seasonPath = DivisionKeyHelper.GetCompetitionPath(year, competitionSlug);
		if (_seasonStorage.ContainsKey(seasonPath))
		{
			return;
		}

		StorageDocument<SeasonDocument> seasonDoc;
		try
		{
			seasonDoc = await _store.GetSeasonDocumentAsync(year, competitionSlug, cancellationToken);
		}
		catch (FileNotFoundException)
		{
			return;
		}

		_seasonStorage[seasonPath] = seasonDoc;
		HydrateSeasonDocument(seasonDoc.Content, year, competitionSlug);

		foreach (var divisionDoc in seasonDoc.Content.Divisions)
		{
			await LoadDivisionDataInternalAsync(year, competitionSlug, divisionDoc.Key, divisionDoc.Id, cancellationToken);
		}
	}

	public async Task LoadDivisionDataAsync(int divisionId, CancellationToken cancellationToken = default)
	{
		if (!_divisionSeasonPath.TryGetValue(divisionId, out var seasonPath) ||
			!_divisionKeyById.TryGetValue(divisionId, out var divisionKey))
		{
			throw new InvalidOperationException($"Division {divisionId} is not loaded.");
		}

		await LoadDivisionDataInternalAsync(seasonPath.Year, seasonPath.Slug, divisionKey, divisionId, cancellationToken);
	}

	public void Add<T>(T entity) where T : BaseEntity
	{
		EnsureLoaded();

		if (entity.Id == 0)
		{
			entity.Id = AllocateNextId();
		}
		else
		{
			_nextId = Math.Max(_nextId, entity.Id + 1);
		}

		ApplyAuditFields(entity, isNew: true);

		switch (entity)
		{
			case Season season:
				_seasons.Add(season);
				if (!_seasonPathById.ContainsKey(season.Id))
				{
					var year = season.Year > 0 ? season.Year : DateTime.UtcNow.Year;
					_seasonPathById[season.Id] = (year, _options.CompetitionSlug);
					UpdateIndexEntry(season, year, _options.CompetitionSlug);
				}
				MarkSeasonDirty(season.Id);
				break;
			case Division division:
				_divisions.Add(division);
				_divisionKeyById[division.Id] = DivisionKeyHelper.ToKey(division.AgeGroup, division.Gender);
				if (_seasonPathById.TryGetValue(division.SeasonId, out var path))
				{
					_divisionSeasonPath[division.Id] = path;
				}
				MarkSeasonDirty(division.SeasonId);
				break;
			case Team team:
				_teams.Add(team);
				MarkSeasonDirtyForDivision(team.DivisionId);
				break;
			case Game game:
				_games.Add(game);
				MarkDivisionDirty(game.DivisionId);
				break;
			case Score score:
				_scores.Add(score);
				var addScoreGame = score.Game ?? _games.FirstOrDefault(g => g.Id == score.GameId);
				if (addScoreGame != null)
				{
					addScoreGame.Score = score;
					score.Game = addScoreGame;
					MarkDivisionDirty(addScoreGame.DivisionId);
				}
				break;
			case VolunteerPoints volunteerPoints:
				_volunteerPoints.Add(volunteerPoints);
				var vpTeamOnAdd = _teams.FirstOrDefault(t => t.Id == volunteerPoints.TeamId);
				if (vpTeamOnAdd != null)
				{
					MarkDivisionDirty(vpTeamOnAdd.DivisionId);
				}
				break;
			case Settings settings:
				_settings = settings;
				MarkAllSeasonsDirty();
				break;
			default:
				throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported.");
		}
	}

	public void Update<T>(T entity) where T : BaseEntity
	{
		EnsureLoaded();
		ApplyAuditFields(entity, isNew: false);

		switch (entity)
		{
			case Season season:
				ReplaceEntity(_seasons, season);
				MarkSeasonDirty(season.Id);
				break;
			case Division division:
				ReplaceEntity(_divisions, division);
				MarkSeasonDirty(division.SeasonId);
				break;
			case Team team:
				ReplaceEntity(_teams, team);
				MarkSeasonDirtyForDivision(team.DivisionId);
				break;
			case Game game:
				ReplaceEntity(_games, game);
				MarkDivisionDirty(game.DivisionId);
				break;
			case Score score:
				ReplaceEntity(_scores, score);
				var updateScoreGame = score.Game ?? _games.FirstOrDefault(g => g.Id == score.GameId);
				if (updateScoreGame != null)
				{
					updateScoreGame.Score = score;
					score.Game = updateScoreGame;
					MarkDivisionDirty(updateScoreGame.DivisionId);
				}
				break;
			case VolunteerPoints volunteerPoints:
				ReplaceEntity(_volunteerPoints, volunteerPoints);
				var vpTeamOnUpdate = _teams.FirstOrDefault(t => t.Id == volunteerPoints.TeamId);
				if (vpTeamOnUpdate != null)
				{
					MarkDivisionDirty(vpTeamOnUpdate.DivisionId);
				}
				break;
			case Settings settings:
				_settings = settings;
				MarkAllSeasonsDirty();
				break;
			default:
				throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported.");
		}
	}

	public void Delete<T>(T entity) where T : BaseEntity
	{
		EnsureLoaded();

		switch (entity)
		{
			case Season season:
				_seasons.RemoveAll(s => s.Id == season.Id);
				MarkSeasonDirty(season.Id);
				break;
			case Division division:
				_divisions.RemoveAll(d => d.Id == division.Id);
				MarkSeasonDirty(division.SeasonId);
				break;
			case Team team:
				_teams.RemoveAll(t => t.Id == team.Id);
				MarkSeasonDirtyForDivision(team.DivisionId);
				break;
			case Game game:
				_games.RemoveAll(g => g.Id == game.Id);
				_scores.RemoveAll(s => s.GameId == game.Id);
				MarkDivisionDirty(game.DivisionId);
				break;
			case Score score:
				_scores.RemoveAll(s => s.Id == score.Id);
				var scoreGameOnDelete = _games.FirstOrDefault(g => g.Id == score.GameId);
				if (scoreGameOnDelete != null)
				{
					scoreGameOnDelete.Score = null;
					MarkDivisionDirty(scoreGameOnDelete.DivisionId);
				}
				break;
			case VolunteerPoints volunteerPoints:
				_volunteerPoints.RemoveAll(vp => vp.Id == volunteerPoints.Id);
				var vpTeamOnDelete = _teams.FirstOrDefault(t => t.Id == volunteerPoints.TeamId);
				if (vpTeamOnDelete != null)
				{
					MarkDivisionDirty(vpTeamOnDelete.DivisionId);
				}
				break;
			default:
				throw new NotSupportedException($"Entity type {typeof(T).Name} is not supported.");
		}
	}

	public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
	{
		EnsureLoaded();
		var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
		var now = DateTime.UtcNow;
		var changes = 0;

		if (_indexDirty)
		{
			await _store.SaveIndexAsync(_index, _indexStorage?.Generation, cancellationToken);
			_indexStorage = await _store.GetIndexAsync(cancellationToken);
			_indexDirty = false;
			changes++;
		}

		foreach (var seasonPath in _dirtySeasonPaths.ToList())
		{
			var parts = seasonPath.Split('/');
			var year = int.Parse(parts[0]);
			var slug = parts[1];
			var seasonId = _seasonPathById.First(kvp => kvp.Value.Year == year && kvp.Value.Slug == slug).Key;
			var document = BuildSeasonDocument(seasonId, currentUser, now);
			var storage = _seasonStorage.GetValueOrDefault(seasonPath);
			await _store.SaveSeasonDocumentAsync(year, slug, document, storage?.Generation, cancellationToken);
			_seasonStorage[seasonPath] = await _store.GetSeasonDocumentAsync(year, slug, cancellationToken);
			changes++;
		}
		_dirtySeasonPaths.Clear();

		foreach (var (year, slug, divisionKey) in _dirtyDivisionPaths.ToList())
		{
			var division = _divisions.First(d => _divisionKeyById[d.Id] == divisionKey);
			var document = BuildDivisionDataDocument(division.Id, divisionKey, currentUser, now);
			var storageKey = $"{year}/{slug}/{divisionKey}";
			var storage = _divisionStorage.GetValueOrDefault(storageKey);
			await _store.SaveDivisionDataAsync(year, slug, divisionKey, document, storage?.Generation, cancellationToken);
			_divisionStorage[storageKey] = await _store.GetDivisionDataAsync(year, slug, divisionKey, cancellationToken);
			changes++;
		}
		_dirtyDivisionPaths.Clear();

		return changes;
	}

	public Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
	{
		EnsureLoaded();
		_transactionSnapshot = CaptureSnapshot();
		_activeTransaction = new JsonDataTransaction(this);
		return Task.FromResult<IDbTransaction>(_activeTransaction);
	}

	internal void CommitTransaction()
	{
		_transactionSnapshot = null;
		_activeTransaction = null;
	}

	internal void RollbackTransaction()
	{
		if (_transactionSnapshot == null)
		{
			return;
		}

		RestoreSnapshot(_transactionSnapshot);
		_transactionSnapshot = null;
		_activeTransaction = null;
	}


	private async Task LoadDivisionDataInternalAsync(int year, string competitionSlug, string divisionKey, int divisionId, CancellationToken cancellationToken)
	{
		var storageKey = $"{year}/{competitionSlug}/{divisionKey}";
		if (_divisionStorage.ContainsKey(storageKey))
		{
			return;
		}

		StorageDocument<DivisionDataDocument> divisionDoc;
		try
		{
			divisionDoc = await _store.GetDivisionDataAsync(year, competitionSlug, divisionKey, cancellationToken);
		}
		catch (FileNotFoundException)
		{
			divisionDoc = new StorageDocument<DivisionDataDocument>
			{
				Content = new DivisionDataDocument
				{
					DivisionKey = divisionKey,
					DivisionId = divisionId
				},
				Generation = null,
				ObjectPath = DivisionKeyHelper.GetDivisionObjectPath(year, competitionSlug, divisionKey)
			};
		}

		_divisionStorage[storageKey] = divisionDoc;
		HydrateDivisionDocument(divisionDoc.Content, divisionId);
	}

	private void HydrateSeasonDocument(SeasonDocument document, int year, string competitionSlug)
	{
		var season = DocumentEntityMapper.ToEntity(document.Season);
		ReplaceOrAddSeason(season, year, competitionSlug);
		_settings = DocumentEntityMapper.ToEntity(document.Settings);

		foreach (var divisionDoc in document.Divisions)
		{
			var division = DocumentEntityMapper.ToEntity(divisionDoc, season.Id);
			ReplaceOrAddDivision(division, year, competitionSlug, divisionDoc.Key);

			foreach (var teamDoc in divisionDoc.Teams)
			{
				var team = DocumentEntityMapper.ToEntity(teamDoc, division.Id);
				ReplaceOrAddTeam(team);
			}
		}
	}

	private void HydrateDivisionDocument(DivisionDataDocument document, int divisionId)
	{
		_standingsByDivision[divisionId] = new Dictionary<string, StandingsSnapshotDocument>(document.StandingsByRound);

		var teamsById = _teams.Where(t => t.DivisionId == divisionId).ToDictionary(t => t.Id);

		foreach (var gameDoc in document.Games)
		{
			teamsById.TryGetValue(gameDoc.HomeTeamId, out var homeTeam);
			teamsById.TryGetValue(gameDoc.AwayTeamId, out var awayTeam);
			var game = DocumentEntityMapper.ToEntity(gameDoc, divisionId, homeTeam, awayTeam);
			ReplaceOrAddGame(game);

			var score = DocumentEntityMapper.ToScoreEntity(gameDoc);
			if (score != null)
			{
				score.Id = game.Id;
				score.Game = game;
				game.Score = score;
				ReplaceOrAddScore(score);
			}
		}

		var nextVolunteerId = _volunteerPoints.Count + 1;
		foreach (var vpDoc in document.VolunteerPoints)
		{
			var vp = DocumentEntityMapper.ToEntity(vpDoc, nextVolunteerId++);
			ReplaceOrAddVolunteerPoints(vp);
		}
	}

	private SeasonDocument BuildSeasonDocument(int seasonId, string currentUser, DateTime now)
	{
		var season = _seasons.First(s => s.Id == seasonId);
		var path = _seasonPathById[seasonId];
		var seasonPath = DivisionKeyHelper.GetCompetitionPath(path.Year, path.Slug);
		var existing = _seasonStorage.GetValueOrDefault(seasonPath)?.Content;

		var document = new SeasonDocument
		{
			SchemaVersion = SeasonDocument.CurrentSchemaVersion,
			Version = (existing?.Version ?? 0) + 1,
			ModifiedAt = now,
			ModifiedBy = currentUser,
			Season = DocumentEntityMapper.ToDocument(season),
			Settings = DocumentEntityMapper.ToDocument(_settings),
			Divisions = _divisions
				.Where(d => d.SeasonId == seasonId)
				.OrderBy(d => d.AgeGroup)
				.ThenBy(d => d.Gender)
				.Select(d => DocumentEntityMapper.ToDocument(d, _teams.Where(t => t.DivisionId == d.Id)))
				.ToList()
		};

		return document;
	}

	private DivisionDataDocument BuildDivisionDataDocument(int divisionId, string divisionKey, string currentUser, DateTime now)
	{
		var seasonPath = _divisionSeasonPath[divisionId];
		var storageKey = $"{seasonPath.Year}/{seasonPath.Slug}/{divisionKey}";
		var existing = _divisionStorage.GetValueOrDefault(storageKey)?.Content;

		return new DivisionDataDocument
		{
			SchemaVersion = DivisionDataDocument.CurrentSchemaVersion,
			Version = (existing?.Version ?? 0) + 1,
			DivisionKey = divisionKey,
			DivisionId = divisionId,
			ModifiedAt = now,
			ModifiedBy = currentUser,
			Games = _games
				.Where(g => g.DivisionId == divisionId)
				.OrderBy(g => g.Round)
				.ThenBy(g => g.ScheduledDateTime)
				.Select(DocumentEntityMapper.ToDocument)
				.ToList(),
			VolunteerPoints = _volunteerPoints
				.Where(vp => _teams.Any(t => t.Id == vp.TeamId && t.DivisionId == divisionId))
				.OrderBy(vp => vp.Round)
				.ThenBy(vp => vp.TeamId)
				.Select(DocumentEntityMapper.ToDocument)
				.ToList(),
			StandingsByRound = GetStandingsByRound(divisionId)
		};
	}

	private void ReplaceOrAddSeason(Season season, int year, string slug)
	{
		_seasons.RemoveAll(s => s.Id == season.Id);
		_seasons.Add(season);
		_seasonPathById[season.Id] = (year, slug);
		UpdateIndexEntry(season, year, slug);
	}

	private void ReplaceOrAddDivision(Division division, int year, string slug, string divisionKey)
	{
		_divisions.RemoveAll(d => d.Id == division.Id);
		_divisions.Add(division);
		_divisionKeyById[division.Id] = divisionKey;
		_divisionSeasonPath[division.Id] = (year, slug);
	}

	private void ReplaceOrAddTeam(Team team)
	{
		_teams.RemoveAll(t => t.Id == team.Id);
		_teams.Add(team);
	}

	private void ReplaceOrAddGame(Game game)
	{
		_games.RemoveAll(g => g.Id == game.Id);
		_games.Add(game);
	}

	private void ReplaceOrAddScore(Score score)
	{
		_scores.RemoveAll(s => s.GameId == score.GameId);
		_scores.Add(score);
	}

	private void ReplaceOrAddVolunteerPoints(VolunteerPoints volunteerPoints)
	{
		_volunteerPoints.RemoveAll(vp => vp.TeamId == volunteerPoints.TeamId && vp.Round == volunteerPoints.Round);
		_volunteerPoints.Add(volunteerPoints);
	}

	private void UpdateIndexEntry(Season season, int year, string slug)
	{
		var entry = _index.Competitions.FirstOrDefault(c => c.SeasonId == season.Id);
		if (entry == null)
		{
			entry = new CompetitionIndexEntryDocument();
			_index.Competitions.Add(entry);
		}

		entry.Year = year;
		entry.Slug = slug;
		entry.SeasonId = season.Id;
		entry.Name = season.Name;
		entry.IsActive = season.IsActive;
		entry.SeasonPath = DivisionKeyHelper.GetSeasonObjectPath(year, slug);
		_indexDirty = true;
	}

	private void MarkSeasonDirty(int seasonId)
	{
		if (_seasonPathById.TryGetValue(seasonId, out var path))
		{
			_dirtySeasonPaths.Add(DivisionKeyHelper.GetCompetitionPath(path.Year, path.Slug));
			UpdateIndexEntry(_seasons.First(s => s.Id == seasonId), path.Year, path.Slug);
		}
	}

	private void MarkSeasonDirtyForDivision(int divisionId)
	{
		var division = _divisions.FirstOrDefault(d => d.Id == divisionId);
		if (division != null)
		{
			MarkSeasonDirty(division.SeasonId);
		}
	}

	private void MarkDivisionDirty(int divisionId)
	{
		if (_divisionSeasonPath.TryGetValue(divisionId, out var seasonPath) &&
			_divisionKeyById.TryGetValue(divisionId, out var divisionKey))
		{
			_dirtyDivisionPaths.Add((seasonPath.Year, seasonPath.Slug, divisionKey));
		}
	}

	private void MarkAllSeasonsDirty()
	{
		foreach (var season in _seasons)
		{
			MarkSeasonDirty(season.Id);
		}
	}

	private static void ReplaceEntity<T>(List<T> list, T entity) where T : BaseEntity
	{
		list.RemoveAll(e => e.Id == entity.Id);
		list.Add(entity);
	}

	private void ApplyAuditFields(BaseEntity entity, bool isNew)
	{
		var currentUser = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "System";
		var now = DateTime.UtcNow;

		if (isNew)
		{
			entity.CreatedAt = now;
			entity.CreatedBy = currentUser;
			entity.ModifiedAt = now;
			entity.ModifiedBy = currentUser;
			entity.RowVersion = 1;
		}
		else
		{
			entity.ModifiedAt = now;
			entity.ModifiedBy = currentUser;
			entity.RowVersion++;
		}
	}

	private void RecalculateNextId()
	{
		var maxId = 0;
		maxId = Math.Max(maxId, _seasons.DefaultIfEmpty().Max(s => s?.Id ?? 0));
		maxId = Math.Max(maxId, _divisions.DefaultIfEmpty().Max(d => d?.Id ?? 0));
		maxId = Math.Max(maxId, _teams.DefaultIfEmpty().Max(t => t?.Id ?? 0));
		maxId = Math.Max(maxId, _games.DefaultIfEmpty().Max(g => g?.Id ?? 0));
		maxId = Math.Max(maxId, _scores.DefaultIfEmpty().Max(s => s?.Id ?? 0));
		maxId = Math.Max(maxId, _volunteerPoints.DefaultIfEmpty().Max(vp => vp?.Id ?? 0));
		_nextId = maxId + 1;
	}

	private void EnsureLoaded()
	{
		if (!_isLoaded)
		{
			LoadAllAsync().GetAwaiter().GetResult();
		}
	}

	private JsonDataTransactionSnapshot CaptureSnapshot()
	{
		return new JsonDataTransactionSnapshot
		{
			Seasons = _seasons.Select(CloneSeason).ToList(),
			Divisions = _divisions.Select(CloneDivision).ToList(),
			Teams = _teams.Select(CloneTeam).ToList(),
			Games = _games.Select(CloneGame).ToList(),
			Scores = _scores.Select(CloneScore).ToList(),
			VolunteerPoints = _volunteerPoints.Select(CloneVolunteerPoints).ToList(),
			Settings = CloneSettings(_settings),
			StandingsByDivision = _standingsByDivision.ToDictionary(
				kvp => kvp.Key,
				kvp => new Dictionary<string, StandingsSnapshotDocument>(kvp.Value)),
			Index = new IndexDocument
			{
				SchemaVersion = _index.SchemaVersion,
				Competitions = _index.Competitions.Select(c => new CompetitionIndexEntryDocument
				{
					Year = c.Year,
					Slug = c.Slug,
					SeasonId = c.SeasonId,
					Name = c.Name,
					IsActive = c.IsActive,
					SeasonPath = c.SeasonPath
				}).ToList()
			},
			DirtySeasonPaths = new HashSet<string>(_dirtySeasonPaths),
			DirtyDivisionPaths = new HashSet<(int Year, string Slug, string DivisionKey)>(_dirtyDivisionPaths),
			IndexDirty = _indexDirty,
			NextId = _nextId
		};
	}

	private void RestoreSnapshot(JsonDataTransactionSnapshot snapshot)
	{
		_seasons.Clear();
		_seasons.AddRange(snapshot.Seasons);
		_divisions.Clear();
		_divisions.AddRange(snapshot.Divisions);
		_teams.Clear();
		_teams.AddRange(snapshot.Teams);
		_games.Clear();
		_games.AddRange(snapshot.Games);
		_scores.Clear();
		_scores.AddRange(snapshot.Scores);
		_volunteerPoints.Clear();
		_volunteerPoints.AddRange(snapshot.VolunteerPoints);
		_settings = snapshot.Settings;
		_standingsByDivision.Clear();
		foreach (var kvp in snapshot.StandingsByDivision)
		{
			_standingsByDivision[kvp.Key] = new Dictionary<string, StandingsSnapshotDocument>(kvp.Value);
		}
		_index = snapshot.Index;
		_dirtySeasonPaths.Clear();
		foreach (var path in snapshot.DirtySeasonPaths)
		{
			_dirtySeasonPaths.Add(path);
		}
		_dirtyDivisionPaths.Clear();
		foreach (var path in snapshot.DirtyDivisionPaths)
		{
			_dirtyDivisionPaths.Add(path);
		}
		_indexDirty = snapshot.IndexDirty;
		_nextId = snapshot.NextId;
	}

	private static Season CloneSeason(Season season) => new()
	{
		Id = season.Id,
		Name = season.Name,
		Year = season.Year,
		IsActive = season.IsActive,
		CustomMessage = season.CustomMessage,
		CreatedAt = season.CreatedAt,
		CreatedBy = season.CreatedBy,
		ModifiedAt = season.ModifiedAt,
		ModifiedBy = season.ModifiedBy,
		RowVersion = season.RowVersion
	};

	private static Division CloneDivision(Division division) => new()
	{
		Id = division.Id,
		SeasonId = division.SeasonId,
		AgeGroup = division.AgeGroup,
		Gender = division.Gender,
		TotalRounds = division.TotalRounds,
		PlayoffSpots = division.PlayoffSpots,
		ScrimmageRounds = division.ScrimmageRounds,
		CustomMessage = division.CustomMessage,
		CreatedAt = division.CreatedAt,
		CreatedBy = division.CreatedBy,
		ModifiedAt = division.ModifiedAt,
		ModifiedBy = division.ModifiedBy,
		RowVersion = division.RowVersion
	};

	private static Team CloneTeam(Team team) => new()
	{
		Id = team.Id,
		Name = team.Name,
		ShortName = team.ShortName,
		DivisionId = team.DivisionId,
		ContactName = team.ContactName,
		IsActive = team.IsActive,
		IsRegion42Team = team.IsRegion42Team,
		CreatedAt = team.CreatedAt,
		CreatedBy = team.CreatedBy,
		ModifiedAt = team.ModifiedAt,
		ModifiedBy = team.ModifiedBy,
		RowVersion = team.RowVersion
	};

	private static Game CloneGame(Game game) => new()
	{
		Id = game.Id,
		DivisionId = game.DivisionId,
		HomeTeamId = game.HomeTeamId,
		AwayTeamId = game.AwayTeamId,
		ScheduledDateTime = game.ScheduledDateTime,
		Round = game.Round,
		Location = game.Location,
		Status = game.Status,
		CreatedAt = game.CreatedAt,
		CreatedBy = game.CreatedBy,
		ModifiedAt = game.ModifiedAt,
		ModifiedBy = game.ModifiedBy,
		RowVersion = game.RowVersion
	};

	private static Score CloneScore(Score score) => new()
	{
		Id = score.Id,
		GameId = score.GameId,
		HomeScore = score.HomeScore,
		AwayScore = score.AwayScore,
		CreatedAt = score.CreatedAt,
		CreatedBy = score.CreatedBy,
		ModifiedAt = score.ModifiedAt,
		ModifiedBy = score.ModifiedBy,
		RowVersion = score.RowVersion
	};

	private static VolunteerPoints CloneVolunteerPoints(VolunteerPoints vp) => new()
	{
		Id = vp.Id,
		TeamId = vp.TeamId,
		Round = vp.Round,
		Points = vp.Points,
		Notes = vp.Notes,
		CreatedAt = vp.CreatedAt,
		CreatedBy = vp.CreatedBy,
		ModifiedAt = vp.ModifiedAt,
		ModifiedBy = vp.ModifiedBy,
		RowVersion = vp.RowVersion
	};

	private static Settings CloneSettings(Settings settings) => new()
	{
		Id = settings.Id,
		MinVolunteerPointsForPlayoff = settings.MinVolunteerPointsForPlayoff,
		DefaultPlayoffSpots = settings.DefaultPlayoffSpots,
		CreatedAt = settings.CreatedAt,
		CreatedBy = settings.CreatedBy,
		ModifiedAt = settings.ModifiedAt,
		ModifiedBy = settings.ModifiedBy,
		RowVersion = settings.RowVersion
	};
}

internal sealed class JsonDataTransaction : IDbTransaction
{
	private readonly CompetitionDataContext _context;
	private bool _completed;

	public JsonDataTransaction(CompetitionDataContext context)
	{
		_context = context;
	}

	public Task CommitAsync(CancellationToken cancellationToken = default)
	{
		_completed = true;
		_context.CommitTransaction();
		return Task.CompletedTask;
	}

	public Task RollbackAsync(CancellationToken cancellationToken = default)
	{
		_completed = true;
		_context.RollbackTransaction();
		return Task.CompletedTask;
	}

	public ValueTask DisposeAsync()
	{
		if (!_completed)
		{
			_context.RollbackTransaction();
		}

		return ValueTask.CompletedTask;
	}
}

internal sealed class JsonDataTransactionSnapshot
{
	public List<Season> Seasons { get; set; } = new();
	public List<Division> Divisions { get; set; } = new();
	public List<Team> Teams { get; set; } = new();
	public List<Game> Games { get; set; } = new();
	public List<Score> Scores { get; set; } = new();
	public List<VolunteerPoints> VolunteerPoints { get; set; } = new();
	public Settings Settings { get; set; } = new();
	public Dictionary<int, Dictionary<string, StandingsSnapshotDocument>> StandingsByDivision { get; set; } = new();
	public IndexDocument Index { get; set; } = new();
	public HashSet<string> DirtySeasonPaths { get; set; } = new();
	public HashSet<(int Year, string Slug, string DivisionKey)> DirtyDivisionPaths { get; set; } = new();
	public bool IndexDirty { get; set; }
	public int NextId { get; set; }
}
