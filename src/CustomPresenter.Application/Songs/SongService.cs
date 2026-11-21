using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Application.Songs;

/// <summary>
/// Casos de uso CRUD, búsqueda FTS5 e importación de canciones.
/// </summary>
public sealed class SongService
{
    private readonly ISongRepository _repository;
    private readonly ISongImporter? _importer;
    private readonly ILyricsLookupClient? _lyricsLookup;

    public SongService(
        ISongRepository repository,
        ISongImporter? importer = null,
        ILyricsLookupClient? lyricsLookup = null)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        _importer = importer;
        _lyricsLookup = lyricsLookup;
    }

    public async Task<Song> CreateSongAsync(
        string title,
        string? author,
        string? musicalKey,
        string rawLyrics,
        CancellationToken cancellationToken = default)
    {
        var song = Song.Create(title, author, musicalKey, rawLyrics);
        await _repository.CreateAsync(song, cancellationToken).ConfigureAwait(false);
        return song;
    }

    public async Task<Song> UpdateSongAsync(
        Guid id,
        string title,
        string? author,
        string? musicalKey,
        string rawLyrics,
        CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw new DomainException($"No se encontró la canción con identificador '{id}'.");

        var updated = existing.WithUpdatedContent(title, author, musicalKey, rawLyrics);
        var saved = await _repository.UpdateAsync(updated, cancellationToken).ConfigureAwait(false);
        if (!saved)
        {
            throw new DomainException($"No se pudo actualizar la canción '{id}'.");
        }

        return updated;
    }

    public Task<bool> DeleteSongAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.DeleteAsync(id, cancellationToken);

    public Task<Song?> GetSongAsync(Guid id, CancellationToken cancellationToken = default) =>
        _repository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Song>> ListSongsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListAllAsync(cancellationToken);

    public async Task<IReadOnlyList<Song>> SearchSongsAsync(
        string query,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            var all = await _repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
            return all.Take(Math.Clamp(limit, 1, 200)).ToList();
        }

        var sanitized = Fts5QuerySanitizer.Sanitize(query);
        if (sanitized.Length == 0)
        {
            return [];
        }

        return await _repository.SearchAsync(sanitized, Math.Clamp(limit, 1, 200), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Song>> ImportSongsAsync(
        Stream stream,
        string fileName,
        long reportedSizeBytes,
        CancellationToken cancellationToken = default)
    {
        if (_importer is null)
        {
            throw new InvalidOperationException("No se configuró un importador de canciones.");
        }

        var songs = await _importer
            .ImportAsync(stream, fileName, reportedSizeBytes, cancellationToken)
            .ConfigureAwait(false);

        await _repository.CreateBatchAsync(songs, cancellationToken).ConfigureAwait(false);
        return songs;
    }

    public Task<IReadOnlyList<LyricsSearchResult>> SearchOnlineLyricsAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new DomainException("Debe ingresar el nombre o artista de la canción a buscar.");
        }

        if (_lyricsLookup is null)
        {
            throw new DomainException("La búsqueda de letras en internet no está habilitada.");
        }

        return _lyricsLookup.SearchAsync(query.Trim(), cancellationToken);
    }

    public static IReadOnlyList<SlideContent> ToSlideContents(Song song)
    {
        ArgumentNullException.ThrowIfNull(song);

        return song.Slides
            .Select(slide => new SlideContent(
                $"{song.Id}:{slide.Index}",
                song.Title,
                slide.Text,
                song.Author))
            .ToList();
    }
}
