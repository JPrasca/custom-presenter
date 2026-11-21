using CustomPresenter.Domain.Backup;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class BackupPayloadBuilder
{
    private int _schemaVersion = BackupPayload.CurrentSchemaVersion;
    private readonly List<BackupSongDto> _songs = [];
    private readonly List<BackupAnnouncementDto> _announcements = [];

    public BackupPayloadBuilder WithSchemaVersion(int schemaVersion)
    {
        _schemaVersion = schemaVersion;
        return this;
    }

    public BackupPayloadBuilder WithSong(
        Guid? id = null,
        string title = "Sublime Gracia",
        string? author = "John Newton",
        string? musicalKey = "G",
        string rawLyrics = "Sublime gracia del Señor\n\nQue a un infeliz salvó")
    {
        var now = DateTimeOffset.UtcNow;
        _songs.Add(new BackupSongDto(
            id ?? Guid.NewGuid(),
            title,
            author,
            musicalKey,
            rawLyrics,
            now,
            now));
        return this;
    }

    public BackupPayloadBuilder WithAnnouncement(
        Guid? id = null,
        string title = "Bienvenida",
        string rawBody = "Bienvenidos a nuestra reunión\n\nDios les bendiga")
    {
        var now = DateTimeOffset.UtcNow;
        _announcements.Add(new BackupAnnouncementDto(
            id ?? Guid.NewGuid(),
            title,
            rawBody,
            now,
            now));
        return this;
    }

    public BackupPayload Build() =>
        new(_schemaVersion, DateTimeOffset.UtcNow, _songs, _announcements);
}
