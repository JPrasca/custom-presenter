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
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleVerse>>([]);

        public Task<IReadOnlyList<BibleVerse>> FindByReferenceAsync(
            Guid versionId,
            BibleReference reference,
            CancellationToken cancellationToken = default)
        {
            LastQueriedVersionId = versionId;
            UsedReferenceLookup = true;
            var matches = verses
                .Where(v => v.VersionId == versionId
                    && v.BookNumber == reference.CanonicalBookNumber
                    && v.Chapter == reference.Chapter)
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
}
