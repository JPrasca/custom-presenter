using System.Text.Json.Serialization;
using CustomPresenter.Domain.Live;

namespace CustomPresenter.Domain.Media;

/// <summary>
/// Categoría de activo multimedia permitido como fondo del proyector.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaKind
{
    Image = 1,
    Video = 2,
}

/// <summary>
/// Formatos multimedia permitidos por la política de seguridad.
/// </summary>
public enum MediaFormat
{
    Png = 1,
    Jpeg = 2,
    WebP = 3,
    Mp4 = 4,
    WebM = 5,
}

/// <summary>
/// Límites máximos de tamaño de archivo definidos en SECURITY.md.
/// </summary>
public static class MediaConstants
{
    public const long MaxImageBytes = 20L * 1024 * 1024;
    public const long MaxVideoBytes = 500L * 1024 * 1024;
    public const int HeaderBytesRequired = 12;
}

/// <summary>
/// Resultado de validación combinada de extensión y magic bytes.
/// </summary>
public sealed record MediaValidationResult(
    MediaKind Kind,
    MediaFormat Format,
    string Extension,
    string ContentType);

/// <summary>
/// Referencia ligera al fondo multimedia activo en vivo.
/// </summary>
public sealed record BackgroundMediaReference(
    Guid Id,
    MediaKind Kind);

/// <summary>
/// Activo multimedia local almacenado bajo nombre opaco UUID en el directorio media/.
/// </summary>
public sealed record MediaAsset(
    Guid Id,
    string OriginalName,
    MediaKind Kind,
    MediaFormat Format,
    string StoredFileName,
    string ContentType,
    long SizeBytes,
    DateTimeOffset ImportedAtUtc)
{
    public static MediaAsset Create(
        Guid id,
        string originalName,
        MediaValidationResult validation,
        long sizeBytes)
    {
        ArgumentNullException.ThrowIfNull(validation);

        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del activo multimedia es inválido.");
        }

        var safeOriginalName = Path.GetFileName(originalName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(safeOriginalName))
        {
            safeOriginalName = $"media.{validation.Extension}";
        }

        return new MediaAsset(
            id,
            safeOriginalName,
            validation.Kind,
            validation.Format,
            $"{id:D}.{validation.Extension}",
            validation.ContentType,
            sizeBytes,
            DateTimeOffset.UtcNow);
    }
}

/// <summary>
/// Validador estricto de extensión, magic bytes y tamaño máximo permitido.
/// </summary>
public static class MediaMagicBytesValidator
{
    public static MediaValidationResult Validate(
        string fileName,
        ReadOnlySpan<byte> headerBytes,
        long sizeBytes)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new DomainException("El nombre del archivo multimedia no puede estar vacío.");
        }

        if (sizeBytes <= 0)
        {
            throw new DomainException("El archivo multimedia está vacío.");
        }

        var ext = Path.GetExtension(fileName).Trim().ToLowerInvariant();
        var result = ext switch
        {
            ".png" when IsPng(headerBytes) => new MediaValidationResult(MediaKind.Image, MediaFormat.Png, "png", "image/png"),
            ".jpg" or ".jpeg" when IsJpeg(headerBytes) => new MediaValidationResult(MediaKind.Image, MediaFormat.Jpeg, "jpg", "image/jpeg"),
            ".webp" when IsWebP(headerBytes) => new MediaValidationResult(MediaKind.Image, MediaFormat.WebP, "webp", "image/webp"),
            ".mp4" when IsMp4(headerBytes) => new MediaValidationResult(MediaKind.Video, MediaFormat.Mp4, "mp4", "video/mp4"),
            ".webm" when IsWebM(headerBytes) => new MediaValidationResult(MediaKind.Video, MediaFormat.WebM, "webm", "video/webm"),
            ".png" or ".jpg" or ".jpeg" or ".webp" or ".mp4" or ".webm" =>
                throw new DomainException($"La cabecera binaria (magic bytes) del archivo '{Path.GetFileName(fileName)}' no corresponde con su extensión '{ext}'."),
            _ => throw new DomainException($"Formato multimedia no permitido: '{ext}'."),
        };

        var maxBytes = result.Kind == MediaKind.Image
            ? MediaConstants.MaxImageBytes
            : MediaConstants.MaxVideoBytes;

        if (sizeBytes > maxBytes)
        {
            throw new DomainException(
                $"El archivo '{Path.GetFileName(fileName)}' excede el tamaño máximo permitido ({maxBytes} bytes).");
        }

        return result;
    }

    public static string GetExtension(MediaFormat format) => format switch
    {
        MediaFormat.Png => "png",
        MediaFormat.Jpeg => "jpg",
        MediaFormat.WebP => "webp",
        MediaFormat.Mp4 => "mp4",
        MediaFormat.WebM => "webm",
        _ => throw new DomainException("Formato multimedia no soportado."),
    };

    private static bool IsPng(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8
        && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
        && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static bool IsJpeg(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3
        && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;

    private static bool IsWebP(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 12
        && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
        && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

    private static bool IsMp4(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8
        && bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70;

    private static bool IsWebM(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 4
        && bytes[0] == 0x1A && bytes[1] == 0x45 && bytes[2] == 0xDF && bytes[3] == 0xA3;
}
