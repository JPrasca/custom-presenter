using CustomPresenter.Application.Backup;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;
using CustomPresenter.Infrastructure.Backup;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class BackupIntegrationTests : IAsyncLifetime
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-backup-{Guid.NewGuid():N}");

    private string SourceConnectionString => $"Data Source={Path.Combine(_tempRoot, "source.db")};Pooling=False";
    private string TargetConnectionString => $"Data Source={Path.Combine(_tempRoot, "target.db")};Pooling=False";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempRoot);
        await SqliteSchemaInitializer.EnsureCreatedAsync(SourceConnectionString);
        await SqliteSchemaInitializer.EnsureCreatedAsync(TargetConnectionString);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task CicloCompletoExportacionEImportacionEnBdLimpiaRestauraSinDuplicadosNiPerdidaDeFormato()
    {
        // Arrange
        var sourceSongs = new SqliteSongRepository(SourceConnectionString);
        var sourceAnns = new SqliteAnnouncementRepository(SourceConnectionString);
        var serializer = new JsonBackupSerializer();

        await sourceSongs.CreateAsync(Song.Create(
            "Cuán Grande Es Él",
            "Carl Boberg",
            "Bb",
            "Señor mi Dios, al contemplar los cielos\n\nMi corazón entona la canción"));
        await sourceAnns.CreateAsync(Announcement.Create(
            "Bienvenida",
            "Bienvenidos a casa\n\nSilencia tu teléfono"));

        var sourceBackupService = new BackupService(sourceSongs, sourceAnns, serializer);
        await using var backupStream = new MemoryStream();
        await sourceBackupService.ExportAsync(backupStream);
        backupStream.Position = 0;

        var targetSongs = new SqliteSongRepository(TargetConnectionString);
        var targetAnns = new SqliteAnnouncementRepository(TargetConnectionString);
        var targetBackupService = new BackupService(targetSongs, targetAnns, serializer);

        // Act
        var restore1 = await targetBackupService.ImportAsync(backupStream);
        backupStream.Position = 0;
        var restore2 = await targetBackupService.ImportAsync(backupStream);

        var songs = await targetSongs.ListAllAsync();
        var ftsResults = await targetSongs.SearchAsync("cielos*");
        var anns = await targetAnns.ListAllAsync();

        // Assert
        Assert.Equal(1, restore1.ImportedSongs);
        Assert.Equal(1, restore1.ImportedAnnouncements);
        Assert.Equal(0, restore2.ImportedSongs);
        Assert.Equal(1, restore2.SkippedSongs);
        Assert.Equal(0, restore2.ImportedAnnouncements);
        Assert.Equal(1, restore2.SkippedAnnouncements);

        Assert.Single(songs);
        Assert.Equal(2, songs[0].Slides.Count);
        Assert.Equal("Bb", songs[0].MusicalKey);
        Assert.Single(ftsResults);
        Assert.Single(anns);
        Assert.Equal(2, anns[0].Slides.Count);
    }

    [Fact]
    public async Task ImportAsyncConRespaldoParcialmenteInvalidoNoDejaRegistrosParcialesEnSqlite()
    {
        // Arrange
        var targetSongs = new SqliteSongRepository(TargetConnectionString);
        var targetAnns = new SqliteAnnouncementRepository(TargetConnectionString);
        var service = new BackupService(targetSongs, targetAnns, new JsonBackupSerializer());

        const string invalidJson = """
            {
              "schemaVersion": 1,
              "exportedAtUtc": "2026-11-21T00:00:00Z",
              "songs": [
                {
                  "id": "11111111-1111-1111-1111-111111111111",
                  "title": "Canción Válida",
                  "author": "Autor",
                  "musicalKey": "C",
                  "rawLyrics": "Verso 1",
                  "createdAtUtc": "2026-11-21T00:00:00Z",
                  "updatedAtUtc": "2026-11-21T00:00:00Z"
                }
              ],
              "announcements": [
                {
                  "id": "22222222-2222-2222-2222-222222222222",
                  "title": "",
                  "rawBody": "Sin título",
                  "createdAtUtc": "2026-11-21T00:00:00Z",
                  "updatedAtUtc": "2026-11-21T00:00:00Z"
                }
              ]
            }
            """;

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(invalidJson));

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(stream));
        Assert.Empty(await targetSongs.ListAllAsync());
        Assert.Empty(await targetAnns.ListAllAsync());
    }
}
