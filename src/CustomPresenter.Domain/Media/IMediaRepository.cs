namespace CustomPresenter.Domain.Media;

/// <summary>
/// Puerto de persistencia de metadatos de activos multimedia en SQLite.
/// </summary>
public interface IMediaRepository
{
    Task CreateAsync(MediaAsset asset, CancellationToken cancellationToken = default);

    Task<MediaAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaAsset>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
