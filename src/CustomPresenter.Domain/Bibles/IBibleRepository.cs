namespace CustomPresenter.Domain.Bibles;

/// <summary>
/// Puerto de persistencia y consulta para versiones y versículos bíblicos en SQLite.
/// </summary>
public interface IBibleRepository
{
    Task SaveBibleAsync(
        BibleVersion version,
        IReadOnlyList<BibleVerse> verses,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleBookSummary>> ListBooksAsync(
        Guid versionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> GetChapterVersesAsync(
        Guid versionId,
        int bookNumber,
        int chapter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> FindByReferenceAsync(
        Guid versionId,
        BibleReference reference,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BibleVerse>> SearchVersesFtsAsync(
        Guid versionId,
        string sanitizedFtsQuery,
        int limit = 50,
        CancellationToken cancellationToken = default);
}
