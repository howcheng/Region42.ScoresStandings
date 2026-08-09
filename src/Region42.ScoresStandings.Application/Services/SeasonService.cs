using Region42.ScoresStandings.Application.Interfaces;
using Region42.ScoresStandings.Domain.Entities;
using Region42.ScoresStandings.Domain.Enums;
using Region42.ScoresStandings.Domain.Interfaces;

namespace Region42.ScoresStandings.Application.Services;

public class SeasonService : ISeasonService
{
	private readonly IRepository<Season> _seasonRepository;
	private readonly IRepository<Division> _divisionRepository;
	private readonly IRepository<Game> _gameRepository;
	private readonly IRepository<Score> _scoreRepository;

	public SeasonService(
		IRepository<Season> seasonRepository,
		IRepository<Division> divisionRepository,
		IRepository<Game> gameRepository,
		IRepository<Score> scoreRepository)
	{
		_seasonRepository = seasonRepository;
		_divisionRepository = divisionRepository;
		_gameRepository = gameRepository;
		_scoreRepository = scoreRepository;
	}

	public async Task<IEnumerable<Season>> GetAllSeasonsAsync()
	{
		var seasons = await _seasonRepository.GetAllAsync();
		return seasons.OrderByDescending(s => s.Year).ThenByDescending(s => s.StartDate);
	}

	public async Task<Season?> GetActiveSeasonAsync()
	{
		var seasons = await _seasonRepository.FindAsync(s => s.IsActive);
		return seasons.FirstOrDefault();
	}

	public async Task<IEnumerable<Season>> GetEmptySeasonsAsync()
	{
		var allSeasons = await _seasonRepository.GetAllAsync();
		var emptySeasons = new List<Season>();

		foreach (var season in allSeasons)
		{
			var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == season.Id);
			var divisionIds = divisions.Select(d => d.Id).ToList();

			if (divisionIds.Any())
			{
				var hasGames = false;
				foreach (var divisionId in divisionIds)
				{
					var games = await _gameRepository.FindAsync(g => g.DivisionId == divisionId);
					if (games.Any())
					{
						hasGames = true;
						break;
					}
				}

				if (!hasGames)
				{
					emptySeasons.Add(season);
				}
			}
			else
			{
				// No divisions means no games
				emptySeasons.Add(season);
			}
		}

		return emptySeasons.OrderByDescending(s => s.Year).ThenByDescending(s => s.StartDate);
	}

	public async Task<Season> CreateSeasonAsync(string? seasonName = null, bool setAsActive = true)
	{
		var name = string.IsNullOrWhiteSpace(seasonName) ? GetDefaultSeasonName() : seasonName;
		var currentYear = DateTime.Now.Year;

		var season = new Season
		{
			Name = name,
			Year = currentYear,
			IsActive = setAsActive
			// StartDate is computed property (August 1 of Year)
		};

		if (setAsActive)
		{
			// Deactivate all other seasons
			var allSeasons = await _seasonRepository.GetAllAsync();
			foreach (var existingSeason in allSeasons.Where(s => s.IsActive))
			{
				existingSeason.IsActive = false;
				_seasonRepository.Update(existingSeason);
			}
		}

		await _seasonRepository.AddAsync(season);
		await _seasonRepository.SaveChangesAsync();

		// Create the 6 standard divisions for the new season
		await CreateStandardDivisionsAsync(season.Id);

		return season;
	}

	public async Task<bool> CanReplaceGamesAsync(int seasonId)
	{
		// Get all divisions for the season
		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == seasonId);
		var divisionIds = divisions.Select(d => d.Id).ToList();

		if (!divisionIds.Any())
		{
			// No divisions means no games, so replacement is allowed
			return true;
		}

		// Check if any Round 1 games have scores
		foreach (var divisionId in divisionIds)
		{
			var round1Games = await _gameRepository.FindAsync(g => g.DivisionId == divisionId && g.Round == 1);
			var gameIds = round1Games.Select(g => g.Id).ToList();

			foreach (var gameId in gameIds)
			{
				var scores = await _scoreRepository.FindAsync(s => s.GameId == gameId);
				if (scores.Any())
				{
					// Found a score for a Round 1 game - cannot replace
					return false;
				}
			}
		}

		// No Round 1 scores found - replacement is allowed
		return true;
	}

	public async Task DeleteAllGamesForSeasonAsync(int seasonId)
	{
		// Get all divisions for the season
		var divisions = await _divisionRepository.FindAsync(d => d.SeasonId == seasonId);
		var divisionIds = divisions.Select(d => d.Id).ToList();

		// Delete all games and their scores for each division
		foreach (var divisionId in divisionIds)
		{
			var games = await _gameRepository.FindAsync(g => g.DivisionId == divisionId);

			foreach (var game in games)
			{
				// Delete associated scores first
				var scores = await _scoreRepository.FindAsync(s => s.GameId == game.Id);
				foreach (var score in scores)
				{
					_scoreRepository.Delete(score);
				}

				// Delete the game
				_gameRepository.Delete(game);
			}
		}

		await _gameRepository.SaveChangesAsync();
	}

	public string GetDefaultSeasonName()
	{
		return $"Fall {DateTime.Now.Year}";
	}

	public async Task SetActiveSeasonAsync(int seasonId)
	{
		var allSeasons = await _seasonRepository.GetAllAsync();

		foreach (var season in allSeasons)
		{
			season.IsActive = (season.Id == seasonId);
			_seasonRepository.Update(season);
		}

		await _seasonRepository.SaveChangesAsync();
	}

	private async Task CreateStandardDivisionsAsync(int seasonId)
	{
		// Create the 6 standard divisions: 10U/12U/14U × Boys/Girls
		var ageGroups = new[] { AgeGroup.U10, AgeGroup.U12, AgeGroup.U14 };
		var genders = new[] { Gender.Boys, Gender.Girls };

		foreach (var ageGroup in ageGroups)
		{
			foreach (var gender in genders)
			{
				var division = new Division
				{
					SeasonId = seasonId,
					AgeGroup = ageGroup,
					Gender = gender,
					TotalRounds = 10, // Default 10 rounds
					PlayoffSpots = 1 // Default 1 playoff spot
				};

				await _divisionRepository.AddAsync(division);
			}
		}

		await _divisionRepository.SaveChangesAsync();
	}
}
