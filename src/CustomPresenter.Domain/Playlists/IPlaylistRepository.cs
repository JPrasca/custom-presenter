namespace CustomPresenter.Domain.Playlists;

/// <summary>
/// Puerto de persistencia para listas de servicio (playlists) mixtas en SQLite.
/// </summary>
public interface IPlaylistRepository
{
    Task CreateAsync(Playlist playlist, CancellationToken cancellationToken = default);

    Task<Playlist?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Playlist>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Playlist playlist, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
