using CustomPresenter.Application.Live;
using CustomPresenter.Application.Media;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class MediaLibraryServiceTests
{
    private sealed class InMemoryMediaRepository : IMediaRepository
    {
        public Dictionary<Guid, MediaAsset> Store { get; } = [];
        public bool FailOnCreate { get; set; }

        public Task CreateAsync(MediaAsset asset, CancellationToken cancellationToken = default)
        {
            if (FailOnCreate)
            {
                throw new InvalidOperationException("Error simulado de SQLite");
            }

            Store[asset.Id] = asset;
            return Task.CompletedTask;
        }

        public Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.GetValueOrDefault(id));

        public Task<IReadOnlyList<MediaAsset>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MediaAsset>>(Store.Values.ToList());

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.Remove(id));
    }

    private sealed class InMemoryMediaStorage : IMediaStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public async Task<long> SaveAsync(
            Stream source,
            string storedFileName,
            long maxBytes,
            CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await source.CopyToAsync(ms, cancellationToken);
            if (ms.Length > maxBytes)
            {
                throw new DomainException("Archivo excede el máximo permitido.");
            }

            Files[storedFileName] = ms.ToArray();
            return ms.Length;
        }

        public string ResolveSafeFilePath(MediaAsset asset) => $"/virtual/media/{asset.StoredFileName}";

        public Task<bool> DeleteAsync(string storedFileName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.Remove(storedFileName));
    }

    [Fact]
    public async Task ImportAsyncHaceRollbackEnDiscoSiFallaSqlite()
    {
        // Arrange
        var repo = new InMemoryMediaRepository { FailOnCreate = true };
        var storage = new InMemoryMediaStorage();
        var service = new MediaLibraryService(repo, storage);
        await using var stream = new MemoryStream(MediaAssetBuilder.CreateValidPngBytes());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ImportAsync(stream, "fondo.png"));

        Assert.Empty(storage.Files);
        Assert.Empty(repo.Store);
    }

    [Fact]
    public async Task AssignBackgroundYDeleteAsyncLimpianBackgroundActivoSiCoincide()
    {
        // Arrange
        var repo = new InMemoryMediaRepository();
        var storage = new InMemoryMediaStorage();
        var service = new MediaLibraryService(repo, storage);
        var liveManager = new LiveStateManager([new ChannelLiveBroadcaster()]);

        await using var stream = new MemoryStream(MediaAssetBuilder.CreateValidMp4Bytes());
        var asset = await service.ImportAsync(stream, "../ruta/peligrosa/loop.mp4");

        // Act
        var assignedState = await service.AssignBackgroundAsync(asset.Id, liveManager);
        var deleted = await service.DeleteAsync(asset.Id, liveManager);

        // Assert
        Assert.Equal(asset.Id, assignedState.BackgroundMedia?.Id);
        Assert.Equal(MediaKind.Video, assignedState.BackgroundMedia?.Kind);
        Assert.True(deleted);
        Assert.Null(liveManager.Snapshot().BackgroundMedia);
        Assert.Empty(storage.Files);
    }
}
