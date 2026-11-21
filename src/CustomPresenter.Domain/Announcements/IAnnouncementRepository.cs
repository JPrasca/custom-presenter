namespace CustomPresenter.Domain.Announcements;

/// <summary>
/// Puerto de persistencia CRUD para anuncios y textos predefinidos.
/// </summary>
public interface IAnnouncementRepository
{
    Task CreateAsync(Announcement announcement, CancellationToken cancellationToken = default);

    Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
