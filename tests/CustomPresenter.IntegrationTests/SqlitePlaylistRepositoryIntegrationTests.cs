using CustomPresenter.Application.Live;
using CustomPresenter.Application.Playlists;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Songs;
using CustomPresenter.Infrastructure.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class SqlitePlaylistRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-pl-{Guid.NewGuid():N}.db");

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
    public async Task CreacionReordenamientoYCargaMixtaPersisteOrdenDeterministaYProyectaSecuencialmente()
    {
        // Arrange
        var songRepo = new SqliteSongRepository(ConnectionString);
        var bibleRepo = new SqliteBibleRepository(ConnectionString);
        var announcementRepo = new SqliteAnnouncementRepository(ConnectionString);
        var playlistRepo = new SqlitePlaylistRepository(ConnectionString);
        var service = new PlaylistService(playlistRepo, songRepo, bibleRepo, announcementRepo);

        var song = Song.Create("Santo Santo", "Tradicional", "C", "Santo, Santo, Santo");
        await songRepo.CreateAsync(song);

        var version = new BibleVersion(Guid.NewGuid(), "Reina Valera 1960", "RVR60", DateTimeOffset.UtcNow);
        await bibleRepo.SaveBibleAsync(version, [new BibleVerse(version.Id, 19, "Salmos", 23, 1, "Jehová es mi pastor")]);

        var announcement = Announcement.Create("Ofrenda", "Tiempo de diezmos y ofrendas");
        await announcementRepo.CreateAsync(announcement);

        var playlist = await service.CreateAsync("Servicio Nocturno");
        playlist = await service.AddSongAsync(playlist.Id, song.Id);
        playlist = await service.AddBiblePassageAsync(playlist.Id, version.Id, 19, "Salmos", 23, 1);
        playlist = await service.AddAnnouncementAsync(playlist.Id, announcement.Id);

        var reorderedIds = new[]
        {
            playlist.Items[2].Id,
            playlist.Items[0].Id,
            playlist.Items[1].Id,
        };

        // Act
        await service.ReorderItemsAsync(playlist.Id, reorderedIds);
        var reloaded = await service.GetByIdAsync(playlist.Id);

        var liveManager = new LiveStateManager([new ChannelLiveBroadcaster()]);
        var slide0 = await service.ProjectItemAsync(playlist.Id, 0, 0, liveManager);
        var slide1 = await service.ProjectNextItemAsync(playlist.Id, 0, liveManager);
        var slide2 = await service.ProjectNextItemAsync(playlist.Id, 1, liveManager);

        // Assert
        Assert.NotNull(reloaded);
        Assert.Equal(3, reloaded.Items.Count);
        Assert.Equal("Ofrenda", reloaded.Items[0].DisplayTitle);
        Assert.Equal(0, reloaded.Items[0].Position);
        Assert.Equal("Santo Santo", reloaded.Items[1].DisplayTitle);
        Assert.Equal(1, reloaded.Items[1].Position);
        Assert.Equal("Salmos 23:1", reloaded.Items[2].DisplayTitle);
        Assert.Equal(2, reloaded.Items[2].Position);

        Assert.Equal("Tiempo de diezmos y ofrendas", slide0.CurrentSlide?.Text);
        Assert.Equal("Santo, Santo, Santo", slide1.CurrentSlide?.Text);
        Assert.Equal("Jehová es mi pastor", slide2.CurrentSlide?.Text);
        Assert.Equal("Salmos 23:1 (RVR60)", slide2.CurrentSlide?.Reference);
    }

    [Fact]
    public async Task DeleteAsyncEliminaItemsEnCascadaYResisteSqlInjection()
    {
        // Arrange
        var songRepo = new SqliteSongRepository(ConnectionString);
        var bibleRepo = new SqliteBibleRepository(ConnectionString);
        var announcementRepo = new SqliteAnnouncementRepository(ConnectionString);
        var playlistRepo = new SqlitePlaylistRepository(ConnectionString);
        var service = new PlaylistService(playlistRepo, songRepo, bibleRepo, announcementRepo);

        var song = Song.Create("Alabanza", null, null, "Aleluya");
        await songRepo.CreateAsync(song);

        const string payload = "Lista'; DROP TABLE playlists; --";
        var playlist = await service.CreateAsync(payload);
        await service.AddSongAsync(playlist.Id, song.Id);

        // Act
        var deleted = await service.DeleteAsync(playlist.Id);
        var remaining = await service.ListAllAsync();

        await using var conn = await SqliteConnectionFactory.OpenAsync(ConnectionString);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM playlist_items WHERE playlist_id = @id;";
        cmd.Parameters.AddWithValue("@id", playlist.Id.ToString("D"));
        var orphanCount = Convert.ToInt64(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        // Assert
        Assert.True(deleted);
        Assert.Empty(remaining);
        Assert.Equal(0, orphanCount);
    }
}
