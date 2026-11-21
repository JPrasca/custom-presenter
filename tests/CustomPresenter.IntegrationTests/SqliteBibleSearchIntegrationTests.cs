using System.Text;
using CustomPresenter.Application.Bibles;
using CustomPresenter.Application.Live;
using CustomPresenter.Domain.Live;
using CustomPresenter.Infrastructure.Importers;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqliteBibleSearchIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-bible-search-{Guid.NewGuid():N}.db");

    private string ConnectionString => $"Data Source={_dbPath};Pooling=False";

    public async Task InitializeAsync()
    {
        await SqliteSchemaInitializer.EnsureCreatedAsync(ConnectionString);
    }

    public Task DisposeAsync()
    {
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task BusquedaPorCitaAbreviaturaRangoYFts5CambiaSegunVersionActivaYProyecta()
    {
        // Arrange
        var repo = new SqliteBibleRepository(ConnectionString);
        var importer = new BibleImporter();
        var importService = new BibleImportService(repo, importer);
        var searchService = new BibleSearchService(repo);
        var liveManager = new LiveStateManager([]);

        const string rvrJson = """
            {
              "name": "Reina Valera 1960",
              "abbreviation": "RVR60",
              "verses": [
                { "bookNumber": 19, "bookName": "Salmos", "chapter": 23, "verse": 1, "text": "Jehová es mi pastor; nada me faltará." },
                { "bookNumber": 19, "bookName": "Salmos", "chapter": 23, "verse": 2, "text": "En lugares de delicados pastos me hará descansar." },
                { "bookNumber": 1, "bookName": "Génesis", "chapter": 1, "verse": 1, "text": "En el principio creó Dios los cielos y la tierra." }
              ]
            }
            """;
        const string nviJson = """
            {
              "name": "Nueva Versión Internacional",
              "abbreviation": "NVI",
              "verses": [
                { "bookNumber": 19, "bookName": "Salmos", "chapter": 23, "verse": 1, "text": "El Señor es mi pastor; nada me faltará." }
              ]
            }
            """;

        await using var s1 = new MemoryStream(Encoding.UTF8.GetBytes(rvrJson));
        await using var s2 = new MemoryStream(Encoding.UTF8.GetBytes(nviJson));
        var rvr = await importService.ImportBibleAsync(s1, "rvr60.json", s1.Length);
        var nvi = await importService.ImportBibleAsync(s2, "nvi.json", s2.Length);

        // Act
        var rvrByRef = await searchService.SearchAsync(rvr.Version.Id, "Sal 23:1-2");
        var rvrGenesisNoAccent = await searchService.SearchAsync(rvr.Version.Id, "Genesis 1:1");
        var nviByRef = await searchService.SearchAsync(nvi.Version.Id, "Salmos 23:1");
        var rvrByFts = await searchService.SearchAsync(rvr.Version.Id, "delicados pastos");
        var books = await searchService.ListBooksAsync(rvr.Version.Id);

        var slide = BibleSearchService.ToSlideContent(rvrByRef[0], rvr.Version.Abbreviation);
        var liveState = await liveManager.ProjectSlideAsync(slide);

        // Assert
        Assert.Equal(2, rvrByRef.Count);
        Assert.Single(rvrGenesisNoAccent);
        Assert.Single(nviByRef);
        Assert.Equal("El Señor es mi pastor; nada me faltará.", nviByRef[0].Text);
        Assert.Single(rvrByFts);
        Assert.Equal(2, books.Count);
        Assert.True(liveState.IsTextVisible);
        Assert.Equal(EffectiveDisplayMode.Slide, liveState.EffectiveMode);
        Assert.Equal("Salmos 23:1 (RVR60)", liveState.CurrentSlide?.Reference);
    }

    [Fact]
    public async Task SearchAsyncPorCitaEncuentraLibroEnBibliaParcialConBookNumberNoCanonico()
    {
        // Arrange
        var repo = new SqliteBibleRepository(ConnectionString);
        var importService = new BibleImportService(repo, new BibleImporter());
        var searchService = new BibleSearchService(repo);

        const string partialXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <bible>
              <b n="Salmos">
                <c n="23">
                  <v n="1">Jehová es mi pastor; nada me faltará.</v>
                </c>
              </b>
            </bible>
            """;

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(partialXml));
        var imported = await importService.ImportBibleAsync(stream, "parcial.xml", stream.Length, "Parcial", "PAR");

        // Act
        var results = await searchService.SearchAsync(imported.Version.Id, "Salmos 23:1");

        // Assert
        Assert.Single(results);
        Assert.Equal("Jehová es mi pastor; nada me faltará.", results[0].Text);
    }
}
