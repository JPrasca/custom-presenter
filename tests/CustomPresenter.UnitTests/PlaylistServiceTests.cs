using CustomPresenter.Application.Live;
using CustomPresenter.Application.Playlists;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Playlists;
using CustomPresenter.Domain.Songs;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class PlaylistServiceTests
{
    private sealed class InMemoryPlaylistRepository : IPlaylistRepository
    {
        private readonly Dictionary<Guid, Playlist> _store = [];

        public Task CreateAsync(Playlist playlist, CancellationToken cancellationToken = default)
        {
            _store[playlist.Id] = playlist;
            return Task.CompletedTask;
        }

        public Task<Playlist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Playlist>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Playlist>>(
                _store.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList());

        public Task<bool> UpdateAsync(Playlist playlist, CancellationToken cancellationToken = default)
        {
            if (!_store.ContainsKey(playlist.Id))
            {
                return Task.FromResult(false);
            }

            _store[playlist.Id] = playlist;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.Remove(id));
    }

    private sealed class InMemorySongRepository : ISongRepository
    {
        public Dictionary<Guid, Song> Store { get; } = [];

        public Task CreateAsync(Song song, CancellationToken cancellationToken = default)
        {
            Store[song.Id] = song;
            return Task.CompletedTask;
        }

        public Task CreateBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Song>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>(Store.Values.ToList());

        public Task<IReadOnlyList<Song>> SearchAsync(string sanitizedFtsQuery, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>([]);

        public Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.Remove(id));
    }

    private sealed class InMemoryBibleRepository : IBibleRepository
    {
        public List<BibleVersion> Versions { get; } = [];
        public List<BibleVerse> Verses { get; } = [];

        public Task SaveBibleAsync(BibleVersion version, IReadOnlyList<BibleVerse> verses, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleVersion>>(Versions);

        public Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<BibleBookSummary>> ListBooksAsync(Guid versionId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleBookSummary>>([]);

        public Task<IReadOnlyList<BibleVerse>> GetChapterVersesAsync(Guid versionId, int bookNumber, int chapter, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleVerse>>(
                Verses.Where(v => v.VersionId == versionId && v.BookNumber == bookNumber && v.Chapter == chapter)
                    .OrderBy(v => v.Verse)
                    .ToList());

        public Task<IReadOnlyList<BibleVerse>> FindByReferenceAsync(Guid versionId, BibleReference reference, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleVerse>>([]);

        public Task<IReadOnlyList<BibleVerse>> SearchVersesFtsAsync(Guid versionId, string sanitizedFtsQuery, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<BibleVerse>>([]);

        public Task SaveVersesBatchAsync(IReadOnlyList<BibleVerse> verses, CancellationToken cancellationToken = default)
        {
            Verses.AddRange(verses);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryAnnouncementRepository : IAnnouncementRepository
    {
        public Dictionary<Guid, Announcement> Store { get; } = [];

        public Task CreateAsync(Announcement announcement, CancellationToken cancellationToken = default)
        {
            Store[announcement.Id] = announcement;
            return Task.CompletedTask;
        }

        public Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.GetValueOrDefault(id));

        public Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Announcement>>(Store.Values.ToList());

        public Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.Remove(id));
    }

    private static PlaylistService CreateService(
        out InMemoryPlaylistRepository playlistRepo,
        out InMemorySongRepository songRepo,
        out InMemoryBibleRepository bibleRepo,
        out InMemoryAnnouncementRepository announcementRepo)
    {
        playlistRepo = new InMemoryPlaylistRepository();
        songRepo = new InMemorySongRepository();
        bibleRepo = new InMemoryBibleRepository();
        announcementRepo = new InMemoryAnnouncementRepository();
        return new PlaylistService(playlistRepo, songRepo, bibleRepo, announcementRepo);
    }

    [Fact]
    public void CreateYWithReorderedItemsValidanNombreYPermutacionExacta()
    {
        // Arrange
        var songId = Guid.NewGuid();
        var annId = Guid.NewGuid();
        var playlist = new PlaylistBuilder()
            .WithName("Culto Central")
            .WithSongItem(songId, "Canción 1")
            .WithAnnouncementItem(annId, "Aviso 1")
            .Build();

        var item0 = playlist.Items[0];
        var item1 = playlist.Items[1];

        // Act
        var reordered = playlist.WithReorderedItems([item1.Id, item0.Id]);

        // Assert
        Assert.Equal(2, reordered.Items.Count);
        Assert.Equal(item1.Id, reordered.Items[0].Id);
        Assert.Equal(0, reordered.Items[0].Position);
        Assert.Equal(item0.Id, reordered.Items[1].Id);
        Assert.Equal(1, reordered.Items[1].Position);

        Assert.Throws<DomainException>(() => Playlist.Create("   "));
        Assert.Throws<DomainException>(() => playlist.WithReorderedItems([item0.Id, item0.Id]));
        Assert.Throws<DomainException>(() => playlist.WithReorderedItems([item0.Id]));
        Assert.Throws<DomainException>(() => playlist.WithReorderedItems([item0.Id, item1.Id, Guid.NewGuid()]));
    }

    [Fact]
    public async Task ProjectYAvanceSecuencialResuelvenItemsMixtosEnLiveStateManager()
    {
        // Arrange
        var service = CreateService(out _, out var songRepo, out var bibleRepo, out var announcementRepo);
        var song = new SongBuilder().WithTitle("Cuan Grande Es El").WithRawLyrics("Señor mi Dios").Build();
        songRepo.Store[song.Id] = song;

        var version = new BibleVersion(Guid.NewGuid(), "Reina Valera 1960", "RVR60", DateTimeOffset.UtcNow);
        bibleRepo.Versions.Add(version);
        bibleRepo.Verses.Add(new BibleVerseBuilder()
            .WithVersionId(version.Id)
            .WithBook(19, "Salmos")
            .WithReference(23, 1)
            .WithText("Jehová es mi pastor; nada me faltará.")
            .Build());

        var announcement = new AnnouncementBuilder().WithTitle("Bienvenida").WithRawBody("Bienvenidos todos").Build();
        announcementRepo.Store[announcement.Id] = announcement;

        var playlist = await service.CreateAsync("Domingo AM");
        playlist = await service.AddSongAsync(playlist.Id, song.Id);
        playlist = await service.AddBiblePassageAsync(playlist.Id, version.Id, 19, "Salmos", 23, 1);
        playlist = await service.AddAnnouncementAsync(playlist.Id, announcement.Id);

        var broadcaster = new ChannelLiveBroadcaster();
        var liveManager = new LiveStateManager([broadcaster]);

        // Act
        var first = await service.ProjectItemAsync(playlist.Id, 0, 0, liveManager);
        var second = await service.ProjectNextItemAsync(playlist.Id, 0, liveManager);
        var third = await service.ProjectNextItemAsync(playlist.Id, 1, liveManager);
        var previous = await service.ProjectPreviousItemAsync(playlist.Id, 2, liveManager);

        // Assert
        Assert.Equal("Señor mi Dios", first.CurrentSlide?.Text);
        Assert.Equal("Jehová es mi pastor; nada me faltará.", second.CurrentSlide?.Text);
        Assert.Equal("Salmos 23:1 (RVR60)", second.CurrentSlide?.Reference);
        Assert.Equal("Bienvenidos todos", third.CurrentSlide?.Text);
        Assert.Equal("Jehová es mi pastor; nada me faltará.", previous.CurrentSlide?.Text);
    }

    [Fact]
    public async Task ResolveSlidesAsyncLanzaDomainExceptionClaraCuandoReferenciaFueEliminada()
    {
        // Arrange
        var service = CreateService(out _, out var songRepo, out var bibleRepo, out var announcementRepo);
        var song = new SongBuilder().WithTitle("Canción Borrada").WithRawLyrics("Texto").Build();
        var announcement = new AnnouncementBuilder().WithTitle("Anuncio Borrado").WithRawBody("Texto").Build();
        songRepo.Store[song.Id] = song;
        announcementRepo.Store[announcement.Id] = announcement;

        var versionId = Guid.NewGuid();
        bibleRepo.Versions.Add(new BibleVersion(versionId, "RVR60", "RVR60", DateTimeOffset.UtcNow));
        bibleRepo.Verses.Add(new BibleVerseBuilder().WithVersionId(versionId).WithBook(43, "Juan").WithReference(3, 16).Build());

        var playlist = await service.CreateAsync("Lista con bajas");
        playlist = await service.AddSongAsync(playlist.Id, song.Id);
        playlist = await service.AddBiblePassageAsync(playlist.Id, versionId, 43, "Juan", 3, 16);
        playlist = await service.AddAnnouncementAsync(playlist.Id, announcement.Id);

        songRepo.Store.Remove(song.Id);
        bibleRepo.Verses.Clear();
        announcementRepo.Store.Remove(announcement.Id);

        var liveManager = new LiveStateManager([new ChannelLiveBroadcaster()]);

        // Act & Assert
        var exSong = await Assert.ThrowsAsync<DomainException>(() => service.ProjectItemAsync(playlist.Id, 0, 0, liveManager));
        var exBible = await Assert.ThrowsAsync<DomainException>(() => service.ProjectItemAsync(playlist.Id, 1, 0, liveManager));
        var exAnn = await Assert.ThrowsAsync<DomainException>(() => service.ProjectItemAsync(playlist.Id, 2, 0, liveManager));

        Assert.Contains("Canción Borrada", exSong.Message, StringComparison.Ordinal);
        Assert.Contains("Juan 3:16", exBible.Message, StringComparison.Ordinal);
        Assert.Contains("Anuncio Borrado", exAnn.Message, StringComparison.Ordinal);
    }
}
