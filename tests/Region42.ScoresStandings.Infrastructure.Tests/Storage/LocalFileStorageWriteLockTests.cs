using FluentAssertions;
using Microsoft.Extensions.Options;
using Region42.ScoresStandings.Domain;
using Region42.ScoresStandings.Infrastructure.Storage;

namespace Region42.ScoresStandings.Infrastructure.Tests.Storage;

public class LocalFileStorageWriteLockTests : IDisposable
{
	private readonly string _tempRoot;
	private readonly LocalFileStorageWriteLock _writeLock;

	public LocalFileStorageWriteLockTests()
	{
		_tempRoot = Path.Combine(Path.GetTempPath(), "region42-lock-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempRoot);

		_writeLock = new LocalFileStorageWriteLock(Options.Create(new StorageOptions
		{
			LocalRoot = _tempRoot
		}));
	}

	[Fact]
	public async Task TryAcquireAsync_ReturnsHandle_WhenLockIsAvailable()
	{
		await using var handle = await _writeLock.TryAcquireAsync(
			StorageWriteLockNames.VolunteerPointsImport,
			"test-holder",
			TimeSpan.FromMinutes(5));

		handle.Should().NotBeNull();
		(await _writeLock.IsLockedAsync(StorageWriteLockNames.VolunteerPointsImport)).Should().BeTrue();
	}

	[Fact]
	public async Task TryAcquireAsync_ReturnsNull_WhenLockIsAlreadyHeld()
	{
		await using var firstHandle = await _writeLock.TryAcquireAsync(
			StorageWriteLockNames.VolunteerPointsImport,
			"first-holder",
			TimeSpan.FromMinutes(5));

		firstHandle.Should().NotBeNull();

		var secondHandle = await _writeLock.TryAcquireAsync(
			StorageWriteLockNames.VolunteerPointsImport,
			"second-holder",
			TimeSpan.FromMinutes(5));

		secondHandle.Should().BeNull();
	}

	[Fact]
	public async Task DisposeAsync_ReleasesLock()
	{
		var handle = await _writeLock.TryAcquireAsync(
			StorageWriteLockNames.VolunteerPointsImport,
			"test-holder",
			TimeSpan.FromMinutes(5));

		handle.Should().NotBeNull();
		await handle!.DisposeAsync();

		(await _writeLock.IsLockedAsync(StorageWriteLockNames.VolunteerPointsImport)).Should().BeFalse();
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempRoot))
		{
			Directory.Delete(_tempRoot, recursive: true);
		}
	}
}
