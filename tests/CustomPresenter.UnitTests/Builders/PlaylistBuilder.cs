using CustomPresenter.Domain.Playlists;

namespace CustomPresenter.UnitTests.Builders;

internal sealed class PlaylistBuilder
{
    private string _name = "Servicio Dominical";
    private readonly List<(PlaylistItemKind Kind, string ReferenceKey, string DisplayTitle, string? Subtitle)> _items = [];

    public PlaylistBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public PlaylistBuilder WithSongItem(Guid songId, string title = "Sublime Gracia", string? subtitle = "John Newton")
    {
        _items.Add((PlaylistItemKind.Song, songId.ToString("D"), title, subtitle));
        return this;
    }

    public PlaylistBuilder WithBibleItem(
        Guid versionId,
        int bookNumber,
        int chapter,
        int startVerse,
        int? endVerse = null,
        string title = "Salmos 23:1",
        string? subtitle = "RVR60")
    {
        var key = Playlist.FormatBibleReferenceKey(versionId, bookNumber, chapter, startVerse, endVerse);
        _items.Add((PlaylistItemKind.BibleVerse, key, title, subtitle));
        return this;
    }

    public PlaylistBuilder WithAnnouncementItem(Guid announcementId, string title = "Bienvenida")
    {
        _items.Add((PlaylistItemKind.Announcement, announcementId.ToString("D"), title, title));
        return this;
    }

    public Playlist Build()
    {
        var playlist = Playlist.Create(_name);
        foreach (var (kind, referenceKey, displayTitle, subtitle) in _items)
        {
            playlist = playlist.WithAddedItem(kind, referenceKey, displayTitle, subtitle);
        }

        return playlist;
    }
}
