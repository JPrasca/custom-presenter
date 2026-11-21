namespace CustomPresenter.Domain.Bibles;

public static class BibleImportLimits
{
    public const int MaxFileSizeBytes = 50 * 1024 * 1024;
    public const int MaxJsonDepth = 16;
    public const long MaxXmlCharacters = 60_000_000;
}

/// <summary>
/// Puerto para importar Biblias desde archivos locales (XML OpenSong/Zefania o JSON).
/// </summary>
public interface IBibleImporter
{
    Task<BibleImportResult> ImportAsync(
        Stream stream,
        string fileName,
        long reportedSizeBytes,
        string? versionNameOverride = null,
        string? abbreviationOverride = null,
        CancellationToken cancellationToken = default);
}
