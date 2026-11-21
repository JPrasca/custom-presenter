using CustomPresenter.Application.Backup;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Backup;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;
using CustomPresenter.Infrastructure.Backup;
using CustomPresenter.UnitTests.Builders;
using Xunit;

namespace CustomPresenter.UnitTests;

public sealed class BackupServiceTests
{
    private sealed class InMemorySongRepository : ISongRepository
    {
        public List<Song> Store { get; } = [];

        public Task CreateAsync(Song song, CancellationToken cancellationToken = default)
        {
            Store.Add(song);
            return Task.CompletedTask;
        }

        public Task CreateBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default)
        {
            Store.AddRange(songs);
            return Task.CompletedTask;
        }

        public Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.FirstOrDefault(s => s.Id == id));

        public Task<IReadOnlyList<Song>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>(Store.ToList());

        public Task<IReadOnlyList<Song>> SearchAsync(string sanitizedFtsQuery, int limit = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Song>>([]);

        public Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class InMemoryAnnouncementRepository : IAnnouncementRepository
    {
        public List<Announcement> Store { get; } = [];

        public Task CreateAsync(Announcement announcement, CancellationToken cancellationToken = default)
        {
            Store.Add(announcement);
            return Task.CompletedTask;
        }

        public Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Store.FirstOrDefault(a => a.Id == id));

        public Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Announcement>>(Store.ToList());

        public Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    [Fact]
    public async Task ExportEImportAsyncPreservanFormatoYDeduplicanInternaYExternamente()
    {
        // Arrange
        var songRepoSource = new InMemorySongRepository();
        var annRepoSource = new InMemoryAnnouncementRepository();
        songRepoSource.Store.Add(new SongBuilder()
            .WithTitle("Grande Es Tu Fidelidad")
            .WithAuthor("Thomas Chisholm")
            .WithMusicalKey("D")
            .WithRawLyrics("Oh tu fidelidad\n\nCada mañana la veo en mí")
            .Build());
        annRepoSource.Store.Add(new AnnouncementBuilder()
            .WithTitle("Horarios")
            .WithRawBody("Domingos 10:00 AM\n\nMiércoles 7:00 PM")
            .Build());

        var serializer = new JsonBackupSerializer();
        var sourceService = new BackupService(songRepoSource, annRepoSource, serializer);

        await using var backupStream = new MemoryStream();
        await sourceService.ExportAsync(backupStream);
        backupStream.Position = 0;

        var songRepoTarget = new InMemorySongRepository();
        var annRepoTarget = new InMemoryAnnouncementRepository();
        var targetService = new BackupService(songRepoTarget, annRepoTarget, serializer);

        // Act
        var firstImport = await targetService.ImportAsync(backupStream);
        backupStream.Position = 0;
        var secondImport = await targetService.ImportAsync(backupStream);

        // Assert
        Assert.Equal(1, firstImport.ImportedSongs);
        Assert.Equal(1, firstImport.ImportedAnnouncements);
        Assert.Equal(0, secondImport.ImportedSongs);
        Assert.Equal(1, secondImport.SkippedSongs);
        Assert.Equal(0, secondImport.ImportedAnnouncements);
        Assert.Equal(1, secondImport.SkippedAnnouncements);
        Assert.Single(songRepoTarget.Store);
        Assert.Equal(2, songRepoTarget.Store[0].Slides.Count);
        Assert.Single(annRepoTarget.Store);
        Assert.Equal(2, annRepoTarget.Store[0].Slides.Count);
    }

    [Fact]
    public async Task ImportAsyncConEntradasDuplicadasEnMismoJsonYValidacionFailFast()
    {
        // Arrange
        var songRepo = new InMemorySongRepository();
        var annRepo = new InMemoryAnnouncementRepository();
        var serializer = new JsonBackupSerializer();
        var service = new BackupService(songRepo, annRepo, serializer);

        var dupId = Guid.NewGuid();
        var dupPayload = new BackupPayloadBuilder()
            .WithSong(dupId, "Canción Repetida", null, null, "Letra")
            .WithSong(dupId, "Canción Repetida", null, null, "Letra")
            .WithAnnouncement(Guid.NewGuid(), "Aviso", "Texto")
            .Build();

        await using var dupStream = new MemoryStream();
        await serializer.SerializeAsync(dupPayload, dupStream);
        dupStream.Position = 0;

        var invalidPayload = new BackupPayloadBuilder()
            .WithSong(Guid.NewGuid(), "Canción Válida", null, null, "Letra")
            .WithAnnouncement(Guid.NewGuid(), "   ", "Aviso inválido sin título")
            .Build();

        await using var invalidStream = new MemoryStream();
        await serializer.SerializeAsync(invalidPayload, invalidStream);
        invalidStream.Position = 0;

        // Act & Assert - Fail-fast no persiste nada si un registro es inválido
        await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(invalidStream));
        Assert.Empty(songRepo.Store);
        Assert.Empty(annRepo.Store);

        // Act & Assert - Deduplica dentro del mismo JSON
        var summary = await service.ImportAsync(dupStream);
        Assert.Equal(1, summary.ImportedSongs);
        Assert.Equal(1, summary.SkippedSongs);
        Assert.Equal(1, summary.ImportedAnnouncements);
    }

    [Fact]
    public async Task ImportAsyncRechazaEsquemaInvalidoYJsonMalformado()
    {
        // Arrange
        var songRepo = new InMemorySongRepository();
        var annRepo = new InMemoryAnnouncementRepository();
        var serializer = new JsonBackupSerializer();
        var service = new BackupService(songRepo, annRepo, serializer);

        var badVersion = new BackupPayloadBuilder().WithSchemaVersion(999).Build();
        await using var badVersionStream = new MemoryStream();
        await serializer.SerializeAsync(badVersion, badVersionStream);
        badVersionStream.Position = 0;

        await using var malformedStream = new MemoryStream("{not valid json"u8.ToArray());

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(badVersionStream));
        await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(malformedStream));
    }
}
