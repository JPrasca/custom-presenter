namespace CustomPresenter.Domain.Songs;

/// <summary>
/// Puerto de persistencia para gestión CRUD y búsqueda FTS5 de canciones.
/// </summary>
public interface ISongRepository
{
    Task CreateAsync(Song song, CancellationToken cancellationToken = default);

    Task CreateBatchAsync(IReadOnlyList<Song> songs, CancellationToken cancellationToken = default);

    Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> SearchAsync(
        string sanitizedFtsQuery,
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
