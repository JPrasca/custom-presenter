namespace CustomPresenter.Domain.Bibles;

/// <summary>
/// Puerto de persistencia para versiones y versículos bíblicos en SQLite.
/// </summary>
public interface IBibleRepository
{
    Task SaveBibleAsync(
        BibleVersion version,
        IReadOnlyList<BibleVerse> verses,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default);
}
