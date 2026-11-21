using CustomPresenter.Domain.Songs;
using CustomPresenter.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqliteSongRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-test-{Guid.NewGuid():N}.db");

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
    public async Task CrudCompletoEnSqlitePersisteActualizaYEliminaConCascada()
    {
        // Arrange
        var repo = new SqliteSongRepository(ConnectionString);
        var song = Song.Create(
            "Sublime Gracia",
            "John Newton",
            "G",
            "Sublime gracia del Señor\nQue a un infeliz salvó\n\nYo ciego fui, mas hoy veo ya");

        // Act - Create & Read
        await repo.CreateAsync(song);
        var loaded = await repo.GetByIdAsync(song.Id);

        // Act - Update
        var updatedSong = song.WithUpdatedContent(
            "Sublime Gracia (Editada)",
            "John Newton",
            "A",
            "Estrofa única renovada");
        var updatedOk = await repo.UpdateAsync(updatedSong);
        var afterUpdate = await repo.GetByIdAsync(song.Id);

        // Act - Delete & verificar cascada en song_slides
        var deletedOk = await repo.DeleteAsync(song.Id);
        var afterDelete = await repo.GetByIdAsync(song.Id);
        var remainingSlides = await CountSlidesAsync(song.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Slides.Count);
        Assert.True(updatedOk);
        Assert.NotNull(afterUpdate);
        Assert.Equal("Sublime Gracia (Editada)", afterUpdate.Title);
        Assert.Single(afterUpdate.Slides);
        Assert.True(deletedOk);
        Assert.Null(afterDelete);
        Assert.Equal(0, remainingSlides);
    }

    [Fact]
    public async Task CreateAsyncConPayloadSqlInjectionLoAlmacenaComoTextoLiteralSinAfectarTabla()
    {
        // Arrange
        var repo = new SqliteSongRepository(ConnectionString);
        const string sqliTitle = "'); DROP TABLE songs; --";
        var song = Song.Create(sqliTitle, "'; DELETE FROM song_slides; --", "C", "Letra segura");

        // Act
        await repo.CreateAsync(song);
        var all = await repo.ListAllAsync();

        // Assert
        Assert.Single(all);
        Assert.Equal(sqliTitle, all[0].Title);
    }

    [Fact]
    public async Task ConnectionFactoryActivaWalYForeignKeysYRechazaPathTraversal()
    {
        // Arrange
        var allowedRoot = Path.GetTempPath();
        var traversalPath = Path.Combine(allowedRoot, "..", "..", "etc", "evil.db");

        // Act
        await using var connection = await SqliteConnectionFactory.OpenAsync(ConnectionString);
        await using var journalCmd = connection.CreateCommand();
        journalCmd.CommandText = "PRAGMA journal_mode;";
        var journalMode = (await journalCmd.ExecuteScalarAsync())?.ToString();

        await using var fkCmd = connection.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys;";
        var fkEnabled = Convert.ToInt32(await fkCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        // Assert
        Assert.Equal("wal", journalMode, ignoreCase: true);
        Assert.Equal(1, fkEnabled);
        Assert.Throws<UnauthorizedAccessException>(
            () => SqliteConnectionFactory.BuildConnectionString(traversalPath, allowedRoot));
    }

    private async Task<long> CountSlidesAsync(Guid songId)
    {
        await using var connection = await SqliteConnectionFactory.OpenAsync(ConnectionString);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM song_slides WHERE song_id = @songId;";
        cmd.Parameters.AddWithValue("@songId", songId.ToString());
        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
