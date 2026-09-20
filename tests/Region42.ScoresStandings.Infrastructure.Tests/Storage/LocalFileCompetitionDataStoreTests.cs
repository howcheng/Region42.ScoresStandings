﻿using FluentAssertions;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain.Documents;
using Region42.ScoresStandings.Infrastructure.Storage;

namespace Region42.ScoresStandings.Infrastructure.Tests.Storage;

public class LocalFileCompetitionDataStoreTests : IDisposable
{
	private readonly string _tempRoot;
	private readonly LocalFileCompetitionDataStore _store;

	public LocalFileCompetitionDataStoreTests()
	{
		_tempRoot = Path.Combine(Path.GetTempPath(), "region42-infra-tests", Guid.NewGuid().ToString("N"));
		_store = new LocalFileCompetitionDataStore(Options.Create(new StorageOptions { LocalRoot = _tempRoot }));
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempRoot))
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
	}

	[Fact]
	public async Task SaveAndGetIndexAsync_RoundTripsDocument()
	{
		var index = new IndexDocument
		{
			Competitions =
			[
				new CompetitionIndexEntryDocument
				{
					Year = 2026,
					Slug = "core-season",
					SeasonId = 1,
					Name = "Fall 2026",
					IsActive = true,
					SeasonPath = "2026/core-season/season.json"
				}
			]
		};

		await _store.SaveIndexAsync(index, expectedGeneration: null);
		var loaded = await _store.GetIndexAsync();

		loaded.Content.Competitions.Should().HaveCount(1);
		loaded.Content.Competitions[0].Name.Should().Be("Fall 2026");
		loaded.Generation.Should().Be(1);
	}

	[Fact]
	public async Task SaveIndexAsync_WithStaleGeneration_ThrowsStorageConcurrencyException()
	{
		var index = new IndexDocument();
		await _store.SaveIndexAsync(index, expectedGeneration: null);
		var loaded = await _store.GetIndexAsync();

		var act = () => _store.SaveIndexAsync(index, expectedGeneration: loaded.Generation - 1);

		await act.Should().ThrowAsync<StorageConcurrencyException>();
	}

	[Fact]
	public async Task ListCompetitionPathsAsync_ReturnsPathsForSavedSeasons()
	{
		var seasonDocument = new SeasonDocument
		{
			Season = new SeasonInfoDocument { Id = 1, Name = "Fall 2026", Year = 2026, IsActive = true },
			Settings = new SettingsDocument(),
			Divisions = []
		};

		await _store.SaveSeasonDocumentAsync(2026, "core-season", seasonDocument, expectedGeneration: null);
		var paths = await _store.ListCompetitionPathsAsync();

		paths.Should().Contain("2026/core-season");
	}
}
