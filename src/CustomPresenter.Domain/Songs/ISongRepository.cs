namespace CustomPresenter.Domain.Songs;

/// <summary>
/// Puerto de persistencia para gestión CRUD de canciones.
/// </summary>
public interface ISongRepository
{
    Task CreateAsync(Song song, CancellationToken cancellationToken = default);

    Task<Song?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Song>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Song song, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
