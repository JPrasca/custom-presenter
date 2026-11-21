using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CustomPresenter.Application.Announcements;
using CustomPresenter.Application.Backup;
using CustomPresenter.Application.Bibles;
using CustomPresenter.Application.Live;
using CustomPresenter.Application.Media;
using CustomPresenter.Application.Playlists;
using CustomPresenter.Application.Songs;
using CustomPresenter.Application.Themes;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Playlists;
using CustomPresenter.Domain.Themes;
using CustomPresenter.Infrastructure.Backup;
using CustomPresenter.Infrastructure.Importers;
using CustomPresenter.Infrastructure.Media;
using CustomPresenter.Infrastructure.ObsServer;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class FullApiIntegrationTests : IAsyncLifetime
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-fullapi-{Guid.NewGuid():N}");

    private string DbPath => Path.Combine(_tempRoot, "presenter.db");
    private string MediaRoot => Path.Combine(_tempRoot, "media");
    private string WebRoot => Path.Combine(_tempRoot, "wwwroot");
    private string ConnectionString => $"Data Source={DbPath};Pooling=False";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(MediaRoot);
        Directory.CreateDirectory(Path.Combine(WebRoot, "assets"));

        await File.WriteAllTextAsync(Path.Combine(WebRoot, "index.html"), "<!doctype html><html><body>OK</body></html>");
        await File.WriteAllTextAsync(Path.Combine(WebRoot, "assets", "app.js"), "console.log('ok');");

        await SqliteSchemaInitializer.EnsureCreatedAsync(ConnectionString);
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }

        return Task.CompletedTask;
    }

    private PresenterServices CreateServices(
        out SqliteSongRepository songRepo,
        out SqliteBibleRepository bibleRepo,
        out SqliteAnnouncementRepository annRepo)
    {
        songRepo = new SqliteSongRepository(ConnectionString);
        bibleRepo = new SqliteBibleRepository(ConnectionString);
        annRepo = new SqliteAnnouncementRepository(ConnectionString);
        var playlistRepo = new SqlitePlaylistRepository(ConnectionString);
        var themeRepo = new SqliteThemeRepository(ConnectionString);
        var mediaRepo = new SqliteMediaRepository(ConnectionString);
        var mediaStorage = new LocalMediaStorage(MediaRoot);

        return new PresenterServices(
            new SongService(songRepo, new SongImporter()),
            new BibleImportService(bibleRepo, new BibleImporter()),
            new BibleSearchService(bibleRepo),
            new AnnouncementService(annRepo),
            new PlaylistService(playlistRepo, songRepo, bibleRepo, annRepo),
            new ThemeService(themeRepo),
            new MediaLibraryService(mediaRepo, mediaStorage),
            new BackupService(songRepo, annRepo, new JsonBackupSerializer()),
            mediaRepo,
            mediaStorage,
            WebRoot);
    }

    [Fact]
    public async Task EndpointsApiCompletosYStaticWebRootFuncionanDePuntaAPunta()
    {
        // Arrange
        var services = CreateServices(out _, out _, out _);
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        await using var server = new ObsWebServer(
            ObsServerOptions.Ephemeral(),
            liveManager,
            broadcaster,
            services);
        await server.StartAsync();

        using var http = new HttpClient { BaseAddress = server.BaseUri };

        // Act - Static Web Root & Path Traversal
        var indexRes = await http.GetAsync("/");
        var projectorRes = await http.GetAsync("/projector");
        var assetRes = await http.GetAsync("/assets/app.js");
        var traversalRes = await http.GetAsync("/assets/../../etc/passwd");

        // Assert - Static Web
        Assert.Equal(HttpStatusCode.OK, indexRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, projectorRes.StatusCode);
        Assert.Equal(HttpStatusCode.OK, assetRes.StatusCode);
        Assert.True(traversalRes.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound);

        // Act - Seguridad sin cabecera operator y DomainException -> 400 { error }
        var unauthorizedRes = await http.PostAsJsonAsync("/api/songs", new { title = "X", rawLyrics = "Y" });
        Assert.Equal(HttpStatusCode.Forbidden, unauthorizedRes.StatusCode);

        http.DefaultRequestHeaders.Add("X-CustomPresenter-Client", "operator");

        var invalidSongRes = await http.PostAsJsonAsync("/api/songs", new { title = "", rawLyrics = "Y" });
        var invalidBody = await invalidSongRes.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.BadRequest, invalidSongRes.StatusCode);
        Assert.Contains("error", invalidBody, StringComparison.OrdinalIgnoreCase);

        // Act - Canciones
        var createSongRes = await http.PostAsJsonAsync("/api/songs", new
        {
            title = "Sublime Gracia",
            author = "John Newton",
            musicalKey = "G",
            rawLyrics = "Sublime gracia del Señor",
        });
        Assert.Equal(HttpStatusCode.OK, createSongRes.StatusCode);
        var createdSong = await createSongRes.Content.ReadFromJsonAsync<IdTitleDto>();
        Assert.NotNull(createdSong);

        var projectSongRes = await http.PostAsync($"/api/songs/{createdSong.Id:D}/project/0", null);
        Assert.Equal(HttpStatusCode.OK, projectSongRes.StatusCode);
        Assert.Equal("Sublime Gracia", liveManager.Snapshot().CurrentSlide?.Title);

        var updateSongRes = await http.PutAsJsonAsync($"/api/songs/{createdSong.Id:D}", new
        {
            title = "Sublime Gracia (Editada)",
            author = "John Newton",
            musicalKey = "A",
            rawLyrics = "Estrofa 1\n\nEstrofa 2",
        });
        Assert.Equal(HttpStatusCode.OK, updateSongRes.StatusCode);
        var updatedSong = await updateSongRes.Content.ReadFromJsonAsync<IdTitleDto>();
        Assert.Equal("Sublime Gracia (Editada)", updatedSong?.Title);

        var projectUpdatedRes = await http.PostAsync($"/api/songs/{createdSong.Id:D}/project/1", null);
        Assert.Equal(HttpStatusCode.OK, projectUpdatedRes.StatusCode);
        Assert.Equal("Estrofa 2", liveManager.Snapshot().CurrentSlide?.Text);

        // Act - Anuncios
        var createAnnRes = await http.PostAsJsonAsync("/api/announcements", new
        {
            title = "Bienvenida",
            rawBody = "Bienvenidos todos",
        });
        Assert.Equal(HttpStatusCode.OK, createAnnRes.StatusCode);
        var createdAnn = await createAnnRes.Content.ReadFromJsonAsync<IdTitleDto>();
        Assert.NotNull(createdAnn);

        // Act - Biblias
        const string xmlBible = """
            <?xml version="1.0" encoding="utf-8"?>
            <bible>
              <b n="Salmos">
                <c n="23">
                  <v n="1">Jehová es mi pastor; nada me faltará.</v>
                </c>
              </b>
            </bible>
            """;
        using var bibleMultipart = new MultipartFormDataContent();
        bibleMultipart.Add(new StringContent("Reina Valera 1960"), "name");
        bibleMultipart.Add(new StringContent("RVR60"), "abbreviation");
        var xmlBytes = new ByteArrayContent(Encoding.UTF8.GetBytes(xmlBible));
        xmlBytes.Headers.ContentType = MediaTypeHeaderValue.Parse("application/xml");
        bibleMultipart.Add(xmlBytes, "file", "rvr60.xml");

        var importBibleRes = await http.PostAsync("/api/bibles/import", bibleMultipart);
        Assert.Equal(HttpStatusCode.OK, importBibleRes.StatusCode);
        var importedVersion = await importBibleRes.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(importedVersion);

        var booksRes = await http.GetAsync($"/api/bibles/{importedVersion.Id:D}/books");
        Assert.Equal(HttpStatusCode.OK, booksRes.StatusCode);
        var booksBody = await booksRes.Content.ReadAsStringAsync();
        Assert.Contains("Salmos", booksBody, StringComparison.Ordinal);

        var chapterRes = await http.GetAsync($"/api/bibles/{importedVersion.Id:D}/books/1/chapters/23");
        Assert.Equal(HttpStatusCode.OK, chapterRes.StatusCode);
        var chapterVerses = await chapterRes.Content.ReadFromJsonAsync<CustomPresenter.Domain.Bibles.BibleVerse[]>();
        Assert.NotNull(chapterVerses);
        Assert.Single(chapterVerses);
        Assert.Contains("Jehová es mi pastor", chapterVerses[0].Text, StringComparison.Ordinal);

        // Act - Playlist
        var createPlRes = await http.PostAsJsonAsync("/api/playlists", new { name = "Domingo" });
        Assert.Equal(HttpStatusCode.OK, createPlRes.StatusCode);
        var playlist = await createPlRes.Content.ReadFromJsonAsync<Playlist>();
        Assert.NotNull(playlist);

        var addPlSong = await http.PostAsJsonAsync($"/api/playlists/{playlist.Id:D}/items", new
        {
            kind = "Song",
            songId = createdSong.Id,
        });
        Assert.Equal(HttpStatusCode.OK, addPlSong.StatusCode);

        // Act - Temas
        var themeRes = await http.PutAsJsonAsync("/api/themes/projector", new
        {
            fontFamily = "Inter",
            fontSizePx = 72,
            textColor = "#FFFFFF",
            backgroundColor = "#0B0D12",
            alignment = "Center",
        });
        Assert.Equal(HttpStatusCode.OK, themeRes.StatusCode);
        Assert.Equal(72, liveManager.Snapshot().EffectiveTheme.Projector.FontSizePx);

        // Act - Multimedia
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02 };
        using var mediaForm = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        mediaForm.Add(fileContent, "file", "fondo.png");

        var uploadMediaRes = await http.PostAsync("/api/media", mediaForm);
        Assert.Equal(HttpStatusCode.OK, uploadMediaRes.StatusCode);
        var uploadedMedia = await uploadMediaRes.Content.ReadFromJsonAsync<IdDto>();
        Assert.NotNull(uploadedMedia);

        var assignRes = await http.PostAsync($"/api/media/{uploadedMedia.Id:D}/assign", null);
        Assert.Equal(HttpStatusCode.OK, assignRes.StatusCode);
        Assert.Equal(uploadedMedia.Id, liveManager.Snapshot().BackgroundMedia?.Id);

        // Act - Backup Export
        var exportRes = await http.GetAsync("/api/backup/export");
        Assert.Equal(HttpStatusCode.OK, exportRes.StatusCode);
        var exportedJson = await exportRes.Content.ReadAsStringAsync();
        Assert.Contains("Sublime Gracia", exportedJson, StringComparison.Ordinal);
        Assert.Contains("Bienvenida", exportedJson, StringComparison.Ordinal);
    }

    private sealed record IdDto(Guid Id);
    private sealed record IdTitleDto(Guid Id, string Title);
}
