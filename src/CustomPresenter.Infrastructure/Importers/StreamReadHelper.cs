using System.Text;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Infrastructure.Importers;

/// <summary>
/// Helper compartido para leer streams de forma acotada y decodificar UTF-8 estricto.
/// </summary>
internal static class StreamReadHelper
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static async Task<string> ReadUtf8BoundedAsync(
        Stream stream,
        long reportedSizeBytes,
        int maxFileSizeBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (reportedSizeBytes > maxFileSizeBytes)
        {
            throw new DomainException(
                $"El archivo excede el tamaño máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var maxAllowedPlusOne = maxFileSizeBytes + 1;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var totalRead = 0;

        while (true)
        {
            var toRead = Math.Min(chunk.Length, maxAllowedPlusOne - totalRead);
            if (toRead <= 0)
            {
                throw new DomainException(
                    $"El archivo excede el tamaño máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
            }

            var read = await stream.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > maxFileSizeBytes)
            {
                throw new DomainException(
                    $"El archivo excede el tamaño máximo permitido de {maxFileSizeBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        try
        {
            return StrictUtf8.GetString(buffer.ToArray());
        }
        catch (DecoderFallbackException)
        {
            throw new DomainException("El archivo no tiene una codificación UTF-8 válida.");
        }
    }
}
