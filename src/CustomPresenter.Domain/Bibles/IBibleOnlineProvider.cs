namespace CustomPresenter.Domain.Bibles;

/// <summary>
/// Contrato para proveedores en línea que obtienen pasajes y capítulos bíblicos a demanda.
/// </summary>
public interface IBibleOnlineProvider
{
    Task<IReadOnlyList<BibleVerse>> FetchChapterVersesAsync(
        Guid versionId,
        string versionAbbreviation,
        int bookNumber,
        string bookName,
        int chapter,
        CancellationToken cancellationToken = default);
}
