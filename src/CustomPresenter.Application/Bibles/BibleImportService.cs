using CustomPresenter.Domain.Bibles;

namespace CustomPresenter.Application.Bibles;

/// <summary>
/// Casos de uso para importar, listar y eliminar versiones bíblicas locales.
/// </summary>
public sealed class BibleImportService
{
    private readonly IBibleRepository _repository;
    private readonly IBibleImporter _importer;

    public BibleImportService(IBibleRepository repository, IBibleImporter importer)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(importer);
        _repository = repository;
        _importer = importer;
    }

    public async Task<BibleImportResult> ImportBibleAsync(
        Stream stream,
        string fileName,
        long reportedSizeBytes,
        string? versionNameOverride = null,
        string? abbreviationOverride = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _importer.ImportAsync(
            stream,
            fileName,
            reportedSizeBytes,
            versionNameOverride,
            abbreviationOverride,
            cancellationToken).ConfigureAwait(false);

        await _repository.SaveBibleAsync(result.Version, result.Verses, cancellationToken)
            .ConfigureAwait(false);

        return result;
    }

    public Task<IReadOnlyList<BibleVersion>> ListVersionsAsync(CancellationToken cancellationToken = default) =>
        _repository.ListVersionsAsync(cancellationToken);

    public Task<bool> DeleteVersionAsync(Guid versionId, CancellationToken cancellationToken = default) =>
        _repository.DeleteVersionAsync(versionId, cancellationToken);
}
