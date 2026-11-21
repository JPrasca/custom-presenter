using System.Globalization;
using CustomPresenter.Application.Announcements;
using CustomPresenter.Application.Bibles;
using CustomPresenter.Application.Live;
using CustomPresenter.Application.Songs;
using CustomPresenter.Domain.Announcements;
using CustomPresenter.Domain.Bibles;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Playlists;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Application.Playlists;

/// <summary>
/// Casos de uso de gestión, reordenamiento y proyección secuencial de playlists mixtas.
/// </summary>
public sealed class PlaylistService
{
    private readonly IPlaylistRepository _playlistRepository;
    private readonly ISongRepository _songRepository;
    private readonly IBibleRepository _bibleRepository;
    private readonly IAnnouncementRepository _announcementRepository;

    public PlaylistService(
        IPlaylistRepository playlistRepository,
        ISongRepository songRepository,
        IBibleRepository bibleRepository,
        IAnnouncementRepository announcementRepository)
    {
        ArgumentNullException.ThrowIfNull(playlistRepository);
        ArgumentNullException.ThrowIfNull(songRepository);
        ArgumentNullException.ThrowIfNull(bibleRepository);
        ArgumentNullException.ThrowIfNull(announcementRepository);

        _playlistRepository = playlistRepository;
        _songRepository = songRepository;
        _bibleRepository = bibleRepository;
        _announcementRepository = announcementRepository;
    }

