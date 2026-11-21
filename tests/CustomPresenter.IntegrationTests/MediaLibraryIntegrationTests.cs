using System.Net;
using CustomPresenter.Application.Live;
using CustomPresenter.Application.Media;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;
using CustomPresenter.Infrastructure.Media;
using CustomPresenter.Infrastructure.ObsServer;
using CustomPresenter.Infrastructure.Sqlite;
using Xunit;

namespace CustomPresenter.IntegrationTests;

public sealed class MediaLibraryIntegrationTests : IAsyncLifetime
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"custom-presenter-media-{Guid.NewGuid():N}");

    private string DbPath => Path.Combine(_tempRoot, "presenter.db");
    private string MediaRoot => Path.Combine(_tempRoot, "media");
    private string ConnectionString => $"Data Source={DbPath};Pooling=False";

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_tempRoot);
        Directory.CreateDirectory(MediaRoot);
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

    [Fact]
    public async Task ImportarImagenYVideoConMagicBytesValidosGuardaUuidYExponePorMediaId()
    {
        // Arrange
        var repo = new SqliteMediaRepository(ConnectionString);
        var storage = new LocalMediaStorage(MediaRoot);
        var service = new MediaLibraryService(repo, storage);
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        await using var obsServer = new ObsWebServer(ObsServerOptions.Ephemeral(), liveManager, broadcaster, repo, storage);
        await obsServer.StartAsync();

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0x04 };
        await using var pngStream = new MemoryStream(pngBytes);

        // Act
        var asset = await service.ImportAsync(pngStream, "fondo-cielo.png");
        await service.AssignBackgroundAsync(asset.Id, liveManager);

        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{obsServer.BoundEndpoint!.Port}") };
        var response = await http.GetAsync($"/media/{asset.Id:D}");
        var servedBytes = await response.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(pngBytes, servedBytes);
        Assert.Equal($"{asset.Id:D}.png", asset.StoredFileName);
        Assert.True(File.Exists(Path.Combine(MediaRoot, asset.StoredFileName)));
        Assert.Equal(asset.Id, liveManager.Snapshot().BackgroundMedia?.Id);
    }

    [Fact]
    public async Task GetMediaRechazaPathTraversalYExtensionesFalsasSinDejarHuerfanos()
    {
        // Arrange
        var repo = new SqliteMediaRepository(ConnectionString);
        var storage = new LocalMediaStorage(MediaRoot);
        var service = new MediaLibraryService(repo, storage);
        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);
        await using var obsServer = new ObsWebServer(ObsServerOptions.Ephemeral(), liveManager, broadcaster, repo, storage);
        await obsServer.StartAsync();

        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{obsServer.BoundEndpoint!.Port}") };

        // Act - Path Traversal
        var traversalRaw = await http.GetAsync("/media/../../etc/passwd");
        var traversalEncoded = await http.GetAsync("/media/%2e%2e%2fetc%2fpasswd");
        var missingGuid = await http.GetAsync($"/media/{Guid.NewGuid():D}");

        // Act - Extensión falsa
        await using var fakeStream = new MemoryStream("NO_ES_PNG_REAL"u8.ToArray());
        await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(fakeStream, "falso.png"));

        // Assert
        Assert.True(traversalRaw.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
        Assert.True(traversalEncoded.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.NotFound, missingGuid.StatusCode);
        Assert.Empty(Directory.GetFiles(MediaRoot));
    }
}
