using CustomPresenter.Application.Bibles;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class BibleSearchServiceTests
{
    private sealed class FakeBibleRepository(IReadOnlyList<BibleVerse> verses) : IBibleRepository
    {
        public Guid? LastQueriedVersionId { get; private set; }
        public bool UsedReferenceLookup { get; private set; }
        public bool UsedFtsLookup { get; private set; }

        public Task SaveBibleAsync(
            BibleVersion version,
            IReadOnlyList<BibleVerse> items,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleVersion>>([]);

        public Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<BibleBookSummary>> ListBooksAsync(
            Guid versionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleBookSummary>>([]);

        public Task<IReadOnlyList<BibleVerse>> GetChapterVersesAsync(
            Guid versionId,
            int bookNumber,
            int chapter,
            CancellationToken cancellationToken = default)
        {
            var matches = verses
                .Where(v => v.VersionId == versionId
                    && v.BookNumber == bookNumber
                    && v.Chapter == chapter)
                .ToList();
            return Task.FromResult<IReadOnlyList<BibleVerse>>(matches);
        }

        public List<BibleVerse> SavedBatches { get; } = [];

        public Task SaveVersesBatchAsync(
            IReadOnlyList<BibleVerse> items,
            CancellationToken cancellationToken = default)
        {
            SavedBatches.AddRange(items);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BibleVerse>> FindByReferenceAsync(
            Guid versionId,
            BibleReference reference,
            CancellationToken cancellationToken = default)
        {
            LastQueriedVersionId = versionId;
            UsedReferenceLookup = true;
            var allVerses = verses.Concat(SavedBatches);
            var matches = allVerses
                .Where(v => v.VersionId == versionId
                    && v.BookNumber == reference.CanonicalBookNumber
                    && v.Chapter == reference.Chapter
                    && (reference.EndVerse == null
                        ? v.Verse == reference.StartVerse
                        : (v.Verse >= reference.StartVerse && v.Verse <= reference.EndVerse.Value)))
                .ToList();
            return Task.FromResult<IReadOnlyList<BibleVerse>>(matches);
        }

        public Task<IReadOnlyList<BibleVerse>> SearchVersesFtsAsync(
            Guid versionId,
            string sanitizedFtsQuery,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            LastQueriedVersionId = versionId;
            UsedFtsLookup = true;
            var matches = verses.Where(v => v.VersionId == versionId).Take(limit).ToList();
            return Task.FromResult<IReadOnlyList<BibleVerse>>(matches);
        }
    }

    [Fact]
    public async Task SearchAsyncEnrutaACitaOFts5YRespetaVersionActiva()
    {
        // Arrange
        var v1 = Guid.NewGuid();
        var v2 = Guid.NewGuid();
        var verseV1 = new BibleVerseBuilder().WithVersionId(v1).WithText("Jehová es mi pastor").Build();
        var verseV2 = new BibleVerseBuilder().WithVersionId(v2).WithText("El Señor es mi pastor").Build();
        var repo = new FakeBibleRepository([verseV1, verseV2]);
        var service = new BibleSearchService(repo);

        // Act
        var refResults = await service.SearchAsync(v2, "Salmos 23:1");
        var ftsResults = await service.SearchAsync(v1, "pastor");

        // Assert
        Assert.True(repo.UsedReferenceLookup);
        Assert.True(repo.UsedFtsLookup);
        Assert.Single(refResults);
        Assert.Equal("El Señor es mi pastor", refResults[0].Text);
        Assert.Single(ftsResults);
        Assert.Equal("Jehová es mi pastor", ftsResults[0].Text);
    }

    [Fact]
    public void ToSlideContentFormateaCitaConVersionEnReference()
    {
        // Arrange
        var verse = new BibleVerseBuilder()
            .WithBook(19, "Salmos")
            .WithReference(23, 1)
            .WithText("Jehová es mi pastor; nada me faltará.")
            .Build();
        var verse2 = new BibleVerseBuilder()
            .WithBook(19, "Salmos")
            .WithReference(23, 2)
            .WithText("En lugares de delicados pastos me hará descansar.")
            .Build();

        // Act
        var singleSlide = BibleSearchService.ToSlideContent(verse, "RVR60");
        var rangeSlide = BibleSearchService.ToSlideContent([verse, verse2], "RVR60");

        // Assert
        Assert.Equal("Salmos 23:1 (RVR60)", singleSlide.Reference);
        Assert.Equal("Salmos 23:1-2 (RVR60)", rangeSlide.Reference);
        Assert.Contains("1 Jehová es mi pastor", rangeSlide.Text, StringComparison.Ordinal);
        Assert.Contains("2 En lugares de delicados pastos", rangeSlide.Text, StringComparison.Ordinal);
    }

    private sealed class FakeBibleOnlineProvider(IReadOnlyList<BibleVerse> onlineVerses) : IBibleOnlineProvider
    {
        public Task<IReadOnlyList<BibleVerse>> FetchChapterVersesAsync(
            Guid versionId,
            string versionAbbreviation,
            int bookNumber,
            string bookName,
            int chapter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(onlineVerses);
    }

    [Fact]
    public async Task GetChapterVersesAsyncSinVersiculosLocalesDescargaPersisteYDevuelveDesdeOnline()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var repo = new FakeBibleRepository([]);
        var downloadedVerse = new BibleVerseBuilder()
            .WithVersionId(versionId)
            .WithBook(43, "Juan")
            .WithReference(3, 16)
            .WithText("Porque de tal manera amó Dios al mundo...")
            .Build();
        var onlineProvider = new FakeBibleOnlineProvider([downloadedVerse]);
        var service = new BibleSearchService(repo, onlineProvider);

        // Act
        var result = await service.GetChapterVersesAsync(versionId, 43, 3);

        // Assert
        Assert.Single(result);
        Assert.Equal("Juan", result[0].BookName);
        Assert.Equal(16, result[0].Verse);
        Assert.Single(repo.SavedBatches);
        Assert.Equal(downloadedVerse.Text, repo.SavedBatches[0].Text);
    }

    [Fact]
    public async Task SearchAsyncConCitaDirectaSinVersiculosLocalesDescargaPersisteYRetornaCoincidenciaDesdeOnline()
    {
        // Arrange
        var versionId = Guid.NewGuid();
        var repo = new FakeBibleRepository([]);
        var downloadedVerse = new BibleVerseBuilder()
            .WithVersionId(versionId)
            .WithBook(43, "Juan")
            .WithReference(3, 16)
            .WithText("Porque de tal manera amó Dios al mundo que dio a su Hijo unigénito...")
            .Build();
        var onlineProvider = new FakeBibleOnlineProvider([downloadedVerse]);
        var service = new BibleSearchService(repo, onlineProvider);

        // Act
        var result = await service.SearchAsync(versionId, "Juan 3:16");

        // Assert
        Assert.Single(result);
        Assert.Equal("Juan", result[0].BookName);
        Assert.Equal(3, result[0].Chapter);
        Assert.Equal(16, result[0].Verse);
        Assert.Single(repo.SavedBatches);
        Assert.Equal(downloadedVerse.Text, repo.SavedBatches[0].Text);
    }
}
