using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Media;

namespace CustomPresenter.Infrastructure.Media;

/// <summary>
/// Almacenamiento local seguro en el subdirectorio media/ con prevención de Path Traversal y symlinks.
/// </summary>
public sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string _mediaRootFullPath;
    private readonly string _mediaRootWithSeparator;

    public LocalMediaStorage(string mediaRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaRootPath);
        _mediaRootFullPath = Path.GetFullPath(mediaRootPath);
        Directory.CreateDirectory(_mediaRootFullPath);

        _mediaRootWithSeparator = _mediaRootFullPath.EndsWith(Path.DirectorySeparatorChar)
            ? _mediaRootFullPath
            : _mediaRootFullPath + Path.DirectorySeparatorChar;
    }

    public async Task<long> SaveAsync(
        Stream source,
        string storedFileName,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var targetPath = ResolveAndVerifyChildPath(storedFileName);

        var buffer = new byte[81920];
        long totalBytes = 0;

        try
        {
            await using var output = new FileStream(
                targetPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                totalBytes += read;
                if (totalBytes > maxBytes)
                {
                    throw new DomainException(
                        $"El archivo multimedia excede el límite máximo permitido de {maxBytes} bytes.");
                }

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return totalBytes;
        }
        catch
        {
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            throw;
        }
    }

    public string ResolveSafeFilePath(MediaAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var fullPath = ResolveAndVerifyChildPath(asset.StoredFileName);

        var info = new FileInfo(fullPath);
        if (!info.Exists || info.LinkTarget is not null)
        {
            throw new FileNotFoundException("El archivo multimedia no existe o es un enlace no permitido.", fullPath);
        }

        return fullPath;
    }

    public Task<bool> DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveAndVerifyChildPath(storedFileName);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private string ResolveAndVerifyChildPath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            throw new DomainException("El nombre de archivo almacenado es inválido.");
        }

        var safeName = Path.GetFileName(storedFileName);
        if (!string.Equals(safeName, storedFileName, StringComparison.Ordinal))
        {
            throw new DomainException("Se rechazó una ruta de archivo con segmentos de directorio.");
        }

        var candidate = Path.GetFullPath(Path.Combine(_mediaRootFullPath, safeName));
        if (!candidate.StartsWith(_mediaRootWithSeparator, StringComparison.Ordinal))
        {
            throw new DomainException("La ruta resuelta escapa del directorio media/.");
        }

        return candidate;
    }
}
