using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Application.Songs;

/// <summary>
/// Casos de uso CRUD de canciones y conversión a diapositivas proyectables.
/// </summary>
public sealed class SongService
{
    private readonly ISongRepository _repository;

    public SongService(ISongRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
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
