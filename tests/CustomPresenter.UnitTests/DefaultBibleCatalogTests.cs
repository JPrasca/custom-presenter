using CustomPresenter.Application.Bibles;
using CustomPresenter.Domain.Bibles;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class DefaultBibleCatalogTests
{
    private sealed class InMemoryBibleRepository : IBibleRepository
    {
        public List<BibleVersion> StoredVersions { get; } = [];

        public Task SaveBibleAsync(BibleVersion version, IReadOnlyList<BibleVerse> verses, CancellationToken cancellationToken = default)
        {
            StoredVersions.Add(version);
            return Task.CompletedTask;
        }

        public Task SaveVersesBatchAsync(IReadOnlyList<BibleVerse> verses, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StoredVersions.Count);
        }

        public Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BibleVersion>>(StoredVersions.AsReadOnly());

        public Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            var removed = StoredVersions.RemoveAll(v => v.Id == versionId) > 0;
            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<BibleBookSummary>> ListBooksAsync(Guid versionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BibleBookSummary>>([]);
        }

        public Task<IReadOnlyList<BibleVerse>> GetChapterVersesAsync(Guid versionId, int bookNumber, int chapter, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BibleVerse>>([]);
        }

        public Task<IReadOnlyList<BibleVerse>> SearchVersesFtsAsync(Guid versionId, string sanitizedFtsQuery, int limit = 50, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BibleVerse>>([]);
        }

        public Task<IReadOnlyList<BibleVerse>> FindByReferenceAsync(Guid versionId, BibleReference reference, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BibleVerse>>([]);
        }
    }

    [Fact]
    public async Task EnsureDefaultsAsyncConRepositorioVacioSiembraTodasLasVersionesPorDefecto()
    {
        // Arrange
        var repo = new InMemoryBibleRepository();

        // Act
        await DefaultBibleCatalog.EnsureDefaultsAsync(repo);

        // Assert
        Assert.Equal(5, repo.StoredVersions.Count);
        var abbreviations = repo.StoredVersions.Select(v => v.Abbreviation).ToList();
        Assert.Contains("RVR60", abbreviations);
        Assert.Contains("RV1909", abbreviations);
        Assert.Contains("NVI", abbreviations);
        Assert.Contains("NTV", abbreviations);
        Assert.Contains("PDT", abbreviations);
    }

    [Fact]
    public async Task EnsureDefaultsAsyncConVersionPreexistenteSoloSiembraLasFaltantesSinDuplicar()
    {
        // Arrange
        var repo = new InMemoryBibleRepository();
        var existingRvr60 = new BibleVersion(
            Guid.NewGuid(),
            "Reina Valera 1960 Existente",
            "RVR60",
            DateTimeOffset.UtcNow);
        repo.StoredVersions.Add(existingRvr60);

        // Act
        await DefaultBibleCatalog.EnsureDefaultsAsync(repo);

        // Assert
        Assert.Equal(5, repo.StoredVersions.Count);
        var rvrCount = repo.StoredVersions.Count(v => v.Abbreviation.Equals("RVR60", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, rvrCount);
        Assert.Contains(repo.StoredVersions, v => v.Abbreviation == "NVI");
        Assert.Contains(repo.StoredVersions, v => v.Abbreviation == "NTV");
        Assert.Contains(repo.StoredVersions, v => v.Abbreviation == "PDT");
        Assert.Contains(repo.StoredVersions, v => v.Abbreviation == "RV1909");
    }
}
