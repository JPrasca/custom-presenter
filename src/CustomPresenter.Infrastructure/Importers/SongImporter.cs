using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CustomPresenter.Domain.Live;
using CustomPresenter.Domain.Songs;

namespace CustomPresenter.Infrastructure.Importers;

/// <summary>
/// Importador seguro de canciones (.txt y .json) con límite real de lectura de 50 MB y UTF-8 estricto.
/// </summary>
public sealed partial class SongImporter : ISongImporter
{
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    [GeneratedRegex(@"(?m)^(?:---|===)\s*$")]
    private static partial Regex TxtSongSeparatorRegex();

    public async Task<IReadOnlyList<Song>> ImportAsync(
        Stream stream,
        string fileName,
        long reportedSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("Debe especificar el nombre del archivo a importar.");
        }

        if (reportedSizeBytes > SongImportLimits.MaxFileSizeBytes)
        {
            throw new DomainException(
                $"El archivo excede el tamaño máximo permitido de {SongImportLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".txt" and not ".json")
        {
            throw new DomainException("Formato de archivo no soportado. Use archivos .txt o .json.");
        }

        var bytes = await ReadBoundedBytesAsync(stream, cancellationToken).ConfigureAwait(false);
        string content;
        try
        {
            content = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new DomainException("El archivo no tiene una codificación UTF-8 válida.");
        }

        return extension == ".json"
            ? ParseJson(content)
            : ParseTxt(content);
    }

    private static async Task<byte[]> ReadBoundedBytesAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        var maxAllowedPlusOne = SongImportLimits.MaxFileSizeBytes + 1;
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var totalRead = 0;

        while (true)
        {
            var toRead = Math.Min(chunk.Length, maxAllowedPlusOne - totalRead);
            if (toRead <= 0)
            {
                throw new DomainException(
                    $"El archivo excede el tamaño máximo permitido de {SongImportLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
            }

            var read = await stream.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            totalRead += read;
            if (totalRead > SongImportLimits.MaxFileSizeBytes)
            {
                throw new DomainException(
                    $"El archivo excede el tamaño máximo permitido de {SongImportLimits.MaxFileSizeBytes / (1024 * 1024)} MB.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static List<Song> ParseTxt(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var blocks = TxtSongSeparatorRegex().Split(normalized);
        var songs = new List<Song>();

        foreach (var block in blocks)
        {
            var trimmed = block.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var firstBreak = trimmed.IndexOf('\n');
            if (firstBreak < 0)
            {
                throw new DomainException(
                    "Cada canción en formato .txt debe incluir título en la primera línea y letra debajo.");
            }

            var title = trimmed[..firstBreak].Trim();
            var lyrics = trimmed[(firstBreak + 1)..].Trim();
            songs.Add(Song.Create(title, null, null, lyrics));
        }

        if (songs.Count == 0)
        {
            throw new DomainException("El archivo .txt no contiene canciones válidas.");
        }

        return songs;
    }

    private static List<Song> ParseJson(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(
                content,
                new JsonDocumentOptions { MaxDepth = SongImportLimits.MaxJsonDepth });

            var arrayElement = doc.RootElement.ValueKind switch
            {
                JsonValueKind.Array => doc.RootElement,
                JsonValueKind.Object when doc.RootElement.TryGetProperty("songs", out var songsProp)
                    && songsProp.ValueKind == JsonValueKind.Array => songsProp,
                _ => throw new DomainException("El JSON debe ser un arreglo de canciones o un objeto con la propiedad 'songs'."),
            };

            var songs = new List<Song>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    throw new DomainException("Cada elemento de canción en JSON debe ser un objeto.");
                }

                var title = GetRequiredString(item, "title");
                var lyrics = GetRequiredString(item, "rawLyrics");
                var author = GetOptionalString(item, "author");
                var musicalKey = GetOptionalString(item, "musicalKey");

                songs.Add(Song.Create(title, author, musicalKey, lyrics));
            }

            if (songs.Count == 0)
            {
                throw new DomainException("El archivo JSON no contiene canciones para importar.");
            }

            return songs;
        }
        catch (JsonException ex)
        {
            throw new DomainException($"Formato JSON inválido: {ex.Message}", ex);
        }
    }

    private static string GetRequiredString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            throw new DomainException($"Falta la propiedad requerida '{propertyName}' en el JSON.");
        }

        return prop.GetString() ?? string.Empty;
    }

    private static string? GetOptionalString(JsonElement obj, string propertyName)
    {
        if (!obj.TryGetProperty(propertyName, out var prop) || prop.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (prop.ValueKind != JsonValueKind.String)
        {
            throw new DomainException($"La propiedad '{propertyName}' debe ser una cadena de texto.");
        }

        return prop.GetString();
    }
}