    public async Task<Playlist> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var playlist = Playlist.Create(name);
        await _playlistRepository.CreateAsync(playlist, cancellationToken).ConfigureAwait(false);
        return playlist;
    }

    public async Task<Playlist> RenameAsync(
        Guid playlistId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        var updated = existing.Rename(name);
        await SaveUpdatedAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public Task<bool> DeleteAsync(Guid playlistId, CancellationToken cancellationToken = default) =>
        _playlistRepository.DeleteAsync(playlistId, cancellationToken);

    public Task<Playlist?> GetByIdAsync(Guid playlistId, CancellationToken cancellationToken = default) =>
        _playlistRepository.GetByIdAsync(playlistId, cancellationToken);

    public Task<IReadOnlyList<Playlist>> ListAllAsync(CancellationToken cancellationToken = default) =>
        _playlistRepository.ListAllAsync(cancellationToken);

    public async Task<Playlist> AddSongAsync(
        Guid playlistId,
        Guid songId,
        CancellationToken cancellationToken = default)
    {
        var playlist = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        var song = await _songRepository.GetByIdAsync(songId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró la canción con identificador '{songId}'.");

        var updated = playlist.WithAddedItem(
            PlaylistItemKind.Song,
            song.Id.ToString("D"),
            song.Title,
            song.Author);

        await SaveUpdatedAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<Playlist> AddBiblePassageAsync(
        Guid playlistId,
        Guid versionId,
        int bookNumber,
        string bookName,
        int chapter,
        int startVerse,
        int? endVerse = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bookName))
        {
            throw new DomainException("El nombre del libro bíblico no puede estar vacío.");
        }

        var effectiveEnd = endVerse ?? startVerse;
        if (bookNumber <= 0 || chapter <= 0 || startVerse <= 0 || effectiveEnd < startVerse)
        {
            throw new DomainException("La referencia bíblica indicada para la playlist es inválida.");
        }

        var playlist = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        var versions = await _bibleRepository.ListVersionsAsync(cancellationToken).ConfigureAwait(false);
        var version = versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new DomainException($"No se encontró la versión bíblica con identificador '{versionId}'.");

        var verseLabel = effectiveEnd != startVerse
            ? string.Create(CultureInfo.InvariantCulture, $"{startVerse}-{effectiveEnd}")
            : startVerse.ToString(CultureInfo.InvariantCulture);

        var title = string.Create(CultureInfo.InvariantCulture, $"{bookName.Trim()} {chapter}:{verseLabel}");
        var key = Playlist.FormatBibleReferenceKey(versionId, bookNumber, chapter, startVerse, effectiveEnd);

        var updated = playlist.WithAddedItem(
            PlaylistItemKind.BibleVerse,
            key,
            title,
            version.Abbreviation);

        await SaveUpdatedAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<Playlist> AddAnnouncementAsync(
        Guid playlistId,
        Guid announcementId,
        CancellationToken cancellationToken = default)
    {
        var playlist = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        var announcement = await _announcementRepository.GetByIdAsync(announcementId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró el anuncio con identificador '{announcementId}'.");

        var updated = playlist.WithAddedItem(
            PlaylistItemKind.Announcement,
            announcement.Id.ToString("D"),
            announcement.Title,
            announcement.Title);

        await SaveUpdatedAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<Playlist> RemoveItemAsync(
        Guid playlistId,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        var playlist = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        var updated = playlist.WithRemovedItem(itemId);
        await SaveUpdatedAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<Playlist> ReorderItemsAsync(
        Guid playlistId,
        IReadOnlyList<Guid> orderedItemIds,
        CancellationToken cancellationToken = default)
    {
        var playlist = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        var updated = playlist.WithReorderedItems(orderedItemIds);
        await SaveUpdatedAsync(updated, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    public async Task<IReadOnlyList<SlideContent>> ResolveItemSlidesAsync(
        PlaylistItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.Kind switch
        {
            PlaylistItemKind.Song => await ResolveSongSlidesAsync(item, cancellationToken).ConfigureAwait(false),
            PlaylistItemKind.BibleVerse => await ResolveBibleSlidesAsync(item, cancellationToken).ConfigureAwait(false),
            PlaylistItemKind.Announcement => await ResolveAnnouncementSlidesAsync(item, cancellationToken).ConfigureAwait(false),
            _ => throw new DomainException($"Tipo de ítem de playlist no soportado: '{item.Kind}'."),
        };
    }

    public async Task<LiveState> ProjectItemAsync(
        Guid playlistId,
        int itemIndex,
        int slideIndex,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveStateManager);

        var playlist = await GetRequiredPlaylistAsync(playlistId, cancellationToken).ConfigureAwait(false);
        if (itemIndex < 0 || itemIndex >= playlist.Items.Count)
        {
            throw new DomainException(
                $"El índice de ítem {itemIndex} está fuera de rango para la playlist '{playlist.Name}'.");
        }

        var item = playlist.Items[itemIndex];
        var slides = await ResolveItemSlidesAsync(item, cancellationToken).ConfigureAwait(false);
        if (slideIndex < 0 || slideIndex >= slides.Count)
        {
            throw new DomainException(
                $"El índice de diapositiva {slideIndex} está fuera de rango para '{item.DisplayTitle}'.");
        }

        return await liveStateManager.ProjectSlideAsync(slides[slideIndex], cancellationToken).ConfigureAwait(false);
    }

    public Task<LiveState> ProjectNextItemAsync(
        Guid playlistId,
        int currentItemIndex,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default) =>
        ProjectItemAsync(playlistId, currentItemIndex + 1, 0, liveStateManager, cancellationToken);

    public Task<LiveState> ProjectPreviousItemAsync(
        Guid playlistId,
        int currentItemIndex,
        LiveStateManager liveStateManager,
        CancellationToken cancellationToken = default) =>
        ProjectItemAsync(playlistId, currentItemIndex - 1, 0, liveStateManager, cancellationToken);

    private async Task<IReadOnlyList<SlideContent>> ResolveSongSlidesAsync(
        PlaylistItem item,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(item.ReferenceKey, out var songId))
        {
            throw new DomainException(
                $"La referencia de la canción '{item.DisplayTitle}' en la playlist es inválida.");
        }

        var song = await _songRepository.GetByIdAsync(songId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException(
                $"La canción '{item.DisplayTitle}' ya no existe en la biblioteca.");

        return SongService.ToSlideContents(song);
    }

    private async Task<IReadOnlyList<SlideContent>> ResolveBibleSlidesAsync(
        PlaylistItem item,
        CancellationToken cancellationToken)
    {
        if (!Playlist.TryParseBibleReferenceKey(
                item.ReferenceKey,
                out var versionId,
                out var bookNumber,
                out var chapter,
                out var startVerse,
                out var endVerse))
        {
            throw new DomainException(
                $"La referencia bíblica '{item.DisplayTitle}' en la playlist es inválida.");
        }

        var versions = await _bibleRepository.ListVersionsAsync(cancellationToken).ConfigureAwait(false);
        var version = versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new DomainException(
                $"La versión bíblica de '{item.DisplayTitle}' ya no existe en la biblioteca.");

        var chapterVerses = await _bibleRepository
            .GetChapterVersesAsync(versionId, bookNumber, chapter, cancellationToken)
            .ConfigureAwait(false);

        var selected = chapterVerses
            .Where(v => v.Verse >= startVerse && v.Verse <= endVerse)
            .OrderBy(v => v.Verse)
            .ToList();

        if (selected.Count == 0)
        {
            throw new DomainException(
                $"El pasaje bíblico '{item.DisplayTitle}' ya no existe en la biblioteca.");
        }

        return [BibleSearchService.ToSlideContent(selected, version.Abbreviation)];
    }

    private async Task<IReadOnlyList<SlideContent>> ResolveAnnouncementSlidesAsync(
        PlaylistItem item,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(item.ReferenceKey, out var announcementId))
        {
            throw new DomainException(
                $"La referencia del anuncio '{item.DisplayTitle}' en la playlist es inválida.");
        }

        var announcement = await _announcementRepository.GetByIdAsync(announcementId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DomainException(
                $"El anuncio '{item.DisplayTitle}' ya no existe en la biblioteca.");

        return AnnouncementService.ToSlideContents(announcement);
    }

    private async Task<Playlist> GetRequiredPlaylistAsync(
        Guid playlistId,
        CancellationToken cancellationToken) =>
        await _playlistRepository.GetByIdAsync(playlistId, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró la playlist con identificador '{playlistId}'.");

    private async Task SaveUpdatedAsync(Playlist updated, CancellationToken cancellationToken)
    {
        var saved = await _playlistRepository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            throw new DomainException($"No se pudo actualizar la playlist '{updated.Id}'.");
        }
    }
}
