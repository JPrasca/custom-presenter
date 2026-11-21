using System.Globalization;
using CustomPresenter.Application.Bibles;
using CustomPresenter.Infrastructure.Importers;
using CustomPresenter.Infrastructure.Sqlite;
using CustomPresenter.IntegrationTests.Builders;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqliteBibleRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-bible-{Guid.NewGuid():N}.db");

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
    public async Task ImportarBibliaXmlPersisteEnSqliteYIndexaEnFts5EnUnaTransaccion()
    {
        // Arrange
        var repo = new SqliteBibleRepository(ConnectionString);
        var service = new BibleImportService(repo, new BibleImporter());
        await using var stream = new BiblePayloadBuilder().BuildStream();

        // Act
        var imported = await service.ImportBibleAsync(stream, "rvr60.xml", stream.Length);
        var versions = await service.ListVersionsAsync();
        var ftsCount = await CountFtsVersesAsync(imported.Version.Id);

        // Assert
        Assert.Single(versions);
        Assert.Equal("RVR60", versions[0].Abbreviation);
        Assert.Equal(2, imported.Verses.Count);
        Assert.Equal(2, ftsCount);
    }

    [Fact]
    public async Task DeleteVersionAsyncEliminaVersionVersiculosPorCascadaYFts5()
    {
        // Arrange
        var repo = new SqliteBibleRepository(ConnectionString);
        var service = new BibleImportService(repo, new BibleImporter());
        await using var stream = new BiblePayloadBuilder().AsJson().BuildStream();
        var imported = await service.ImportBibleAsync(stream, "nvi.json", stream.Length);

        // Act
        var deleted = await service.DeleteVersionAsync(imported.Version.Id);
        var versions = await service.ListVersionsAsync();
        var versesCount = await CountVersesAsync(imported.Version.Id);
        var ftsCount = await CountFtsVersesAsync(imported.Version.Id);

        // Assert
        Assert.True(deleted);
        Assert.Empty(versions);
        Assert.Equal(0, versesCount);
        Assert.Equal(0, ftsCount);
    }

    private async Task<long> CountVersesAsync(Guid versionId)
    {
        await using var conn = await SqliteConnectionFactory.OpenAsync(ConnectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bible_verses WHERE version_id = @id;";
        cmd.Parameters.AddWithValue("@id", versionId.ToString());
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private async Task<long> CountFtsVersesAsync(Guid versionId)
    {
        await using var conn = await SqliteConnectionFactory.OpenAsync(ConnectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM bible_verses_fts WHERE version_id = @id;";
        cmd.Parameters.AddWithValue("@id", versionId.ToString());
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
}
