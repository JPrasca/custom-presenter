namespace CustomPresenter.Domain.Media;

/// <summary>
/// Puerto de almacenamiento seguro de archivos multimedia en el directorio interno media/.
/// </summary>
public interface IMediaStorage
{
    Task<long> SaveAsync(
        Stream source,
        string storedFileName,
        long maxBytes,
        CancellationToken cancellationToken = default);

    string ResolveSafeFilePath(MediaAsset asset);

    Task<bool> DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);
}
