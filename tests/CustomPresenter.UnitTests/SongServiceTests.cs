using CustomPresenter.Application.Songs;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class SongServiceTests
{
    private sealed class InMemorySongRepository : ISongRepository
    {
        private readonly Dictionary<Guid, Song> _store = [];

        public Task CreateAsync(Song song, CancellationToken cancellationToken = default)
        {
            _store[song.Id] = song;
            return Task.CompletedTask;
        }

        public Task CreateBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default)
        {
            foreach (var song in songs)
            {
                _store[song.Id] = song;
            }

            return Task.CompletedTask;
        }

        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Song>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>(_store.Values.OrderBy(s => s.Title).ToList());

        public Task<IReadOnlyList<Song>> SearchAsync(
            string sanitizedFtsQuery,
            int limit = 50,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>(_store.Values.Take(limit).ToList());

        public Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default)
        {
            if (!_store.ContainsKey(song.Id))
            {
                return Task.FromResult(false);
            }

            _store[song.Id] = song;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.Remove(id));
    }

    [Fact]
    public async Task CreateSongAsyncDivideLetraEnDiapositivasYLaPersiste()
    {
        // Arrange
        var repo = new InMemorySongRepository();
        var service = new SongService(repo);
        var template = new SongBuilder().Build();

        // Act
        var created = await service.CreateSongAsync(
            template.Title,
            template.Author,
            template.MusicalKey,
            template.RawLyrics);
        var fetched = await service.GetSongAsync(created.Id);

        // Assert
        Assert.NotNull(fetched);
        Assert.Equal(2, fetched.Slides.Count);
        Assert.Equal(template.Title, fetched.Title);
    }

    [Fact]
    public async Task UpdateYDeleteSongAsyncReflejanCambiosDeInmediato()
    {
        // Arrange
        var repo = new InMemorySongRepository();
        var service = new SongService(repo);
        var created = await service.CreateSongAsync("Original", null, null, "Verso 1\n\nCoro");

        // Act
        var updated = await service.UpdateSongAsync(
            created.Id,
            "Editada",
            "Autor",
            "C",
            "Nuevo verso 1\n\nNuevo coro\n\nPuente");
        var allAfterUpdate = await service.ListSongsAsync();
        var deleted = await service.DeleteSongAsync(created.Id);
        var allAfterDelete = await service.ListSongsAsync();

        // Assert
        Assert.Equal("Editada", updated.Title);
        Assert.Equal(3, updated.Slides.Count);
        Assert.Single(allAfterUpdate);
        Assert.True(deleted);
        Assert.Empty(allAfterDelete);
    }

    [Fact]
    public async Task UpdateSongAsyncConIdInexistenteLanzaDomainException()
    {
        // Arrange
        var service = new SongService(new InMemorySongRepository());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(
            () => service.UpdateSongAsync(Guid.NewGuid(), "Título", null, null, "Letra"));
    }

    [Fact]
    public void ToSlideContentsConvierteEstrofasEnSlideContentParaProyeccion()
    {
        // Arrange
        var song = new SongBuilder().Build();

        // Act
        var slides = SongService.ToSlideContents(song);

        // Assert
        Assert.Equal(2, slides.Count);
        Assert.Equal(song.Title, slides[0].Title);
        Assert.Equal(song.Slides[0].Text, slides[0].Text);
        Assert.Equal($"{song.Id}:0", slides[0].Id);
    }
}
