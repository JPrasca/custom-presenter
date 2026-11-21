using System.Diagnostics;
using System.Text;
using CustomPresenter.Application.Songs;
using CustomPresenter.Domain.Songs;
using CustomPresenter.Infrastructure.Importers;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqliteSongSearchAndImportIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-fts-{Guid.NewGuid():N}.db");

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
    public async Task BusquedaFts5IgnoraAcentosYMayusculasYNeutralizaOperadoresPeligrosos()
    {
        // Arrange
        var repo = new SqliteSongRepository(ConnectionString);
        var service = new SongService(repo, new SongImporter());
        await service.CreateSongAsync(
            "Canción del Corazón",
            "Autor Prueba",
            "G",
            "Mi corazón te canta hoy\n\nCon alegría y paz");
        await service.CreateSongAsync(
            "Otra Melodía",
            null,
            null,
            "Letra distinta sin relación");

        // Act
        var byNoAccents = await service.SearchSongsAsync("cancion corazon");
        var bySymbolsAndMatch = await service.SearchSongsAsync("corazon * ( ) : ^");
        var byMaliciousOperators = await service.SearchSongsAsync("title:\"corazon\" OR * ( ) :");

        // Assert
        Assert.Single(byNoAccents);
        Assert.Equal("Canción del Corazón", byNoAccents[0].Title);
        Assert.Single(bySymbolsAndMatch);
        Assert.Equal("Canción del Corazón", bySymbolsAndMatch[0].Title);
        Assert.Empty(byMaliciousOperators); // "title" y "OR" tratados como texto literal, sin error FTS5
    }

    [Fact]
    public async Task BusquedaFts5SobreMilCancionesRespondeEnMenosDe30Ms()
    {
        // Arrange
        var repo = new SqliteSongRepository(ConnectionString);
        var batch = new List<Song>(1000);
        for (var i = 0; i < 999; i++)
        {
            batch.Add(Song.Create(
                $"Himno de Alabanza {i}",
                "Coro General",
                "C",
                $"Estrofa número {i} de adoración continua"));
        }

        batch.Add(Song.Create(
            "Roca Eterna Especial",
            "Autor Único",
            "E",
            "Tú eres mi refugio y fortaleza en la tormenta"));
        await repo.CreateBatchAsync(batch);

        // Calentamiento breve de caché de página SQLite
        _ = await repo.SearchAsync(Fts5QuerySanitizer.Sanitize("refugio"), 20);

        // Act
        var sw = Stopwatch.StartNew();
        var results = await repo.SearchAsync(Fts5QuerySanitizer.Sanitize("refugio fortaleza"), 20);
        sw.Stop();

        // Assert
        Assert.Single(results);
        Assert.Equal("Roca Eterna Especial", results[0].Title);
        Assert.True(sw.ElapsedMilliseconds < 30, $"Búsqueda FTS5 tardó {sw.ElapsedMilliseconds} ms (límite < 30 ms)");
    }

    [Fact]
    public async Task ImportarCancionesDesdeJsonYIndexarEnFts5FuncionaEnTransaccion()
    {
        // Arrange
        var repo = new SqliteSongRepository(ConnectionString);
        var service = new SongService(repo, new SongImporter());
        const string json = """
            [
              {
                "title": "Gloria en las Alturas",
                "author": "Salmista",
                "musicalKey": "A",
                "rawLyrics": "Gloria a Dios en las alturas\n\nY paz en la tierra"
              }
            ]
            """;
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var imported = await service.ImportSongsAsync(stream, "repertorio.json", stream.Length);
        var found = await service.SearchSongsAsync("alturas");

        // Assert
        Assert.Single(imported);
        Assert.Single(found);
        Assert.Equal("Gloria en las Alturas", found[0].Title);
    }

    [Fact]
    public async Task EnsureCreatedAsyncReindexaCancionesExistentesFaltantesEnSongsFts()
    {
        // Arrange - insertar canción manualmente y vaciar songs_fts simulando tabla previa
        var repo = new SqliteSongRepository(ConnectionString);
        var song = Song.Create("Canción Previa", null, null, "Verso de respaldo previo");
        await repo.CreateAsync(song);

        await using (var conn = await SqliteConnectionFactory.OpenAsync(ConnectionString))
        await using (var clearFts = conn.CreateCommand())
        {
            clearFts.CommandText = "DELETE FROM songs_fts;";
            await clearFts.ExecuteNonQueryAsync();
        }

        // Act
        await SqliteSchemaInitializer.EnsureCreatedAsync(ConnectionString);
        var found = await repo.SearchAsync(Fts5QuerySanitizer.Sanitize("respaldo"), 10);

        // Assert
        Assert.Single(found);
        Assert.Equal("Canción Previa", found[0].Title);
    }
}
