using Region42.ScoresStandings.Domain.Documents;

namespace Region42.ScoresStandings.Domain.Interfaces;

public interface ICompetitionDataStore
{
	Task<StorageDocument<IndexDocument>> GetIndexAsync(CancellationToken cancellationToken = default);
	Task SaveIndexAsync(IndexDocument document, long? expectedGeneration, CancellationToken cancellationToken = default);

	Task<StorageDocument<SeasonDocument>> GetSeasonDocumentAsync(int year, string competitionSlug, CancellationToken cancellationToken = default);
	Task SaveSeasonDocumentAsync(int year, string competitionSlug, SeasonDocument document, long? expectedGeneration, CancellationToken cancellationToken = default);

	Task<StorageDocument<DivisionDataDocument>> GetDivisionDataAsync(int year, string competitionSlug, string divisionKey, CancellationToken cancellationToken = default);
	Task SaveDivisionDataAsync(int year, string competitionSlug, string divisionKey, DivisionDataDocument document, long? expectedGeneration, CancellationToken cancellationToken = default);

	Task<IReadOnlyList<string>> ListCompetitionPathsAsync(CancellationToken cancellationToken = default);
}
