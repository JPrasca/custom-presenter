namespace CustomPresenter.Domain.Songs;

public static class SongImportLimits
{
    public const int MaxFileSizeBytes = 50 * 1024 * 1024;
    public const int MaxJsonDepth = 16;
}

/// <summary>
/// Puerto para importar canciones desde archivos locales (.txt, .json).
/// </summary>
public interface ISongImporter
{
    Task<IReadOnlyList<Song>> ImportAsync(
        Stream stream,
        string fileName,
        long reportedSizeBytes,
        CancellationToken cancellationToken = default);
}
